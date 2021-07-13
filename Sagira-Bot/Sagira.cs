using System;
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

        public string PullItemByName (string itemName)
        {
            return bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE CHARINDEX('\"name\":\"{itemName}\"', json) > 0 AND CHARINDEX('\"collectibleHash\"', json) > 0");
        }
        public string PullPlugFromHashes(long? hash)
        {
            return bungie.QueryDB($"SELECT json FROM '{perkSetTable}' WHERE id={generateIDfromHash((long)hash)}");
            
        }
        public string PullItemFromHash(long? hash)
        {
            return bungie.QueryDB($"SELECT json FROM '{itemTable}' WHERE id={generateIDfromHash((long)hash)}");
        }
        public ItemData ParseItem(string itemJson)
        {
            return JsonConvert.DeserializeObject<ItemData>(itemJson);
        }
        public PlugSetData ParsePlug(string perkJson)
        {
            return JsonConvert.DeserializeObject<PlugSetData>(perkJson);
        }

        public List<long?> PullPerkHash(ItemData item)
        {
            List<long?> hashes = new List<long?>();
            foreach(SocketEntry sock in item.Sockets.SocketEntries)
            {
                if(sock.PlugSources == 2)
                    hashes.Add(sock.RandomizedPlugSetHash);
            }
            return hashes;
        }
        public int generateIDfromHash(long hash)
        {
            return unchecked((int)hash);
        }
        public List<long> PullPerksInSet(PlugSetData plug)
        {
            List<long> perkHashes = new List<long>();
            foreach(PerkReusablePlugItem perk in plug.ReusablePlugItems)
            {
                if (perk.CurrentlyCanRoll)
                    perkHashes.Add(perk.PlugItemHash);
            }
            return perkHashes;
        }

    }
}
