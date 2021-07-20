using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sagira
{
    public class SagiraConfiguration
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        internal const string DefaultFileName = @"sagiraConfig.json";

        internal string _bungieApiKey;
        internal char _defaultBotCommandPrefix;
        internal string _discordBotToken;
        internal ulong _debugServerID = 0;

        public bool success = false;
        public string BungieApiKey { get => default; set => _bungieApiKey = value; }
        public char DefaultBotCommandPrefix { get => default; set => _defaultBotCommandPrefix = value; }
        public string DiscordBotToken { get => default; set => _discordBotToken = value; }
        public ulong DebugServerID { get => default; set => _debugServerID = value; }

        public async Task Load(string fileName = DefaultFileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName + " does not exist.", fileName);
            }

            var config = JsonSerializer.Deserialize<SagiraConfiguration>(await File.ReadAllTextAsync(fileName));

            if (config is null)
            {
                throw new NullReferenceException(nameof(config));
            }

            BungieApiKey = config._bungieApiKey;
            DiscordBotToken = config._discordBotToken;
            DefaultBotCommandPrefix = config._defaultBotCommandPrefix;
            DebugServerID = config._debugServerID;
            success = true;
        }

        public static async Task<SagiraConfiguration> LoadConfig(string fileName = DefaultFileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName + " does not exist.", fileName);
            }

            var config = JsonSerializer.Deserialize<SagiraConfiguration>(await File.ReadAllTextAsync(fileName));

            return config;
        }

        public Task Save(string fileName = DefaultFileName)
        {
            var configContent = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            return File.WriteAllTextAsync(fileName, configContent);
        }

        public static Task SaveConfig(SagiraConfiguration config, string fileName = DefaultFileName)
        {
            var configContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            return File.WriteAllTextAsync(fileName, configContent);
        }
    }
}