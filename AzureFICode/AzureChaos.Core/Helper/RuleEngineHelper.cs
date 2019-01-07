using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Models;
using AzureChaos.Core.Models.Configs;
using AzureChaos.Core.Providers;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AzureChaos.Core.Helper
{
    public class RuleEngineHelper
    {
        public static ScheduledRules ConvertToScheduledRuleEntity<T>(T entity, string sessionId,
            ActionType action, string fiOperation, DateTime executionTime, VirtualMachineGroup virtualMachineGroup) where T : CrawlerResponse
        {
            if (entity == null || !Mappings.FunctionNameMap.ContainsKey(virtualMachineGroup.ToString()))
            {
                return null;
            }
            var localGUID = sessionId;// System.Guid.NewGuid().ToString();
            var scheduleRule = new ScheduledRules(localGUID, entity.RowKey)
            {
                ResourceType = virtualMachineGroup.ToString(),
                ScheduledExecutionTime = executionTime,
                ResourceName = entity.ResourceName,
                CurrentAction = action.ToString(),
                FiOperation = fiOperation,
                TriggerData = GetTriggerData(entity, action, localGUID, entity.RowKey, virtualMachineGroup.ToString()),
                SchedulerSessionId = sessionId
            };

            if (fiOperation.Equals(AzureFiOperation.PowerCycle.ToString()))
            {
                scheduleRule.Rolledback = false;
            }

            return scheduleRule;
        }

        public static ScheduledRules ConvertToScheduledRuleEntityForAvailabilitySet<T>(T entity, string sessionId,
            ActionType action, string fiOperation, DateTime executionTime, bool domainFlage) where T : VirtualMachineCrawlerResponse
        {
            if (entity == null || !Mappings.FunctionNameMap.ContainsKey(VirtualMachineGroup.AvailabilitySets.ToString()))
            {
                return null;
            }
            string combinationKey;
            if (domainFlage)
            {
                combinationKey = entity.AvailabilitySetId + Delimeters.Exclamatory + entity.FaultDomain?.ToString();
            }
            else
            {
                combinationKey = entity.AvailabilitySetId + Delimeters.At + entity.UpdateDomain?.ToString();
            }
            var localGUID = sessionId;// System.Guid.NewGuid().ToString();
            var scheduleRule = new ScheduledRules(localGUID, entity.RowKey)
            //return new ScheduledRules(localGUID, entity.RowKey)
            {
                ResourceType = VirtualMachineGroup.AvailabilitySets.ToString(),
                ScheduledExecutionTime = executionTime,
                FiOperation = fiOperation,
                ResourceName = entity.ResourceName,
                CurrentAction = action.ToString(),
                TriggerData = GetTriggerData(entity, action, localGUID, entity.RowKey, VirtualMachineGroup.AvailabilitySets.ToString()),
                SchedulerSessionId = sessionId,
                CombinationKey = combinationKey,
                //Rolledback = false
            };
            if (fiOperation.Equals(AzureFiOperation.PowerCycle.ToString()))
            {
                scheduleRule.Rolledback = false;
            }

            return scheduleRule;
        }

        public static ScheduledRules ConvertToScheduledRuleEntityForAvailabilityZone<T>(T entity, string sessionId,
            ActionType action, string fiOperation, DateTime executionTime) where T : VirtualMachineCrawlerResponse
        {
            if (!Mappings.FunctionNameMap.ContainsKey(VirtualMachineGroup.AvailabilityZones.ToString()))
            {
                return null;
            }

            var localGUID = sessionId;// System.Guid.NewGuid().ToString();
            var scheduleRule = new ScheduledRules(localGUID, entity.RowKey)
            //return new ScheduledRules(localGUID, entity.RowKey)
            {
                ResourceType = VirtualMachineGroup.AvailabilityZones.ToString(),
                ScheduledExecutionTime = executionTime,
                FiOperation = fiOperation,
                ResourceName = entity.ResourceName,
                CurrentAction = action.ToString(),
                TriggerData = GetTriggerData(entity, action, localGUID, entity.RowKey, VirtualMachineGroup.AvailabilityZones.ToString()),
                SchedulerSessionId = sessionId,
                CombinationKey = entity.RegionName + Delimeters.Exclamatory.ToString() + entity.AvailabilityZone,
                //Rolledback = false
            };
            if (fiOperation.Equals(AzureFiOperation.PowerCycle.ToString()))
            {
                scheduleRule.Rolledback = false;
            }

            return scheduleRule;
        }

        public static string GetTriggerData(CrawlerResponse crawlerResponse, ActionType action, string partitionKey, string rowKey, string guid)
        {
            InputObject triggerdata = new InputObject
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                ResourceType = guid,
                Action = action.ToString(),
                ResourceId = crawlerResponse.ResourceName,
                ResourceGroup = crawlerResponse.ResourceGroupName,
                VirtualMachineScaleSetId = crawlerResponse.PartitionKey.Replace(Delimeters.Exclamatory, Delimeters.ForwardSlash)
            };
            return JsonConvert.SerializeObject(triggerdata);
        }

        public static List<VirtualMachineGroup> GetEnabledChaosSet(AzureSettings azureSettings)
        {
            var enabledChaos = Mappings.GetEnabledChaos(azureSettings);

            var selectionQuery = TableQuery.GenerateFilterConditionForDate("ScheduledExecutionTime", QueryComparisons.GreaterThanOrEqual,
                DateTimeOffset.UtcNow.AddMinutes(-azureSettings.Chaos.MeanTime));
            var scheduledQuery = new TableQuery<ScheduledRules>().Where(selectionQuery);
            var executedResults = StorageAccountProvider.GetEntities(scheduledQuery, StorageTableNames.ScheduledRulesTableName);
            if (executedResults == null)
            {
                var chaos = enabledChaos.Where(x => x.Value);
                return chaos.Select(x => x.Key).ToList();
            }

            var scheduledRuleses = executedResults.ToList();
            var executedChaos = scheduledRuleses.Select(x => x.PartitionKey).Distinct().ToList();
            var excludedChaos = enabledChaos.Where(x => x.Value && !executedChaos.Contains(x.Key.ToString(), StringComparer.OrdinalIgnoreCase));
            return excludedChaos.Select(x => x.Key).ToList();
        }
    }
}