using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.UpdateDDNS.Entity
{
	internal class DynamicDnsServiceV1
	{
		[JsonProperty]
		public string Name { get; set; } = string.Empty;

		[JsonProperty]
		public string Url { get; set; } = string.Empty;

		[JsonProperty]
		public string Domain { get; set; } = string.Empty;

		[JsonProperty(Required = Required.Default)]
		public string PreviousWanIP { get; set; } = string.Empty;

		[JsonProperty(Required = Required.Default)]
		public DateTime PreviousWanIPdetected { get; set; } = DateTime.MinValue;

		[JsonProperty(Required = Required.Default)]
		public string CurrentWanIP { get; set; } = string.Empty;

		[JsonProperty(Required = Required.Default)]
		public DateTime CurrentWanIPdetected { get; set; } = DateTime.MinValue;

		[JsonProperty(Required = Required.Default)]
		public DateTime LastCheck { get; set; } = DateTime.MinValue;
	}
}
