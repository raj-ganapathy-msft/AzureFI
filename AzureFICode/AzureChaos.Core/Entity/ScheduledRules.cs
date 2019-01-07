using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureChaos.Core.Entity
{
    public class ScheduledRules : TableEntity
    {
        public ScheduledRules()
        { }

        public ScheduledRules(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }
        
        // Keeping the resource name for reporting purpose
        public string ResourceName { get; set; }

        public string ResourceType { get; set; }
        public string TriggerData { get; set; }

        public string SchedulerSessionId { get; set; }

        public bool? Rolledback { get; set; }

        public DateTime? ScheduledExecutionTime { get; set; }

        public string ExecutionStatus { get; set; }
        public string RollbackExecutionStatus { get; set; }

        public string FiOperation { get; set; }

        public string CurrentAction { get; set; }

        public string CombinationKey { get; set; }

        public DateTime? ExecutionStartTime { get; set; }
        public DateTime? RollbackExecutionStartTime { get; set; }

        /// <summary>Event completed date time.</summary>
        public DateTime? EventCompletedTime { get; set; }
        public DateTime? RollbackEventCompletedTime { get; set; }

        /// <summary>Initial State of the resource</summary>
        public string InitialState { get; set; }
        public string RollbackInitialState { get; set; }
        /// <summary>Final State of the resource</summary>
        public string FinalState { get; set; }
        public string RollbackFinalState { get; set; }
        /// <summary>Error message if anything occured on the time of execution.</summary>
        public string Warning { get; set; }

        public string RollbackWarning { get; set; }
        /// <summary>Error message if anything occured on the time of execution.</summary>
        public string Error { get; set; }
        public string RollbackError { get; set; }
    }
}