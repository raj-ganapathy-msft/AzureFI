using System.ComponentModel.DataAnnotations;

namespace AzureChaos.Core.Entity
{
    public class ResourceGroupCrawlerResponse : CrawlerResponse
    {
        public ResourceGroupCrawlerResponse()
        {
        }

        public ResourceGroupCrawlerResponse(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        [Required] public string ProvisionalState { get; set; }
    }
}