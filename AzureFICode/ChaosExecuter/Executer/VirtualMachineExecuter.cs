using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Models;
using AzureChaos.Core.Providers;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;

namespace ChaosExecuter.Executer
{
    /// <summary>Virtual Machine chaos executer<see>
    ///         <cref>VirtualMachineExecuter.cs</cref>
    ///     </see>
    /// </summary>
    public static class VirtualMachineExecuter
    {
        private const string FunctionName = "virtualmachinesexecuter";

        /// <summary>Chaos executer on the Virtual Machines.</summary>
        /// <param name="context"></param>
        /// <param name="log">The trace writer.</param>
        /// <returns>Returns the http response message.</returns>
        [FunctionName("virtualmachinesexecuter")]
        public static bool Run([OrchestrationTrigger] DurableOrchestrationContext context, TraceWriter log)
        {
            var inputData = context.GetInput<string>();
            if (!ValidateInput(inputData, log, out var inputObject))
            {
                return false;
            }

            var azureClient = new AzureClient();

            var scheduleRule = new ScheduledRules(inputObject.PartitionKey, inputObject.RowKey);
            if(inputObject.EnableRollback)
            {
                scheduleRule.RollbackExecutionStatus = Status.Started.ToString();
            }
            else
            {
                scheduleRule.ExecutionStatus = Status.Started.ToString();
            }

            if (!azureClient.IsChaosEnabledByGroup(inputObject.ResourceType))
            {
                if (inputObject.EnableRollback)
                {
                    scheduleRule.RollbackWarning = Warnings.ChaosDisabledAfterRules;
                }
                else
                {
                    scheduleRule.Warning = Warnings.ChaosDisabledAfterRules;
                }

                StorageAccountProvider.InsertOrMerge(scheduleRule, StorageTableNames.ScheduledRulesTableName);
                return false;
            }

            try
            {
                IVirtualMachine virtualMachine = GetVirtualMachine(azureClient.AzureInstance, inputObject);
                if (virtualMachine == null)
                {
                    log.Info($"VM Chaos : No resource found for the resource name : " + inputObject.ResourceId);
                    return false;
                }

                log.Info($"VM Chaos received the action: " + inputObject.Action + " for the virtual machine: " + inputObject.ResourceId);

                if (!Enum.TryParse(virtualMachine.ProvisioningState, out ProvisioningState provisioningState) || provisioningState != ProvisioningState.Succeeded)
                {
                    log.Info($"VM Chaos :  The vm '" + inputObject.ResourceId + "' is in the state of " + virtualMachine.ProvisioningState + ", so cannont perform the same action " + inputObject.Action);
                    if (inputObject.EnableRollback)
                    {
                        scheduleRule.RollbackExecutionStatus = Status.Failed.ToString();
                        scheduleRule.RollbackWarning = string.Format(Warnings.ProvisionStateCheck, provisioningState);
                    }
                    else
                    {
                        scheduleRule.ExecutionStatus = Status.Failed.ToString();
                        scheduleRule.Warning = string.Format(Warnings.ProvisionStateCheck, provisioningState);
                    }

                    StorageAccountProvider.InsertOrMerge(scheduleRule, StorageTableNames.ScheduledRulesTableName);
                    return false;
                }

                SetInitialEventActivity(virtualMachine, scheduleRule,inputObject.EnableRollback);

                // if its not valid chaos then update the event table with  warning message and return false
                bool isValidChaos = IsValidChaos(inputObject.Action, virtualMachine.PowerState);
                if (!isValidChaos)
                {
                    log.Info($"VM Chaos- Invalid action: " + inputObject.Action);
                    if (inputObject.EnableRollback)
                    {
                        scheduleRule.RollbackExecutionStatus = Status.Failed.ToString();
                        if (inputObject.Action == "Restart")
                        {
                            scheduleRule.RollbackWarning = Warnings.RestartOnStop;
                        }
                        else
                        {
                            scheduleRule.RollbackWarning = Warnings.ActionAndStateAreSame;
                        }
                    }
                    else
                    {
                        scheduleRule.ExecutionStatus = Status.Failed.ToString();
                        if (inputObject.Action == "Restart")
                        {
                            scheduleRule.RollbackWarning = Warnings.RestartOnStop;
                        }
                        else
                        {
                            scheduleRule.Warning = Warnings.ActionAndStateAreSame;
                        }
                    }

                    StorageAccountProvider.InsertOrMerge(scheduleRule, StorageTableNames.ScheduledRulesTableName);
                    return false;
                }

                StorageAccountProvider.InsertOrMerge(scheduleRule, StorageTableNames.ScheduledRulesTableName);
                PerformChaosOnVirtualMachine(inputObject.Action, virtualMachine, scheduleRule, inputObject.EnableRollback);
                // Can we break from here to check the status later ?
                virtualMachine = GetVirtualMachine(azureClient.AzureInstance, inputObject);
                if (virtualMachine != null)
                {
                    
                    if (inputObject.EnableRollback)
                    {
                        scheduleRule.Rolledback = true;
                        scheduleRule.RollbackEventCompletedTime = DateTime.UtcNow;
                        scheduleRule.RollbackFinalState = virtualMachine.PowerState.Value;
                        scheduleRule.RollbackExecutionStatus = Status.Completed.ToString();
                    }
                    else
                    {
                        scheduleRule.EventCompletedTime = DateTime.UtcNow;
                        scheduleRule.FinalState = virtualMachine.PowerState.Value;
                        scheduleRule.ExecutionStatus = Status.Completed.ToString();
                        if (scheduleRule.FiOperation == "Restart")
                        {
                            scheduleRule.Rolledback = null;
                        }
                    }
                }

                StorageAccountProvider.InsertOrMerge(scheduleRule, StorageTableNames.ScheduledRulesTableName);
                log.Info($"VM Chaos Completed");
                return true;
            }
            catch (Exception ex)
            {
                if (inputObject.EnableRollback)
                {
                    scheduleRule.RollbackError = ex.Message;
                    scheduleRule.RollbackExecutionStatus = Status.Failed.ToString();
                }
                else
                {
                    scheduleRule.Error = ex.Message;
                    scheduleRule.ExecutionStatus = Status.Failed.ToString();
                }

                StorageAccountProvider.InsertOrMerge(scheduleRule, StorageTableNames.ScheduledRulesTableName);

                // dont throw the error here just handle the error and return the false
                log.Error($"VM Chaos trigger function threw the exception ", ex, FunctionName);
                log.Info($"VM Chaos Completed with error");
            }

            return false;
        }

        /// <summary>Validate the request input on this functions, and log the invalid.</summary>
        /// <param name="inputData"></param>
        /// <param name="log"></param>
        /// <param name="inputObject"></param>
        /// <returns></returns>
        private static bool ValidateInput(string inputData, TraceWriter log, out InputObject inputObject)
        {
            try
            {
                inputObject = JsonConvert.DeserializeObject<InputObject>(inputData);
                if (inputObject == null)
                {
                    log.Error("input data is empty");
                    return false;
                }
                if (!Enum.TryParse(inputObject.Action.ToString(), out ActionType _))
                {
                    log.Error("Virtual Machine action is not valid action");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(inputObject.ResourceId))
                {
                    log.Error("Virtual Machine Resource name is not valid name");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(inputObject.ResourceGroup))
                {
                    log.Error("Virtual Machine Resource Group is not valid resource group");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("Threw exception on the validate input method", ex, FunctionName + ": ValidateInput");
                inputObject = null;
                return false;
            }

            return true;
        }

        /// <summary>Perform the Chaos Operation</summary>
        /// <param name="actionType">Action type</param>
        /// <param name="virtualMachine">Virtual Machine</param>
        /// <param name="scheduledRules">Event activity entity</param>
        /// <returns></returns>
        private static void PerformChaosOnVirtualMachine(string action, IVirtualMachine virtualMachine, ScheduledRules scheduledRules, bool enableRollback)
        {
            ActionType actionType;
            if (!Enum.TryParse(action, out actionType))
            {
                return;
            }

            switch (actionType)
            {
                case ActionType.Start:
                    virtualMachine.StartAsync();
                    break;

                case ActionType.PowerOff:
                case ActionType.Stop:
                    virtualMachine.PowerOffAsync();
                    break;

                case ActionType.Restart:
                    virtualMachine.RestartAsync();
                    break;
            }

            if (enableRollback)
            {
                scheduledRules.RollbackExecutionStatus = Status.Executing.ToString();
            }
            else
            {
                scheduledRules.ExecutionStatus = Status.Executing.ToString();
            }
        }

        /// <summary>Check the given action is valid chaos to perform on the vm</summary>
        /// <param name="currentAction">Current request action</param>
        /// <param name="state">Current Vm state.</param>
        /// <returns></returns>
        private static bool IsValidChaos(string action, PowerState state)
        {
            ActionType currentAction;
            if (!Enum.TryParse(action, out currentAction))
            {
                return false;
            }

            if (currentAction == ActionType.Start)
            {
                return state != PowerState.Running && state != PowerState.Starting;
            }

            if (currentAction == ActionType.Stop || currentAction == ActionType.PowerOff || currentAction == ActionType.Restart) // Restart on stop is a valid operation
            {
                return state != PowerState.Stopping && state != PowerState.Stopped && state != PowerState.Deallocated;
            }

            return false;
        }

        /// <summary>Set the initial property of the activity entity</summary>
        /// <param name="virtualMachine">The vm</param>
        /// <param name="scheduledRules">Event activity entity.</param>
        /// /// <param name="enableRollback">Event activity entity.</param>
        private static void SetInitialEventActivity(IVirtualMachine virtualMachine, ScheduledRules scheduledRules, bool enableRollback)
        {
            if (enableRollback)
            {
                scheduledRules.RollbackInitialState = virtualMachine.PowerState.Value;
                scheduledRules.RollbackExecutionStartTime = DateTime.UtcNow;
            }
            else
            {
                scheduledRules.InitialState = virtualMachine.PowerState.Value;
                scheduledRules.ExecutionStartTime = DateTime.UtcNow;
            }
        }

        /// <summary>Get the virtual machine.</summary>
        /// <param name="azure"></param>
        /// <param name="inputObject"></param>
        /// <returns>Returns the virtual machine.</returns>
        private static IVirtualMachine GetVirtualMachine(IAzure azure, InputObject inputObject)
        {
            var id = inputObject.RowKey.Replace(Delimeters.Exclamatory, Delimeters.ForwardSlash);
            return azure.VirtualMachines.GetById(id);
        }
    }
}