namespace AzureChaos.Core.Entity
{
    public class LoadBalancerCrawlerResponse : CrawlerResponse
    {
        public LoadBalancerCrawlerResponse()
        {
        }

        public LoadBalancerCrawlerResponse(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public bool HasVirtualMachines { get; set; } = false;
        public int? AvailabilityZone { get; set; }
    }
}