using System;
using System.Collections.Generic;
using System.Text;

namespace Sagira_Bot
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using J = Newtonsoft.Json.JsonPropertyAttribute;
    using R = Newtonsoft.Json.Required;
    using N = Newtonsoft.Json.NullValueHandling;

    public partial class Manifest
    {
        [J("Response")] public Response Response { get; set; }
    }
    public partial class Response
    {
        [J("mobileWorldContentPaths")] public WorldContentPaths MobileWorldContentPaths { get; set; }
    }
    public partial class WorldContentPaths
    {
        [J("en")] public string En { get; set; }
        [J("fr")] public string Fr { get; set; }
        [J("es")] public string Es { get; set; }
        [J("es-mx")] public string EsMx { get; set; }
        [J("de")] public string De { get; set; }
        [J("it")] public string It { get; set; }
        [J("ja")] public string Ja { get; set; }
        [J("pt-br")] public string PtBr { get; set; }
        [J("ru")] public string Ru { get; set; }
        [J("pl")] public string Pl { get; set; }
        [J("ko")] public string Ko { get; set; }
        [J("zh-cht")] public string ZhCht { get; set; }
        [J("zh-chs")] public string ZhChs { get; set; }
    }

}
