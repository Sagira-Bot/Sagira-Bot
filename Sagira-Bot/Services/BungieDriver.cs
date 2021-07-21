using System.Threading.Tasks;
using BungieSharper.Client;
using BungieSharper.Entities.Destiny.Config;

namespace Sagira.Services
{

    public class BungieDriver
    {
        public readonly BungieApiClient BungieClient;
        public DestinyManifest Manifest;

        public BungieDriver(string ApiKey)
        {   
            var config = new BungieClientConfig();
            config.ApiKey = ApiKey;
            BungieClient = new BungieApiClient(config);
        }

        public async Task PullManifest()
        {
            Manifest = await BungieClient.Api.Destiny2_GetDestinyManifest();
        }
        public async Task<string> GetTable(string Table, string lang = "en")
        {
            var tableUrl = Manifest.JsonWorldComponentContentPaths[lang][Table];
            return await BungieClient.DownloadString(tableUrl);
        }
    }
}
