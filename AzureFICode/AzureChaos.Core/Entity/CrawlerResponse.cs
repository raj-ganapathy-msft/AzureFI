using AzureChaos.Core.Enums;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.ComponentModel.DataAnnotations;

namespace AzureChaos.Core.Entity
{
    public class CrawlerResponse : TableEntity
    {
        /// <summary>The Region Name</summary>
        public string RegionName { get; set; }

        [Required] public string ResourceGroupName { get; set; }

        /// <summary>Resource Id </summary>
        public string Id { get; set; }

        [Required] public string ResourceType { get; set; }

        /// <summary>The Resource Name.</summary>
        public string ResourceName { get; set; }

        /// <summary>Triggered Event </summary>
        public TriggeredEvent EventType { get; set; }

        /// <summary>Error message if anything occured on the time of execution.</summary>
        public string Error { get; set; }
    }
}