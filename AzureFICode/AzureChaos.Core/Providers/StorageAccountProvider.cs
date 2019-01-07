using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace AzureChaos.Core.Providers
{
    /// <summary>The storage account provider.</summary>
    /// Creates the storage account if not any for the given storage account name in the config.
    /// Create the table client for the given storage account.
    public static class StorageAccountProvider
    {
        /// <summary>Default format for the storage connection string.</summary>
        //private const string ConnectionStringFormat = "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1};EndpointSuffix=core.windows.net";

        private static readonly CloudStorageAccount StorageAccount;

        static StorageAccountProvider()
        {
            if(!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["ConfigStorageConnectionString"]))
            {
                StorageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["ConfigStorageConnectionString"]);
            }
        }

        public static CloudTable CreateOrGetTable(string tableName)
        {
            if(StorageAccount == null)
            {
                return null;
            }

            var tableClient = StorageAccount.CreateCloudTableClient() ?? throw new ArgumentNullException($"storageAccount.CreateCloudTableClient()");

            // Retrieve a reference to the table.
            var table = tableClient.GetTableReference(tableName);

            // Create the table if it doesn't exist.
            table.CreateIfNotExists();

            return table;
        }

        public static void InsertOrMerge<T>(T entity, string tableName) where T : ITableEntity
        {
            var table = CreateOrGetTable(tableName);
            if (table == null)
            {
                return;
            }

            var tableOperation = TableOperation.InsertOrMerge(entity);
            table.Execute(tableOperation);
        }

        public static IEnumerable<T> GetEntities<T>(TableQuery<T> query, string tableName) where T : ITableEntity, new()
        {
            if (query == null)
            {
                return null;
            }

            var table = CreateOrGetTable(tableName);
            if (table == null)
            {
                return null;
            }

            TableContinuationToken continuationToken = null;
            IEnumerable<T> results = null;
            do
            {
                var token = continuationToken;
                var result = table.ExecuteQuerySegmented(query, token);
                results = results?.Concat(result.Results) ?? result;
                continuationToken = result.ContinuationToken;
            }
            while (continuationToken != null);

            return results;
        }
    }
}