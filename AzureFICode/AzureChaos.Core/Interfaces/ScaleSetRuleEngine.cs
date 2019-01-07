using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Models;
using AzureChaos.Core.Providers;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using Microsoft.Azure.Management.Compute.Fluent;

namespace AzureChaos.Core.Interfaces
{
    //TODO : Exception ending and logging
    /// <summary>Scale set rule engine will create the rules for the virtual machine based on the config settings and existing schedule/event tables.</summary>
    public class ScaleSetRuleEngine : IRuleEngine
    {
        private AzureClient azureClient = new AzureClient();

        /// <summary>Create the rule for the virtual machine scale vms </summary>
        /// <param name="log"></param>
        public void CreateRule(TraceWriter log)
        {
            log.Info("Scaleset RuleEngine: Started the creating rules for the scale set.");
            try
            {
                var scaleSet = GetRandomScaleSet();
                if (scaleSet == null)
                {
                    log.Info("Scaleset RuleEngine: No scale set found with virtual machines.");
                    return;
                }

                var filteredVmSet = GetVirtualMachineSet(scaleSet.Id);
                if (filteredVmSet == null)
                {
                    log.Info("Scaleset RuleEngine: No virtual machines found for the scale set name: " + scaleSet.ResourceName);
                    return;
                }

                var table = StorageAccountProvider.CreateOrGetTable(StorageTableNames.ScheduledRulesTableName);
                if (table == null)
                {
                    return;
                }

                var count = VmCount(filteredVmSet.Count);
                var tasks = new List<Task>();

               // do
               // {
                    var randomSets = filteredVmSet.Take(count).ToList();
                    filteredVmSet = filteredVmSet.Except(randomSets).ToList();
                    randomSets = randomSets.Where(x => PowerState.Parse(x.State) == PowerState.Running).ToList();
                    for (var i = 0;
                        i < randomSets.Count;
                        i += TableConstants.TableServiceBatchMaximumOperations)
                    {
                        var batchItems = randomSets.Skip(i)
                            .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                        var batchOperation = VirtualMachineHelper.CreateScheduleEntity(batchItems,
                            azureClient.AzureSettings.Chaos.SchedulerFrequency,
                            azureClient.AzureSettings.Chaos.AzureFaultInjectionActions,
                            VirtualMachineGroup.VirtualMachineScaleSets);

                        var operation = batchOperation;
                        tasks.Add(table.ExecuteBatchAsync(operation));
                    }
               // } while (filteredVmSet.Any());

                Task.WhenAll(tasks);
                log.Info("Scaleset RuleEngine: Completed creating rule engine.");
            }
            catch (Exception ex)
            {
                log.Error("Scaleset RuleEngine: Exception thrown. ", ex);
            }
        }

        /// <summary>Pick the random scale set.</summary>
        /// <returns></returns>
        private VirtualMachineScaleSetCrawlerResponse GetRandomScaleSet()
        {
            var filter = TableQuery.GenerateFilterConditionForBool("HasVirtualMachines", QueryComparisons.Equal, true);
            var resultsSet = ResourceFilterHelper.QueryCrawlerResponseByMeanTime<VirtualMachineScaleSetCrawlerResponse>(azureClient.AzureSettings,
               StorageTableNames.VirtualMachinesScaleSetCrawlerTableName, filter);

            if (resultsSet == null || !resultsSet.Any())
            {
                return null;
            }
            
            var random = new Random();
            var randomScaleSetIndex = random.Next(0, resultsSet.Count);
            return resultsSet.ToArray()[randomScaleSetIndex];
        }

        /// <summary>Get the list of virtual machines, based on the precondition check on the schedule table and activity table.
        /// here precondion ==> get the virtual machines from the crawler which are not in the recent scheduled list and not in the recent activities.</summary>
        /// <param name="scaleSetId">scale set id to filter the virtual machines.</param>
        /// <returns></returns>
        private IList<VirtualMachineCrawlerResponse> GetVirtualMachineSet(string scaleSetId)
        {
            var partitionKey = scaleSetId.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory);
            var groupNameFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey);
            var resultsSet = ResourceFilterHelper.QueryCrawlerResponseByMeanTime<VirtualMachineCrawlerResponse>(azureClient.AzureSettings,
                StorageTableNames.VirtualMachineCrawlerTableName, groupNameFilter);
            resultsSet = resultsSet.Where(x => PowerState.Parse(x.State) == PowerState.Running).ToList();
            if (resultsSet == null || !resultsSet.Any())
            {
                return null;
            }

            // TODO combine the schedule and activity table
            var scheduleEntities = ResourceFilterHelper.QuerySchedulesByMeanTime<ScheduledRules>(
                azureClient.AzureSettings,
                StorageTableNames.ScheduledRulesTableName);

            var scheduleEntitiesResourceIds = scheduleEntities == null || !scheduleEntities.Any()
                ? new List<string>()
                : scheduleEntities.Select(x => x.RowKey.Replace(Delimeters.Exclamatory,
                    Delimeters.ForwardSlash));

            var result = resultsSet.Where(x =>
                !scheduleEntitiesResourceIds.Contains(x.Id));
            return result.ToList();
        }

        /// <summary>Get the virtual machine count based on the config percentage.</summary>
        /// <param name="totalCount">Total number of the virual machines.</param>
        /// <returns></returns>
        private int VmCount(int totalCount)
        {
            var vmPercentage = azureClient.AzureSettings?.Chaos?.ScaleSetChaos?.PercentageTermination;

            return vmPercentage == null ? totalCount : (int)(vmPercentage / 100 * totalCount);
        }
    }
}