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
    /// </summary>
    public class Sagira
    {
        readonly BungieDriver bungie; //Singleton, we just need one BungieDriver ever.
        const string itemTable = "DestinyInventoryItemDefinition"; //Main db we'll be using to pull an item's manifest entry. Results from here are all JSON.
        const string perkSetTable = "DestinyPlugSetDefinition"; //Main db we'll use to translate every item's perk plug set hash "randomizedPlugSetHash" (combo of perks a gun can roll in a column) into an array of perks
        const long trackerDisabled = 2285418970;
        readonly Dictionary<string, string> RandomExotics = new Dictionary<string, string>()
        {
            {"hawkmoon",""},
            {"dead man's tale",""}
        };
        
        public Sagira()
        {
            bungie = new BungieDriver(); //init
        }
        /// <summary>
        /// Search by substring for an item's name. Item name has to be exact in this way -- Will add a general search feature later for substrings of item names to work.
        /// The if condition into the for loop does one thing. It doubles up on any instance of an apostraphe in the item's name, so that the DB query doesn't escape at the item's initial apostraphe. 
        /// Of course basically every item is limited to a single apostraphe, so it's a bit superfluous to iterate over the entire string to double every instance of an apostraphe, but in the off chance an item with multiple ever gets added, this'll cover it. 
        /// </summary>
        /// <param name="itemName">item name, either partial or exact</param>
        /// <param name="Year">Year mode. 0 = Ignore year, pull any matches. 1 = Year 1 only. 2 = Year 2 only. </param>
        /// <returns>JSON entries of items whose name contain itemName</returns>
        public List<ItemData> PullItemByName(string itemName, int Year = 0)
        {
            if (itemName.Contains("'"))
            {
                string doubleSingleQuote = "";
                char[] chars = itemName.ToCharArray();
                for(int i = 0; i < chars.Length; i++)
                {
                    doubleSingleQuote += chars[i];
                    if (chars[i] == '\'')
                        doubleSingleQuote += "'";
                }
                itemName = doubleSingleQuote;
            }
            List<ItemData> resultingItems = new List<ItemData>();
            List<string> itemList = bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE CHARINDEX('\"name\":\"{itemName.ToLower()}\"', lower(json)) > 0 AND CHARINDEX('\"collectibleHash\"', json) > 0 AND CHARINDEX('item_type.weapon', json) > 0");
            if(itemList.Count > 0)
            {
                if (itemList.Count > 1)
                {
                    foreach (string itemVersion in itemList)
                    {
                        if (itemVersion.Contains("randomizedPlugSetHash") && (Year == 0 || Year == 2))
                        {
                            resultingItems.Add(ParseItem(itemVersion));
                        }
                        else if (!(itemVersion.Contains("randomizedPlugSetHash")) && (Year == 0 || Year == 1)) //If no random perks and you only want year 1 (aka non-year2)
                        {
                            resultingItems.Add(ParseItem(itemVersion));
                        }
                    }
                }
                else
                {
                    resultingItems.Add(ParseItem(itemList[0]));
                }
                return resultingItems; //Technically there is no guarantee we only return 1 item here. So we need to check if we returned multiple.
            }
            else
            {
                itemList = bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE lower(json) like '%\"name\":\"%{itemName.ToLower()}%\"%' AND CHARINDEX('\"collectibleHash\"', json) > 0 AND CHARINDEX('item_type.weapon', json) > 0");
                //string RegexPattern = $".*\"name\":\"[a-z ]*{itemName.ToLower()}[a-z ]*\".*";
                if(itemList.Count > 0)
                {
                    if(itemList.Count > 1)
                    {
                        for (int i = 0; i < itemList.Count; i++)
                        {
                            ItemData deserialize = ParseItem(itemList[i]);
                            if (deserialize.DisplayProperties.Name.ToLower().Contains(itemName.ToLower()))
                            {
                                if (itemList[i].Contains("randomizedPlugSetHash") && (Year == 0 || Year == 2))
                                {
                                    resultingItems.Add(deserialize);
                                }
                                else if (!(itemList[i].Contains("randomizedPlugSetHash")) && (Year == 0 || Year == 1))
                                {
                                    resultingItems.Add(deserialize);
                                }
                            }
                        }
                    }
                    else
                    {
                        resultingItems.Add(ParseItem(itemList[0]));
                    }
                }

                return resultingItems; //Any item whose name strictly contains a substring that is our item name. Exact matches are prioritized and returned without these substring-holding entries. This could be empty.
            }

        }

        /// <summary>
        /// Search for a plug set in the plug set definition table after converting the plug set's hash to its id
        /// Query returns a list of strings, but this is guaranteed to only provide 1 result or an error
        /// </summary>
        /// <param name="hash">Plug set's hash to convert into its id for selections</param>
        /// <returns>JSON entry of the plug set</returns>
        public string PullPlugFromHashes(long? hash, bool Debug = false)
        {
            return bungie.QueryDB($"SELECT json FROM '{perkSetTable}' WHERE id={generateIDfromHash((long)hash)}", Debug)[0];

        }

        /// <summary>
        /// Pull from the item db using an item's hash. Hash converted to id.
        /// Query returns a list of strings, but this is guaranteed to only provide 1 result or an error
        /// </summary>
        /// <param name="hash"></param>
        /// <returns>JSON entry of the item</returns>
        public string PullItemFromHash(long? hash, bool Debug = false)
        {
            return bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE id={generateIDfromHash((long)hash)}", Debug)[0];
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
        /// Returns a list of longs since these aren't nullable. A plug set must have perks in a randomizedPlugSet.
        /// </summary>
        /// <param name="plug">PlugSet from an item's item data</param>
        /// <returns>Returns a list of perks in a plugset</returns>
        public List<long> PullPerksInSet(PlugSetData plug)
        {
            List<long> perkHashes = new List<long>();
            foreach (PerkReusablePlugItem perk in plug.ReusablePlugItems)
            {
                if (perk.CurrentlyCanRoll)
                    perkHashes.Add(perk.PlugItemHash);
            }
            return perkHashes;
        }

        public List<ItemData>[] PullCuratedRoll(ItemData item)
        {
            List<ItemData>[] perkHashes = new List<ItemData>[6];
            //List<long>[] perkHashes = new List<long>[5];
            int curIdx = 0;
            perkHashes[curIdx] = new List<ItemData>();
            perkHashes[curIdx++].Add(item);
            foreach (SocketEntry perk in item.Sockets.SocketEntries)
            {
                if ((perk.PlugSources == 2 || perk.PlugSources == 6) && perk.ReusablePlugSetHash != 3)
                {
                    perkHashes[curIdx] = new List<ItemData>();
                    if(perk.ReusablePlugItems.Length == 0 && perk.SingleInitialItemHash != 0)
                    {
                        perkHashes[curIdx].Add(ParseItem(PullItemFromHash(perk.SingleInitialItemHash)));
                    }
                    else
                    {
                        foreach(ReusablePlugItem hash in perk.ReusablePlugItems)
                        {
                            perkHashes[curIdx].Add(ParseItem(PullItemFromHash(hash.PlugItemHash)));
                        }
                    }
                    curIdx++;
                }
                    
            }
            for (int i = 1; i < perkHashes.Length; i++) //Use perkHashes as a reference to how many columns we have. i.e if 3 columns. +1 so we can also account for the item in our first index.
            {
                if(perkHashes[i] != null)
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
        /// Encapsulated workflow that takes an item name and generates a list of the perks available for that item per slot. 
        /// Need to add consideration for static rolled items (i.e blues, exotics, y1, etc.)
        /// Also need to add good error handling for when a non-gun item is used.
        /// </summary>
        /// <param name="itemName">Name of the Item you're trying to look up</param>
        /// <returns>Array of List objects. [0] will always be the original item. Every index i past 0 will be the list of available perks in the item's ith column.</returns>
        public List<ItemData> GenerateItemList(string itemName, int Year = 0)
        {
            try
            {
                List<ItemData> items = PullItemByName(itemName, Year);
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
        /// Prior validation and selection of y1 or y2 usage should guarantee that whatever this function parses is intended
        /// It checks if the gun is legendary and has random rolls, and if so return the columns. If not return the y1 curated roll. 
        /// Y1 static roll workflow conveniently matches the non-random roll exotic workflow :)
        /// Note that y2 curated rolls not added just yet.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public List<ItemData>[] GeneratePerkColumns(ItemData item)
        {

            List<long?> hashes;
            List<ItemData>[] curatedRoll; 
            if ((item.Inventory.TierTypeName == "Legendary" && item.DisplaySource.ToLower().Contains("random perks")) || RandomExotics.ContainsKey(item.DisplayProperties.Name.ToLower()))
            {
                bungie.DebugLog($"Y2 Workflow Initiliazed For Item: {item.DisplayProperties.Name}", bungie.LogFile);
                hashes = PullRandomizedPerkHash(item);
                //curatedRoll = PullCuratedRoll(item);
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
            //hashes.RemoveAll(h => (h == 3 || h == 5 || h == 7 || h == 9));
            List<List<long>> perkHashes = new List<List<long>>();
            List<ItemData>[] perkList = new List<ItemData>[6]; //index 0 = item itself, 1-5 = perk columns [frame] [col1-5]

            foreach (long? hash in hashes)
            {
                   perkHashes.Add(PullPerksInSet(ParsePlug(PullPlugFromHashes(hash)))); //index 0 = column 1, index 3 = column 4
            }
            //First entry will always be the Base item. Rest will be perks split by column.
            perkList[0] = new List<ItemData>();
            perkList[0].Add(item);
            for (int i = 0; i < perkHashes.Count; i++)
            {
                perkList[i + 1] = new List<ItemData>();
                bungie.DebugLog("------------------", bungie.LogFile);
                foreach (long hash in perkHashes[i])
                {
                    perkList[i + 1].Add(ParseItem(PullItemFromHash(hash)));
                }
            }

            for (int i = 1; i < perkHashes.Count+1; i++) //Use perkHashes as a reference to how many columns we have. i.e if 3 columns. +1 so we can also account for the item in our first index.
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
    }
}
