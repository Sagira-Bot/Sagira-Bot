using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
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
    /// <summary>
    /// Driver Object to handle all the set up needed to get to the point of our desired workflow of:
    /// Query item name -> Parse item's JSON -> Pull perk set hash from item's Json -> Spit out what that perk set is per column.
    /// This code basically initiliazes a Rest Client for any api requests necessary towards Bungie's api, and also a SQLite db instance to work with Bungie's manifest db.
    /// </summary>
    public class BungieDriver
    {
        static RestClient Bungie;  //Singleton, we only want a single Rest Client at any given time to make requests through. Note that all requests need the header X-Api-Key : ApiKey
        readonly SQLiteConnection DB; //Singleton, we only want a single connection up at any given time.
        const string BaseURL = "https://www.bungie.net"; //the base url to pull Bungie's sqlite manifest file excludes /Platform
        const string BaseURL2 = "https://www.bungie.net/Platform"; //Most api calls hit /Platform, so this is a good candidate for baseURL if their web api is necessary down the road.
        const string DbDir = "./ManifestDB/"; //Doesn't matter - change if you care
        readonly string ApiKey; //Api Key to be loaded from .env file. Format of file is KEY=VALUE so APIKEY=YOURKEYHERE
        readonly IDictionary<string, string> envs; //Envs returns to an IDictionary. 
        public readonly string LogFile = ""; //Combination of 2 env variables. Combines LOG=LOGFILENAME and LOGDIR=LOGFOLDERNAME. Same format as above in .env file, just pass in the name of the log, in my case it's "DEBUG.log" and "Logs"
        /// <summary>
        /// Generic Constructor. Loads all envs from .env file (hidden from git, but put it in the same directory as your binaries. KEY=VALUE format, KEYs are all the mentioned associative indexes below i.e "APIKEY" for your Api Key)
        /// Empties logs/Creates necessary directories. Pulls the URL of the english db from Bungie's manifest page, then downloads the manifest db and initializes it for usage by Sagira object. 
        /// </summary>
        public BungieDriver()
        {
            DotEnv.Load(); //Load .env file
            envs = DotEnv.Read(); //Populate IDict
            ApiKey = envs["APIKEY"]; //Set variables based on envs
            LogFile = envs["LOGDIR"] + "\\" + envs["LOG"];
            if (!Directory.Exists(envs["LOGDIR"]))
                Directory.CreateDirectory(envs["LOGDIR"]);
            EmptyFile(LogFile);

            Bungie = new RestClient(BaseURL); //Initialize RestClient with BaseUrl that doesn't use platform for now. Any future uses of RestClient should probably reinitialize with BaseURL2

            string enURL = JsonConvert.DeserializeObject<Manifest>(GenericGetRequest("Platform/Destiny2/Manifest")).Response.MobileWorldContentPaths.En; //Pull specifically the english-based URL for the Manifest file.
            string DbFileName = enURL.Split("/")[enURL.Split("/").Length - 1]; //Pull the file name from path.
            DebugLog("SQLITE MANIFEST URL: " + enURL +" ||DB FILE NAME: " + DbFileName , LogFile);
            PullManifest(enURL, "manifest.zip"); //Download the file to manifest.zip
            ExtractManifest("manifest.zip", DbDir, DbFileName); //Unzip the file to our assigned location
            DebugLog($"Attempting to initialize DB from:{DbDir}{DbFileName}", LogFile);
            try
            {
                DB = new SQLiteConnection("Data Source=" + DbDir + DbFileName); //Initialize our DB connection
                DB.Open();
            }
            catch (Exception e)
            {
                DebugLog($"Error initialized Manifest DB: {e}", LogFile);
                DebugLog("Aborting", LogFile);
                Environment.Exit(0); //No db no service
            }
        }

        /// <summary>
        /// Generic Query method to query our DB. Currently only returns single result -- only use this if your query is guaranteed to return a single result. 
        /// If not, it returns the first result.
        /// </summary>
        /// <param name="Query">SQL Query string</param>
        /// <returns>result of the query</returns>
        public List<string> QueryDB(string Query, bool Debug = false)
        {
            //DB.Open();
            List<string> Results = new List<string>();
            try
            {
                DebugLog($"Attempting to query db with: {Query}", LogFile);
                //DB.Open();
                var command = DB.CreateCommand();
                command.CommandText = Query; //Assign our DB Command's text to our Query so when we exeucte our command we execute our desired query
                using (var reader = command.ExecuteReader()) //Execute command via SQLite's DB Reader. 
                {
                    while (reader.Read()) //While still getting results, progress until results are exhausted.
                    {
                       Results.Add(reader.GetString(0));
                       if(Debug)
                        DebugLog(reader.GetString(0), LogFile);

                    }
                }
            }
            catch (Exception e)
            {
                Results = new List<string>();
                Results.Add($"Failed to execute query: {Query}\nDue to error: {e}"); //Log any errors with query
            }
            //DebugLog(result, LogFile);
            //DB.Close();
            return Results;
        }

        /// <summary>
        /// Pull Manifest from BaseUrl+URL (which in our case is the en url). Results in a .zip file being downloaded.
        /// </summary>
        /// <param name="URL">URL of the manifest's db file</param>
        /// <param name="fileName">resulting file name to be written to</param>
        private void PullManifest(string URL, string fileName)
        {
            if (File.Exists(fileName))
                File.Delete(fileName); //Delete existing manifest.zip
            if (DownloadFile(URL, fileName))
            {
                DebugLog("Finished Downloading Manifest", LogFile);
            }
            else
            {
                DebugLog("Error Downloading Manifest", LogFile);
                DebugLog("Aborting", LogFile);
                Environment.Exit(0); //No Db file no service
            }
        }
        /// <summary>
        /// Extract sqlite file (or .content file) from manifest.zip
        /// </summary>
        /// <param name="fileName">name of .zip file to extract manifest from</param>
        /// <param name="dir">location to extract to</param>
        /// <param name="DbFileName">Expected Extracted File Name</param>
        /// 
        private void ExtractManifest(string fileName, string dir, string DbFileName)
        {
            ExtractFile(fileName, dir);
            DebugLog($"Validating DB Extracted to:{dir}{DbFileName}", LogFile); //Validate that we actually unzipped the file.
            if (!(new FileInfo(dir + DbFileName).Length == 0))
            {
                DebugLog("Finished Unzipping Manifest", LogFile);
            }
            else
            {
                DebugLog("Error Unzipping Manifest", LogFile);
                DebugLog("Aborting", LogFile);
                Environment.Exit(0); //No db no service
            }
        }
        /// <summary>
        /// Download File via GET request to BaseURL + URL
        /// </summary>
        /// <param name="URL">URL from our Base URL to download file from</param>
        /// <param name="fileName">Name of the file we'll be generating</param>
        /// <returns>True if download file completed, false if not</returns>
        private bool DownloadFile(string URL, string fileName)
        {
            DebugLog("Starting "+fileName+" Download", LogFile);
            var writer = File.OpenWrite(fileName); //We need to write our GET result to a writestream. 
            var request = new RestRequest(URL);
            //How to handle downloading stream content instead of json content
            request.ResponseWriter = responseStream =>
            {
                using (responseStream)
                {
                    responseStream.CopyTo(writer);
                }
            };
            var response = Bungie.DownloadData(request); //Execute Download Request
            writer.Close();
            if(new FileInfo(fileName).Length == 0) //Validate if we actually downloaded the file
                return false;
            return true;
        }
        /// <summary>
        /// Generic unzip method
        /// </summary>
        /// <param name="fileName">File to extract</param>
        /// <param name="dir">Directory to extract to</param>
        private void ExtractFile(string fileName, string dir)
        {
            DebugLog("Starting to Extract: " + fileName, LogFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir); //Ensure directory exists before we try
            ZipFile.ExtractToDirectory(fileName, dir, true); //Validate later.
        } 
        /// <summary>
        /// Basic API Request Method
        /// </summary>
        /// <param name="URL">URL to send a GET request to</param>
        /// <returns></returns>
        private string GenericGetRequest(string URL)
        {
            var Req = new RestRequest(URL, Method.GET);
            Req.AddHeader("Content-Type", "application/json");
            Req.AddHeader("X-API-Key", ApiKey);
            return Bungie.Execute(Req).Content;//.Content;
        }
        /// <summary>
        /// Debugging method. Writes debug messages to a log file and console.
        /// </summary>
        /// <param name="data">Debug info to write</param>
        /// <param name="LogName">Log file to write to</param>
        public void DebugLog(string data, string LogName)
        {
            Console.WriteLine(data);
            try
            {
                using StreamWriter w = File.AppendText(LogName);
                w.WriteLine($"{DateTime.Now.ToLongTimeString()}:{data}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error writing to debug log: {e}");
            }

        }
        /// <summary>
        /// Empties target file. Ensure we never write to a file that already has contents. Not really that useful.
        /// </summary>
        /// <param name="FileName">File to empty</param>
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

        /// <summary>
        /// Originally used this to inject REGEXP as a command into our SQLite instance.
        /// Dumped it
        /// </summary>
        [SQLiteFunction(Name = "REGEXP", Arguments = 2, FuncType = FunctionType.Scalar)]
        public class RegExSQLiteFunction : SQLiteFunction
        {
            public override object Invoke(object[] args)
            {
                return System.Text.RegularExpressions.Regex.IsMatch(Convert.ToString(args[1]), Convert.ToString(args[0]));
            }
        }

        public void BindLiteFunction(SQLiteConnection connection, SQLiteFunction function)
        {
            var attributes = function.GetType().GetCustomAttributes(typeof(SQLiteFunctionAttribute), true).Cast<SQLiteFunctionAttribute>().ToArray();
            if (attributes.Length == 0)
            {
                throw new InvalidOperationException("SQLiteFunction doesn't have SQLiteFunctionAttribute");
            }
            connection.BindFunction(attributes[0], function);
        }
    }
}
