namespace AzureChaos.Core.Entity
{
    public class VirtualMachineScaleSetCrawlerResponse : CrawlerResponse
    {
        public VirtualMachineScaleSetCrawlerResponse()
        {
        }

        public VirtualMachineScaleSetCrawlerResponse(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public bool HasVirtualMachines { get; set; } = false;
        public int? AvailabilityZone { get; set; }
    }
}