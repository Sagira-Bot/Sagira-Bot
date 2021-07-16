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
            List<ItemData> ItemList = sagira.GenerateItemList("Midnight");
            List<ItemData>[] PerkColumns = sagira.GeneratePerkColumns(ItemList[0]);

        }

    }
}
