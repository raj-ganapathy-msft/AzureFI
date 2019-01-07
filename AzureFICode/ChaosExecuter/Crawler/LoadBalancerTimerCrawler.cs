using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Models;
using AzureChaos.Core.Providers;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using AzureChaos.Core.Constants;
using System.Collections.Concurrent;
using System.Linq;

namespace ChaosExecuter.Crawler
{
    public static class LoadBalancerTimerCrawler
    {
        [FunctionName("LoadBalancerTimerCrawler")]
        public static async System.Threading.Tasks.Task RunAsync([TimerTrigger("%CrawlerFrequency%")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("timercrawlerforloadbalancers function processed a request.");
            var sw = Stopwatch.StartNew();//Recording
            var resourceGroupList = ResourceGroupHelper.GetResourceGroupsInSubscription(log);
            if (resourceGroupList == null)
            {
                log.Info($"timercrawlerforloadbalancers: no resource groups to crawler");
                return;
            }

            await GetLoadBalancersForResourceGroupsAsync(resourceGroupList, log);
            log.Info($"timercrawlerforloadbalancers function processed a request. time elapsed : {sw.ElapsedMilliseconds}");
        }

        /// <summary>1. Iterate the resource groups to get the scale sets for individual resource group.
        /// 2. Convert the List of scale sets into scale set entity and add them into the table batch operation.
        /// 3. Get the list of virtual machine instances, convert into entity and them into the table batach operation
        /// 3. Execute all the task parallely</summary>
        /// <param name="resourceGroups">List of resource groups for the particular subscription.</param>
        /// <param name="log">Trace writer instance</param>
        private static async Task GetLoadBalancersForResourceGroupsAsync(IEnumerable<IResourceGroup> resourceGroups,
                                                                     TraceWriter log)
        {
            try
            {
                var virtualMachineCloudTable = StorageAccountProvider.CreateOrGetTable(StorageTableNames.VirtualMachineCrawlerTableName);
                var loadBalancerTable = StorageAccountProvider.CreateOrGetTable(StorageTableNames.LoadBalancerCrawlerTableName);
                if (virtualMachineCloudTable == null || loadBalancerTable == null)
                {
                    return;
                }

                var batchTasks = new ConcurrentBag<Task>();
                var azureClient = new AzureClient();
                // using parallel here to run all the resource groups parallelly, parallel is 10times faster than normal foreach.
                foreach (var eachResourceGroup in resourceGroups)
                //Parallel.ForEach(resourceGroups, eachResourceGroup =>
                {
                    try
                    {
                        var loadBalancersList = azureClient.AzureInstance.LoadBalancers
                            .ListByResourceGroup(eachResourceGroup.Name);
                        //var count = loadBalancersList.Count();
                        if (loadBalancersList.Count() > 0)
                        {
                            GetVirtualMachineAndLoadBalancerBatch(loadBalancersList.ToList(), batchTasks,
                                virtualMachineCloudTable,
                                loadBalancerTable, azureClient, log);

                        }
                    }
                    catch (Exception e)
                    {
                        //  catch the error, to continue adding other entities to table
                        log.Error($"timercrawlerforloadbalancer threw the exception ", e,
                            "GetLoadBalancerForResourceGroups: for the resource group " + eachResourceGroup.Name);
                    }
                }
                //);

                // execute all batch operation as parallel
                await Task.WhenAll(batchTasks);
            }
            catch (Exception ex)
            {
                log.Error($"timercrawlerforloadbalancer threw the exception ", ex, "GetLoadBalancerForResourceGroups");
            }
        }

        /// <summary>1. Get the List of scale sets for the resource group.
        /// 2. Get all the virtual machines from the each scale set and them into batch operation
        /// 3. Combine all the tasks and return the list of tasks.</summary>
        /// <param name="virtualMachineScaleSets">List of scale sets for the resource group</param>
        /// <param name="batchTasks"></param>
        /// <param name="virtualMachineCloudTable">Get the virtual machine table instance</param>
        /// <param name="virtualMachineScaleSetCloudTable">Get the scale set table instance</param>
        /// <param name="log">Trace writer instance</param>
        /// <returns></returns>
        private static void GetVirtualMachineAndLoadBalancerBatch(IEnumerable<ILoadBalancer> loadBalancers,
                                                              ConcurrentBag<Task> batchTasks, CloudTable virtualMachineCloudTable,
                                                              CloudTable loadBalancerCloudTable, AzureClient azureClient, TraceWriter log)
        {
            if (loadBalancers == null || virtualMachineCloudTable == null || loadBalancerCloudTable == null)
            {
                return;
            }

            var listOfLoadBalancerEntities = new ConcurrentBag<LoadBalancerCrawlerResponse>();
            // get the batch operation for all the load balancers and corresponding virtual machine instances
            //foreach (var eachLoadBalancer in loadBalancers)
            Parallel.ForEach(loadBalancers, eachLoadBalancer =>
            {
                try
                {
                    listOfLoadBalancerEntities.Add(ConvertToLoadBalancerCrawlerResponse(eachLoadBalancer, azureClient, log));
                    var loadBalancersVirtualMachines = GetVirtualMachinesFromLoadBalancers(eachLoadBalancer.ResourceGroupName, eachLoadBalancer.Id, azureClient, log);
                    var tasks = new List<Task>
                                {
                                    loadBalancersVirtualMachines
                                };
                    Task.WhenAll(tasks);
                    var virtualMachineIds = loadBalancersVirtualMachines.Result;
                    var virtualMachinesBatchOperation = InsertLoadBalancerVirtualMachines(virtualMachineIds, eachLoadBalancer, azureClient, log);
                    if (virtualMachinesBatchOperation != null && virtualMachinesBatchOperation.Count > 0)
                    {
                        batchTasks.Add(virtualMachineCloudTable.ExecuteBatchAsync(virtualMachinesBatchOperation));
                    }
                    var virtualMachinesList = new List<string>();

                }
                catch (Exception e)
                {
                    //  catch the error, to continue adding other entities to table
                    log.Error($"timercrawlerforvirtualmachinescaleset threw the exception ", e,
                        "GetVirtualMachineAndScaleSetBatch for the scale set: " + eachLoadBalancer.Name);
                }
            });

            // table batch operation currently allows only 100 per batch, So ensuring the one batch operation will have only 100 items
            for (var i = 0; i < listOfLoadBalancerEntities.Count; i += TableConstants.TableServiceBatchMaximumOperations)
            {
                var batchItems = listOfLoadBalancerEntities.Skip(i)
                    .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                var loadBalancerTableBatchOperation = new TableBatchOperation();
                foreach (var entity in batchItems)
                {
                    loadBalancerTableBatchOperation.InsertOrReplace(entity);
                }

                batchTasks.Add(loadBalancerCloudTable.ExecuteBatchAsync(loadBalancerTableBatchOperation));
            }
        }

        private static TableBatchOperation InsertLoadBalancerVirtualMachines(List<string> virtualMachineIds, ILoadBalancer eachLoadBalancer, AzureClient azureClient, TraceWriter log)
        {
            if (virtualMachineIds == null)
            {
                return null;
            }

            var virtualMachineBatchOperation = new TableBatchOperation();
            foreach (var eachvirtualMachineId in virtualMachineIds)
            {
                var virtualMachine = azureClient.AzureInstance.VirtualMachines.GetById(eachvirtualMachineId);
                var partitionKey = eachLoadBalancer.Id.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory);
                virtualMachineBatchOperation.InsertOrReplace(VirtualMachineHelper.ConvertToVirtualMachineEntityFromLB(virtualMachine,
                                                                                  partitionKey, VirtualMachineGroup.LoadBalancer.ToString()));
            }

            return virtualMachineBatchOperation;
        }

        private static LoadBalancerCrawlerResponse ConvertToLoadBalancerCrawlerResponse(ILoadBalancer loadBalancer, AzureClient azureClient, TraceWriter log)
        {
            var loadBalancerEntity = new LoadBalancerCrawlerResponse(loadBalancer.ResourceGroupName,
                loadBalancer.Id.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory))
            {
                ResourceName = loadBalancer.Name,
                RegionName = loadBalancer.RegionName,
                Id = loadBalancer.Id
            };

            //var loadBalancerList = new List<ILoadBalancer>();
            //loadBalancer.SelectMany(x => x.Backends).SelectMany(x => x.Value.GetVirtualMachineIds());
            var lbtest = GetVirtualMachinesFromLoadBalancers(loadBalancer.ResourceGroupName, loadBalancer.Id, azureClient, log);
            var tasks = new List<Task>
                                {
                                    lbtest
                                };
            Task.WhenAll(tasks);
            var virtualMachineIds = lbtest.Result.Count;
            if (lbtest.Result.Count > 0)
            {
                loadBalancerEntity.HasVirtualMachines = true;
            }

            return loadBalancerEntity;
        }
        /// <summary>Get the list of the load balancer virtual machines by resource group.</summary>
        /// <param name="resourceGroup">The resource group name.</param>
        /// <param name="azureClient"></param>
        /// <param name="log">Trace writer instance</param>
        /// <returns>Returns the list of vm ids which are in the load balancers.</returns>
        private static async Task<List<string>> GetVirtualMachinesFromLoadBalancers(string resourceGroup, string id, AzureClient azureClient, TraceWriter log)
        {
            log.Info($"timercrawlerforvirtualmachines getting the load balancer virtual machines");
            var virtualMachinesIds = new List<string>();
            var loadBalancer = await azureClient.AzureInstance.LoadBalancers.GetByIdAsync(id);
            if (loadBalancer == null)
            {
                return virtualMachinesIds;
            }

            virtualMachinesIds.AddRange(loadBalancer.Backends.SelectMany(x => x.Value.GetVirtualMachineIds()));
            return virtualMachinesIds;
        }

    }
}
