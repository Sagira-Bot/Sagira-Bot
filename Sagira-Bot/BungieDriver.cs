using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using RestSharp;
using dotenv.net;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

//using RestSharp.Serialization.Json;

namespace Sagira_Bot
{
    public class BungieDriver
    {
        static RestClient Bungie;  //Singleton
        const string BaseURL = "https://www.bungie.net";
        const string BaseURL2 = "https://www.bungie.net/Platform";
        const string DbDir = "./ManifestDB/";
        readonly string ApiKey;
        readonly string LogFile;
        readonly string DbFileName;
        readonly SQLiteConnection DB;
        readonly IDictionary<string, string> envs;
        string TempID = "16626694";


        public BungieDriver()
        {
            DotEnv.Load();
            envs = DotEnv.Read();
            ApiKey = envs["APIKEY"];
            LogFile = envs["LOGDIR"] + "\\" + envs["LOG"];
            if (!Directory.Exists(envs["LOGDIR"]))
                Directory.CreateDirectory(envs["LOGDIR"]);
            EmptyFile(LogFile);

            Bungie = new RestClient(BaseURL);

            string enURL = JsonConvert.DeserializeObject<Manifest>(GenericGetRequest("Platform/Destiny2/Manifest")).Response.MobileWorldContentPaths.En;
            DbFileName = enURL.Split("/")[enURL.Split("/").Length - 1];
            DebugLog("SQLITE MANIFEST URL: " + enURL +" ||DB FILE NAME: " + DbFileName , LogFile);
            PullManifest(enURL, "manifest.zip");
            ExtractManifest("manifest.zip", DbDir);
            DebugLog($"Attempting to initialize DB from:{DbDir}{DbFileName}", LogFile);
            DB = new SQLiteConnection("Data Source=" + DbDir + DbFileName);

            //JUST TESTING FOR NOW
            QueryDB(@"SELECT name FROM sqlite_master WHERE type='table'");
        }

        private void PullManifest(string URL, string fileName)
        {
            if (DownloadFile(URL, fileName))
            {
                DebugLog("Finished Downloading Manifest", LogFile);
            }
            else
            {
                DebugLog("Error Downloading Manifest", LogFile);
                DebugLog("Aborting", LogFile);
                Environment.Exit(0);
            }
        }
        private void ExtractManifest(string fileName, string dir)
        {
            ExtractFile(fileName, DbDir);
            DebugLog($"Validating DB Extracted to:{DbDir}{DbFileName}", LogFile);
            if (!(new FileInfo(DbDir + DbFileName).Length == 0))
            {
                DebugLog("Finished Unzipping Manifest", LogFile);
            }
            else
            {
                DebugLog("Error Unzipping Manifest", LogFile);
                DebugLog("Aborting", LogFile);
                Environment.Exit(0);
            }
        }
        private void QueryDB(string Query)
        {
            try
            {
                DebugLog($"Attempting to query db with: {Query}", LogFile);
                DB.Open();
                var command = DB.CreateCommand();
                command.CommandText = Query;
                //command.Parameters.AddWithValue("$id", id);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DebugLog($"{reader.GetString(0)}", LogFile);
                    }
                }
            }
            catch(Exception e)
            {
                DebugLog($"Failed to execute query: {Query}\nDue to error: {e}", LogFile);
            }
        }

        private bool DownloadFile(string URL, string fileName)
        {
            DebugLog("Starting "+fileName+" Download", LogFile);
            var writer = File.OpenWrite(fileName);
            var request = new RestRequest(URL);
            request.ResponseWriter = responseStream =>
            {
                using (responseStream)
                {
                    responseStream.CopyTo(writer);
                }
            };
            var response = Bungie.DownloadData(request);
            writer.Close();
            if(new FileInfo(fileName).Length == 0)
                return false;
            return true;
        }
        private void ExtractFile(string fileName, string dir)
        {
            DebugLog("Starting to Extract: " + fileName, LogFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            ZipFile.ExtractToDirectory(fileName, dir, true);
        } 
        private string GenericGetRequest(string URL)
        {
            var Req = new RestRequest(URL, Method.GET);
            Req.AddHeader("Content-Type", "application/json");
            Req.AddHeader("X-API-Key", ApiKey);
            return Bungie.Execute(Req).Content;//.Content;
        }

        private void DebugLog(string data, string LogName)
        {
            Console.WriteLine(data);
            using StreamWriter w = File.AppendText(LogName);
            w.WriteLine($"{DateTime.Now.ToLongTimeString()}:{data}");

        }
        private void EmptyFile(string FileName)
        {
            using (FileStream fs = File.Open(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                lock (fs)
                {
                    fs.SetLength(0);
                }
            }
        }
    }
}
