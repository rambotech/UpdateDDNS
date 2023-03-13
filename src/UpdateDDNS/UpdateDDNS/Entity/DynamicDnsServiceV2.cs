using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.UpdateDDNS.Entity
{
	internal class DynamicDnsServiceV2
	{
		[JsonProperty]
		public string Name { get; set; } = string.Empty;

		[JsonProperty]
		public string Url { get; set; } = string.Empty;

		[JsonProperty]
		public string Domain { get; set; } = string.Empty;

		[JsonProperty(Required = Required.Default)]
		public IPInfo IPv4 { get; set; } = new IPInfo();

		[JsonProperty(Required = Required.Default)]
		public IPInfo IPv6 { get; set; } = new IPInfo();
	}
}
