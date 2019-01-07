using System.ComponentModel.DataAnnotations;

namespace AzureChaos.Core.Entity
{
    //Todo Declare the default values if possible
    public class AvailabilitySetsCrawlerResponse : CrawlerResponse
    {
        public AvailabilitySetsCrawlerResponse()
        { }

        public AvailabilitySetsCrawlerResponse(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        [Required] public string Key { get; set; }

        public bool HasVirtualMachines { get; set; }

        [Required] public int FaultDomainCount { get; set; }

        [Required] public int UpdateDomainCount { get; set; }
    }
}