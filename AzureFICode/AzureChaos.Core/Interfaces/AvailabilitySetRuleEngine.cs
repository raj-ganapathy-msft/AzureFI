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
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using Microsoft.Azure.Management.Compute.Fluent;

namespace AzureChaos.Core.Interfaces
{
    public class AvailabilitySetRuleEngine : IRuleEngine
    {
        private readonly AzureClient _azureClient = new AzureClient();

        public void CreateRule(TraceWriter log)
        {
            try
            {
                log.Info("Availability RuleEngine: Started the creating rules for the availability set.");
                Random random = new Random();
                //1) OpenSearch with Vm Count > 0
                var possibleAvailabilitySets = GetPossibleAvailabilitySets();
                if (possibleAvailabilitySets == null)
                {
                    log.Info("Availability RuleEngine: Not found any Avilability sets with virtual machines");
                    return;
                }

                var recentlyExcludedAvailabilitySetDomainCombination = GetRecentlyExecutedAvailabilitySetDomainCombination();
                var availableSetDomainOptions = possibleAvailabilitySets.Except(recentlyExcludedAvailabilitySetDomainCombination);
                var availableSetDomainOptionsList = availableSetDomainOptions.ToList();
                if (!availableSetDomainOptionsList.Any())
                {
                    return;
                }

                var randomAvailabilitySetDomainCombination = availableSetDomainOptionsList[random.Next(0, availableSetDomainOptionsList.Count - 1)];
                var componentsInAvailabilitySetDomainCombination = randomAvailabilitySetDomainCombination.Split(Delimeters.At);
                if (!componentsInAvailabilitySetDomainCombination.Any())
                {
                    return;
                }

                var domainId = componentsInAvailabilitySetDomainCombination.Last();
                if (string.IsNullOrWhiteSpace(domainId))
                {
                    return;
                }
                var domainNumber = int.Parse(domainId);
                var availabilitySetId = componentsInAvailabilitySetDomainCombination.First();
                InsertVirtualMachineAvailabilitySetDomainResults(availabilitySetId, domainNumber);
                log.Info("AvailabilitySet RuleEngine: Completed creating rule engine");
            }
            catch (Exception ex)
            {
                log.Error("Availability RuleEngine: Exception thrown. ", ex);
            }
        }

        private void InsertVirtualMachineAvailabilitySetDomainResults(string availabilitySetId, int domainNumber)
        {
            var virtualMachineQuery = TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("AvailabilitySetId",
                    QueryComparisons.Equal,
                    availabilitySetId),
                TableOperators.And,
                _azureClient.AzureSettings.Chaos.AvailabilitySetChaos.FaultDomainEnabled
                    ? TableQuery.GenerateFilterConditionForInt("FaultDomain",
                        QueryComparisons.Equal,
                        domainNumber)
                    : TableQuery.GenerateFilterConditionForInt("UpdateDomain",
                        QueryComparisons.Equal,
                        domainNumber));

            //TableQuery.GenerateFilterConditionForInt("AvailabilityZone", QueryComparisons.GreaterThanOrEqual, 0);
            var virtualMachinesTableQuery = new TableQuery<VirtualMachineCrawlerResponse>().Where(virtualMachineQuery);
            var crawledVirtualMachinesResults = StorageAccountProvider.GetEntities(virtualMachinesTableQuery,
                StorageTableNames.VirtualMachineCrawlerTableName);
            var virtualMachinesResults = crawledVirtualMachinesResults.ToList();
            if (!virtualMachinesResults.Any())
            {
                return;
            }

            var domainFlag = !_azureClient.AzureSettings.Chaos.AvailabilitySetChaos.UpdateDomainEnabled;
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
                var scheduledRulesbatchOperation =
                    VirtualMachineHelper.CreateScheduleEntityForAvailabilitySet(batchItems,
                        _azureClient.AzureSettings.Chaos.SchedulerFrequency,
                        _azureClient.AzureSettings.Chaos.AzureFaultInjectionActions,
                        domainFlag);
                if (scheduledRulesbatchOperation.Count <= 0)
                {
                    return;
                }

                batchTasks.Add(table.ExecuteBatchAsync(scheduledRulesbatchOperation));
            }

            if (batchTasks.Count > 0)
            {
                Task.WhenAll(batchTasks);
            }
        }

        private IEnumerable<string> GetRecentlyExecutedAvailabilitySetDomainCombination()
        {
            var recentlyExecutedAvailabilitySetDomainCombination = new List<string>();
            var possibleAvailabilitySetDomainCombinationVmCount = new Dictionary<string, int>();
            var meanTimeQuery = TableQuery.GenerateFilterConditionForDate("ScheduledExecutionTime",
                QueryComparisons.GreaterThanOrEqual,
                DateTimeOffset.UtcNow.AddMinutes(-_azureClient.AzureSettings.Chaos.MeanTime));

            var recentlyExecutedAvailabilitySetDomainCombinationQuery = TableQuery.GenerateFilterCondition(
                "ResourceType",
                QueryComparisons.Equal,
                VirtualMachineGroup.AvailabilitySets.ToString());

            var recentlyExecutedFinalAvailabilitySetDomainQuery = TableQuery.CombineFilters(meanTimeQuery,
                TableOperators.And,
                recentlyExecutedAvailabilitySetDomainCombinationQuery);

            var scheduledQuery = new TableQuery<ScheduledRules>().Where(recentlyExecutedFinalAvailabilitySetDomainQuery);
            var executedAvilabilitySetCombinationResults = StorageAccountProvider.GetEntities(scheduledQuery, StorageTableNames.ScheduledRulesTableName);
            if (executedAvilabilitySetCombinationResults == null)
                return recentlyExecutedAvailabilitySetDomainCombination;

            foreach (var eachExecutedAvilabilitySetCombinationResults in executedAvilabilitySetCombinationResults)
            {
                if (_azureClient.AzureSettings.Chaos.AvailabilitySetChaos.FaultDomainEnabled)
                {
                    if (!eachExecutedAvilabilitySetCombinationResults.CombinationKey.Contains(Delimeters.Exclamatory.ToString())) continue;

                    if (possibleAvailabilitySetDomainCombinationVmCount.ContainsKey(eachExecutedAvilabilitySetCombinationResults.CombinationKey))
                    {
                        possibleAvailabilitySetDomainCombinationVmCount[eachExecutedAvilabilitySetCombinationResults.CombinationKey] += 1;
                    }
                    else
                    {
                        possibleAvailabilitySetDomainCombinationVmCount[eachExecutedAvilabilitySetCombinationResults.CombinationKey] = 1;
                    }
                }
                else
                {
                    if (!eachExecutedAvilabilitySetCombinationResults.CombinationKey.Contains(Delimeters.Exclamatory.ToString())) continue;

                    if (possibleAvailabilitySetDomainCombinationVmCount.ContainsKey(eachExecutedAvilabilitySetCombinationResults.CombinationKey))
                    {
                        possibleAvailabilitySetDomainCombinationVmCount[eachExecutedAvilabilitySetCombinationResults.CombinationKey] += 1;
                    }
                    else
                    {
                        possibleAvailabilitySetDomainCombinationVmCount[eachExecutedAvilabilitySetCombinationResults.CombinationKey] = 1;
                    }
                }
            }

            recentlyExecutedAvailabilitySetDomainCombination = new List<string>(possibleAvailabilitySetDomainCombinationVmCount.Keys);
            return recentlyExecutedAvailabilitySetDomainCombination;
        }

        private List<string> GetPossibleAvailabilitySets()
        {
            string availabilitySetQuery = TableQuery.GenerateFilterConditionForBool("HasVirtualMachines", QueryComparisons.Equal, true);
            var availabilitySetTableQuery = new TableQuery<AvailabilitySetsCrawlerResponse>().Where(availabilitySetQuery);

            var crawledAvailabilitySetResults = StorageAccountProvider.GetEntities(availabilitySetTableQuery, StorageTableNames.AvailabilitySetCrawlerTableName);
            if (crawledAvailabilitySetResults == null)
            {
                return null;
            }

            Dictionary<string, int> possibleAvailabilitySetDomainCombinationVmCount = new Dictionary<string, int>();
            var bootStrapQuery = string.Empty;
            var initialQuery = true;
            foreach (var eachAvailabilitySet in crawledAvailabilitySetResults)
            {
                if (initialQuery)
                {
                    bootStrapQuery = TableQuery.GenerateFilterCondition("AvailabilitySetId", QueryComparisons.Equal, ConvertToProperAvailableSetId(eachAvailabilitySet.RowKey));
                    initialQuery = false;
                }
                else
                {
                    var localAvailabilitySetQuery = TableQuery.GenerateFilterCondition("AvailabilitySetId", QueryComparisons.Equal, ConvertToProperAvailableSetId(eachAvailabilitySet.RowKey));
                    bootStrapQuery = TableQuery.CombineFilters(localAvailabilitySetQuery, TableOperators.Or, bootStrapQuery);
                }
            }

            var virtualMachineTableQuery = new TableQuery<VirtualMachineCrawlerResponse>().Where(bootStrapQuery);
            var crawledVirtualMachineResults = StorageAccountProvider.GetEntities(virtualMachineTableQuery, StorageTableNames.VirtualMachineCrawlerTableName);
            crawledVirtualMachineResults = crawledVirtualMachineResults.Where(x => PowerState.Parse(x.State) == PowerState.Running);
            foreach (var eachVirtualMachine in crawledVirtualMachineResults)
            {
                string entryIntoPossibleAvailabilitySetDomainCombinationVmCount;
                if (_azureClient.AzureSettings.Chaos.AvailabilitySetChaos.FaultDomainEnabled)
                {
                    entryIntoPossibleAvailabilitySetDomainCombinationVmCount = eachVirtualMachine.AvailabilitySetId + Delimeters.Exclamatory.ToString() + eachVirtualMachine.FaultDomain;
                }
                else
                {
                    entryIntoPossibleAvailabilitySetDomainCombinationVmCount = eachVirtualMachine.AvailabilitySetId + Delimeters.At.ToString() + eachVirtualMachine.UpdateDomain;
                }

                if (possibleAvailabilitySetDomainCombinationVmCount.ContainsKey(entryIntoPossibleAvailabilitySetDomainCombinationVmCount))
                {
                    possibleAvailabilitySetDomainCombinationVmCount[entryIntoPossibleAvailabilitySetDomainCombinationVmCount] += 1;
                }
                else
                {
                    possibleAvailabilitySetDomainCombinationVmCount[entryIntoPossibleAvailabilitySetDomainCombinationVmCount] = 1;
                }
            }

            var possibleAvailableSets = new List<string>(possibleAvailabilitySetDomainCombinationVmCount.Keys);
            return possibleAvailableSets;
        }

        private static string ConvertToProperAvailableSetId(string rowKey)
        {
            var rowKeySplit = rowKey.Split(Delimeters.Exclamatory);
            return string.Join(Delimeters.ForwardSlash.ToString(), rowKeySplit).Replace(rowKeySplit.Last(), rowKeySplit.Last().ToUpper());
        }
    }
}