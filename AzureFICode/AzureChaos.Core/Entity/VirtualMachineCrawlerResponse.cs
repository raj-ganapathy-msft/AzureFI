namespace AzureChaos.Core.Entity
{
    public class VirtualMachineCrawlerResponse : CrawlerResponse
    {
        public VirtualMachineCrawlerResponse()
        {
        }

        public VirtualMachineCrawlerResponse(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        /// <summary>Power state of the resource.</summary>
        public string State { get; set; }

        /// <summary>Available Set Id. i.e. Vm belongs to which avaiable set if any.</summary>
        public string AvailabilitySetId { get; set; }

        /// <summary>Update domain value.</summary>
        public int? UpdateDomain { get; set; }

        /// <summary>Fault domain value.</summary>
        public int? FaultDomain { get; set; }

        /// <summary>Scale Set Id. i.e. Vm belongs to which scale set if any.</summary>
        public string VirtualMachineScaleSetId { get; set; }

        /// <summary>The virtual machine group name i.e. the virtual machine belongs to which resource type ex. is it from Available Set, Scale Set  or Load balancers etc...</summary>
        public string VirtualMachineGroup { get; set; }

        /// <summary> Availability Zone for which the Virtual Machine belongs to </summary>
        public int? AvailabilityZone { get; set; }
    }
}