using BOG.SwissArmyKnife;
using BOG.SwissArmyKnife.Extensions;
using BOG.UpdateDDNS.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace BOG.UpdateDDNS
{
	class Program
	{
		static List<DynamicDnsServiceV2> configuration = new List<DynamicDnsServiceV2>();
		static Dictionary<string, string> argAlias = new Dictionary<string, string>()
		{
			{ "--path", "PATH" },
			{ "-p", "PATH" },
			{ "--service", "SERVICE" },
			{ "-s", "SERVICE" },
			{ "--useIP6", "USEIP6" },
			{ "-6", "USEIP6" },
			{ "-f", "FORCEUPDATE" },
			{ "--force", "FORCEUPDATE" }
		};
		static Dictionary<string, string> argValues = new Dictionary<string, string>()
		{
			{ "PATH", "$HOME" },
			{ "SERVICE", "" },
			{ "IPVERSION", "4" }
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
			Console.WriteLine("BOG.UpdateDDNS [-p {path}] [-6 true|false] --s {serviceName} [-f true|false]");
			Console.WriteLine("  -p, --path: (optional) overrides the default path for config and log files.");
			Console.WriteLine("  -6, --useIP6: (optional) true for IPv6, otherwise IPv4 (default)");
			Console.WriteLine("  -s, --service: name of DDNS service name to update.");
			Console.WriteLine("  -f, --force: (optional) true to always update DDNS server (default:when changed).");
		}

		static void Main(string[] args)
		{
			var usingIPv6 = false;
			var forceUpdate = false;
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
							configuration.Add(new DynamicDnsServiceV2
							{
								Name = "DuckDns",
								Domain = "myhost.duckdns.org",
								Url = "https://www.duckdns.org/update?domains=myhost,myotherhost&token=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee&ip={IP}"
							});
							configuration.Add(new DynamicDnsServiceV2
							{
								Name = "GoogleDomains",
								Domain = "myhost.mydomain.org",
								Url = "https://user:password@domains.google.com/nic/update?hostname=myhost.mydomain.org&myip={IP}"
							});
							configuration.Add(new DynamicDnsServiceV2
							{
								Name = "DynDns",
								Domain = "myhost.dyndns.org",
								Url = "https://user:updater-client-key@members.dyndns.org/v3/update?hostname=myhost&myip={IP}"
							});
							SaveConfiguration(configFile);
							updateAction.Result = UpdateAction.State.DefaultCreated;
						}
						else
						{
							throw new FileNotFoundException($"Missing configuration file: {configFile}");
						}
					}
					else
					{
						LoadConfiguration(configFile);
						if (updateAction.Result == UpdateAction.State.Unknown)
						{
							if (argValues.ContainsKey("USEIP6") && !string.IsNullOrWhiteSpace(argValues["USEIP6"]))
							{
								if (!bool.TryParse(argValues["USEIP6"], out usingIPv6))
								{
									throw new ArgumentException("USEIP6 argument must be boolean value.");
								}
								updateAction.IpVersion = usingIPv6 ? "v6" : "v4";
							}

							if (argValues.ContainsKey("FORCEUPDATE") && !string.IsNullOrWhiteSpace(argValues["FORCEUPDATE"]))
							{
								if (!bool.TryParse(argValues["FORCEUPDATE"], out forceUpdate))
								{
									throw new ArgumentException("FORCEUPDATE argument must be boolean value.");
								}
							}

							var configName = argValues["SERVICE"];
							if (string.IsNullOrWhiteSpace(configName)) throw new ArgumentException("Missing service argument.");
							configName = configName.Replace("\r", "").Replace("\n", "");   // linux/win precaution

							Console.WriteLine($"Loading config file: {configFile}");

							Console.WriteLine($"Extracting config for service: {configName}");
							var serviceDDNS = configuration
								.Where(o => string.Compare(o.Name, configName, false) == 0)
								.FirstOrDefault();

							if (serviceDDNS == null)
							{
								throw new Exception($"No service found in config file for {args[0]}");
							}
							updateAction.Name = serviceDDNS.Name;

							var thisIpInfo = (usingIPv6 ? serviceDDNS.IPv6 : serviceDDNS.IPv4);
							var ipCheckURL = (usingIPv6 ? "https://api64.ipify.org": "https://api.ipify.org");

							Console.WriteLine(string.Format("Querying current WAN IPv{0} address via: {1}", usingIPv6 ? "6" : "4", ipCheckURL));

							var client = new HttpClient();
							var ipAddress = client.GetStringAsync(ipCheckURL).GetAwaiter().GetResult();
							Console.Write($"Validating answer: {ipAddress}");
							var ipParsed = IPAddress.Parse(ipAddress);
							var isValid = false;
							switch (usingIPv6)
							{
								case true:
									isValid = ipParsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
									break;

								case false:
									isValid = ipParsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
									break;
							}
							if (!isValid)
							{
								Console.WriteLine($"IP address is not in the expected address family; it belongs to: {ipParsed.AddressFamily}");
								Console.WriteLine($"Skipping.");
								throw new ArgumentOutOfRangeException($"IP check returns invalid address value: {ipAddress}");
							}
							updateAction.WanIpAddress = ipAddress;
							thisIpInfo.LastCheck = DateTime.Now;
							SaveConfiguration(configFile);

							if (string.Compare(updateAction.WanIpAddress, thisIpInfo.CurrentWanAddress, false) == 0)
							{
								Console.WriteLine("IP address unchanged.");
								updateAction.Result = UpdateAction.State.NoChange;
							}
							else
							{
								updateAction.Result = UpdateAction.State.Updated;
								Console.WriteLine($"IP address change detected from {thisIpInfo.PreviousWanAddress} to {updateAction.WanIpAddress}");
								Console.WriteLine($"Sending change request to DDNS service: {args[0]}");

								var url = serviceDDNS.Url.Replace("{IP}", updateAction.WanIpAddress);
								var uri = new Uri(url);

								client = new HttpClient();
								if (!string.IsNullOrWhiteSpace(uri.UserInfo))
								{
									client.DefaultRequestHeaders.Authorization =
										new AuthenticationHeaderValue(
											"Basic", Convert.ToBase64String(
												System.Text.ASCIIEncoding.ASCII.GetBytes(
												   uri.UserInfo)));
								}
								var response = client.GetStringAsync(url).GetAwaiter().GetResult();
								Console.WriteLine($"Server Response: {response}");

								updateAction.Notes = response;
								if (response.ToUpper().Contains("GOOD") || response.ToUpper().Contains("OK") || response.ToUpper().Contains("NOCHG"))
								{
									Console.WriteLine("Valid response received");
									thisIpInfo.PreviousWanAddress = thisIpInfo.CurrentWanAddress;
									thisIpInfo.PreviousWanAddressDetected = thisIpInfo.CurrentWanAddressDetected;
									thisIpInfo.CurrentWanAddress = updateAction.WanIpAddress;
									thisIpInfo.CurrentWanAddressDetected = DateTime.Now;
								}
							}
							Console.WriteLine("Save configuration");
							SaveConfiguration(configFile);
						}
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
				sw.Write(BOG.SwissArmyKnife.Serializer<List<DynamicDnsServiceV2>>.ToJson(configuration));
			}
		}

		static void LoadConfiguration(string filename)
		{
			using (var sr = new StreamReader(filename))
			{
				configuration = BOG.SwissArmyKnife.Serializer<List<DynamicDnsServiceV2>>.FromJson(sr.ReadToEnd());
			}
		}

		//static List<DynamicDnsServiceV1> LoadObsoleteConfiguration(string filename)
		//{
		//	using (var sr = new StreamReader(filename))
		//	{
		//		return BOG.SwissArmyKnife.Serializer<List<DynamicDnsServiceV1>>.FromJson(sr.ReadToEnd());
		//	}
		//}

		//static void ConvertConfigV1toV2(List<DynamicDnsServiceV1> v1)
		//{
		//	foreach (var item in v1)
		//	{
		//		configuration.Add(new DynamicDnsServiceV2
		//		{
		//			Name = item.Name,
		//			Domain = item.Domain,
		//			Url = item.Url,
		//			IPv4 = new IPInfo
		//			{
		//				CurrentWanAddress = item.CurrentWanIP,
		//				CurrentWanAddressDetected = item.CurrentWanIPdetected,
		//				PreviousWanAddress = item.PreviousWanIP,
		//				PreviousWanAddressDetected = item.PreviousWanIPdetected
		//			}
		//		});
		//	}
		//}

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
}
