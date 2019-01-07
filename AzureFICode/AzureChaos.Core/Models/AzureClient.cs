using AzureChaos.Core.Enums;
using AzureChaos.Core.Models.Configs;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Configuration;

namespace AzureChaos.Core.Models
{
    /// <summary>Azure configuration model which azure tenant, subscription
    /// and resource group needs to be crawled</summary>
    public class AzureClient
    {
        public readonly IAzure AzureInstance;
        public readonly AzureSettings AzureSettings;

        /// <summary>
        /// Initialize the configuration information and AzureInstance
        /// </summary>
        public AzureClient(TraceWriter log = null)
        {
            try
            {
                var connectiongString = ConfigurationManager.AppSettings["ConfigStorageConnectionString"];
                AzureSettings = GetAzureSettings(connectiongString,log);
                if (AzureSettings != null)
                {
                    AzureInstance = GetAzure(AzureSettings.Client.ClientId, AzureSettings.Client.ClientSecret,
                        AzureSettings.Client.TenantId, AzureSettings.Client.SubscriptionId);
                }
            }
            catch (Exception ex)
            {
                // TODO: Logs
            }
        }

        public static IResourceManagementClient GetResourceManagementClientClient(string clientId, string clientSecret, string tenantId, string subscriptionId)
        {
            var azureCredentials = GetAzureCredentials(clientId, clientSecret, tenantId);
            return azureCredentials == null ? null : new ResourceManagementClient(azureCredentials)
            {
                SubscriptionId = subscriptionId
            };
        }

        public static ISubscriptionClient GetSubscriptionClient(string clientId, string clientSecret, string tenantId)
        {
            var azureCredentials = GetAzureCredentials(clientId, clientSecret, tenantId);
            return azureCredentials == null ? null : new SubscriptionClient(azureCredentials);
        }

        /// <summary>Get the Azure object to read the all resources from azure</summary>
        /// <returns>Returns the Azure object.</returns>
        public static IAzure GetAzure(string clientId, string clientSecret, string tenantId, string subscriptionId)
        {
            var azureCredentials = GetAzureCredentials(clientId, clientSecret, tenantId);
            return azureCredentials == null ? null : Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(azureCredentials)
                .WithSubscription(subscriptionId);
        }

        /// <summary>Get azure credentials based on the client id and client secret.</summary>
        /// <returns></returns>
        private static AzureCredentials GetAzureCredentials(string clientId, string clientSecret, string tenantId)
        {
            return SdkContext.AzureCredentialsFactory
                            .FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
        }

        public static AzureSettings GetAzureSettings(string connectionString, TraceWriter log = null)
        {
            try
            {
                if (log != null)
                {
                    log.Info($"connection string {connectionString}");
                }
                // TODO: Add to app settings of the function.
                //  const string connectionString = "UseDevelopmentStorage=true";

                // TODO: Add below code to try catch & log
                var storageAccount = CloudStorageAccount.Parse(connectionString);

                var blobClinet = storageAccount.CreateCloudBlobClient();
                var blobContainer = blobClinet.GetContainerReference("configs");
                var blobReference = blobContainer.GetBlockBlobReference("azuresettings.json");
                var data = blobReference.DownloadText();
                return JsonConvert.DeserializeObject<AzureSettings>(data);
            }
            catch (Exception e)
            {
                if (log != null)
                {
                    log.Error($"connection string {e}");
                }

                throw;
            }
        }

        public bool IsChaosEnabledByGroup(string vmGroup)
        {
            if (!Enum.TryParse(vmGroup, out VirtualMachineGroup virtualMachineGroup))
            {
                return false;
            }

            var chaosEnabled = AzureSettings.Chaos.ChaosEnabled;
            switch (virtualMachineGroup)
            {
                case VirtualMachineGroup.VirtualMachines:
                    return chaosEnabled && AzureSettings.Chaos.VirtualMachineChaos.Enabled;
                case VirtualMachineGroup.AvailabilitySets:
                    return chaosEnabled && AzureSettings.Chaos.AvailabilitySetChaos.Enabled;
                case VirtualMachineGroup.AvailabilityZones:
                    return chaosEnabled && AzureSettings.Chaos.AvailabilityZoneChaos.Enabled;
                case VirtualMachineGroup.VirtualMachineScaleSets:
                    return chaosEnabled && AzureSettings.Chaos.ScaleSetChaos.Enabled;
                case VirtualMachineGroup.LoadBalancer:
                    return chaosEnabled && AzureSettings.Chaos.LoadBalancerChaos.Enabled;
            }

            return false;
        }
    }
}