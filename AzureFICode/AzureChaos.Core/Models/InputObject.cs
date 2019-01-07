using AzureChaos.Core.Enums;

namespace AzureChaos.Core.Models
{
    /// <summary>The input object for the chaos executer.</summary>
    public class InputObject
    {
        /// <summary>Get or sets the action name i.e. what action should be performed on the resource.</summary>
        public string Action { get; set; }

        /// <summary>Get or sets the resource type.</summary>
        public string ResourceType { get; set; }

        /// <summary>Get or sets  the resource name.</summary>
        public string ResourceId { get; set; }

        /// <summary>Get or sets  the resource group.</summary>
        public string ResourceGroup { get; set; }

        /// <summary>Get or sets  the partition key.</summary>
        public string PartitionKey { get; set; }

        /// <summary>Get or sets  the resource key.</summary>
        public string RowKey { get; set; }

        /// <summary>Get or sets  the resource group.</summary>
        public string VirtualMachineScaleSetId { get; set; }

        /// <summary>Get or sets  the roolback.</summary>
        public bool EnableRollback { get; set; }
    }
}