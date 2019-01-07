using Newtonsoft.Json;
using System.Collections.Generic;

namespace AzureChaos.Core.Models.Configs
{
    public class AvailabilityZoneChaosConfig
    {
        [JsonProperty("microsoft.faultinjection.AvZones.enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("microsoft.faultinjection.AvZones.regions")]
        public List<string> Regions { get; set; } = new List<string>();
    }
}