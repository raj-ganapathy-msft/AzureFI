using Newtonsoft.Json;

namespace AzureChaos.Core.Models.Configs
{
    public class LoadBalancerChaosConfig
    {
        [JsonProperty("microsoft.faultinjection.LB.enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("microsoft.faultinjection.LB.percentageTermination")]
        public decimal PercentageTermination { get; set; }
    }
}