using AzureChaos.Core.Models.Configs;
using AzureChaos.Core.Providers;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using AzureChaos.Core.Entity;

namespace AzureChaos.Core.Helper
{
    public class ResourceFilterHelper
    {
        private static readonly Random Random = new Random();

        // TODO - this is not thread safe will modify the code.
        // just shuffle method to shuffle the list  of items to get the random  items
        public static void Shuffle<T>(IList<T> list)
        {
            if (list == null || !list.Any())
            {
                return;
            }

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static List<T> QueryCrawlerResponseByMeanTime<T>(AzureSettings azureSettings,
            string tableName,
            string filter = "") where T : CrawlerResponse, new()
        {
            var tableQuery = new TableQuery<T>();
            var dateFilter = TableQuery.CombineFilters(TableQuery.GenerateFilterConditionForDate("Timestamp",
                    QueryComparisons.LessThanOrEqual,
                    DateTimeOffset.UtcNow),
                TableOperators.And,
                TableQuery.GenerateFilterConditionForDate("Timestamp",
                    QueryComparisons.GreaterThanOrEqual,
                    DateTimeOffset.UtcNow.AddMinutes(-(azureSettings.Chaos.MeanTime + azureSettings.Chaos.SchedulerFrequency))));
            var combineFilter = !string.IsNullOrWhiteSpace(filter)
                ? TableQuery.CombineFilters(dateFilter,
                    TableOperators.And,
                    filter)
                : dateFilter;
            tableQuery = tableQuery.Where(combineFilter);
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet.ToList();
        }

        public static List<ScheduledRules> QuerySchedulesByMeanTime<T>(AzureSettings azureSettings,
            string tableName,
            string filter = "")
        {
            var tableQuery = new TableQuery<ScheduledRules>();
            var dateFilter = TableQuery.CombineFilters(TableQuery.GenerateFilterConditionForDate("ScheduledExecutionTime",
                    QueryComparisons.LessThanOrEqual,
                    DateTimeOffset.UtcNow),
                TableOperators.And,
                TableQuery.GenerateFilterConditionForDate("ScheduledExecutionTime",
                    QueryComparisons.GreaterThanOrEqual,
                    DateTimeOffset.UtcNow.AddMinutes(-azureSettings.Chaos.MeanTime)));
            var combineFilter = !string.IsNullOrWhiteSpace(filter)
                ? TableQuery.CombineFilters(dateFilter,
                    TableOperators.And,
                    filter)
                : dateFilter;
            tableQuery = tableQuery.Where(combineFilter);
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet.ToList();
        }

        public static List<T> QueryByPartitionKey<T>(string partitionKey, string tableName) where T : ITableEntity, new()
        {
            var tableQuery = new TableQuery<T>();
            tableQuery = tableQuery.Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet.ToList();
        }

        public static List<T> QueryByRowKey<T>(string rowKey, string tableName) where T : ITableEntity, new()
        {
            var tableQuery = new TableQuery<T>();
            tableQuery = tableQuery.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey));
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet.ToList();
        }

        public static List<T> QueryByPartitionKeyAndRowKey<T>(string partitionKey, string rowKey, string tableName) where T : ITableEntity, new()
        {
            var tableQuery = new TableQuery<T>();
            var dateFilter = TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey));
            tableQuery = tableQuery.Where(dateFilter);
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet.ToList();
        }

        public static List<T> QueryByFromToDate<T>(DateTimeOffset fromDate,
            DateTimeOffset toDate,
            string propertyName,
            string tableName)
            where T : ITableEntity, new()
        {
            var tableQuery = new TableQuery<T>();
            var dateFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterConditionForDate(propertyName, QueryComparisons.GreaterThanOrEqual,
                    fromDate),
                TableOperators.And,
                TableQuery.GenerateFilterConditionForDate(propertyName, QueryComparisons.LessThanOrEqual,
                    toDate));
            tableQuery = tableQuery.Where(dateFilter);
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet?.ToList();
        }
        public static List<T> QueryByFromToDateForActivities<T>(DateTimeOffset fromDate,
            DateTimeOffset toDate,
            string propertyName,
            string tableName)
            where T : ITableEntity, new()
        {
            var tableQuery = new TableQuery<T>();
            var dateFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterConditionForDate(propertyName, QueryComparisons.GreaterThanOrEqual,
                    fromDate),
                TableOperators.And,
                TableQuery.GenerateFilterConditionForDate(propertyName, QueryComparisons.LessThanOrEqual,
                    toDate));
            tableQuery = tableQuery.Where(dateFilter);
            var resultsSet = StorageAccountProvider.GetEntities(tableQuery, tableName);
            return resultsSet?.ToList();
        }
    }
}