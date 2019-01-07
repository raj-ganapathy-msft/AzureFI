using Newtonsoft.Json;
using System.Collections.Generic;

namespace AzureChaos.Core.Models.Configs
{
    public class ChaosConfig
    {
        [JsonProperty("microsoft.faultinjection.enabled")]
        public bool ChaosEnabled { get; set; }

        [JsonProperty("microsoft.faultinjection.meantime")]
        // Donot execute chaos on the resource in mean time more than the minimum time
        public int MeanTime { get; set; }

        [JsonProperty("microsoft.faultinjection.scheduler.frequency")]
        public int SchedulerFrequency { get; set; }

        [JsonProperty("microsoft.faultinjection.rollback.frequency")]
        public int RollbackRunFrequency { get; set; }

        [JsonProperty("microsoft.faultinjection.crawler.frequency")]
        public int CrawlerFrequency { get; set; }

        [JsonProperty("microsoft.faultinjection.notification.global.enabled")]
        public bool NotificationEnabled { get; set; }

        [JsonProperty("microsoft.faultinjection.notification.sourceEmail")]
        public string SourceEmail { get; set; }

        [JsonProperty("microsoft.faultinjection.notification.global.receiverEmail")]
        public string ReceiverEmail { get; set; }

        [JsonProperty("microsoft.faultinjection.excludedResourceGroups")]
        public List<string> ExcludedResourceGroupList { get; set; }

        [JsonProperty("microsoft.faultinjection.actions")]
        public List<string> AzureFaultInjectionActions { get; set; }

        [JsonProperty("microsoft.faultinjection.AvSets")]
        public AvailabilitySetChaosConfig AvailabilitySetChaos { get; set; }

        [JsonProperty("microsoft.faultinjection.LB")]
        public LoadBalancerChaosConfig LoadBalancerChaos { get; set; }

        [JsonProperty("microsoft.faultinjection.VmSS")]
        public ScaleSetChaosConfig ScaleSetChaos { get; set; }

        [JsonProperty("microsoft.faultinjection.VM")]
        public VirtualMachineChaosConfig VirtualMachineChaos { get; set; }

        [JsonProperty("microsoft.faultinjection.AvZones")]
        public AvailabilityZoneChaosConfig AvailabilityZoneChaos { get; set; }
    }
}