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
    public class AvailabilityZoneRuleEngine : IRuleEngine
    {
        private AzureClient azureClient = new AzureClient();

        public void CreateRule(TraceWriter log)
        {
            log.Info("AvailabilityZone RuleEngine: Started the creating rules for the scale set.");
            try
            {
                var possibleAvailabilityZoneRegionCombinations = GetAllPossibleAvailabilityZoneRegionCombination();
                if (possibleAvailabilityZoneRegionCombinations == null)
                {
                    log.Info("AvailabilityZone RuleEngine: Not found any possible Avilability zones");
                    return;
                }

                var recentlyExecutedAvailabilityZoneRegionCombination = GetRecentlyExecutedAvailabilityZoneRegionCombination();
                var avilabilityZoneRegionCombinations = possibleAvailabilityZoneRegionCombinations.Except(recentlyExecutedAvailabilityZoneRegionCombination);
                var avilabilityZoneRegionCombinationsList = avilabilityZoneRegionCombinations.ToList();
                if (avilabilityZoneRegionCombinationsList.Count == 0)
                {
                    return;
                }

                var random = new Random();
                var randomAvailabilityZoneRegion = avilabilityZoneRegionCombinationsList[random.Next(0, avilabilityZoneRegionCombinationsList.Count - 1)];
                var componentsInRandomAvailabilityZoneRegion = randomAvailabilityZoneRegion.Split(Delimeters.Exclamatory);
                var availabilityZone = int.Parse(componentsInRandomAvailabilityZoneRegion.Last());
                var region = componentsInRandomAvailabilityZoneRegion.First();
                InsertVirtualMachineAvailabilityZoneRegionResults(region, availabilityZone,log);
                log.Info("AvailabilityZones RuleEngine: Completed creating rule engine");
            }
            catch (Exception ex)
            {
                log.Error("AvailabilityZone RuleEngine: thrown exception", ex);
            }
        }

        private void InsertVirtualMachineAvailabilityZoneRegionResults(string region, int availbilityZone, TraceWriter log)
        {
            try
            {
                var virtualMachineQuery = TableQuery.CombineFilters(TableQuery.GenerateFilterConditionForInt(
                    "AvailabilityZone",
                    QueryComparisons.Equal,
                    availbilityZone),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RegionName",
                    QueryComparisons.Equal,
                    region));

                //TableQuery.GenerateFilterConditionForInt("AvailabilityZone", QueryComparisons.GreaterThanOrEqual, 0);
                var virtualMachinesTableQuery = new TableQuery<VirtualMachineCrawlerResponse>().Where(virtualMachineQuery);
                var crawledVirtualMachinesResults = StorageAccountProvider.GetEntities(
                    virtualMachinesTableQuery,
                    StorageTableNames.VirtualMachineCrawlerTableName);
                ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                crawledVirtualMachinesResults = crawledVirtualMachinesResults.Where(x => PowerState.Parse(x.State) == PowerState.Running);
                var virtualMachinesResults = crawledVirtualMachinesResults.ToList();
                var meanTimeQuery = TableQuery.GenerateFilterConditionForDate("ScheduledExecutionTime",
                    QueryComparisons.GreaterThanOrEqual,
                    DateTimeOffset.UtcNow.AddMinutes(-azureClient.AzureSettings.Chaos.MeanTime));

                var recentlyExecutedAvailabilityZoneRegionCombinationQuery = TableQuery.GenerateFilterCondition(
                    "ResourceType",
                    QueryComparisons.Equal,
                    VirtualMachineGroup.VirtualMachineScaleSets.ToString());

                var recentlyExecutedVMScaleSetsQuery = TableQuery.CombineFilters(meanTimeQuery,
                    TableOperators.And,
                    recentlyExecutedAvailabilityZoneRegionCombinationQuery);

                var scheduledQuery = new TableQuery<ScheduledRules>().Where(recentlyExecutedVMScaleSetsQuery);
                //Will get the executed query results. 
                var executedVMScaleSetsResults = StorageAccountProvider.GetEntities(scheduledQuery,
                    StorageTableNames.ScheduledRulesTableName);

                List<VirtualMachineCrawlerResponse> executedVMAvailabilityZones = null;

                if (virtualMachinesResults.Count() != 0 && executedVMScaleSetsResults.Count() != 0)
                {

                    foreach (var virtualMachineResult in virtualMachinesResults)
                    {
                        foreach (var executedVMScaleSetsResult in executedVMScaleSetsResults)
                        {
                            if ((executedVMScaleSetsResult.RowKey.ToLowerInvariant().Contains(virtualMachineResult.ResourceName.ToLowerInvariant()))
                               && (executedVMScaleSetsResult.RowKey.ToLowerInvariant().Contains(virtualMachineResult.ResourceGroupName.ToLowerInvariant()))
                               && (executedVMScaleSetsResult.RowKey.ToLowerInvariant().Contains(virtualMachineResult.AvailabilityZone.ToString()))
                                //&& (executedVMScaleSetsResult.RowKey.ToLowerInvariant().Contains(executedVMScaleSetsResult..ToLowerInvariant()))
                                )
                            {
                                executedVMAvailabilityZones.Add(virtualMachineResult);
                            }
                        }
                    }
                    if(executedVMAvailabilityZones != null && executedVMAvailabilityZones.Count() != 0)
                    virtualMachinesResults = virtualMachinesResults.Except(executedVMAvailabilityZones).ToList();
                }
                if (!virtualMachinesResults.Any()) return;
                var batchTasks = new List<Task>();
                var table = StorageAccountProvider.CreateOrGetTable(StorageTableNames.ScheduledRulesTableName);
                if (table == null)
                {
                    return;
                }

                for (var i = 0; i < virtualMachinesResults.Count; i += TableConstants.TableServiceBatchMaximumOperations)
                {
                    var batchItems = virtualMachinesResults.Skip(i)
                        .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                    var scheduledRulesbatchOperation = VirtualMachineHelper
                        .CreateScheduleEntityForAvailabilityZone(
                            batchItems,
                        azureClient.AzureSettings.Chaos.SchedulerFrequency,
                        azureClient.AzureSettings.Chaos.AzureFaultInjectionActions);

                    if (scheduledRulesbatchOperation.Count <= 0) return;
                    batchTasks.Add(table.ExecuteBatchAsync(scheduledRulesbatchOperation));
                }

                if (batchTasks.Count > 0)
                {
                    Task.WhenAll(batchTasks);
                }
            }
            catch (Exception ex)
            {
                log.Error("AvailabilityZone RuleEngine: thrown exception", ex);
                log.Error(ex.Source);
                log.Error(ex.Message);
            }
        }

        private IEnumerable<string> GetRecentlyExecutedAvailabilityZoneRegionCombination()
        {
            var recentlyExecutedAvailabilityZoneRegionCombination = new List<string>();
            var possibleAvailabilityZoneRegionCombinationVmCount = new Dictionary<string, int>();
            var meanTimeQuery = TableQuery.GenerateFilterConditionForDate("ScheduledExecutionTime",
                QueryComparisons.GreaterThanOrEqual,
                DateTimeOffset.UtcNow.AddMinutes(-azureClient.AzureSettings.Chaos.MeanTime));

            var recentlyExecutedAvailabilityZoneRegionCombinationQuery = TableQuery.GenerateFilterCondition(
                "ResourceType",
                QueryComparisons.Equal,
                VirtualMachineGroup.AvailabilityZones.ToString());

            var recentlyExecutedFinalAvailabilityZoneRegionQuery = TableQuery.CombineFilters(meanTimeQuery,
                TableOperators.And,
                recentlyExecutedAvailabilityZoneRegionCombinationQuery);

            var scheduledQuery = new TableQuery<ScheduledRules>().Where(recentlyExecutedFinalAvailabilityZoneRegionQuery);
            //Will get the executed query results. 
            var executedAvilabilityZoneCombinationResults = StorageAccountProvider.GetEntities(scheduledQuery,
                StorageTableNames.ScheduledRulesTableName);

            if (executedAvilabilityZoneCombinationResults == null)
            {
                return recentlyExecutedAvailabilityZoneRegionCombination;
            }
            
            foreach (var eachExecutedAvilabilityZoneCombinationResults in executedAvilabilityZoneCombinationResults)
            {
                if (!eachExecutedAvilabilityZoneCombinationResults.CombinationKey.Contains("!")) continue;

                if (possibleAvailabilityZoneRegionCombinationVmCount.ContainsKey(eachExecutedAvilabilityZoneCombinationResults.CombinationKey))
                {
                    possibleAvailabilityZoneRegionCombinationVmCount[eachExecutedAvilabilityZoneCombinationResults.CombinationKey] += 1;
                }
                else
                {
                    possibleAvailabilityZoneRegionCombinationVmCount[eachExecutedAvilabilityZoneCombinationResults.CombinationKey] = 1;
                }
            }

            recentlyExecutedAvailabilityZoneRegionCombination = new List<string>(possibleAvailabilityZoneRegionCombinationVmCount.Keys);
            return recentlyExecutedAvailabilityZoneRegionCombination;
        }

        private List<string> GetAllPossibleAvailabilityZoneRegionCombination()
        {
            var possibleAvailabilityZoneRegionCombinationVmCount = new Dictionary<string, int>();
            var crawledAvailabilityZoneVmQuery =
                TableQuery.GenerateFilterConditionForInt("AvailabilityZone", QueryComparisons.GreaterThan, 0);

            var availabilityZoneTableQuery = new TableQuery<VirtualMachineCrawlerResponse>().Where(crawledAvailabilityZoneVmQuery);
            var crawledVirtualMachinesResults = StorageAccountProvider.GetEntities(availabilityZoneTableQuery,
                StorageTableNames.VirtualMachineCrawlerTableName);

            crawledVirtualMachinesResults = crawledVirtualMachinesResults.Where(x => PowerState.Parse(x.State) == PowerState.Running);
            foreach (var eachCrawledVirtualMachinesResult in crawledVirtualMachinesResults)
            {
                var entryIntoPossibleAvailabilityZoneRegionCombinationVmCount =
                    eachCrawledVirtualMachinesResult.RegionName +
                    "!" +
                    eachCrawledVirtualMachinesResult.AvailabilityZone.ToString();

                if (possibleAvailabilityZoneRegionCombinationVmCount.ContainsKey(entryIntoPossibleAvailabilityZoneRegionCombinationVmCount))
                {
                    possibleAvailabilityZoneRegionCombinationVmCount[entryIntoPossibleAvailabilityZoneRegionCombinationVmCount] += 1;
                }
                else
                {
                    possibleAvailabilityZoneRegionCombinationVmCount[entryIntoPossibleAvailabilityZoneRegionCombinationVmCount] = 1;
                }
            }

            return new List<string>(possibleAvailabilityZoneRegionCombinationVmCount.Keys);
        }
    }
}