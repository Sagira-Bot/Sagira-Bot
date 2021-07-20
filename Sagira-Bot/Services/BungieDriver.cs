using System.Threading.Tasks;
using BungieSharper.Client;
using BungieSharper.Entities.Destiny.Config;

namespace Sagira.Services
{

    public class BungieDriver
    {
        public readonly BungieApiClient bungieClient;
        public DestinyManifest Mani;

        public BungieDriver(string ApiKey)
        {   
            var config = new BungieClientConfig();
            config.ApiKey = ApiKey;
            bungieClient = new BungieApiClient(config);
        }

        public async Task PullManifest()
        {
            Mani = await bungieClient.Api.Destiny2_GetDestinyManifest();
        }
        public async Task<string> GetTable(string Table, string lang = "en")
        {
            var tableUrl = Mani.JsonWorldComponentContentPaths[lang][Table];
            return await bungieClient.DownloadString(tableUrl);
        }
    }
}
