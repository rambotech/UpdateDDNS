using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Web;
using System.Linq;
using BOG.SwissArmyKnife;
using BOG.SwissArmyKnife.Extensions;
using Newtonsoft.Json;

namespace BOG.UpdateDDNS
{
    class Program
    {
        static List<EndpointDDNS> configuration = new List<EndpointDDNS>();

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

                var localFolder = "$HOME";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    localFolder = localFolder.Replace(@"/", @"\").ResolvePathPlaceholders();
                    localFolder = localFolder.Replace("$HOME", Environment.GetEnvironmentVariable("USERPROFILE"));
                    while (localFolder[localFolder.Length - 1] == '\\') localFolder = localFolder.Substring(0, localFolder.Length - 1);
                    localFolder += "\\";
                }
                else
                {
                    localFolder = localFolder.Replace("$HOME", Environment.GetEnvironmentVariable("HOME"));
                    while (localFolder[localFolder.Length - 1] == '/') localFolder = localFolder.Substring(0, localFolder.Length - 1);
                    localFolder = localFolder + "/";
                }

                configFile = localFolder + Path.GetFileNameWithoutExtension(a.Filename) + ".json";
                logFile = localFolder + Path.GetFileNameWithoutExtension(a.Filename) + ".log";
                if (!File.Exists(configFile))
                {
                    if (args.Length == 0)
                    {
                        Console.WriteLine($"Creating sample config file: {configFile}");
                        configuration.Add(new EndpointDDNS
                        {
                            Name = "DuckDns",
                            Domain = "myhost.duckdns.org",
                            Url = "https://www.duckdns.org/update?domains=myhost,myotherhost&token=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&ip={IP}"
                        });
                        configuration.Add(new EndpointDDNS
                        {
                            Name = "GoogleDomains",
                            Domain = "myhost.mydomain.org",
                            Url = "https://user:password@domains.google.com/nic/update?hostname=myhost.mydomain.org&myip={IP}"
                        });
                        configuration.Add(new EndpointDDNS
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
                    if (args.Length != 1) throw new ArgumentException("Requires a single argument with the name of the endpoint to use.");

                    Console.WriteLine($"Loading config file: {configFile}");
                    LoadConfiguration(configFile);

                    var configName = args[0].Replace("\r", "").Replace("\n", "");   // linux/win precaution

                    Console.WriteLine($"Extracting config: {configName}");
                    var thisEndpoint = configuration
                        .Where(o => string.Compare(o.Name, args[0], false) == 0)
                        .FirstOrDefault();

                    if (thisEndpoint == null)
                    {
                        throw new Exception($"No endpoint found in config file using this name: {args[0]}");
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
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                updateAction.Result = UpdateAction.State.Error;
                updateAction.Notes = err.Message;
            }
            LogUpdateAction(logFile, updateAction);
            var exitCode = 0;
            switch (updateAction.Result)
            {
                case UpdateAction.State.Error:
                case UpdateAction.State.Unknown:
                    exitCode = 1;
                    break;
                default:
                    exitCode = 0;
                    break;
            }
            Console.WriteLine();
            System.Environment.Exit(exitCode);
        }

        static void SaveConfiguration(string filename)
        {
            using (var sw = new StreamWriter(filename))
            {
                sw.Write(BOG.SwissArmyKnife.Serializer<List<EndpointDDNS>>.ToJson(configuration));
            }
        }

        static void LoadConfiguration(string filename)
        {
            using (var sr = new StreamReader(filename))
            {
                configuration = BOG.SwissArmyKnife.Serializer<List<EndpointDDNS>>.FromJson(sr.ReadToEnd());
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
                            "{0,-20} {1,-10} {2,-10} {3,-20} {4,-39} {5}",
                            "OccurredOn",
                            string.Empty,
                            "Result",
                            "Name",
                            "WanIpAddress",
                            "Notes")
                        );
                }
                sw.WriteLine(string.Format(
                        "{0:s} {0,-10:dddd} {1,-10} {2,-20} {3,-39} {4}",
                        action.Occurred,
                        action.Result,
                        action.Name,
                        action.WanIpAddress,
                        action.Notes)
                    );
            }
        }
    }

    public class EndpointDDNS
    {
        [JsonProperty]
        public string Name { get; set; } = "GoogleDomainsDDNS";

        [JsonProperty]
        public string Url { get; set; } = "https://user:password@domains.google.com/nic/update?hostname=myhostname.com&myip={IP}";

        [JsonProperty]
        public string Domain { get; set; } = "my-domain-name.ddns-domain.com";

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
        public enum State : int { Unknown = 0, Error = 1, NoChange = 2, Updated = 3, Minor = 4 }

        [JsonProperty]
        public DateTime Occurred { get; set; } = DateTime.Now;

        [JsonProperty]
        public string Name { get; set; } = "*none*";

        [JsonProperty]
        public State Result { get; set; } = State.Unknown;

        [JsonProperty]
        public string WanIpAddress { get; set; }

        [JsonProperty]
        public string Notes { get; set; }
    }
}
