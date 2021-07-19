using System;
using System.IO;
using System.Threading.Tasks;
using dotenv.net;
using BungieSharper.Client;
using BungieSharper.Entities.Destiny.Config;

//using RestSharp.Serialization.Json;

namespace Sagira_Bot
{

    public class BungieDriver
    {
        public readonly BungieApiClient bungieClient;
        public DestinyManifest Mani;

        public BungieDriver()
        {
            DotEnv.Load(); //Load .env file
            var envs = DotEnv.Read();          
            var config = new BungieClientConfig();
            config.ApiKey = envs["APIKEY"];
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
