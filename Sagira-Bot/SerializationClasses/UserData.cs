using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using J = Newtonsoft.Json.JsonPropertyAttribute;
using R = Newtonsoft.Json.Required;
using N = Newtonsoft.Json.NullValueHandling;

namespace Sagira_Bot
{
    public partial class UserData
    {
        [J("Response")] public Response Response { get; set; }
        [J("ErrorCode")] public long ErrorCode { get; set; }
        [J("ThrottleSeconds")] public long ThrottleSeconds { get; set; }
        [J("ErrorStatus")] public string ErrorStatus { get; set; }
        [J("Message")] public string Message { get; set; }
        [J("MessageData")] public MessageData MessageData { get; set; }
    }

    public partial class MessageData
    {
    }

    public partial class Response
    {
        [J("membershipId")] public string MembershipId { get; set; }
        [J("uniqueName")] public string UniqueName { get; set; }
        [J("displayName")] public string DisplayName { get; set; }
        [J("profilePicture")] public long ProfilePicture { get; set; }
        [J("profileTheme")] public long ProfileTheme { get; set; }
        [J("userTitle")] public long UserTitle { get; set; }
        [J("successMessageFlags")] public string SuccessMessageFlags { get; set; }
        [J("isDeleted")] public bool IsDeleted { get; set; }
        [J("about")] public string About { get; set; }
        [J("firstAccess")] public DateTimeOffset FirstAccess { get; set; }
        [J("lastUpdate")] public DateTimeOffset LastUpdate { get; set; }
        [J("psnDisplayName")] public string PsnDisplayName { get; set; }
        [J("showActivity")] public bool ShowActivity { get; set; }
        [J("locale")] public string Locale { get; set; }
        [J("localeInheritDefault")] public bool LocaleInheritDefault { get; set; }
        [J("showGroupMessaging")] public bool ShowGroupMessaging { get; set; }
        [J("profilePicturePath")] public string ProfilePicturePath { get; set; }
        [J("profileThemeName")] public string ProfileThemeName { get; set; }
        [J("userTitleDisplay")] public string UserTitleDisplay { get; set; }
        [J("statusText")] public string StatusText { get; set; }
        [J("statusDate")] public DateTimeOffset StatusDate { get; set; }
        [J("blizzardDisplayName")] public string BlizzardDisplayName { get; set; }
        [J("steamDisplayName")] public string SteamDisplayName { get; set; }
        [J("stadiaDisplayName")] public string StadiaDisplayName { get; set; }
    }
}
