using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Halo5ReqParser
{
	public class Parser
	{
		public string reqFileCustomization = "H5ReqCust.htm";
		public string reqFileLoadout = "H5ReqLoad.htm";
		public string reqFilePower = "H5ReqPower.htm";

		public string haveOwnedPattern = "data-have-owned=\"((True)|(False))\"";
		public string haveCertPattern = "data-has-certification=\"((True)|(False))\"";
		public string isDurablePattern = "data-is-durable=\"((True)|(False))\"";
		public string rarityLinePattern = "data-rarity=\"((Common)|(Uncommon)|(Rare)|(Ultra Rare)|(Legendary))\"";
		public string rarityLevelPattern = "((Common)|(Uncommon)|(Rare)|(Ultra Rare)|(Legendary))";

		public List<ReqGroupCategory> reqGroups = new List<ReqGroupCategory>();

		//public static ReqPack BronzePack = new ReqPack("Bronze", 1250, 1, 100);
		//public static ReqPack SilverPack = new ReqPack("Silver", 5000, 2);
		//public static ReqPack GoldPack = new ReqPack("Gold", 10000, 2);
		public readonly ReqPack[] ReqPacks = new ReqPack[]
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

		public void ParseAllReqs()
		{
			ParseData(reqFileCustomization, ReqCategory.Customization);
			ParseData(reqFileLoadout, ReqCategory.Loadout);
			ParseData(reqFilePower, ReqCategory.PowerVehicle);

			int ownedCount = 0, totalCount = 0;
			//int[] totals = new int[] { 0, 0, 0, 0, 0 };
			List<ReqGroupStat> totals = new List<ReqGroupStat>();
			for (int i = 0; i < Enum.GetNames(typeof(RarityLevel)).Length; i++)
			{
				totals.Add(new ReqGroupStat((RarityLevel)i));
			}

			foreach (var group in reqGroups)
			{
				ownedCount += group.OwnedCount;
				totalCount += group.TotalCount;

				printl("Category: " + group.ReqCategory + "(" + group.OwnedCount + "/" + group.TotalCount + ")");
				foreach (var item in group.reqGroupStats)
				{
					//printl(" - " + item.RarityLevel + ": " + item.OwnedCount + "/" + item.TotalCount);
					printl(string.Format(" - {0, -9}: {1}/{2}", item.RarityLevel, item.OwnedCount, item.TotalCount));
					//costs[(int)item.RarityLevel] += GetMinimumRequiredCards(item.RarityLevel, item.TotalCount, item.OwnedCount);
					totals[(int)item.RarityLevel].OwnedCount += item.OwnedCount;
					totals[(int)item.RarityLevel].TotalCount += item.TotalCount;
				}
				printl();
			}

			foreach (var total in totals)
			{
				GetLowestCardForRarity(total.RarityLevel).NeededCount += total.TotalCount - total.OwnedCount;
			}

			printl(string.Format("Total: {0}/{1}", ownedCount, totalCount));
			//for (int i = 0; i < totals.Length; i++)
			//foreach (var total in totals)
			//{
			//	printl(string.Format("Total {0} count: {1}/{2} ({3} left)", total.RarityLevel, total.OwnedCount, total.TotalCount, (total.TotalCount - total.OwnedCount)));
			//	int cardsRequired = GetMinimumRequiredCards(total.RarityLevel, total.TotalCount, total.OwnedCount);
			//	printl(string.Format(" -> {0} packs needed: {1}", GetLowestCardForRarity(total.RarityLevel).Name, cardsRequired));
			//}

			foreach (var pack in ReqPacks)
			{
				printl(string.Format(" {0, 7} packs needed: {1} ({2:###,###} RP)", pack.Name, pack.NeededCount, pack.NeededCount * pack.Price));
			}
		}

		

		public void ParseData(string path, ReqCategory category)
		{
			HtmlDocument doc = new HtmlDocument();
			//var path = reqFileCustomization; //"HWPage.htm";
			doc.Load(path);
			print("-> Getting REQ data " + path + "...");

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
				//print("Rarity = " + rarity);

				// Check for data-have-owned
				//Match match = Regex.Match(card, haveOwenedPattern);
				//if (match.Success)
				//{
				//	//print("Match: " + match.Value);
				//	bool hasOwned = match.Value.Contains("True");
				//	if (hasOwned)
				//	{
				//		totalUnlockedItems++;
				//		reqGroupStats[(int)rarity].OwnedCount++;
				//	}
				//}
				bool isDurable = IsDurable(card); // True if the card is from customization or loadout, false if it is from power & vehicle
				//printl("IsDuarble: " + isDurable);
				if (isDurable)
				{
					if (HaveOwned(card))
					{
						reqGroups[(int)category].OwnedCount++;
						reqGroups[(int)category].reqGroupStats[(int)rarity].OwnedCount++;
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
				reqGroups[(int)category].reqGroupStats[(int)rarity].TotalCount++;
				reqGroups[(int)category].TotalCount++;
			}

			printl(" done\n");
		}

		/// <summary>
		/// Check if the card HTML element has the "data-have-owned" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element.</param>
		/// <returns></returns>
		bool HaveOwned(string card)
		{
			Match match = Regex.Match(card, haveOwnedPattern);
			if (match.Success)
			{
				return match.Value.Contains("True");
			}
			return false;
		}

		/// <summary>
		/// Check if the card HTML element has the "data-has-certification" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element.</param>
		/// <returns></returns>
		bool HaveCert(string card)
		{
			Match match = Regex.Match(card, haveCertPattern);
			if (match.Success)
			{
				return match.Value.Contains("True");
			}
			return false;
		}

		/// <summary>
		/// Checks if the card HTML element has the "data-is-durable" set to True. If not, or something fails it returns false.
		/// </summary>
		/// <param name="card">HTML of the card element</param>
		/// <returns></returns>
		bool IsDurable(string card)
		{
			Match match = Regex.Match(card, isDurablePattern);
			if (match.Success)
			{
				return match.Value.Contains("True");
			}
			return false;
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

		void printl()
		{
			Console.WriteLine();
		}

		void printl(string s)
		{
			Console.WriteLine(s);
		}

		void print(string s)
		{
			Console.Write(s);
		}

		public RarityLevel ParseLineToRarity(string line)
		{
			Match match = Regex.Match(line, rarityLevelPattern);
			string name = match.Value;

			string rarityNoSpaces = Regex.Replace(name, @"\s+", "");
			var rarityLevel = (RarityLevel)Enum.Parse(typeof(RarityLevel), rarityNoSpaces, true);
			return rarityLevel;
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

		public class ReqPack
		{
			public string Name { get; set; }
			public int Price { get; set; }
			public int PermCount { get; set; }
			public int PermChance { get; set; }
			public int NeededCount { get; set; }

			public ReqPack(string name, int price, int permCount, int permChance = 100)
			{
				Name = name;
				Price = price;
				PermCount = permCount;
				PermChance = permChance;
				NeededCount = 0;
			}
		}
	}
}
