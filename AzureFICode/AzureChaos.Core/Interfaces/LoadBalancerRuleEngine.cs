using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Models;
using AzureChaos.Core.Providers;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureChaos.Core.Interfaces
{
    public class LoadBalancerRuleEngine : IRuleEngine
    {
        private readonly AzureClient _azureClient = new AzureClient();
        public void CreateRule(TraceWriter log)
        {
            try
            {
                var loadBalancer = GetRandomLoadBalancer();
                if (loadBalancer == null)
                {
                    log.Info("Loadbalancer RuleEngine: No load balancer found with virtual machines.");
                    return;
                }

                var filteredVmSet = GetVirtualMachineSet(loadBalancer.Id);
                if (filteredVmSet == null)
                {
                    log.Info("Loadbalancer RuleEngine: No virtual machines found for the load balancer name: " + loadBalancer.ResourceName);
                    return;
                }

                var table = StorageAccountProvider.CreateOrGetTable(StorageTableNames.ScheduledRulesTableName);
                if (table == null)
                {
                    return;
                }

                var count = VmCount(filteredVmSet.Count);
                var tasks = new List<Task>();

              //  do
              //  {
                    var randomSets = filteredVmSet.Take(count).ToList();
                    filteredVmSet = filteredVmSet.Except(randomSets).ToList();
                    for (var i = 0;
                        i < randomSets.Count;
                        i += TableConstants.TableServiceBatchMaximumOperations)
                    {
                        var batchItems = randomSets.Skip(i)
                            .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();

                        var batchOperation = VirtualMachineHelper.CreateScheduleEntity(batchItems,
                            _azureClient.AzureSettings.Chaos.SchedulerFrequency,
                            _azureClient.AzureSettings.Chaos.AzureFaultInjectionActions,
                            VirtualMachineGroup.LoadBalancer);

                        var operation = batchOperation;
                        tasks.Add(table.ExecuteBatchAsync(operation));
                    }

              //  } while (filteredVmSet.Any());

                Task.WhenAll(tasks);
                log.Info("Loadbalancer RuleEngine: Completed creating rule engine.");
            }
            catch (Exception ex)
            {
                log.Error("LoadBalancer RuleEngine: Exception thrown. ", ex);
            }
        }

        /// <summary>Get the virtual machine count based on the config percentage.</summary>
        /// <param name="totalCount">Total number of the virual machines.</param>
        /// <returns></returns>
        private int VmCount(int totalCount)
        {
            var vmPercentage = _azureClient.AzureSettings?.Chaos?.LoadBalancerChaos?.PercentageTermination;
            if (totalCount == 1)
            {
                return 1;
            }
            else
            {
                return vmPercentage == null ? totalCount : (int)(vmPercentage / 100 * totalCount);
            }
        }

        /// <summary>Pick the random scale set.</summary>
        /// <returns></returns>
        private LoadBalancerCrawlerResponse GetRandomLoadBalancer()
        {
            var filter = TableQuery.GenerateFilterConditionForBool("HasVirtualMachines", QueryComparisons.Equal, true);
            var resultsSet = ResourceFilterHelper.QueryCrawlerResponseByMeanTime<LoadBalancerCrawlerResponse>(_azureClient.AzureSettings,
               StorageTableNames.LoadBalancerCrawlerTableName, filter);

            if (resultsSet == null || !resultsSet.Any())
            {
                return null;
            }

            var random = new Random();
            var randomLoadBalancerIndex = random.Next(0, resultsSet.Count);
            return resultsSet.ToArray()[randomLoadBalancerIndex];
        }

        /// <summary>Get the list of virtual machines, based on the precondition check on the schedule table and activity table.
        /// here precondion ==> get the virtual machines from the crawler which are not in the recent scheduled list and not in the recent activities.</summary>
        /// <param name="scaleSetId">scale set id to filter the virtual machines.</param>
        /// <returns></returns>
        private IList<VirtualMachineCrawlerResponse> GetVirtualMachineSet(string loadBalancerId)
        {
            var rowKey = loadBalancerId.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory);
            var groupNameFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, rowKey);
            var resultsSet = ResourceFilterHelper.QueryCrawlerResponseByMeanTime<VirtualMachineCrawlerResponse>(_azureClient.AzureSettings,
                StorageTableNames.VirtualMachineCrawlerTableName, groupNameFilter);
            resultsSet = resultsSet.Where(x => PowerState.Parse(x.State) == PowerState.Running).ToList();
            if (resultsSet == null || !resultsSet.Any())
            {
                return null;
            }

            // TODO combine the schedule and activity table
            var scheduleEntities = ResourceFilterHelper.QuerySchedulesByMeanTime<ScheduledRules>(
                _azureClient.AzureSettings,
                StorageTableNames.ScheduledRulesTableName);

            var scheduleEntitiesResourceIds = scheduleEntities == null || !scheduleEntities.Any()
                ? new List<string>()
                : scheduleEntities.Select(x => x.RowKey.Replace(Delimeters.Exclamatory,
                    Delimeters.ForwardSlash));

            var result = resultsSet.Where(x =>
                !scheduleEntitiesResourceIds.Contains(x.Id));
            return result.ToList();
        }
    }
}
