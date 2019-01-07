using Newtonsoft.Json;

namespace AzureChaos.Core.Models.Configs
{
    public class AvailabilitySetChaosConfig
    {
        [JsonProperty("microsoft.faultinjection.AvSets.enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("microsoft.faultinjection.AvSets.faultDomain.enabled")]
        public bool FaultDomainEnabled { get; set; }

        [JsonProperty("microsoft.faultinjection.AvSets.updateDomain.enabled")]
        public bool UpdateDomainEnabled { get; set; }
    }
}