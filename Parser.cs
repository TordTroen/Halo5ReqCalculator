using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Halo5ReqParser
{
	public class Parser
	{
		public string reqFileCustomization = "Data/H5ReqCust.htm";
		public string reqFileLoadout = "Data/H5ReqLoad.htm";
		public string reqFilePower = "Data/H5ReqPower.htm";

		private const string DataFieldHasCert = "data-has-certification";
		private const string DataFieldHaveOwned = "data-have-owned";
		private const string DataFieldIsDuarble = "data-is-durable";
		private const string DataFieldRarity = "data-rarity";
		private const string DataFieldName = "data-name";
		private const string DataFieldSubCategory = "data-subcategory";

		private readonly string haveOwnedPattern = "data-have-owned=\"((True)|(False))\"";
		private readonly string haveCertPattern = "data-has-certification=\"((True)|(False))\"";
		private readonly string isDurablePattern = "data-is-durable=\"((True)|(False))\"";
		private readonly string rarityLinePattern = "data-rarity=\"((Common)|(Uncommon)|(Rare)|(Ultra Rare)|(Legendary))\"";
		private readonly string rarityLevelPattern = "((Common)|(Uncommon)|(Rare)|(Ultra Rare)|(Legendary))";

		private readonly string dataValuePattern = "=\".*?\"";

		private List<ReqSpecialRewardDataContainer> reqSpecialDataContainer = new List<ReqSpecialRewardDataContainer>();
		private readonly string[] specialDataFiles = new string[]
		{
			"Data/dbHelmetList.json",
			"Data/dbArmorList.json",
			"Data/dbEmblemList.json"
		};
		private List<ReqGroupCategory> reqGroups = new List<ReqGroupCategory>();

		//public static ReqPack BronzePack = new ReqPack("Bronze", 1250, 1, 100);
		//public static ReqPack SilverPack = new ReqPack("Silver", 5000, 2);
		//public static ReqPack GoldPack = new ReqPack("Gold", 10000, 2);
		private readonly ReqPack[] ReqPacks = new ReqPack[]
		{
			new ReqPack("Bronze", 1250, 1, 100),
			new ReqPack("Silver", 5000, 2),
			new ReqPack("Gold", 10000, 2)
		};

		private static Random rnd = new Random();

		public Parser()
		{
			for (int i = 0; i < Enum.GetNames(typeof(ReqCategory)).Length; i ++)
			{
				reqGroups.Add(new ReqGroupCategory((ReqCategory)i));
			}
		}

		public int ParseAllReqs()
		{
			int parseStatus = 0;

			// Parsing
			ParseJsonData();
			parseStatus += ParseData(reqFileCustomization, ReqCategory.Customization);
			parseStatus += ParseData(reqFileLoadout, ReqCategory.Loadout);
			parseStatus += ParseData(reqFilePower, ReqCategory.PowerVehicle);
			if (parseStatus != 0)
			{
				IO.Printl("Couldn't parse, exiting...");
				return 1;
			}

			// Tally up missing reqs by category into a total by category
			int ownedCount = 0, totalCount = 0;
			List<ReqGroupStat> totals = new List<ReqGroupStat>();
			for (int i = 0; i < Enum.GetNames(typeof(RarityLevel)).Length; i++)
			{
				totals.Add(new ReqGroupStat((RarityLevel)i));
			}

			foreach (var group in reqGroups)
			{
				ownedCount += group.OwnedCount;
				totalCount += group.TotalCount;

				IO.Printl("Category: " + group.ReqCategory + "(" + group.OwnedCount + "/" + group.TotalCount + ")");
				foreach (var item in group.reqGroupStats)
				{
					//printl(" - " + item.RarityLevel + ": " + item.OwnedCount + "/" + item.TotalCount);
					IO.Printl(string.Format(" - {0, -9}: {1}/{2}", item.RarityLevel, item.OwnedCount, item.TotalCount));
					//costs[(int)item.RarityLevel] += GetMinimumRequiredCards(item.RarityLevel, item.TotalCount, item.OwnedCount);
					totals[(int)item.RarityLevel].OwnedCount += item.OwnedCount;
					totals[(int)item.RarityLevel].TotalCount += item.TotalCount;
				}
				IO.Printl();
			}

			IO.Printl("Totals: ");
			foreach (var total in totals)
			{
				int needed = total.TotalCount - total.OwnedCount;
				IO.Printl(string.Format(" - {0, -9}: {1}/{2} ({3})", total.RarityLevel, total.OwnedCount, total.TotalCount, needed));
				GetLowestCardForRarity(total.RarityLevel).ItemsNeededCount = needed;
			}
			IO.Printl();

			IO.Printl(string.Format("Total: {0}/{1}", ownedCount, totalCount));
			foreach (var pack in ReqPacks)
			{
				IO.Printl(string.Format(" {0, 7} packs needed: {1} ({2:###,###} RP)", pack.Name, pack.ItemsNeededCount, pack.PacksNeededCount * pack.Price));
			}
			return 0;
		}

		

		public int ParseData(string path, ReqCategory category)
		{
			HtmlDocument doc = new HtmlDocument();
			//var path = reqFileCustomization; //"HWPage.htm";
			try
			{
				doc.Load(path);
			}
			catch (FileNotFoundException ex)
			{
				IO.Printl("Couldn't open the file [" + path + "]");
				return 1;
			}
			catch (Exception ex)
			{
				IO.Printl("An error occured while openeing the file [" + path + "] (" + ex.Message + ")");
				return 1;
			}
			IO.Print("-> Getting REQ data " + path + "...");

			//foreach (var link in doc.DocumentNode.SelectNodes("//a[@class]"))
			foreach (var item in doc.DocumentNode.Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value.Contains("card")))
			{
				var card = item.InnerHtml;
				//print("Item: {" + card + "}");

				// Get the card's rarity level
				RarityLevel rarity = RarityLevel.Common;
				Match rarityMatch = Regex.Match(card, rarityLinePattern);
				if (rarityMatch.Success)
				{
					rarity = ParseLineToRarity(rarityMatch.Value);
				}

				bool isSpecial = false;
				if (IsDurable(card)) // True if the card is from customization or loadout, false if it is from power & vehicle
				{
					if (HaveOwned(card))
					{
						string name = GetValueFromDataField(card, DataFieldName);
						string subCategoryData = GetValueFromDataField(card, DataFieldSubCategory);
						ReqType reqType = DataFieldToReqType(subCategoryData);
						isSpecial = IsSpecialReq(name, reqType);

						if (!isSpecial)
						{
							reqGroups[(int)category].OwnedCount++;
							reqGroups[(int)category].reqGroupStats[(int)rarity].OwnedCount++;
						}
					}
				}
				else
				{
					if (HaveCert(card))
					{
						reqGroups[(int)category].OwnedCount++;
						reqGroups[(int)category].reqGroupStats[(int)rarity].OwnedCount++;
					}
				}
				if (!isSpecial)
				{
					reqGroups[(int)category].reqGroupStats[(int)rarity].TotalCount++;
					reqGroups[(int)category].TotalCount++;
				}
			}

			IO.Printl(" done\n");
			return 0;
		}

		private void ParseJsonData()
		{
			IO.Print("Initializing... ");

			reqSpecialDataContainer = new List<ReqSpecialRewardDataContainer>();
			//foreach (var dataFile in specialDataFiles)
			for(int i = 0; i < specialDataFiles.Length; i ++)
			{
				//var dataPath = "~/Content/dummy-data.json";
				//dataPath = HttpContext.Current.Server.MapPath(dataPath);
				var json = File.ReadAllText(specialDataFiles[i]);
				var dataObj = JsonConvert.DeserializeObject<ReqSpecialRewardDataContainer>(json);
				reqSpecialDataContainer.Add(dataObj);
				//foreach (var item in dataObj.Items)
				//{
				//	IO.Printl("Name: "+ item.Name);
				//}

				//reqSpecialDataContainer.Add(dataObj);

				IO.Print(string.Format("[{0}/{1}] ", i+1, specialDataFiles.Length));
			}
			IO.Print("done\n");
		}

		private bool IsSpecialReq(string name, ReqType reqType)
		{
			if ((int)reqType < reqSpecialDataContainer.Count)
			{
				foreach (var item in reqSpecialDataContainer[(int)reqType].Items)
				{
					if (item.Name == name)
					{
						if (!string.IsNullOrEmpty(item.Notes))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private string GetValueFromDataField(string text, string handle)
		{
			Match match = Regex.Match(text, handle + dataValuePattern);
			if (match.Success)
			{
				// TODO Put this in its own Regex object for performance??
				match = Regex.Match(match.Value, dataValuePattern);
				if (match.Success)
				{
					string value = match.Value;
					return value.Substring(2, value.Length - 3);
				}
			}
			return null;
		}

		private bool GetDataBoolValue(string data)
		{
			if (data == null) return false;
			return data.ToLower().Contains("true");
		}

		private ReqType DataFieldToReqType(string data)
		{
			ReqType reqType = ReqType.Loadout;
			Enum.TryParse(data, out reqType);
			return reqType;
		}

		/// <summary>
		/// Check if the card HTML element has the "data-have-owned" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element.</param>
		/// <returns></returns>
		bool HaveOwned(string card)
		{
			string data = GetValueFromDataField(card, DataFieldHaveOwned);
			return GetDataBoolValue(data);
		}

		/// <summary>
		/// Check if the card HTML element has the "data-has-certification" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element.</param>
		/// <returns></returns>
		bool HaveCert(string card)
		{
			string data = GetValueFromDataField(card, DataFieldHasCert);
			return GetDataBoolValue(data);
		}

		/// <summary>
		/// Checks if the card HTML element has the "data-is-durable" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element</param>
		/// <returns></returns>
		bool IsDurable(string card)
		{
			string data = GetValueFromDataField(card, DataFieldIsDuarble);
			return GetDataBoolValue(data);
		}
		
		/// <summary>
		/// Returns the total number of required cards to get the items needed (<total> - <owned>) for the specified rarity.
		/// </summary>
		public int GetMinimumRequiredCards(RarityLevel rarity, int total, int owned)
		{
			int count = 0;
			ReqPack packToBuy = GetLowestCardForRarity(rarity);

			int itemsLeft = total - owned;
			count = (itemsLeft - 1) / packToBuy.PermCount + 1; // Divide the items that are needed by two (rounded up)

			//for (int i = 0; i < itemsLeft; i++)
			//{
			//	bool gotPermanent = false;
			//	int random = rnd.Next(0, 100);
			//	//while ((random < packToBuy.PermChance))
			//	//if (gotPermanent)
			//	if (random < 100)
			//	{
			//		//count += packToBuy.PermCount;
			//		count++;
			//	}
			//}

			return count;
		}

		/// <summary>
		/// Returns a reference to the lowest cardpack needed to get an item with the specified rarity.
		/// </summary>
		public ReqPack GetLowestCardForRarity(RarityLevel rarity)
		{
			ReqPack packToBuy = ReqPacks[0];
			if (rarity == RarityLevel.Uncommon || rarity == RarityLevel.Rare)
			{
				packToBuy = ReqPacks[1];
			}
			else if (rarity == RarityLevel.UltraRare || rarity == RarityLevel.Legendary)
			{
				packToBuy = ReqPacks[2];
			}
			return packToBuy;
		}

		public RarityLevel ParseLineToRarity(string line)
		{
			//Match match = Regex.Match(line, rarityLevelPattern);
			//string name = match.Value;
			string name = GetValueFromDataField(line, DataFieldRarity);

			string rarityNoSpaces = Regex.Replace(name, @"\s+", "");
			var rarityLevel = (RarityLevel)Enum.Parse(typeof(RarityLevel), rarityNoSpaces, true);
			return rarityLevel;
		}

		public class ReqSpecialRewardData
		{
			public string Name { get; set; }
			public string Rarity { get; set; }
			public string Have { get; set; }
			public string Standard { get; set; }
			public string Notes { get; set; }
		}

		public class ReqSpecialRewardDataContainer
		{
			public List<ReqSpecialRewardData> Items { get; set; }
		}

		public class ReqGroupCategory
		{
			public int TotalCount { get; set; }
			public int OwnedCount { get; set; }
			public ReqCategory ReqCategory { get; set; }
			public List<ReqGroupStat> reqGroupStats = new List<ReqGroupStat>();

			public ReqGroupCategory(ReqCategory reqCategory)
			{
				ReqCategory = reqCategory;
				for (int i = 0; i < Enum.GetNames(typeof(RarityLevel)).Length; i++)
				{
					reqGroupStats.Add(new ReqGroupStat((RarityLevel)i));
				}
			}
		}

		public class ReqGroupStat
		{
			public int TotalCount { get; set; }
			public int OwnedCount { get; set; }
			public RarityLevel RarityLevel { get; set; }

			public ReqGroupStat(RarityLevel rarityLevel)
			{
				RarityLevel = rarityLevel;
			}
		}

		public enum RarityLevel
		{
			Common,
			Uncommon,
			Rare,
			UltraRare,
			Legendary
		}

		public enum ReqCategory
		{
			Customization,
			Loadout,
			PowerVehicle
		}

		public enum ReqType
		{
			Helmet,
			ArmorSuit,
			Emblem,
			Visor,
			Stance,
			Assassination,
			WeaponSkin,
			PowerWeapon,
			Vehicle,
			Equipment,
			Loadout
		}

		public class ReqPack
		{
			public string Name { get; set; }
			public int Price { get; set; }
			public int PermCount { get; set; }
			public int PermChance { get; set; }
			public int ItemsNeededCount { get; set; }
			public int PacksNeededCount { get { return (ItemsNeededCount - 1) / PermCount + 1; ; } }

			public ReqPack(string name, int price, int permCount, int permChance = 100)
			{
				Name = name;
				Price = price;
				PermCount = permCount;
				PermChance = permChance;
				ItemsNeededCount = 0;
			}
		}
	}
}
