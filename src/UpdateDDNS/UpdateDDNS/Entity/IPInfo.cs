using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.UpdateDDNS.Entity
{
	public class IPInfo
	{
		[JsonProperty(Required = Required.Default)]
		public string PreviousWanAddress { get; set; } = string.Empty;

		[JsonProperty(Required = Required.Default)]
		public DateTime PreviousWanAddressDetected { get; set; } = DateTime.MinValue;

		[JsonProperty(Required = Required.Default)]
		public string CurrentWanAddress { get; set; } = string.Empty;

		[JsonProperty(Required = Required.Default)]
		public DateTime CurrentWanAddressDetected { get; set; } = DateTime.MinValue;

		[JsonProperty(Required = Required.Default)]
		public DateTime LastCheck { get; set; } = DateTime.MinValue;
	}
}
