using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using J = Newtonsoft.Json.JsonPropertyAttribute;
using R = Newtonsoft.Json.Required;
using N = Newtonsoft.Json.NullValueHandling;
namespace Sagira_Bot
{
    public partial class PlugSetData
    {
        [J("displayProperties")] public PerkDisplayProperties DisplayProperties { get; set; }
        [J("reusablePlugItems")] public PerkReusablePlugItem[] ReusablePlugItems { get; set; }
        [J("isFakePlugSet")] public bool IsFakePlugSet { get; set; }
        [J("hash")] public long Hash { get; set; }
        [J("index")] public long Index { get; set; }
        [J("redacted")] public bool Redacted { get; set; }
        [J("blacklisted")] public bool Blacklisted { get; set; }
    }

    public partial class PerkDisplayProperties
    {
        [J("description")] public string Description { get; set; }
        [J("name")] public string Name { get; set; }
        [J("hasIcon")] public bool HasIcon { get; set; }
    }

    public partial class PerkReusablePlugItem
    {
        [J("weight")] public long Weight { get; set; }
        [J("alternateWeight")] public long AlternateWeight { get; set; }
        [J("currentlyCanRoll")] public bool CurrentlyCanRoll { get; set; }
        [J("plugItemHash")] public long PlugItemHash { get; set; }
    }
}
