using BOG.SwissArmyKnife;
using BOG.SwissArmyKnife.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace BOG.UpdateDDNS
{
    class Program
    {
        static List<DynamicDnsService> configuration = new List<DynamicDnsService>();
        static Dictionary<string, string> argAlias = new Dictionary<string, string>()
        {
            { "--path", "PATH" },
            { "-p", "PATH" },
            { "--service", "SERVICE" },
            { "-s", "SERVICE" },
        };
        static Dictionary<string, string> argValues = new Dictionary<string, string>()
        {
            { "PATH", "$HOME" },
            { "SERVICE", "" }
        };

        static string ReadArguments(string[] args)
        {
            var key = string.Empty;
            if (args.Length > 0)
            {
                for (var index = 0; index < args.Length; index++)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        if (!argAlias.ContainsKey(args[index]))
                        {
                            return $"Key {args[index]} is unknown.";
                        }
                        key = argAlias[args[index]];
                        continue;
                    }
                    argValues[key] = args[index];
                    key = string.Empty;
                }
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return $"Key {args[args.Length - 1]} specified with no value.";
                }
            }
            return string.Empty;
        }

        static void Help()
        {
            Console.WriteLine();
            Console.WriteLine("BOG.UpdateDDNS [--path {path}] --service {serviceName}");
            Console.WriteLine("  --path: (optional) overrides the default path for config and log files.");
            Console.WriteLine("  --service: name of DDNS service name to update.");
        }

        static void Main(string[] args)
        {
            var updateAction = new UpdateAction();
            string configFile = null;
            string logFile = null;

            try
            {
                var a = new AssemblyVersion();
                Console.WriteLine("==================");
                Console.WriteLine($"{Path.GetFileName(a.Filename)}, v{a.Version}");
                Console.WriteLine("==================");
                Console.WriteLine();
                Console.WriteLine("Read command line arguments...");
                var error = ReadArguments(args);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine($"One or more invalid command-line arguments: {error}");
                    updateAction.Result = UpdateAction.State.InvalidParameters;
                    updateAction.Notes = error;
                    Help();
                }
                configFile = BuildConfigFilePath() + Path.GetFileNameWithoutExtension(a.Filename) + ".json";
                logFile = BuildConfigFilePath() + Path.GetFileNameWithoutExtension(a.Filename) + ".log";

                if (updateAction.Result == UpdateAction.State.Unknown)
                {
                    if (!File.Exists(configFile))
                    {
                        if (string.IsNullOrWhiteSpace(argValues["PATH"]))
                        {
                            Console.WriteLine($"Creating sample config file: {configFile}");
                            configuration.Add(new DynamicDnsService
                            {
                                Name = "DuckDns",
                                Domain = "myhost.duckdns.org",
                                Url = "https://www.duckdns.org/update?domains=myhost,myotherhost&token=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&ip={IP}"
                            });
                            configuration.Add(new DynamicDnsService
                            {
                                Name = "GoogleDomains",
                                Domain = "myhost.mydomain.org",
                                Url = "https://user:password@domains.google.com/nic/update?hostname=myhost.mydomain.org&myip={IP}"
                            });
                            configuration.Add(new DynamicDnsService
                            {
                                Name = "DynDns",
                                Domain = "myhost.dyndns.org",
                                Url = "https://user:updater-client-key@members.dyndns.org/v3/update?hostname=myhost&myip={IP}"
                            });
                            SaveConfiguration(configFile);
                            updateAction.Result = UpdateAction.State.Minor;
                        }
                        else
                        {
                            throw new FileNotFoundException($"Missing configuration file: {configFile}");
                        }
                    }

                    if (updateAction.Result == UpdateAction.State.Unknown)
                    {
                        var configName = argValues["SERVICE"];
                        if (string.IsNullOrWhiteSpace(configName)) throw new ArgumentException("Missing service argument.");
                        configName = configName.Replace("\r", "").Replace("\n", "");   // linux/win precaution

                        Console.WriteLine($"Loading config file: {configFile}");
                        LoadConfiguration(configFile);

                        Console.WriteLine($"Extracting config for service: {configName}");
                        var thisEndpoint = configuration
                            .Where(o => string.Compare(o.Name, configName, false) == 0)
                            .FirstOrDefault();

                        if (thisEndpoint == null)
                        {
                            throw new Exception($"No service found in config file for {args[0]}");
                        }
                        updateAction.Name = thisEndpoint.Name;

                        Console.WriteLine($"Querying current WAN IP address via: https://domains.google.com/checkip");

                        var client = new WebClient();
                        client.Headers.Add("Accept", "text/plain");

                        var currentWanIp = client.DownloadString("https://domains.google.com/checkip");
                        updateAction.WanIpAddress = currentWanIp;

                        thisEndpoint.LastCheck = DateTime.Now;
                        SaveConfiguration(configFile);

                        if (string.Compare(currentWanIp, thisEndpoint.CurrentWanIP, false) == 0)
                        {
                            Console.WriteLine("IP address unchanged.");
                            updateAction.Result = UpdateAction.State.NoChange;
                        }
                        else
                        {
                            updateAction.Result = UpdateAction.State.Updated;
                            Console.WriteLine($"IP address change detected from {thisEndpoint.PreviousWanIP} to {currentWanIp}");
                            Console.WriteLine($"Sending change request to DDNS service: {args[0]}");

                            var url = thisEndpoint.Url.Replace("{IP}", currentWanIp);
                            var uri = new Uri(url);

                            client = new WebClient();
                            client.Headers.Add("Accept", "*/*");
                            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
                            {
                                byte[] toEncodeAsBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(uri.UserInfo);
                                var auth = System.Convert.ToBase64String(toEncodeAsBytes);
                                client.Headers.Add("Authorization", $"Basic {auth}");
                            }
                            var response = client.DownloadString(url);
                            Console.WriteLine($"Server Response: {response}");
                            updateAction.Notes = response;
                            if (response.ToUpper().Contains("OK") || response.ToUpper().Contains("NOCHG"))
                            {
                                Console.WriteLine("Valid response received");
                                thisEndpoint.PreviousWanIP = thisEndpoint.CurrentWanIP;
                                thisEndpoint.PreviousWanIPdetected = thisEndpoint.CurrentWanIPdetected;
                                thisEndpoint.CurrentWanIP = currentWanIp;
                                thisEndpoint.CurrentWanIPdetected = DateTime.Now;
                            }
                        }
                        Console.WriteLine("Save configuration");
                        SaveConfiguration(configFile);
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                updateAction.Result = UpdateAction.State.Error;
                updateAction.Notes = err.Message;
            }
            switch (updateAction.Result)
            {
                case UpdateAction.State.Error:
                case UpdateAction.State.Unknown:
                    updateAction.ExitCode = 2;
                    break;
                case UpdateAction.State.InvalidParameters:
                    updateAction.ExitCode = 1;
                    break;
                default:
                    updateAction.ExitCode = 0;
                    break;
            }
            LogUpdateAction(logFile, updateAction);
            Console.WriteLine();
#if DEBUG
			Console.WriteLine($"Exit code: {updateAction.ExitCode} ... press ENTER");
			Console.ReadLine();
#endif
			System.Environment.Exit(updateAction.ExitCode);
        }

        static string BuildConfigFilePath()
        {
            var localFolder = argValues["PATH"];
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                localFolder = localFolder.Replace(@"/", @"\").ResolvePathPlaceholders();
                localFolder = localFolder.Replace("$HOME", Environment.GetEnvironmentVariable("USERPROFILE"));
                while (localFolder[localFolder.Length - 1] == '\\') localFolder = localFolder.Substring(0, localFolder.Length - 1);
                localFolder += "\\";
            }
            else
            {
                localFolder = localFolder.Replace("$HOME", Environment.GetEnvironmentVariable("HOME")).ResolvePathPlaceholders();
                while (localFolder[localFolder.Length - 1] == '/') localFolder = localFolder.Substring(0, localFolder.Length - 1);
                localFolder = localFolder + "/";
            }
            return localFolder;
        }

        static void SaveConfiguration(string filename)
        {
            using (var sw = new StreamWriter(filename))
            {
                sw.Write(BOG.SwissArmyKnife.Serializer<List<DynamicDnsService>>.ToJson(configuration));
            }
        }

        static void LoadConfiguration(string filename)
        {
            using (var sr = new StreamReader(filename))
            {
                configuration = BOG.SwissArmyKnife.Serializer<List<DynamicDnsService>>.FromJson(sr.ReadToEnd());
            }
        }

        static void LogUpdateAction(string filename, UpdateAction action)
        {
            var writeHeader = !File.Exists(filename);
            using (var sw = new StreamWriter(filename, true))
            {
                if (writeHeader)
                {
                    sw.WriteLine(string.Format(
                            "{0,-30} {1,1} {2,-20} {3,-20} {4,-39} {5}",
                            "OccurredOn",
                            "X",
                            "Result",
                            "Name",
                            "WanIpAddress",
                            "Notes")
                        );
                }
                sw.WriteLine(string.Format(
                        "{0:s} {0,-10:dddd} {1,1} {2,-20} {3,-20} {4,-39} {5}",
                        action.Occurred,
                        action.ExitCode,
                        action.Result,
                        action.Name,
                        action.WanIpAddress,
                        action.Notes)
                    );
            }
        }
    }

    public class DynamicDnsService
    {
        [JsonProperty]
        public string Name { get; set; } = string.Empty;

        [JsonProperty]
        public string Url { get; set; } = string.Empty;

        [JsonProperty]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty]
        public string PreviousWanIP { get; set; } = string.Empty;

        [JsonProperty]
        public DateTime PreviousWanIPdetected { get; set; } = DateTime.MinValue;

        [JsonProperty]
        public string CurrentWanIP { get; set; } = string.Empty;

        [JsonProperty]
        public DateTime CurrentWanIPdetected { get; set; } = DateTime.MinValue;

        [JsonProperty]
        public DateTime LastCheck { get; set; } = DateTime.MinValue;
    }

    [JsonObject]
    public class UpdateAction
    {
        public enum State : int { Unknown = 0, InvalidParameters = 1, Error = 2, NoChange = 3, Updated = 4, Minor = 5 }

        [JsonProperty]
        public DateTime Occurred { get; set; } = DateTime.Now;

        [JsonProperty]
        public string Name { get; set; } = "*none*";

        [JsonProperty]
        public State Result { get; set; } = State.Unknown;

        [JsonProperty]
        public int ExitCode { get; set; } = 99;

        [JsonProperty]
        public string WanIpAddress { get; set; }

        [JsonProperty]
        public string Notes { get; set; }
    }
}
