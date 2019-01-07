using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureChaos.Core.Models
{
    public class ConfigModel
    {
        [JsonProperty("tenantId")] public string TenantId { get; set; }

        [JsonProperty("clientId")] public string ClientId { get; set; }

        [JsonProperty("clientSecret")] public string ClientSecret { get; set; }

        [JsonProperty("subscription")] public string Subscription { get; set; }

        [JsonIgnore]
        public string SelectedDeploymentRg { get; set; }

        [JsonProperty("region")]
        public string SelectedRegion { get; set; }

        [JsonIgnore]
        public string StorageAccountName { get; set; }

        public string StorageConnectionString { get; set; }

        [JsonProperty("excludedResourceGroups")] public List<string> ExcludedResourceGroups { get; set; }

        [JsonProperty("azureFiActions")] public List<string> AzureFiActions { get; set; }

        [JsonProperty("isChaosEnabled")] public bool IsChaosEnabled { get; set; }

        [JsonProperty("schedulerFrequency")] public int SchedulerFrequency { get; set; }

        [JsonProperty("rollbackFrequency")] public int RollbackFrequency { get; set; }

        [JsonProperty("triggerFrequency")] public int TriggerFrequency { get; set; }

        [JsonProperty("crawlerFrequency")] public int CrawlerFrequency { get; set; }

        [JsonProperty("meanTime")] public int MeanTime { get; set; }

        [JsonProperty("isAvZoneEnabled")] public bool IsAvZoneEnabled { get; set; }

        [JsonProperty("avZoneRegions")] public List<string> AvZoneRegions { get; set; }

        [JsonProperty("isVmEnabled")] public bool IsVmEnabled { get; set; }

        [JsonProperty("vmPercentage")]
        public decimal VmPercentage { get; set; }

        [JsonProperty("isVmssEnabled")] public bool IsVmssEnabled { get; set; }

        [JsonProperty("vmssPercentage")]
        public decimal VmssPercentage { get; set; }

        [JsonProperty("isLoadbalancerEnabled")] public bool IsLoadbalancerEnabled { get; set; }

        [JsonProperty("loadbalancerPercentage")]
        public decimal LoadBalancerPercentage { get; set; }

        [JsonProperty("isAvSetEnabled")] public bool IsAvSetEnabled { get; set; }

        [JsonProperty("isFaultDomainEnabled")]
        public bool IsFaultDomainEnabled { get; set; }

        [JsonProperty("isUpdateDomainEnabled")]
        public bool IsUpdateDomainEnabled { get; set; }
    }
}