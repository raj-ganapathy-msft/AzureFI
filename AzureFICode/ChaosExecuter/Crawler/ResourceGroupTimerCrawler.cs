using AzureChaos.Core.Entity;
using AzureChaos.Core.Constants;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Providers;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace ChaosExecuter.Crawler
{
    public static class ResourceGroupTimerCrawler
    {
        [FunctionName("timercrawlerresourcegroups")]
        public static async Task Run([TimerTrigger("%CrawlerFrequency%")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"timercrawlerresourcegroups executed at: {DateTime.UtcNow}");
            try
            {
                var resourceGroups = ResourceGroupHelper.GetResourceGroupsInSubscription();
                //Todo make it as Sync
                await InsertOrReplaceResourceGroupsAsync(resourceGroups, log); //Can we make it as task.run ??
            }
            catch (Exception ex)
            {
                log.Error($"timercrawlerresourcegroups threw exception ", ex, "timercrawlerresourcegroups");
            }
        }

        private static async Task InsertOrReplaceResourceGroupsAsync(List<IResourceGroup> resourceGroups, TraceWriter log)
        {
            var tableBatchOperation = new TableBatchOperation();
            // table batch operation currently allows only 100 per batch, So ensuring the one batch operation will have only 100 items
            for (var i = 0; i < resourceGroups.Count; i += TableConstants.TableServiceBatchMaximumOperations)
            {
                var batchItems = resourceGroups.Skip(i)
                    .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                foreach (var eachResourceGroup in batchItems)
                {
                    var resourceGroupCrawlerResponseEntity =
                        new ResourceGroupCrawlerResponse("", eachResourceGroup.Name);
                    try
                    {
                        resourceGroupCrawlerResponseEntity.Id = eachResourceGroup.Id;
                        resourceGroupCrawlerResponseEntity.RegionName = eachResourceGroup.RegionName;
                        tableBatchOperation.InsertOrReplace(resourceGroupCrawlerResponseEntity);
                    }
                    catch (Exception ex)
                    {
                        resourceGroupCrawlerResponseEntity.Error = ex.Message;
                        log.Error($"timercrawlerresourcegroups threw exception ", ex, "timercrawlerresourcegroups");
                    }
                }
            }

            if (tableBatchOperation.Count > 0)
            {
                try
                {
                    var table = StorageAccountProvider.CreateOrGetTable(StorageTableNames.ResourceGroupCrawlerTableName);
                    await table.ExecuteBatchAsync(tableBatchOperation);
                }
                catch (Exception ex)
                {
                    log.Error($"timercrawlerresourcegroups threw exception ", ex, "timercrawlerresourcegroups");
                }
            }
        }
    }
}