using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
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
    /// </summary>
    class Sagira
    {
        readonly BungieDriver bungie; //Singleton, we just need one BungieDriver ever.
        const string itemTable = "DestinyInventoryItemDefinition"; //Main db we'll be using to pull an item's manifest entry. Results from here are all JSON.
        const string perkSetTable = "DestinyPlugSetDefinition"; //Main db we'll use to translate every item's perk plug set hash "randomizedPlugSetHash" (combo of perks a gun can roll in a column) into an array of perks

        public Sagira()
        {
            bungie = new BungieDriver(); //init
        }
        /// <summary>
        /// Search by substring for an item's name. Item name has to be exact in this way -- Will add a general search feature later for substrings of item names to work.
        /// The if condition into the for loop does one thing. It doubles up on any instance of an apostraphe in the item's name, so that the DB query doesn't escape at the item's initial apostraphe. 
        /// Of course basically every item is limited to a single apostraphe, so it's a bit superfluous to iterate over the entire string to double every instance of an apostraphe, but in the off chance an item with multiple ever gets added, this'll cover it. 
        /// </summary>
        /// <param name="itemName">exact item name</param>
        /// <returns></returns>
        public string PullItemByName(string itemName)
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
            return bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE CHARINDEX('\"name\":\"{itemName}\"', json) > 0 AND CHARINDEX('\"collectibleHash\"', json) > 0");
        }

        /// <summary>
        /// Search for a plug set in the plug set definition table after converting the plug set's hash to its id
        /// </summary>
        /// <param name="hash">Plug set's hash to convert into its id for selections</param>
        /// <returns></returns>
        public string PullPlugFromHashes(long? hash)
        {
            return bungie.QueryDB($"SELECT json FROM '{perkSetTable}' WHERE id={generateIDfromHash((long)hash)}");

        }

        /// <summary>
        /// Pull from the item db using an item's hash. Hash converted to id.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public string PullItemFromHash(long? hash)
        {
            return bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE id={generateIDfromHash((long)hash)}");
        }

        /// <summary>
        /// Deserialize an item into an ItemData object. This itemData object should contain all relevant information, reference the ItemData class in /SerializationClasses/ItemData.cs
        /// </summary>
        /// <param name="itemJson">json which should come from a db column</param>
        /// <returns></returns>
        public ItemData ParseItem(string itemJson)
        {
            return JsonConvert.DeserializeObject<ItemData>(itemJson);
        }

        /// <summary>
        /// Deserialize a Plugset into PlugsetData object which should have all perks listed inside of it. Reference the PlugSetData class in /SerializationClasses/PlugSetData.cs
        /// </summary>
        /// <param name="perkJson"></param>
        /// <returns></returns>
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
        /// <returns></returns>
        public List<long?> PullPerkHash(ItemData item)
        {
            List<long?> hashes = new List<long?>();
            foreach (SocketEntry sock in item.Sockets.SocketEntries)
            {
                if (sock.PlugSources == 2)
                    hashes.Add(sock.RandomizedPlugSetHash);
            }
            return hashes;
        }

        /// <summary>
        /// An item's db id is the item's hash cast to a signed int. 
        /// </summary>
        /// <param name="hash">Item's hash</param>
        /// <returns></returns>
        public int generateIDfromHash(long hash)
        {
            return unchecked((int)hash);
        }

        /// <summary>
        /// Takes PlugSetData and pulls a list of its available perks. Only returns currently available perks for now. 
        /// Returns a list of longs since these aren't nullable. A plug set must have perks in a randomizedPlugSet.
        /// </summary>
        /// <param name="plug">PlugSet from an item's item data</param>
        /// <returns></returns>
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
