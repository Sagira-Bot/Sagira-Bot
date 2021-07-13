using System;
using System.Collections.Generic;

namespace Sagira_Bot
{
    class Sagira_Bot
    {
        static void Main(string[] args)
        {
            //SampleBitMapEdit();
            Sagira sagira = new Sagira();
            string item = sagira.PullItemByName("Dead Man's Tale");
            ItemData itemData = sagira.ParseItem(item);
            List<long?> hashes = sagira.PullPerkHash(itemData);
            List<List<long>> perkHashes = new List<List<long>>();
            List<ItemData>[] perkList = new List<ItemData>[4];

            //This workflow only works for LEGENDARY items and Random Rolled Exotics like Hawkmoon and DMT. Static roll items come later (i.e y1 guns, exotics, etc).
            //Obviously this workflow will be migrated to container methods later, this is here solely to test components of the workflow.
            foreach(long? hash in hashes)
            {
                perkHashes.Add(sagira.PullPerksInSet(sagira.ParsePlug(sagira.PullPlugFromHashes(hash))));
            }
            for(int i=0;i< perkHashes.Count ; i++)
            {
                perkList[i] = new List<ItemData>();
                Console.WriteLine("------------------");
                foreach (long hash in perkHashes[i])
                {
                    perkList[i].Add(sagira.ParseItem(sagira.PullItemFromHash(hash)));
                }
            }

            for(int i=0;i<perkHashes.Count;i++)
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
