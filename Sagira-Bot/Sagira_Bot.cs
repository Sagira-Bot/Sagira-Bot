using System;
using System.Collections.Generic;

namespace Sagira_Bot
{
    class Sagira_Bot
    {
        static void Main(string[] args)
        {
            Sagira sagira = new Sagira();
            string item = sagira.PullItemByName("Night Watch");
            ItemData itemData = sagira.ParseItem(item);
            List<long?> hashes = sagira.PullPerkHash(itemData);
            List<List<long>> perkHashes = new List<List<long>>();
            List<ItemData>[] perkList = new List<ItemData>[4];
            foreach(long? hash in hashes)
            {
                perkHashes.Add(sagira.PullPerksInSet(sagira.ParsePlug(sagira.PullPlugFromHashes(hash))));
            }
            for(int i=0;i<4;i++)
            {
                perkList[i] = new List<ItemData>();
                Console.WriteLine("------------------");
                foreach (long hash in perkHashes[i])
                {
                    perkList[i].Add(sagira.ParseItem(sagira.PullItemFromHash(hash)));
                }
            }

            for(int i=0;i<4;i++)
            {
                Console.Write($"COLUMN {i+1}: ");
                foreach(ItemData perk in perkList[i])
                {
                    Console.Write($"{perk.DisplayProperties.Name}, ");
                }
                Console.WriteLine("");
            }
            
        }
    }
}
