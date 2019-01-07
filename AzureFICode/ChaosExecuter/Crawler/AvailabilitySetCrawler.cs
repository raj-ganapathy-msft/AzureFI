using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Models;
using AzureChaos.Core.Providers;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ChaosExecuter.Crawler
{
    public static class AvailabilitySetCrawler
    {
        [FunctionName("AvailabilitySetCrawler")]
        public static async Task Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequestMessage req, TraceWriter log)
        {
            log.Info("timercrawlerforavailabilitysets function processed a request.");
            var sw = Stopwatch.StartNew();//Recording
            var resourceGroupList = ResourceGroupHelper.GetResourceGroupsInSubscription(log);
            if (resourceGroupList == null)
            {
                log.Info($"timercrawlerforavailabilitysets: no resource groups to crawler");
                return;
            }

            await InsertAvailabilitySets(resourceGroupList, log);
            log.Info($"timercrawlerforavailabilitysets function processed a request. time elapsed : {sw.ElapsedMilliseconds}");
        }

        /// <summary>1. Iterate the resource groups to get the availability sets for individual resource group.
        /// 2. Convert the List of availability sets into availability set entity and add them into the table batch operation.
        /// 3. Get the list of virtual machines, convert into entity and add them into the table batach operation
        /// 3. Everything will happen parallel using TPL Parallel.Foreach</summary>
        /// <param name="resourceGroupList">List of resource groups for the particular subscription.</param>
        /// <param name="log">Trace writer instance</param>
        private static async Task InsertAvailabilitySets(IEnumerable<IResourceGroup> resourceGroupList, TraceWriter log)
        {
            try
            {
                // using concurrent bag to get the results from the parallel execution
                var virtualMachineConcurrentBag = new ConcurrentBag<IEnumerable<IGrouping<string, IVirtualMachine>>>();
                var batchTasks = new ConcurrentBag<Task>();

                var azureClient = new AzureClient();
                // get the availability set batch operation and vm list by availability sets
                SetTheVirtualMachinesAndAvailabilitySetBatchTask(resourceGroupList, virtualMachineConcurrentBag, batchTasks, azureClient, log);

                // get the virtual machine table batch operation parallely
                IncludeVirtualMachineTask(virtualMachineConcurrentBag, batchTasks, log);

                // execute all batch operation as parallel
                await Task.WhenAll(batchTasks);
            }
            catch (Exception ex)
            {
                log.Error($"timercrawlerforavailableset threw the exception ", ex,
                    "GetAvilabilitySetsForResourceGroups");
            }
        }

        /// <summary>Include the virtual machine batch operation into existing batch opeation</summary>
        /// <param name="virtualMachineConcurrentBag"></param>
        /// <param name="batchTasks"></param>
        /// <param name="log"></param>
        private static void IncludeVirtualMachineTask(ConcurrentBag<IEnumerable<IGrouping<string, IVirtualMachine>>> virtualMachineConcurrentBag,
                                                      ConcurrentBag<Task> batchTasks, TraceWriter log)
        {
            var groupsByVirtulaMachine = virtualMachineConcurrentBag.SelectMany(x => x);
            var virtualmachineCloudTable = StorageAccountProvider.CreateOrGetTable(StorageTableNames.VirtualMachineCrawlerTableName);
            if (virtualmachineCloudTable == null)
            {
                return;
            }

            Parallel.ForEach(groupsByVirtulaMachine, groupItem =>
            {
                // table batch operation currently allows only 100 per batch, So ensuring the one batch operation will have only 100 items
                for (var i = 0; i < groupItem.Count(); i += TableConstants.TableServiceBatchMaximumOperations)
                {
                    var batchItems = groupItem.Skip(i)
                        .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                    var virtualMachineBatchOperation = GetVirtualMachineBatchOperation(batchItems, groupItem.Key, log);
                    if (virtualMachineBatchOperation != null && virtualMachineBatchOperation.Count > 0 && virtualmachineCloudTable != null)
                    {
                        batchTasks.Add(virtualmachineCloudTable.ExecuteBatchAsync(virtualMachineBatchOperation));
                    }
                }
            });
        }

        /// <summary>Get the list of the availability sets by resource group.
        /// And get the virtual machine by resource group and the availability sets.
        /// And get the batch operation for the availability sets</summary>
        /// <param name="resourceGroupList"></param>
        /// <param name="virtualMachinesConcurrent"></param>
        /// <param name="batchTasks"></param>
        /// <param name="azureClient"></param>
        /// <param name="log"></param>
        private static void SetTheVirtualMachinesAndAvailabilitySetBatchTask(IEnumerable<IResourceGroup> resourceGroupList,
            ConcurrentBag<IEnumerable<IGrouping<string, IVirtualMachine>>> virtualMachinesConcurrent,
            ConcurrentBag<Task> batchTasks,
            AzureClient azureClient,
            TraceWriter log)
        {
            var availabilitySetCloudTable = StorageAccountProvider.CreateOrGetTable(StorageTableNames.AvailabilitySetCrawlerTableName);
            if (availabilitySetCloudTable == null)
            {
                return;
            }

            Parallel.ForEach(resourceGroupList, eachResourceGroup =>
            {
                try
                {
                    var availabilitySetIds = new List<string>();
                    var availabilitySetsByResourceGroup = azureClient.AzureInstance.AvailabilitySets.ListByResourceGroup(eachResourceGroup.Name);
                    // table batch operation currently allows only 100 per batch, So ensuring the one batch operation will have only 100 items
                    var setsByResourceGroup = availabilitySetsByResourceGroup.ToList();
                    for (var i = 0; i < setsByResourceGroup.Count; i += TableConstants.TableServiceBatchMaximumOperations)
                    {
                        var batchItems = setsByResourceGroup.Skip(i)
                            .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                        // get the availability sets by resource group
                        // get the availability sets batch operation and get the list of availability set ids
                        var availabilitySetbatchOperation =
                            GetAvailabilitySetBatchOperation(batchItems, availabilitySetIds);

                        // add the batch operation into task list
                        if (availabilitySetbatchOperation.Count > 0 && availabilitySetCloudTable != null)
                        {
                            batchTasks.Add(availabilitySetCloudTable.ExecuteBatchAsync(availabilitySetbatchOperation));
                        }
                    }

                    // Get the virtual machines by resource group and by availability set ids
                    var virtualMachinesByAvailabilitySetId = GetVirtualMachineListByResourceGroup(eachResourceGroup.Name, availabilitySetIds, azureClient);
                    if (virtualMachinesByAvailabilitySetId != null && virtualMachinesByAvailabilitySetId.Count > 0)
                    {
                        virtualMachinesConcurrent.Add(virtualMachinesByAvailabilitySetId);
                    }
                }
                catch (Exception e)
                {
                    log.Error($"timercrawlerforavailableset threw the exception ", e,
                        "for resource group: " + eachResourceGroup.Name);
                }
            });
        }

        /// <summary>Get the virtual machines by resource group and availability set ids.</summary>
        /// <param name="resourceGroupName"></param>
        /// <param name="availabilitySetIds"></param>
        /// <param name="azureClient"></param>
        /// <returns></returns>
        private static IList<IGrouping<string, IVirtualMachine>> GetVirtualMachineListByResourceGroup(string resourceGroupName,
            List<string> availabilitySetIds,
            AzureClient azureClient)
        {
            // Get the virtual machines by resource group
            var virtualMachinesList = azureClient.AzureInstance.VirtualMachines.ListByResourceGroup(resourceGroupName).ToList();
            if (!virtualMachinesList.Any())
            {
                return null;
            }

            var loadBalancerVmTask = VirtualMachineHelper.GetVirtualMachinesFromLoadBalancers(resourceGroupName, azureClient);
            var loadbalancerVms = loadBalancerVmTask.Result;
            if (loadbalancerVms != null && loadbalancerVms.Any())
            {
                virtualMachinesList = virtualMachinesList.Where(x => !loadbalancerVms.Contains(x.Id))?.ToList();
            }

            // Group the the virtual machine based on the availability set id
            var virtualMachinesByAvailabilitySetId = virtualMachinesList?.Where(x => availabilitySetIds
                    .Contains(x.AvailabilitySetId, StringComparer.OrdinalIgnoreCase))
                .GroupBy(x => x.AvailabilitySetId, x => x).ToList();
            return virtualMachinesByAvailabilitySetId;
        }

        /// <summary>Get the availability set batch operation</summary>
        /// <param name="availabilitySetsByResourceGroup">Resource group name to filter the availability set</param>
        /// <param name="availabilitySetIdList">List of availability set,
        /// which will be using to filter the virtual machine list by availability set ids</param>
        /// <returns></returns>
        private static TableBatchOperation GetAvailabilitySetBatchOperation(
            IEnumerable<IAvailabilitySet> availabilitySetsByResourceGroup,
            List<string> availabilitySetIdList)
        {
            var availabilitySetBatchOperation = new TableBatchOperation();
            foreach (var eachAvailabilitySet in availabilitySetsByResourceGroup)
            {
                availabilitySetBatchOperation.InsertOrReplace(
                    ConvertToAvailabilitySetsCrawlerResponse(eachAvailabilitySet));
                availabilitySetIdList.Add(eachAvailabilitySet.Id);
            }

            return availabilitySetBatchOperation;
        }

        /// <summary>Get the virtual machine batch operation.</summary>
        /// <param name="virtualMachines">List of the virtual machines</param>
        /// <param name="partitionKey"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static TableBatchOperation GetVirtualMachineBatchOperation(IList<IVirtualMachine> virtualMachines,
            string partitionKey, TraceWriter log)
        {
            if (virtualMachines == null)
            {
                return null;
            }

            var virtualMachineTableBatchOperation = new TableBatchOperation();
            partitionKey = partitionKey.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory);
            foreach (var eachVirtualMachine in virtualMachines)
            {
                try
                {
                    var virtualMachineEntity = VirtualMachineHelper.ConvertToVirtualMachineEntity(
                                                                    eachVirtualMachine, partitionKey,
                                                                    eachVirtualMachine.ResourceGroupName);
                    virtualMachineEntity.VirtualMachineGroup = VirtualMachineGroup.AvailabilitySets.ToString();
                    virtualMachineTableBatchOperation.InsertOrReplace(virtualMachineEntity);
                }
                catch (Exception ex)
                {
                    log.Error($"timercrawlerforavailableset threw the exception ", ex,
                        "GetVirtualMachineBatchOperation");
                }
            }

            return virtualMachineTableBatchOperation;
        }

        /// <summary>Convert the Availability Set instance to Availability set entity.</summary>
        /// <param name="availabilitySet">The scale set instance.</param>
        /// <returns></returns>
        private static AvailabilitySetsCrawlerResponse ConvertToAvailabilitySetsCrawlerResponse(IAvailabilitySet availabilitySet)
        {
            var availabilitySetEntity = new AvailabilitySetsCrawlerResponse(availabilitySet.ResourceGroupName, availabilitySet.Id.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory))
            {
                Id = availabilitySet.Id,
                RegionName = availabilitySet.RegionName,
                ResourceName = availabilitySet.Name,
                FaultDomainCount = availabilitySet.FaultDomainCount,
                UpdateDomainCount = availabilitySet.UpdateDomainCount,
            };

            if (availabilitySet.Inner?.VirtualMachines != null && availabilitySet.Inner.VirtualMachines.Count > 0)
            {
                availabilitySetEntity.HasVirtualMachines = true;
            }

            return availabilitySetEntity;
        }
    }
}