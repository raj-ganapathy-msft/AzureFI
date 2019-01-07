using Newtonsoft.Json;

namespace AzureChaos.Core.Models.Configs
{
    public class TargetConfig
    {
        [JsonProperty("microsoft.faultinjection.client.subscription.id")]
        public string SubscriptionId { get; set; }

        [JsonProperty("microsoft.faultinjection.client.id")]
        public string ClientId { get; set; }

        [JsonProperty("microsoft.faultinjection.client.secretKey")]
        public string ClientSecret { get; set; }

        [JsonProperty("microsoft.faultinjection.client.tenant.id")]
        public string TenantId { get; set; }

        [JsonProperty("microsoft.faultinjection.client.region")]
        public string Region { get; set; }

        [JsonProperty("microsoft.faultinjection.client.resourceGroup")]
        public string ResourceGroup { get; set; }

        [JsonProperty("microsoft.faultinjection.client.storageAccount.name")]
        public string StorageAccountName { get; set; }

        [JsonProperty("microsoft.faultinjection.client.storageAccount.connectionString")]
        public string StorageAccountKey { get; set; }
    }
}