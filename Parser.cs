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
		public string reqFileCustomization = "Data/H5ReqCustTest.htm";
		public string reqFileLoadout = "Data/H5ReqLoadTest.htm";
		public string reqFilePower = "Data/H5ReqPowerTest.htm";

		private const string DataFieldHasCert = "data-has-certification";
		private const string DataFieldHaveOwned = "data-have-owned";
		private const string DataFieldIsDuarble = "data-is-durable";
		private const string DataFieldRarity = "data-rarity";
		private const string DataFieldName = "data-name";
		private const string DataFieldSubCategory = "data-subcategory";

		private readonly string rarityLinePattern = "data-rarity=\"((Common)|(Uncommon)|(Rare)|(Ultra Rare)|(Legendary))\"";

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
			IO.PrintBreak();

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
				foreach (var item in group.ReqGroupStats)
				{
					//printl(" - " + item.RarityLevel + ": " + item.OwnedCount + "/" + item.TotalCount);
					IO.Printl(string.Format(" - {0, -9}: {1}/{2}", item.RarityLevel, item.OwnedCount, item.TotalCount));
					//costs[(int)item.RarityLevel] += GetMinimumRequiredCards(item.RarityLevel, item.TotalCount, item.OwnedCount);
					totals[(int)item.RarityLevel].OwnedCount += item.OwnedCount;
					totals[(int)item.RarityLevel].TotalCount += item.TotalCount;
				}
				IO.Printl();
			}
			IO.PrintBreak();

			IO.Printl("Totals: ");
			foreach (var total in totals)
			{
				int needed = total.TotalCount - total.OwnedCount;
				IO.Printl(string.Format(" - {0, -9}: {1}/{2} ({3})", total.RarityLevel, total.OwnedCount, total.TotalCount, needed));
				GetLowestCardForRarity(total.RarityLevel).ItemsNeededCount = needed;
			}
			IO.Printl();
			IO.PrintDivider();

			IO.Printl(string.Format("Total: {0}/{1}", ownedCount, totalCount));
			foreach (var pack in ReqPacks)
			{
				IO.Printl(string.Format(" {0, 7} packs needed: {1} ({2:###,###} RP)", pack.Name, pack.ItemsNeededCount, pack.PacksNeededCount * pack.Price));
			}
			return 0;
		}

		
		/// <summary>
		/// Parse the data from the given filepath in the given ReqCategory. Returns non-0 on fail.
		/// </summary>
		private int ParseData(string path, ReqCategory category)
		{
			HtmlDocument doc = new HtmlDocument();
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

				// Get the card's rarity level
				RarityLevel rarity = RarityLevel.Common;
				rarity = GetRarityFromText(card);

				// Count based on 
				bool isSpecial = false;
				bool haveOwned = HaveOwned(card);
				string name = GetValueFromDataField(card, DataFieldName);
				if ((IsDurable(card) && haveOwned) || HaveCert(card))
				{
					if (haveOwned)
					{
						// Check if the REQ is special (not awarded from a purchasable pack)
						string subCategoryData = GetValueFromDataField(card, DataFieldSubCategory);
						ReqType reqType = GetReqTypeFromText(subCategoryData);
						isSpecial = IsSpecialReq(name, reqType);
					}
					

					if (!isSpecial)// && !isRandomReq)
					{
						reqGroups[(int)category].OwnedCount++;
						reqGroups[(int)category].ReqGroupStats[(int)rarity].OwnedCount++;
					}
				}

				if (!isSpecial && !name.StartsWith("Random"))
				{
					reqGroups[(int)category].ReqGroupStats[(int)rarity].TotalCount++;
					reqGroups[(int)category].TotalCount++;
				}
			}

			IO.Printl(" done\n");
			return 0;
		}

		/// <summary>
		/// Parses the JSON special data files, and puts the data in reqSpecialDataContainer
		/// </summary>
		private void ParseJsonData()
		{
			IO.Print("-> Initializing... ");
			
			reqSpecialDataContainer = new List<ReqSpecialRewardDataContainer>();
			for(int i = 0; i < specialDataFiles.Length; i ++)
			{
				var json = File.ReadAllText(specialDataFiles[i]);
				var dataObj = JsonConvert.DeserializeObject<ReqSpecialRewardDataContainer>(json);
				reqSpecialDataContainer.Add(dataObj);

				IO.Print(string.Format("[{0}/{1}] ", i+1, specialDataFiles.Length));
			}
			IO.Print("done\n\n");
		}

		/// <summary>
		/// Checks if the REQ is special (has something in the Notes columns of the parsed JSON files)
		/// </summary>
		/// <param name="name"></param>
		/// <param name="reqType"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Returns the value from a field specified with the handle in the specified text. Returns null if no value could be found for that handle.
		/// </summary>
		/// <param name="text">The text containing the field.</param>
		/// <param name="handle">The handle of the field (e.g. data-name, data-has-owned, etc)</param>
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

		/// <summary>
		/// Checks wheter the data has the value "true". Returns false if data is null.
		/// </summary>
		private bool GetDataBoolValue(string data)
		{
			if (data == null) return false;
			return data.ToLower().Contains("true");
		}

		/// <summary>
		/// Parses the text to a ReqType.
		/// </summary>
		private ReqType GetReqTypeFromText(string text)
		{
			ReqType reqType = ReqType.Loadout;
			Enum.TryParse(text, out reqType);
			return reqType;
		}

		/// <summary>
		/// Check if the card HTML element has the "data-have-owned" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element.</param>
		/// <returns></returns>
		private bool HaveOwned(string card)
		{
			string data = GetValueFromDataField(card, DataFieldHaveOwned);
			return GetDataBoolValue(data);
		}

		/// <summary>
		/// Check if the card HTML element has the "data-has-certification" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element.</param>
		/// <returns></returns>
		private bool HaveCert(string card)
		{
			string data = GetValueFromDataField(card, DataFieldHasCert);
			return GetDataBoolValue(data);
		}

		/// <summary>
		/// Checks if the card HTML element has the "data-is-durable" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element</param>
		/// <returns></returns>
		private bool IsDurable(string card)
		{
			string data = GetValueFromDataField(card, DataFieldIsDuarble);
			return GetDataBoolValue(data);
		}
		
		///// <summary>
		///// Returns the total number of required cards to get the items needed (<total> - <owned>) for the specified rarity.
		///// </summary>
		//public int GetMinimumRequiredCards(RarityLevel rarity, int total, int owned)
		//{
		//	int count = 0;
		//	ReqPack packToBuy = GetLowestCardForRarity(rarity);

		//	int itemsLeft = total - owned;
		//	count = (itemsLeft - 1) / packToBuy.PermCount + 1; // Divide the items that are needed by two (rounded up)

		//	//for (int i = 0; i < itemsLeft; i++)
		//	//{
		//	//	bool gotPermanent = false;
		//	//	int random = rnd.Next(0, 100);
		//	//	//while ((random < packToBuy.PermChance))
		//	//	//if (gotPermanent)
		//	//	if (random < 100)
		//	//	{
		//	//		//count += packToBuy.PermCount;
		//	//		count++;
		//	//	}
		//	//}

		//	return count;
		//}

		/// <summary>
		/// Returns a reference to the lowest cardpack needed to get an item with the specified rarity.
		/// </summary>
		private ReqPack GetLowestCardForRarity(RarityLevel rarity)
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

		/// <summary>
		/// Returns the rarity parsed from the rarity data field in the given text.
		/// </summary>
		private RarityLevel GetRarityFromText(string text)
		{
			string name = GetValueFromDataField(text, DataFieldRarity);

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
			public List<ReqGroupStat> ReqGroupStats { get; set; }

			public ReqGroupCategory(ReqCategory reqCategory)
			{
				ReqCategory = reqCategory;
				ReqGroupStats = new List<ReqGroupStat>();
				for (int i = 0; i < Enum.GetNames(typeof(RarityLevel)).Length; i++)
				{
					ReqGroupStats.Add(new ReqGroupStat((RarityLevel)i));
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
			/// <summary>
			/// The name of this pack.
			/// </summary>
			public string Name { get; set; }

			/// <summary>
			/// The price of this pack.
			/// </summary>
			public int Price { get; set; }

			/// <summary>
			/// Amount of permanent unlock this pack gives.
			/// </summary>
			public int PermCount { get; set; }

			/// <summary>
			/// The chance to get a permanen unlock (0-100). Default = 100.
			/// </summary>
			public int PermChance { get; set; }

			/// <summary>
			/// The amount of items from this pack needed.
			/// </summary>
			public int ItemsNeededCount { get; set; }

			/// <summary>
			/// The amount of packs needed to buy based on ItemsNeededCount and PermCount.
			/// </summary>
			public int PacksNeededCount { get { return (ItemsNeededCount - 1) / PermCount + 1; ; } }

			/// <summary>
			/// Initialize a new pack with the given values.
			/// </summary>
			/// <param name="name">Name of the pack</param>
			/// <param name="price">Price of the pack</param>
			/// <param name="permCount">Permanent count of the pack</param>
			/// <param name="permChance">Permanent chance of the pack</param>
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
