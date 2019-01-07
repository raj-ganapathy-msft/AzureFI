using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Models;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureChaos.Core.Helper
{
    public static class VirtualMachineHelper
    {
        /// <summary>Convert the Virtual machine to virtual machine crawler response entity.</summary>
        /// <param name="virtualMachine">The virtual machine.</param>
        /// <param name="partitionKey">The partition key for the virtaul machine entity.</param>
        /// <param name="vmGroup">Vm group name.</param>
        /// <returns></returns>
        public static VirtualMachineCrawlerResponse ConvertToVirtualMachineEntity(IVirtualMachine virtualMachine, string partitionKey, string vmGroup = "")
        {
            var virtualMachineCrawlerResponseEntity = new VirtualMachineCrawlerResponse(partitionKey,
                                                       virtualMachine.Id.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory))
            {
                RegionName = virtualMachine.RegionName,
                ResourceGroupName = virtualMachine.ResourceGroupName,
                ResourceName = virtualMachine.Name,
                AvailabilitySetId = virtualMachine.AvailabilitySetId,
                ResourceType = virtualMachine.Type,
                AvailabilityZone = virtualMachine.AvailabilityZones.Count > 0 ?
                    int.Parse(virtualMachine.AvailabilityZones.FirstOrDefault().Value) : 0,
                VirtualMachineGroup = string.IsNullOrWhiteSpace(vmGroup) ? VirtualMachineGroup.VirtualMachines.ToString() : vmGroup,
                State = virtualMachine.PowerState?.Value
            };

            if (virtualMachine.InstanceView?.PlatformUpdateDomain > 0)
            {
                virtualMachineCrawlerResponseEntity.UpdateDomain = virtualMachine.InstanceView.PlatformUpdateDomain;
            }
            if (virtualMachine.InstanceView?.PlatformFaultDomain > 0)
            {
                virtualMachineCrawlerResponseEntity.FaultDomain = virtualMachine.InstanceView.PlatformFaultDomain;
            }

            return virtualMachineCrawlerResponseEntity;
        }



        /// <summary>Convert the Virtual machine to virtual machine crawler response entity.</summary>
        /// <param name="virtualMachine">The virtual machine.</param>
        /// <param name="partitionKey">The partition key for the virtaul machine entity.</param>
        /// <param name="vmGroup">Vm group name.</param>
        /// <returns></returns>
        public static VirtualMachineCrawlerResponse ConvertToVirtualMachineEntityFromLB(IVirtualMachine virtualMachine, string partitionKey, string vmGroup = "")
        {
            var virtualMachineCrawlerResponseEntity = new VirtualMachineCrawlerResponse(partitionKey,
                                                       virtualMachine.Id.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory))
            {
                RegionName = virtualMachine.RegionName,
                ResourceGroupName = virtualMachine.ResourceGroupName,
                ResourceName = virtualMachine.Name,
                AvailabilitySetId = virtualMachine.AvailabilitySetId,
                ResourceType = virtualMachine.Type,
                AvailabilityZone = virtualMachine.AvailabilityZones.Count > 0 ?
                    int.Parse(virtualMachine.AvailabilityZones.FirstOrDefault().Value) : 0,
                VirtualMachineGroup = string.IsNullOrWhiteSpace(vmGroup) ? VirtualMachineGroup.VirtualMachines.ToString() : vmGroup,
                State = virtualMachine.PowerState?.Value
            };

            if (virtualMachine.InstanceView?.PlatformUpdateDomain > 0)
            {
                virtualMachineCrawlerResponseEntity.UpdateDomain = virtualMachine.InstanceView.PlatformUpdateDomain;
            }
            if (virtualMachine.InstanceView?.PlatformFaultDomain > 0)
            {
                virtualMachineCrawlerResponseEntity.FaultDomain = virtualMachine.InstanceView.PlatformFaultDomain;
            }

            return virtualMachineCrawlerResponseEntity;
        }


        /// <summary>Convert the Virtual machine to virtual machine crawler response entity.</summary>
        /// <param name="scaleSetVirtualMachines">The virtual machine.</param>
        /// <param name="resourceGroup">The resource group name.</param>
        /// <param name="virtualMachineScaleSetId">Scale set name of the vm.</param>
        /// <param name="partitionKey">The partition key for the virtaul machine entity.</param>
        /// <param name="availabilityZone">The availability zone value for the virtual machine scale set vm instance</param>
        /// <param name="vmGroup">Virtual machine group name.</param>
        /// <returns></returns>
        public static VirtualMachineCrawlerResponse ConvertToVirtualMachineEntity(IVirtualMachineScaleSetVM scaleSetVirtualMachines, string resourceGroup,
                                                                                 string virtualMachineScaleSetId, string partitionKey, int? availabilityZone,
                                                                                  string vmGroup = "")
        {
            var virtualMachineCrawlerResponseEntity = new VirtualMachineCrawlerResponse(partitionKey, scaleSetVirtualMachines.Id.Replace(Delimeters.ForwardSlash, Delimeters.Exclamatory))
            {
                RegionName = scaleSetVirtualMachines.RegionName,
                ResourceGroupName = resourceGroup,
                ResourceName = scaleSetVirtualMachines.Name,
                ResourceType = scaleSetVirtualMachines.Type,
                VirtualMachineScaleSetId = virtualMachineScaleSetId,
                AvailabilityZone = availabilityZone != 0 ? availabilityZone : 0,
                VirtualMachineGroup = string.IsNullOrWhiteSpace(vmGroup) ? VirtualMachineGroup.VirtualMachines.ToString() : vmGroup,
                State = scaleSetVirtualMachines.PowerState?.Value
            };

            return virtualMachineCrawlerResponseEntity;
        }

        /// <summary>Create the table batch operation for the scheduled entity for the set of virtual machines.</summary>
        /// <param name="filteredVmSet">Set of virtual machines.</param>
        /// <param name="schedulerFrequency">Schedule frequency, it will be reading from the config</param>
        /// <param name="virtualMachineGroup"></param>
        /// <returns></returns>
        public static TableBatchOperation CreateScheduleEntity(IList<VirtualMachineCrawlerResponse> filteredVmSet,
            int schedulerFrequency, List<string> azureFiOperationList, VirtualMachineGroup virtualMachineGroup)
        {
            TableBatchOperation tableBatchOperation = new TableBatchOperation();
            Random random = new Random();
            DateTime randomExecutionDateTime = DateTime.UtcNow.AddMinutes(random.Next(1, schedulerFrequency));
            var sessionId = Guid.NewGuid().ToString();
            foreach (var item in filteredVmSet)
            {
                if (item == null)
                {
                    continue;
                }

                string fiOperation = string.Empty;
                var actionType = GetActionType(item.RowKey, azureFiOperationList, item.State, out fiOperation);
                var entityEntry = RuleEngineHelper.ConvertToScheduledRuleEntity(item, sessionId, actionType,
                    fiOperation, randomExecutionDateTime, virtualMachineGroup);
                if (entityEntry != null)
                {
                    tableBatchOperation.InsertOrMerge(entityEntry);
                }
            }

            return tableBatchOperation;
        }

        public static TableBatchOperation CreateScheduleEntityForAvailabilityZone(IList<VirtualMachineCrawlerResponse> filteredVmSet,
            int schedulerFrequency, List<string> azureFiOperationList)
        {
            var tableBatchOperation = new TableBatchOperation();
            var random = new Random();
            var randomExecutionDateTime = DateTime.UtcNow.AddMinutes(random.Next(1, schedulerFrequency));
            var sessionId = Guid.NewGuid().ToString();
            foreach (var item in filteredVmSet)
            {
                if (item == null)
                {
                    continue;
                }

                string fiOperation = string.Empty;
                var actionType = GetActionType(item.RowKey, azureFiOperationList, item.State, out fiOperation);

                tableBatchOperation.InsertOrMerge(RuleEngineHelper.ConvertToScheduledRuleEntityForAvailabilityZone(item,
                    sessionId, actionType, fiOperation, randomExecutionDateTime));
            }

            return tableBatchOperation;
        }

        public static TableBatchOperation CreateScheduleEntityForAvailabilitySet(IList<VirtualMachineCrawlerResponse> filteredVmSet,
            int schedulerFrequency, List<string> azureFiOperationList, bool domainFlage)
        {
            var tableBatchOperation = new TableBatchOperation();
            var random = new Random();
            var randomExecutionDateTime = DateTime.UtcNow.AddMinutes(random.Next(1, schedulerFrequency));
            var sessionId = Guid.NewGuid().ToString();
            foreach (var item in filteredVmSet)
            {
                if (item == null)
                {
                    continue;
                }

                string fiOperation = string.Empty;
                var actionType = GetActionType(item.RowKey, azureFiOperationList, item.State, out fiOperation);

                tableBatchOperation.InsertOrMerge(RuleEngineHelper.ConvertToScheduledRuleEntityForAvailabilitySet(item,
                    sessionId, actionType, fiOperation, randomExecutionDateTime, domainFlage));
            }

            return tableBatchOperation;
        }

        public static string GetAzureFiOperation(List<string> azureFaultInjectionActions)
        {
            if (azureFaultInjectionActions == null || !azureFaultInjectionActions.Any())
            {
                return string.Empty;
            }

            Random random = new Random();
            int index = random.Next(0, azureFaultInjectionActions.Count );
            return azureFaultInjectionActions[index];
        }

        public static ActionType GetActionTobePerformed(string state, string selectedOpeartion)
        {
            if (!Enum.TryParse(selectedOpeartion, out AzureFiOperation azureFiOperation))
            {
                return ActionType.Unknown;
            }

            switch (azureFiOperation)
            {
                case AzureFiOperation.PowerCycle:
                    return GetAction(state);
                case AzureFiOperation.Restart:
                    return ActionType.Restart;
            }

            return ActionType.Unknown;
        }


        /// <summary>Get the action based on the current state of the virtual machine.</summary>
        /// <param name="state">Current state of the virtual machine.</param>
        /// <returns></returns>
        public static ActionType GetAction(string state)
        {
            var powerState = PowerState.Parse(state);
            if (powerState == PowerState.Running || powerState == PowerState.Starting)
            {
                return ActionType.PowerOff;
            }

            if (powerState == PowerState.Stopping || powerState == PowerState.Stopped)
            {
                return ActionType.Start;
            }

            return ActionType.Unknown;
        }

        /// <summary>Get the list of the load balancer virtual machines by resource group.</summary>
        /// <param name="resourceGroup">The resource group name.</param>
        /// <param name="azureClient"></param>
        /// <returns>Returns the list of vm ids which are in the load balancers.</returns>
        public static async Task<List<string>> GetVirtualMachinesFromLoadBalancers(string resourceGroup, AzureClient azureClient)
        {
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

        private static ActionType GetActionType(string rowKey, List<string> azureFiOperationList, string state, out string fiOperation)
        {
            var items = ResourceFilterHelper.QueryByRowKey<ScheduledRules>(rowKey, StorageTableNames.ScheduledRulesTableName);
            if (items != null || items.Any())
            {
                /*if (state == "PowerState/stopped")
                {
                    var includedActionList = azureFiOperationList.Where(x => x.Equals("PowerCycle", StringComparison.OrdinalIgnoreCase)).ToList();
                    //What if the azureOperationList is only restart?The ActionType.Unknown will be selected.
                    azureFiOperationList = includedActionList == null || !includedActionList.Any() ? azureFiOperationList : includedActionList;
                }
                else
                {
                */
                    var latestItem = items.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                    if (latestItem != null)
                    {
                        var excludeActionType = latestItem.CurrentAction;
                        var excludedActionList = azureFiOperationList.Where(x => !x.Equals(excludeActionType, StringComparison.OrdinalIgnoreCase)).ToList();
                        azureFiOperationList = excludedActionList == null || !excludedActionList.Any() ? azureFiOperationList : excludedActionList;
                        /*if (latestItem.CurrentAction == "PowerOff")
                        {
                            azureFiOperationList = azureFiOperationList.Where(x => x.Equals(latestItem.FiOperation, StringComparison.OrdinalIgnoreCase)).ToList();
                        }
                        else
                        {
                            var excludedActionList = azureFiOperationList.Where(x => !x.Equals(excludeActionType, StringComparison.OrdinalIgnoreCase)).ToList();
                            azureFiOperationList = excludedActionList == null || !excludedActionList.Any() ? azureFiOperationList : excludedActionList;
                        }*/
                    }
               // }
                
            }

            fiOperation = GetAzureFiOperation(azureFiOperationList);
            if (string.IsNullOrWhiteSpace(fiOperation))
            {
                return ActionType.Unknown;
            }

            return GetActionTobePerformed(state, fiOperation);
        }
    }
}