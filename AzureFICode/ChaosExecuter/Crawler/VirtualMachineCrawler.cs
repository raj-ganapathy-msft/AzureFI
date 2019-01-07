using AzureChaos.Core.Constants;
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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ChaosExecuter.Crawler
{
    public static class VirtualMachineCrawler
    {
        [FunctionName("VirtualMachineCrawler")]
        public static async Task Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");
            try
            {
                var resourceGroupList = ResourceGroupHelper.GetResourceGroupsInSubscription();
                if (resourceGroupList == null)
                {
                    log.Info($"timercrawlerforvirtualmachines: no resource groups to crawl");
                    return;
                }

                await GetVirtualMachinesByResourceGroups(resourceGroupList, log);
            }
            catch (Exception ex)
            {
                log.Error($"timercrawlerforvirtualmachines threw the exception ", ex, "timercrawlerforvirtualmachines");
            }

            log.Info($"timercrawlerforvirtualmachines end  at: {DateTime.UtcNow}");
        }

        /// <summary>1. Iterate the resource groups to get the virtual machines for individual resource group.
        /// 2. Convert the List of virtual machines into entities and add them into tasklist, repeat this process for the VM's under the resource group.
        /// 3. Execute all the task parallely</summary>
        /// <param name="resourceGroups">List of resource groups for the particular subscription.</param>
        /// <param name="log">Trace writer instance</param>
        private static async Task GetVirtualMachinesByResourceGroups(IEnumerable<IResourceGroup> resourceGroups,
                                                                TraceWriter log)
        {
            try
            {
                var virtualMachineCloudTable = StorageAccountProvider.CreateOrGetTable(StorageTableNames.VirtualMachineCrawlerTableName);
                var batchTasks = new ConcurrentBag<Task>();

                // using parallel here to run all the resource groups parallelly, parallel is 10times faster than normal foreach.
                Parallel.ForEach(resourceGroups, async eachResourceGroup =>
                {
                    log.Info($"timercrawlerforvirtualmachines: crawling virtual machines for  rg: {eachResourceGroup.Name}");
                    var virtualMachinesByResourceGroup = await GetVirtualMachinesByResourceGroup(eachResourceGroup, log);
                    if (virtualMachinesByResourceGroup == null) return;
                    var virtualMachineList = virtualMachinesByResourceGroup.ToList();

                    // table batch operation currently allows only 100 per batch, So ensuring the one batch operation will have only 100 items
                    for (var i = 0; i < virtualMachineList.Count(); i += TableConstants.TableServiceBatchMaximumOperations)
                    {
                        var batchItems = virtualMachineList.Skip(i)
                            .Take(TableConstants.TableServiceBatchMaximumOperations).ToList();
                        var batchOperation = InsertOrReplaceEntitiesIntoTable(batchItems,
                                                                              eachResourceGroup.Name, log);
                        if (batchOperation != null)
                        {
                            batchTasks.Add(virtualMachineCloudTable.ExecuteBatchAsync(batchOperation));
                        }
                    }
                });

                await Task.WhenAll(batchTasks);
            }
            catch (Exception e)
            {
                log.Error($"timercrawlerforvirtualmachines:threw exception", e, "GetVirtualMachinesForResourceGroups");
            }
        }

        /// <summary>1. Get the List of virtual machines for the resource group.
        /// 2. Get all the virtual machines from the load balancers.</summary>
        /// <param name="resourceGroup">From which resource group needs to get the virtual machines.</param>
        /// <param name="log">Trace writer instance</param>
        /// <returns>List of virtual machines which excludes the load balancer virtual machines and availability set virtual machines.</returns>
        private static async Task<IEnumerable<IVirtualMachine>> GetVirtualMachinesByResourceGroup(IResourceGroup resourceGroup, TraceWriter log)
        {
            var azureClient = new AzureClient();
            var loadBalancersVirtualMachines = GetVirtualMachinesFromLoadBalancers(resourceGroup.Name, azureClient, log);
            var pagedCollection = azureClient.AzureInstance.VirtualMachines.ListByResourceGroupAsync(resourceGroup.Name);
            var tasks = new List<Task>
            {
                loadBalancersVirtualMachines,
                pagedCollection
            };

            await Task.WhenAll(tasks);
            if (pagedCollection.Result == null || !pagedCollection.Result.Any())
            {
                log.Info($"timercrawlerforvirtualmachines: no virtual machines for the resource group: {resourceGroup.Name}");
                return null;
            }

            var loadBalancerIds = loadBalancersVirtualMachines.Result;
            var virtuallMachines = pagedCollection.Result;
            return virtuallMachines?.Select(x => x).Where(x => string.IsNullOrWhiteSpace(x.AvailabilitySetId) &&
                                                               !loadBalancerIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>1. Convert the list of virtual machines into Virtual machine crawler entities
        /// 2. Add the entity into the table using the batch operation task</summary>
        /// <param name="virtualMachines">List of virtual machines, which needs to push into table</param>
        /// <param name="resourceGroupName"></param>
        /// <param name="log">Trace writer instance</param>
        /// <returns>Returns the list of the insert batch operation as task.</returns>
        private static TableBatchOperation InsertOrReplaceEntitiesIntoTable(List<IVirtualMachine> virtualMachines,
                                                                            string resourceGroupName, TraceWriter log)
        {
            if (virtualMachines.Count == 0)
            {
                return null;
            }

            var batchOperation = new TableBatchOperation();
            foreach (var eachVirtualMachine in virtualMachines)
            {
                try
                {
                    var partitionKey = resourceGroupName;
                    batchOperation.InsertOrReplace(VirtualMachineHelper.ConvertToVirtualMachineEntity(eachVirtualMachine, partitionKey));
                }
                catch (Exception e)
                {
                    log.Error($"timercrawlerforvirtualmachines threw the exception ", e, "InsertEntitiesIntoTable");
                }
            }

            return batchOperation;
        }

        /// <summary>Get the list of the load balancer virtual machines by resource group.</summary>
        /// <param name="resourceGroup">The resource group name.</param>
        /// <param name="azureClient"></param>
        /// <param name="log">Trace writer instance</param>
        /// <returns>Returns the list of vm ids which are in the load balancers.</returns>
        private static async Task<List<string>> GetVirtualMachinesFromLoadBalancers(string resourceGroup, AzureClient azureClient, TraceWriter log)
        {
            log.Info($"timercrawlerforvirtualmachines getting the load balancer virtual machines");
            var virtualMachinesIds = new List<string>();
            var pagedCollection = await azureClient.AzureInstance.LoadBalancers.ListByResourceGroupAsync(resourceGroup);
            if (pagedCollection == null)
            {
                return virtualMachinesIds;
            }

            var loadBalancers = pagedCollection.Select(x => x).ToList();
            if (!loadBalancers.Any())
            {
                return virtualMachinesIds;
            }

            virtualMachinesIds.AddRange(loadBalancers.SelectMany(x => x.Backends).SelectMany(x => x.Value.GetVirtualMachineIds()));
            return virtualMachinesIds;
        }
    }
}