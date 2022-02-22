using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using ItemData = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemDefinition;
using PlugSetData = BungieSharper.Entities.Destiny.Definitions.Sockets.DestinyPlugSetDefinition;
using SocketEntry = BungieSharper.Entities.Destiny.Definitions.DestinyItemSocketEntryDefinition;
using ItemStatDefinition = BungieSharper.Entities.Destiny.Definitions.DestinyInventoryItemStatDefinition;
using StatDefinition = BungieSharper.Entities.Destiny.Definitions.DestinyStatDefinition;
using Sagira.Util;

namespace Sagira.Services
{
    /// <summary>
    /// This is the main DB handler class. It interacts with the db initialized in BungieDriver and parses information as needed. 
    /// Results go from here to the bot.
    /// Simplified Workflow for Perks (Stats are similar, but easier)
    /// Pull item by NAME from itemTable. Per Perk Column pull each "randomizedPlugSetHash" value. Pull array of every reusablePlugItem perk from from perkSetTable. Convery each Hash by searching for them in itemTable again.
    /// Can use the initial itemTable entry for any properties past that, stats, image, etc. included.
    /// Workflows: Y2, Y1, and Exotics are all the same except that Y1 and Exotics use "reusablePlugSetHash" instead of "randomizedPlugSetHash". 
    /// Y2 Curated rolls are different. Per Socket they have "reusablePlugItems[]" but that's effectively skipping a step per socket, provided that plugSource is 2 or 6.
    /// Exotic catalysts all start with: singleInitialItemHash: 1498917124 
    /// ASSUMPTION: Only 2 entries of a single weapon can exist at any given moment -- y1 and y2. But not all y2 guns have y1 variants.
    /// If this turns out to be false, change Y1/Y2WeaponTable Dictionaries to a single weapon table again.
    /// </summary>
    public class ItemHandler
    {
        readonly BungieDriver bungie; //Singleton, we just need one BungieDriver ever.

        private const string _itemTableName = "DestinyInventoryItemDefinition"; //Main db we'll be using to pull an item's manifest entry. Results from here are all JSON.
        private const string _perkSetTableName = "DestinyPlugSetDefinition"; //Main db we'll use to translate every item's perk plug set hash "randomizedPlugSetHash" (combo of perks a gun can roll in a column) into an array of perks
        private const string _statDefinitionTableName = "DestinyStatDefinition";

        private const long trackerDisabled = 2285418970; //Hash for Tracker Socket
        private const long intrinsicSocket = 3956125808; //Hash for Intrinsic Perk
        private const long originSocket = 3993098925; //Hash for origin perk

        private Dictionary<uint, ItemData> _itemTable;
        private Dictionary<string, ItemData> _staticWeaponTable;
        private Dictionary<string, ItemData> _randomWeaponTable;
        private Dictionary<uint, PlugSetData> _plugSetTable;
        private Dictionary<uint, StatDefinition> _statDefinitionTable;

        

        public readonly Dictionary<string, string> RandomExotics = new Dictionary<string, string>()
        {
            {"hawkmoon",""},
            {"dead man's tale",""}
        };

        public ItemHandler(string bungieApiKey = "")
        {
            bungie = new BungieDriver(bungieApiKey); //init
            _itemTable = new();
            _staticWeaponTable = new();
            _randomWeaponTable = new();
            _plugSetTable = new();
            _statDefinitionTable = new();

            PullDbTables().GetAwaiter().GetResult();
            Console.WriteLine($"Item Table Entries: {_itemTable.Count}{Environment.NewLine}" +
                $"Y1 Weapon Table Entries: {_staticWeaponTable.Count}{Environment.NewLine}" +
                $"Y2 Weapon Table Entries: {_randomWeaponTable.Count}{Environment.NewLine}" +
                $"Plug Set Table Entries: {_plugSetTable.Count}{Environment.NewLine}" +
                $"Stat Definition Table Entries: {_statDefinitionTable.Count}{Environment.NewLine}");
        }

        /// <summary>
        /// Pulls entire DB into memory instead of keeping a DB connection maintained.
        /// Only pulls weapons from InventoryItemDB and PlugSetDefinitionDB.
        /// </summary>
        private async Task PullDbTables()
        {
            await bungie.PullManifest();

            _itemTable = JsonSerializer.Deserialize<Dictionary<uint, ItemData>>(await bungie.GetTable(_itemTableName));
            _plugSetTable = JsonSerializer.Deserialize<Dictionary<uint, PlugSetData>>(await bungie.GetTable(_perkSetTableName));
            _statDefinitionTable = JsonSerializer.Deserialize<Dictionary<uint, StatDefinition>>(await bungie.GetTable(_statDefinitionTableName));

            foreach (KeyValuePair<uint, ItemData> pair in _itemTable)
            {
                var currentItem = pair.Value;
                if (IsWeapon(currentItem) && currentItem.CollectibleHash != null) //Weapons only + weapons with collectibles (aka every real instance of a weapon. See: VOG weapons that have 2x weapon entries)
                {
                    //Only random roll weapons have randomizedPlugSetHash, so label them as y2.
                    if (IsRandomRollable(currentItem))
                    {
                        _randomWeaponTable[currentItem.DisplayProperties.Name.ToLower()] = currentItem;
                    }
                    else
                    {
                        _staticWeaponTable[currentItem.DisplayProperties.Name.ToLower()] = currentItem;
                    }
                }
            }
        }

        /// <summary>
        /// Checks Weapon Dictionary for items whose name match or partially match passed in item name
        /// Prioritizes y2 in the case of duplicates if year = 0 or 2
        /// Prioritizs y1 if year = 1
        /// For substring matches y1 and y2 both get added (with y2 priority) if year = 0
        /// </summary>
        /// <param name="itemName">Name of the item to search for</param>
        /// <param name="year">0,1,2. 0 = both, 1 = Static rolls, 2 = Random rolls</param>
        /// <returns></returns>
        public List<ItemData> PullItemListByName(string itemName, int year = 0)
        {
            List<ItemData> resultingItems = new List<ItemData>();
            Dictionary<string, ItemData> resultQueue = new Dictionary<string, ItemData>();
            string searchTarget = itemName.ToLower();
            if (year == 0 || year == 2)
            {
                foreach (KeyValuePair<string, ItemData> pair in _randomWeaponTable)
                {
                    if (CommonUtils.RemoveSpecialCharacters(pair.Key).Contains(CommonUtils.RemoveSpecialCharacters(searchTarget)))
                    {
                        resultQueue[pair.Key] = pair.Value;
                    }
                }
            }
            if (year == 0 || year == 1)
            {
                foreach (KeyValuePair<string, ItemData> pair in _staticWeaponTable)
                {
                    if (CommonUtils.RemoveSpecialCharacters(pair.Key).Contains(CommonUtils.RemoveSpecialCharacters(searchTarget)))
                    {
                        if (!resultQueue.ContainsKey(pair.Key))
                            resultQueue[pair.Key] = pair.Value; //Implication here is if the key already exists, it must be a y2 version. If a y2 version exists, it must be prioritized if Year != 1
                    }
                }
            }
            foreach (KeyValuePair<string, ItemData> pair in resultQueue)
            {
                resultingItems.Add(pair.Value);
            }

            return resultingItems;
        }

        /// <summary>
        /// Minor workflow for generating a list of items that match an item name that includes some debugging and error handling
        /// Brunt of the work is just calling PullItemListByName
        /// If nothing is found, return an empty list. If something is found, return it all.
        /// </summary>
        /// <param name="itemName">Name of the item you're searching for, can be partial</param>
        /// <param name="year">Desired year of the item. Default is 0, but can pass in 1.</param>
        /// <returns></returns>
        public List<ItemData> GenerateItemList(string itemName, int year = 0)
        {
            try
            {
                List<ItemData> items = PullItemListByName(itemName, year);
                if (items.Count == 0)
                {
                    Console.WriteLine($"Could not find desired item: {itemName}");
                    return new List<ItemData>();
                }
                foreach (ItemData item in items)
                {
                    Console.WriteLine($"Found Item: {item.DisplayProperties.Name}");
                }
                return items;
            }
            catch (Exception e)
            {
                Console.WriteLine("Item Request Failed Due To: " + e);
                return new List<ItemData>();
            }
        }
        public string GetIntrinsicOnly(ItemData item)
        {
            foreach(SocketEntry socket in item.Sockets.SocketEntries)
            {
                if(socket.SocketTypeHash == intrinsicSocket)
                {
                    return PullItemFromHash(socket.SingleInitialItemHash).DisplayProperties.Name;
                }
            }
            return "";
        }
        /// <summary>
        /// Takes an Item and generates a formatted Dictionary array of perks with Key: Perk Name, Value: Perk State. Each index of the array represents a column of the weapon (column 0 is intrinsic)
        /// Perk State refers to 1 of 4 stats: Intrinsic Perk (intrinsic), Curated Non-Rollable (curated0), Curated Rollable (curated1),  Random only (random)
        /// Note that some Curated rolls have perks appearing in different columns that they're random rollable in, these count as Curated Non-Rollable.
        /// Perks are pulled in two ways. First we pull Curated rolls (also doubles as y1 rolls for most if not all guns) and mark them all as Curated Non-Rollable (intrinsic is marked as intrinsic). 
        /// Curated rolls attempt to pull from ReusablePlugItems first, and if that fails try ReusablePlugSetHash, and as a final resort try SingleInitialItemHash.
        /// Random rolls just try to pull from randomizedPlugSetHash. When we add random rolls, if any random roll is already found in that specific column's Dictionary entry, we set that perk's state from curated0 to curated1 to indicate rollable.
        /// All other rolls are marked Random.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Dictionary<string, string>[] GeneratePerkDict(ItemData item)
        {
            Dictionary<string, string>[] perkDict = new Dictionary<string, string>[6]; //Intrinsic + 4 random perk columns + 1 origin column max
            int curIdx = 0;
            foreach (SocketEntry socket in item.Sockets.SocketEntries)
            {
                int PlugSrc = (int)socket.PlugSources;
                //We look for curated first. If we match any curated perks in random section we know that we can roll these curated perks. Else we keep our default mark of Curated-Unrollable (curated0)
                if ((PlugSrc == 2 || PlugSrc == 6 || PlugSrc == 0) && socket.SingleInitialItemHash != trackerDisabled)
                {
                    //Curated perks are either SingleInitialItemHash(y1+2), ReusablePlugSetHash(y1), or ReusablePlugItems(y2).
                    perkDict[curIdx] = new Dictionary<string, string>();
                    if (socket.ReusablePlugItems.Count() == 0)
                    {
                        if (socket.ReusablePlugSetHash != null)
                        {
                            List<ItemData> StaticPerks = PullPerksInSet(PullPlugFromHashes(socket.ReusablePlugSetHash));
                            foreach (ItemData perkData in StaticPerks)
                            {
                                perkDict[curIdx][perkData.DisplayProperties.Name] = DetermineCuratedSocketType(socket.SocketTypeHash);
                            }
                        }
                        else if (socket.SingleInitialItemHash != 0)
                        {
                            perkDict[curIdx][PullItemFromHash(socket.SingleInitialItemHash).DisplayProperties.Name] = DetermineCuratedSocketType(socket.SocketTypeHash);
                        }
                    }
                    else if (socket.ReusablePlugItems.Count() > 0)
                    {
                        foreach (var hash in socket.ReusablePlugItems)
                        {
                            perkDict[curIdx][PullItemFromHash(hash.PlugItemHash).DisplayProperties.Name] = "curated0";
                        }
                    }
                    //Curated perks for this column pulled -- Now pull random perks.
                    if (socket.RandomizedPlugSetHash != null)
                    {
                        //We only look at RandomizedPlugSetHash if applicable here. If not, there is no non-curated roll so we just skip this portion.
                        List<ItemData> RandomPerks = PullPerksInSet(PullPlugFromHashes(socket.RandomizedPlugSetHash));
                        //Generic workflow of using Perk's Hash -> ID to query the DB and grab the perk, then check if the perk already exists in our Dictionary Array's corresponding Column Dictionary.
                        //If so, mark as curated rollable
                        foreach (ItemData perkData in RandomPerks)
                        {
                            string tmpPerkName = perkData.DisplayProperties.Name;
                            if (perkDict[curIdx].ContainsKey(tmpPerkName) && perkDict[curIdx][tmpPerkName].Contains("curated"))
                            {
                                perkDict[curIdx][tmpPerkName] = "curated1";
                            }
                            else
                            {
                                perkDict[curIdx][tmpPerkName] = "random";
                            }
                        }
                    }
                    curIdx++;
                }
            }
            for (int i = 0; i < perkDict.Length; i++)
            {
                if (perkDict[i] != null)
                {
                    Console.WriteLine($"Column {i + 1}:");
                    foreach (KeyValuePair<string, string> pair in perkDict[i])
                    {
                        Console.WriteLine($"{pair.Key} - {pair.Value}");
                    }
                }
            }
            return perkDict;
        }

        private string DetermineCuratedSocketType (long socketHash)
        {
            if(socketHash == intrinsicSocket)
            {
                return "intrinsic";
            }
            //if(socketHash == originSocket)
            //{
            //    return "origin";
            //}
            return "curated0";
        }
        public Dictionary<string, int> GenerateStatDict(ItemData item)
        {
            Dictionary<string, int> statDict = new Dictionary<string, int>();
            Dictionary<uint, ItemStatDefinition> statContainer = item.Stats.Stats;
            foreach(var entry in statContainer)
            {
                string name = _statDefinitionTable[entry.Key].DisplayProperties.Name;
                if (!name.ToLower().Contains("attack") && !name.ToLower().Contains("power") && name.Length > 0)
                {
                    string entryKey = _statDefinitionTable[entry.Key].DisplayProperties.Name;
                    if(entryKey.ToLower().Equals("recoil direction"))
                    {
                        entryKey = "Recoil";
                    }
                    if (entryKey.ToLower().Contains("rounds"))
                    {
                        entryKey = "RPM";
                    }
                    statDict.Add(entryKey, entry.Value.Value);
                }
            }
            return statDict;
        }

        public string DetermineRecoilDirection(int Recoil)
        {
            string direction = "";

            double calculatedValue = Math.Round(Math.Sin((Recoil + 5) * (2 * Math.PI) / 20) * (100 - Recoil));
            if(calculatedValue > 0)
            {
                direction = "Tends Right";
            }
            else if(calculatedValue < 0)
            {
                direction = "Tends Left";
            }
            else
            {
                direction = "Tends Verical";
            }
            Console.WriteLine($"Calculated Direction Numerically: {calculatedValue}");
            return direction;
        }

        public PlugSetData PullPlugFromHashes(uint? hash, bool Debug = false)
        {
            if (hash != null)
                return _plugSetTable[(uint)hash];
            else
                return null;
        }

        public ItemData PullItemFromHash(uint? hash, bool Debug = false)
        {
            if (hash != null)
                return _itemTable[(uint)hash];
            else
                return null;
        }

        public StatDefinition PullStatFromHash(uint? hash, bool Debug = false)
        {
            if (hash != null)
                return _statDefinitionTable[(uint)hash];
            else
                return null;
        }

        public List<ItemData> PullPerksInSet(PlugSetData plug)
        {
            List<ItemData> perkHashes = new List<ItemData>();
            foreach (var perk in plug.ReusablePlugItems)
            {
                if (perk.CurrentlyCanRoll)
                    perkHashes.Add(_itemTable[perk.PlugItemHash]);
            }
            return perkHashes;
        }

        public bool IsRandomRollable(ItemData item)
        {
            foreach (SocketEntry socket in item.Sockets.SocketEntries)
            {
                if (socket.RandomizedPlugSetHash != null)
                    return true;
            }
            return false;
        }

        public bool IsWeapon(ItemData item)
        {
            if (item.TraitIds != null)
            {
                foreach (var trait in item.TraitIds)
                {
                    if (trait == "item_type.weapon")
                        return true;
                }
            }
            return false;
        }

    }
}
