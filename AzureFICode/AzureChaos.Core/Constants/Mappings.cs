using AzureChaos.Core.Enums;
using AzureChaos.Core.Models.Configs;
using System.Collections.Generic;

namespace AzureChaos.Core.Constants
{
    public class Mappings
    {
        public static IDictionary<VirtualMachineGroup, bool> GetEnabledChaos(AzureSettings azureSettings)
        {
            return new Dictionary<VirtualMachineGroup, bool>()
            {
                { VirtualMachineGroup.AvailabilitySets, azureSettings.Chaos.AvailabilitySetChaos.Enabled},
                { VirtualMachineGroup.VirtualMachines, azureSettings.Chaos.VirtualMachineChaos.Enabled},
                { VirtualMachineGroup.AvailabilityZones, azureSettings.Chaos.AvailabilityZoneChaos.Enabled},
                { VirtualMachineGroup.VirtualMachineScaleSets, azureSettings.Chaos.ScaleSetChaos.Enabled},
                { VirtualMachineGroup.LoadBalancer, azureSettings.Chaos.LoadBalancerChaos.Enabled}
            };
        }

        public static Dictionary<string, string> FunctionNameMap = new Dictionary<string, string>()
        {
            { VirtualMachineGroup.VirtualMachines.ToString(), "virtualmachinesexecuter" },
            { VirtualMachineGroup.VirtualMachineScaleSets.ToString(), "virtualmachinescalesetexecuter" },
            { VirtualMachineGroup.AvailabilitySets.ToString(), "virtualmachinesexecuter" },
            { VirtualMachineGroup.AvailabilityZones.ToString(), "virtualmachinesexecuter" },
            { VirtualMachineGroup.AvailabilityZones.ToString() + VirtualMachineGroup.VirtualMachineScaleSets.ToString(),
                "virtualmachinescalesetexecuter" },
            { VirtualMachineGroup.LoadBalancer.ToString(), "virtualmachinesexecuter" },
        };

        ///Microsoft subscription blob endpoint for configs:  https://chaostest.blob.core.windows.net/config/azuresettings.json
        ///Zen3 subscription blob endpoint for configs: ==>  https://cmonkeylogs.blob.core.windows.net/configs/azuresettings.json
        /// Microsoft demo config file ==> https://stachaosteststorage.blob.core.windows.net/configs/azuresettings.json

        public const string ConfigEndpoint = "https://cmnewschema.blob.core.windows.net/configs/azuresettings.json";

        public const string TargetConfigObject = "TargetConfig";
        public const string TargetSubscriptionId = "microsoft.faultinjection.client.subscription.id";
        public const string TargetTenantId = "microsoft.faultinjection.client.tenant.id";
        public const string TargetClientId = "microsoft.faultinjection.client.id";
        public const string TargetClientSecret = "microsoft.faultinjection.client.secretKey";
        public const string TargetStorageAccount = "microsoft.faultinjection.client.storageAccount.name";
        public const string TargetStorageConnectionString = "microsoft.faultinjection.client.storageAccount.connectionString";
        public const string TargetResourceGroup = "microsoft.faultinjection.client.resourceGroup";
        public const string TargetRegion = "microsoft.faultinjection.client.region";
        public const string FaultInjectionObject = "ChaosConfig";
        public const string SchedulerFrequency = "microsoft.faultinjection.scheduler.frequency";
        public const string TriggerFrequency = "microsoft.faultinjection.trigger.frequency";
        public const string CrawlerFrequency = "microsoft.faultinjection.crawler.frequency";
        public const string RollbackFrequency = "microsoft.faultinjection.rollback.frequency";
        public const string FaultInjectionEnable = "microsoft.faultinjection.enabled";
        public const string MeanTime = "microsoft.faultinjection.meantime";
        public const string ExcludedResourceGroups = "microsoft.faultinjection.excludedResourceGroups";
        public const string AzureFaultInjectionActions = "microsoft.faultinjection.actions";
        public const string AvZoneObject = "microsoft.faultinjection.AvZones";
        public const string AvZoneEnabled = "microsoft.faultinjection.AvZones.enabled";
        public const string AvZoneRegions = "microsoft.faultinjection.AvZones.regions";
        public const string VmObject = "microsoft.faultinjection.VM";
        public const string VmEnabled = "microsoft.faultinjection.VM.enabled";
        public const string VmTerminationPercentage = "microsoft.faultinjection.VM.percentageTermination";
        public const string VmssObject = "microsoft.faultinjection.VmSS";
        public const string VmssEnabled = "microsoft.faultinjection.VmSS.enabled";
        public const string VmssTerminationPercentage = "microsoft.faultinjection.VmSS.percentageTermination";
        public const string loadBalancerObject = "microsoft.faultinjection.LB";
        public const string loadBalancerEnabled = "microsoft.faultinjection.LB.enabled";
        public const string loadBalancerTerminationPercentage = "microsoft.faultinjection.LB.percentageTermination";
        public const string AvSetObject = "microsoft.faultinjection.AvSets";
        public const string AvSetEnabled = "microsoft.faultinjection.AvSets.enabled";
        public const string AvSetFaultDomainEnabled = "microsoft.faultinjection.AvSets.faultDomain.enabled";
        public const string AvSetUpdateDomainEnabled = "microsoft.faultinjection.AvSets.updateDomain.enabled";
    }
}