using Newtonsoft.Json;

namespace AzureChaos.Core.Models.Configs
{
    public class ScaleSetChaosConfig
    {
        [JsonProperty("microsoft.faultinjection.VmSS.enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("microsoft.faultinjection.VmSS.percentageTermination")]
        public decimal PercentageTermination { get; set; }
    }
}