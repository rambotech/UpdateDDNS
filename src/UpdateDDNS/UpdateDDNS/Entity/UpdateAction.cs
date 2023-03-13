using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.UpdateDDNS.Entity
{
	[JsonObject]
	internal class UpdateAction
	{
		public enum State : int { Unknown = 0, InvalidParameters = 1, Error = 2, NoChange = 3, Updated = 4, DefaultCreated = 5  }

		[JsonProperty]
		public DateTime Occurred { get; set; } = DateTime.Now;

		[JsonProperty]
		public string Name { get; set; } = "*none*";

		[JsonProperty]
		public string IpVersion{ get; set; } = "4";

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
