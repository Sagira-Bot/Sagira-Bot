using System.Collections.Generic;

namespace Sagira.Services
{
    public class Constants
    {
		public readonly Dictionary<string, string> ColorDict = new Dictionary<string, string>
		{
			{ "Arc", "#7AECF3" },
			{ "Solar", "#F36F21" },
			{ "Void", "#B283CC" },
			{ "Kinetic", "#FFFFFF" },
			{ "Stasis", "#4D88FF" }
		};

		public readonly string[] NumberUnicodes = new string[] { "0️⃣", "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣" };
		public readonly string[] NumberEmoji = new string[] { ":zero:", ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:" };
		public readonly string BlankChar = "\u200b";
	}
}
