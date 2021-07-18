using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace Sagira_Bot
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
    public class Sagira
    {
        readonly BungieDriver bungie; //Singleton, we just need one BungieDriver ever.
        const string itemTable = "DestinyInventoryItemDefinition"; //Main db we'll be using to pull an item's manifest entry. Results from here are all JSON.
        const string perkSetTable = "DestinyPlugSetDefinition"; //Main db we'll use to translate every item's perk plug set hash "randomizedPlugSetHash" (combo of perks a gun can roll in a column) into an array of perks
        readonly Dictionary<int, ItemData> ItemTable;
        readonly Dictionary<string, ItemData> Y1WeaponTable;
        readonly Dictionary<string, ItemData> Y2WeaponTable;
        readonly Dictionary<int, PlugSetData> PlugSetTable;
        
        const long trackerDisabled = 2285418970; //Hash for Tracker Socket
        const long intrinsicSocket = 3956125808; //Hash for Intrinsic Perk
        public readonly Dictionary<string, string> RandomExotics = new Dictionary<string, string>()
        {
            {"hawkmoon",""},
            {"dead man's tale",""}
        };
        
        public Sagira()
        {
            bungie = new BungieDriver(); //init
            ItemTable = new Dictionary<int, ItemData>();
            Y1WeaponTable = new Dictionary<string, ItemData>();
            Y2WeaponTable = new Dictionary<string, ItemData>();
            PlugSetTable = new Dictionary<int, PlugSetData>();
            PullDbTables();
            bungie.CloseDB();
        }

        /// <summary>
        /// Pulls entire DB into memory instead of keeping a DB connection maintained.
        /// Only pulls weapons from InventoryItemDB and PlugSetDefinitionDB.
        /// </summary>
        private void PullDbTables()
        {
            Dictionary<int, string> iTable = bungie.QueryEntireDb($"SELECT * FROM {itemTable}");
            Dictionary<int, string> psTable = bungie.QueryEntireDb($"SELECT * FROM {perkSetTable}");
            foreach(KeyValuePair<int, string> pair in iTable)
            {
                ItemData curItem = ParseItem(pair.Value);
                ItemTable[pair.Key] = curItem;
                if (pair.Value.Contains("item_type.weapon") && pair.Value.Contains("collectibleHash")) //Weapons only + weapons with collectibles (aka every real instance of a weapon. See: VOG weapons that have 2x weapon entries)
                {
                    //Only random roll weapons have randomizedPlugSetHash, so label them as y2.
                    if (pair.Value.Contains("randomizedPlugSetHash"))
                    {
                        curItem.Year = 2;
                        Y2WeaponTable[curItem.DisplayProperties.Name.ToLower()] = curItem;
                    }
                    else
                    {
                        curItem.Year = 1;
                        Y1WeaponTable[curItem.DisplayProperties.Name.ToLower()] = curItem;
                    }
                }                   
            }
            foreach (KeyValuePair<int, string> pair in psTable)
            {
                PlugSetTable[pair.Key] = ParsePlug(pair.Value);
            }

        }

        private ItemData PullItemById(int id)
        {
            return ItemTable[id];
        }
        
        /// <summary>
        /// Checks Weapon Dictionary for items whose name match or partially match passed in item name
        /// Prioritizes y2 in the case of duplicates if year = 0 or 2
        /// Prioritizs y1 if year = 1
        /// For substring matches y1 and y2 both get added (with y2 priority) if year = 0
        /// </summary>
        /// <param name="itemName">Name of the item to search for</param>
        /// <param name="Year">0,1,2. 0 = both, 1 = Static rolls, 2 = Random rolls</param>
        /// <returns></returns>
        public List<ItemData> PullItemListByName(string itemName, int Year = 0)
        {
            List<ItemData> resultingItems = new List<ItemData>();
            List<ItemData> ExactMatches = new List<ItemData>();
            List<ItemData> SubstringMatches = new List<ItemData>();
            Dictionary<string, ItemData> resultQueue = new Dictionary<string, ItemData>();
            string searchTarget = itemName.ToLower();
            //Exact Match Check. Prioritize y2 if no year is passed, else y1.
            if (Y2WeaponTable.ContainsKey(searchTarget) && (Year == 0 || Year == 2))
            {
                resultingItems.Add(Y2WeaponTable[searchTarget]);
                return resultingItems;
            }
            else if (Y1WeaponTable.ContainsKey(searchTarget) && (Year == 0 || Year == 1))
            {
                resultingItems.Add(Y1WeaponTable[searchTarget]);
                return resultingItems;
            }
            //Substring Check
            if(Year == 0 || Year == 2)
            {
                foreach (KeyValuePair<string, ItemData> pair in Y2WeaponTable)
                {
                    if (pair.Key.Contains(searchTarget))
                    {
                        resultQueue[pair.Key] = pair.Value;
                    }
                }
            }
            if(Year == 0 || Year == 1)
            {
                foreach (KeyValuePair<string, ItemData> pair in Y1WeaponTable)
                {
                    if (pair.Key.Contains(searchTarget))
                    {
                        if(!resultQueue.ContainsKey(pair.Key))
                            resultQueue[searchTarget] = pair.Value; //Implication here is if the key already exists, it must be a y2 version. If a y2 version exists, it must be prioritized if Year != 1
                    }
                }
            }
            foreach(KeyValuePair<string, ItemData> pair in resultQueue)
            {
                resultingItems.Add(pair.Value);
            }

            return resultingItems;
        }

        /// <summary>
        /// Search for a plug set in the plug set definition dictionary after converting the plug set's hash to its id
        /// </summary>
        /// <param name="hash">Plug set's hash to convert into its id for selections</param>
        /// <returns>PlugSetData object found in dict</returns>
        public PlugSetData PullPlugFromHashes(long? hash, bool Debug = false)
        {
            return PlugSetTable[generateIDfromHash((long)hash)];
        }

        /// <summary>
        /// Pull from the item table using an item's hash. Hash converted to id.
        /// </summary>
        /// <param name="hash">Item's hash to be converted</param>
        /// <returns>ItemData object found in dict</returns>
        public ItemData PullItemFromHash(long? hash, bool Debug = false)
        {
            return ItemTable[generateIDfromHash((long)hash)];
        }

        /// <summary>
        /// Deserialize an item into an ItemData object. This itemData object should contain all relevant information, reference the ItemData class in /SerializationClasses/ItemData.cs
        /// </summary>
        /// <param name="itemJson">json which should come from a db column</param>
        /// <returns>Deserialized ItemData object from ItemData JSON</returns>
        public ItemData ParseItem(string itemJson)
        {
            return JsonConvert.DeserializeObject<ItemData>(itemJson);
        }

        /// <summary>
        /// Deserialize a Plugset into PlugsetData object which should have all perks listed inside of it. Reference the PlugSetData class in /SerializationClasses/PlugSetData.cs
        /// </summary>
        /// <param name="perkJson"></param>
        /// <returns>Deserialized PlugSetData object from randomizedPlugSet JSON</returns>
        public PlugSetData ParsePlug(string perkJson)
        {
            return JsonConvert.DeserializeObject<PlugSetData>(perkJson);
        }

        /// <summary>
        /// An item's db id is the item's hash cast to a signed int. 
        /// </summary>
        /// <param name="hash">Item's hash</param>
        /// <returns>Item's ID in the db</returns>
        public int generateIDfromHash(long hash)
        {
            return unchecked((int)hash);
        }

        /// <summary>
        /// Takes PlugSetData and pulls a list of its available perks. Only returns currently available perks for now. 
        /// </summary>
        /// <param name="plug">PlugSet from an item's item data</param>
        /// <returns>Returns a list of ItemData objects used to represent perks in a plugset</returns>
        public List<ItemData> PullPerksInSet(PlugSetData plug)
        {
            List<ItemData> perkHashes = new List<ItemData>();
            foreach (PerkReusablePlugItem perk in plug.ReusablePlugItems)
            {
                if (perk.CurrentlyCanRoll)
                    perkHashes.Add(PullItemFromHash(perk.PlugItemHash));
            }
            return perkHashes;
        }

        
        /// <summary>
        /// Minor workflow for generating a list of items that match an item name that includes some debugging and error handling
        /// Brunt of the work is just calling PullItemListByName
        /// If nothing is found, return an empty list. If something is found, return it all.
        /// </summary>
        /// <param name="itemName">Name of the item you're searching for, can be partial</param>
        /// <param name="Year">Desired year of the item. Default is 0, but can pass in 1.</param>
        /// <returns></returns>
        public List<ItemData> GenerateItemList(string itemName, int Year = 0)
        {
            try
            {
                List<ItemData> items = PullItemListByName(itemName, Year);
                if(items.Count == 0)
                {
                    bungie.DebugLog($"Could not find desired item: {itemName}", bungie.LogFile);
                    return new List<ItemData>();
                }
                foreach(ItemData item in items)
                {
                    bungie.DebugLog($"Found Item: {item.DisplayProperties.Name}", bungie.LogFile);
                }
                return items;
            }
            catch (Exception e)
            {
                bungie.DebugLog("Item Request Failed Due To: " + e, bungie.LogFile);
                return new List<ItemData>();
            }
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
            Dictionary<string, string>[] perkDict = new Dictionary<string, string>[5]; //Intrinsic + 4 columns max
            int curIdx = 0;
            foreach(SocketEntry socket in item.Sockets.SocketEntries)
            {
                //We look for curated first. If we match any curated perks in random section we know that we can roll these curated perks. Else we keep our default mark of Curated-Unrollable (curated0)
                if ((socket.PlugSources == 2 || socket.PlugSources == 6 || socket.PlugSources == 0) && socket.SingleInitialItemHash != trackerDisabled)
                {
                    //Curated perks are either SingleInitialItemHash(y1+2), ReusablePlugSetHash(y1), or ReusablePlugItems(y2).
                    perkDict[curIdx] = new Dictionary<string, string>();
                    if (socket.ReusablePlugItems.Length == 0)
                    {
                        if(socket.ReusablePlugSetHash != null)
                        {
                            List<ItemData> StaticPerks = PullPerksInSet(PullPlugFromHashes(socket.ReusablePlugSetHash));
                            foreach (ItemData perkData in StaticPerks)
                            {
                                perkDict[curIdx][perkData.DisplayProperties.Name] = socket.SocketTypeHash == intrinsicSocket ? "intrinsic" : "curated0";
                            }
                        }
                        else if(socket.SingleInitialItemHash != 0)
                        {
                           perkDict[curIdx][PullItemFromHash(socket.SingleInitialItemHash).DisplayProperties.Name] = socket.SocketTypeHash == intrinsicSocket ? "intrinsic" : "curated0";
                        }
                    }
                    else if(socket.ReusablePlugItems.Length > 0)
                    {
                        foreach (ReusablePlugItem hash in socket.ReusablePlugItems)
                        {
                            perkDict[curIdx][PullItemFromHash(hash.PlugItemHash).DisplayProperties.Name] = "curated0";
                        }
                    }
                    //Curated perks for this column pulled -- Now pull random perks.
                    if(socket.RandomizedPlugSetHash != null)
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
            for(int i = 0; i < perkDict.Length; i++)
            {
                if(perkDict[i] != null)
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

        ///-----------------------------------------------------------UNUSED CODE BELOW HERE TEMPORARILY-----------------------------------------------------------------------------
/*
        /// <summary>
        /// NOT USED AT THE MOMENT
        /// Pulls specifically the curated roll of y2 gun. Not really used anymore.
        /// Logic here is curated perks take up 1 of 2 slots for y2 guns (3 for y1)
        /// Either SingleInitialItemHash OR ReusablePlugItems[] are populared for curated rolls, so per column just pull either or (prioritizing the array).
        /// </summary>
        /// <param name="item">ItemData object whose curated roll you want to pull</param>
        /// <returns></returns>
        public List<ItemData>[] PullCuratedRoll(ItemData item)
        {
            List<ItemData>[] perkHashes = new List<ItemData>[6];
            //List<long>[] perkHashes = new List<long>[5];
            int curIdx = 0;
            perkHashes[curIdx] = new List<ItemData>();
            perkHashes[curIdx++].Add(item);
            foreach (SocketEntry perk in item.Sockets.SocketEntries)
            {
                if ((perk.PlugSources == 2 || perk.PlugSources == 6) && perk.SingleInitialItemHash != trackerDisabled)
                {
                    perkHashes[curIdx] = new List<ItemData>();
                    if (perk.ReusablePlugItems.Length == 0 && perk.SingleInitialItemHash != 0)
                    {
                        perkHashes[curIdx].Add(PullItemFromHash(perk.SingleInitialItemHash));
                    }
                    else
                    {
                        foreach (ReusablePlugItem hash in perk.ReusablePlugItems)
                        {
                            perkHashes[curIdx].Add(PullItemFromHash(hash.PlugItemHash));
                        }
                    }
                    curIdx++;
                }

            }
            for (int i = 1; i < perkHashes.Length; i++) //Use perkHashes as a reference to how many columns we have. i.e if 3 columns. +1 so we can also account for the item in our first index.
            {
                if (perkHashes[i] != null) //Since some guns have variable number of columns, we need to nullcheck
                {
                    bungie.DebugLog($"CURATED COLUMN {i}: ", bungie.LogFile);
                    foreach (ItemData perk in perkHashes[i])
                    {
                        bungie.DebugLog($"{perk.DisplayProperties.Name}, ", bungie.LogFile);
                    }
                    bungie.DebugLog("", bungie.LogFile);
                }

            }
            return perkHashes;
        }

        /// <summary>
        /// NOT USED AT THE MOMENT
        /// Takes an Item's ItemData and pulls out all randomizedPlugSetHashes to search for in the PlugSetDefinition db.
        /// We know we're look at perks (and not intrisincis, masterworks, shaders, or mods) by only pulling hashes with a PlugSource of 2.
        /// Returns a long? list since RandomizedPlugSetHashes are nullable. 
        /// </summary>
        /// <param name="item">ItemData object for the item whose perks you'd like to pull</param>
        /// <returns>List of every randomized plug set hash (aka perk column)</returns>
        public List<long?> PullRandomizedPerkHash(ItemData item)
        {
            List<long?> hashes = new List<long?>();
            foreach (SocketEntry sock in item.Sockets.SocketEntries)
            {
                if (sock.PlugSources == 2 || sock.PlugSources == 0) //PlugSource = 2 for random perks generally, but =0 if the y1 version of the gun didn't have that column of perk.
                    hashes.Add(sock.RandomizedPlugSetHash);
            }
            return hashes;
        }
        /// <summary>
        /// NOT USED AT THE MOMENT
        /// Y1 Method of pulling perks. Y1 guns use "ReusablePlugSetHash" instead of Randomized. After this point the y1 and y2 workflows are the same. This also works on Exotics and any static roll guns.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public List<long?> PullReusablePerkHash(ItemData item)
        {
            List<long?> hashes = new List<long?>();
            foreach (SocketEntry sock in item.Sockets.SocketEntries)
            {
                if ((sock.PlugSources == 6 || sock.PlugSources == 2) && sock.SingleInitialItemHash != trackerDisabled) //Y2 curated rolls still utilize source = 2, but y1 guns generally have source = 6. Note that FRAME is now a perk, so these curated rolls have up to 5 total columns.
                    hashes.Add(sock.ReusablePlugSetHash);
            }
            return hashes;
        }

        /// <summary>
        /// NOT USED AT THE MOMENT
        /// Prior validation and selection of y1 or y2 usage should guarantee that whatever this function parses is intended
        /// It checks if the gun is legendary and has random rolls, and if so return the columns. If not return the y1 curated roll. 
        /// Y1 static roll workflow conveniently matches the non-random roll exotic workflow :)
        /// Note that y2 curated rolls not added just yet.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public List<ItemData>[] GeneratePerkColumns(ItemData item, bool isCurated = false)
        {

            List<long?> hashes;
            if ((item.Inventory.TierTypeName == "Legendary" && item.DisplaySource.ToLower().Contains("random perks")) || RandomExotics.ContainsKey(item.DisplayProperties.Name.ToLower()))
            {
                bungie.DebugLog($"Y2 Workflow Initiliazed For Item: {item.DisplayProperties.Name}", bungie.LogFile);
                if (!isCurated)
                    hashes = PullRandomizedPerkHash(item);
                else
                    return PullCuratedRoll(item);
            }
            else
            {
                bungie.DebugLog($"Y1/Exotic Workflow Initiliazed For Item: {item.DisplayProperties.Name}", bungie.LogFile);
                hashes = PullReusablePerkHash(item);
                //Pull Exotic Catalysts too
            }
            if (hashes.Count == 0)
            {
                bungie.DebugLog($"Couldn't pull perks for: {item.DisplayProperties.Name}", bungie.LogFile);
                return null;
            }
            List<List<ItemData>> perkHashes = new List<List<ItemData>>();
            List<ItemData>[] perkList = new List<ItemData>[6]; //index 0 = item itself, 1-5 = perk columns [frame] [col1-5]

            foreach (long? hash in hashes)
            {
                perkHashes.Add(PullPerksInSet(PullPlugFromHashes(hash))); //index 0 = column 1, index 3 = column 4
            }
            //First entry will always be the Base item. Rest will be perks split by column.
            perkList[0] = new List<ItemData>();
            perkList[0].Add(item);
            for (int i = 0; i < perkHashes.Count; i++)
            {
                perkList[i + 1] = new List<ItemData>();
                bungie.DebugLog("------------------", bungie.LogFile);
                foreach (ItemData itm in perkHashes[i])
                {
                    perkList[i + 1].Add(item);
                }
            }

            for (int i = 1; i < perkHashes.Count + 1; i++) //Use perkHashes as a reference to how many columns we have. i.e if 3 columns. +1 so we can also account for the item in our first index.
            {
                bungie.DebugLog($"COLUMN {i}: ", bungie.LogFile);
                foreach (ItemData perk in perkList[i])
                {
                    bungie.DebugLog($"{perk.DisplayProperties.Name}, ", bungie.LogFile);
                }
                bungie.DebugLog("", bungie.LogFile);
            }
            return perkList;
        }
        /// <summary>
        /// Take a base image and a smaller image. Overlays smaller image over the base, this is just a sample.
        /// It will eventually be expanded to generate entire infographics anytime a gun is requested. 
        /// Don't bother with this
        /// </summary>
        /// <param name="baseImage">Base image (aka background)</param>
        /// <param name="iconImage">Sample image we're overlaying to the top left corner.</param>
        public static void SampleBitMapEdit(string baseImage, string iconImage)
        {
            Image icon;
            Image frame;
            try
            {
                icon = Image.FromFile(iconImage);
                frame = Image.FromFile(baseImage);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to initialize bitmaps: {e}");
                return;
            }
            using (frame)
            {
                using (var bitmap = new Bitmap(frame.Width, frame.Height))
                {
                    using (var canvas = Graphics.FromImage(bitmap))
                    {
                        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        canvas.DrawImage(frame, new Rectangle(0, 0, icon.Width, icon.Height), new Rectangle(0, 0, frame.Width, frame.Height), GraphicsUnit.Pixel);
                        canvas.DrawImage(icon, 0, 0);
                        canvas.Save();
                    }
                    try
                    {
                        bitmap.Save(@"C:\Users\13476\source\repos\Sagira-Bot\Sagira-Bot\BaseImage\sampleResult.bmp",
                                    System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                    catch (Exception e) { Console.WriteLine($"Failed to overlay bitmaps: {e}"); }
                }
            }

        }
*/
    }
}
