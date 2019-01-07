using System;
using System.IO;
using System.Text;
using AzureChaos.Core.Constants;
using AzureChaos.Core.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Storage.Fluent;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace AzureChaos.Core.Helper
{
    public static class ApiHelper
    {
        public static IResourceGroup CreateResourceGroup(IAzure azure, string resourceGroupName, string regionName)
        {
            try
            {
                var resourceGroup = azure.ResourceGroups
                    .Define(resourceGroupName)
                    .WithRegion(regionName)
                    .Create();
                return resourceGroup;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static IStorageAccount CreateStorageAccount(IAzure azure, string resourceGroupName, string regionName, string storageAccountName)
        {
            try
            {
                var storageAccount = azure.StorageAccounts.Define(storageAccountName)
                    .WithRegion(regionName)
                    .WithExistingResourceGroup(resourceGroupName)
                    .Create();
                return storageAccount;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static CloudBlockBlob CreateBlobContainer(CloudStorageAccount storageAccount)
        {
            try
            {
                // Create the blob client.
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                // Retrieve a reference to a container.
                CloudBlobContainer container = blobClient.GetContainerReference("configs");

                // Create the container if it doesn't already exist.
                container.CreateIfNotExists();

                container.SetPermissions(
                    new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                // Retrieve reference to a blob named "myblob".
                CloudBlockBlob blockBlob = container.GetBlockBlobReference("azuresettings.json");
                blockBlob.Properties.ContentType = "application/json";
                return blockBlob;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static string ConvertConfigObjectToString(ConfigModel queryParams)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();

                // target config properties
                writer.WritePropertyName(Mappings.TargetConfigObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.TargetSubscriptionId);
                writer.WriteValue(queryParams.Subscription);
                writer.WritePropertyName(Mappings.TargetTenantId);
                writer.WriteValue(queryParams.TenantId);
                writer.WritePropertyName(Mappings.TargetClientId);
                writer.WriteValue(queryParams.ClientId);
                writer.WritePropertyName(Mappings.TargetClientSecret);
                writer.WriteValue(queryParams.ClientSecret);
                writer.WritePropertyName(Mappings.TargetResourceGroup);
                writer.WriteValue(queryParams.SelectedDeploymentRg);
                writer.WritePropertyName(Mappings.TargetRegion);
                writer.WriteValue(queryParams.SelectedRegion);
                writer.WritePropertyName(Mappings.TargetStorageAccount);
                writer.WriteValue(queryParams.StorageAccountName);
                writer.WritePropertyName(Mappings.TargetStorageConnectionString);
                writer.WriteValue(queryParams.StorageConnectionString);
                writer.WriteEndObject();

                // fault injection config properties
                writer.WritePropertyName(Mappings.FaultInjectionObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.FaultInjectionEnable);
                writer.WriteValue(queryParams.IsChaosEnabled);
                writer.WritePropertyName(Mappings.SchedulerFrequency);
                writer.WriteValue(queryParams.SchedulerFrequency);
                writer.WritePropertyName(Mappings.TriggerFrequency);
                writer.WriteValue(queryParams.TriggerFrequency);
                writer.WritePropertyName(Mappings.CrawlerFrequency);
                writer.WriteValue(queryParams.CrawlerFrequency);
                writer.WritePropertyName(Mappings.RollbackFrequency);
                writer.WriteValue(queryParams.RollbackFrequency);
                writer.WritePropertyName(Mappings.MeanTime);
                writer.WriteValue(queryParams.MeanTime);
                writer.WritePropertyName(Mappings.AzureFaultInjectionActions);
                writer.WriteStartArray();
                if (queryParams.AzureFiActions != null)
                {
                    foreach (var azureFiAction in queryParams.AzureFiActions)
                    {
                        writer.WriteValue(azureFiAction);
                    }
                }

                writer.WriteEndArray();

                writer.WritePropertyName(Mappings.ExcludedResourceGroups);
                writer.WriteStartArray();
                if (queryParams.ExcludedResourceGroups != null)
                {
                    foreach (var excludedResourceGroup in queryParams.ExcludedResourceGroups)
                    {
                        writer.WriteValue(excludedResourceGroup);
                    }
                }

                writer.WriteEndArray();

                writer.WritePropertyName(Mappings.VmObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.VmEnabled);
                writer.WriteValue(queryParams.IsVmEnabled);
                writer.WritePropertyName(Mappings.VmTerminationPercentage);
                writer.WriteValue(queryParams.VmPercentage);
                writer.WriteEndObject();

                writer.WritePropertyName(Mappings.VmssObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.VmssEnabled);
                writer.WriteValue(queryParams.IsVmssEnabled);
                writer.WritePropertyName(Mappings.VmssTerminationPercentage);
                writer.WriteValue(queryParams.VmssPercentage);
                writer.WriteEndObject();

                writer.WritePropertyName(Mappings.loadBalancerObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.loadBalancerEnabled);
                writer.WriteValue(queryParams.IsLoadbalancerEnabled);
                writer.WritePropertyName(Mappings.loadBalancerTerminationPercentage);
                writer.WriteValue(queryParams.LoadBalancerPercentage);
                writer.WriteEndObject();

                writer.WritePropertyName(Mappings.AvSetObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.AvSetEnabled);
                writer.WriteValue(queryParams.IsAvSetEnabled);
                writer.WritePropertyName(Mappings.AvSetFaultDomainEnabled);
                writer.WriteValue(queryParams.IsFaultDomainEnabled);
                writer.WritePropertyName(Mappings.AvSetUpdateDomainEnabled);
                writer.WriteValue(queryParams.IsUpdateDomainEnabled);
                writer.WriteEndObject();

                writer.WritePropertyName(Mappings.AvZoneObject);
                writer.WriteStartObject();
                writer.WritePropertyName(Mappings.AvZoneEnabled);
                writer.WriteValue(queryParams.IsAvZoneEnabled);

                writer.WritePropertyName(Mappings.AvZoneRegions);
                writer.WriteStartArray();
                if (queryParams.AvZoneRegions != null)
                {
                    foreach (var avZoneRegion in queryParams.AvZoneRegions)
                    {
                        writer.WriteValue(avZoneRegion);
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndObject();

                writer.WriteEndObject();
            }

            return sb.ToString();
        }
    }
}