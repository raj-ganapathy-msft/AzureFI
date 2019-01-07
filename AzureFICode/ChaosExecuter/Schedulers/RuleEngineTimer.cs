using AzureChaos.Core.Enums;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Interfaces;
using AzureChaos.Core.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Linq;

namespace ChaosExecuter.Schedulers
{
    public static class RuleEngineTimer
    {
        [FunctionName("RuleEngineTimer")]
        public static void Run([TimerTrigger("%SchedulerFrequency%")]TimerInfo myTimer, TraceWriter log)
        {

            log.Info("C# RuleEngine: trigger function started processing the request.");
            var azureClient = new AzureClient();
            if (azureClient.AzureSettings?.Chaos == null || !azureClient.AzureSettings.Chaos.ChaosEnabled)
            {
                log.Info("C# RuleEngine: Chaos is not enabled.");
                return;
            }

            var enabledChaos = RuleEngineHelper.GetEnabledChaosSet(azureClient.AzureSettings);
            if (enabledChaos == null || !enabledChaos.Any())
            {
                log.Info("C# RuleEngine: Chaos is not enabled on any resources.");
                return;
            }

            Random random = new Random();
            var randomIdex = random.Next(0, enabledChaos.Count);
            switch (enabledChaos[randomIdex])
            {
                case VirtualMachineGroup.VirtualMachines:
                    log.Info("C# RuleEngine: Virtual Machine Rule engine got picked");
                    IRuleEngine virtualMachine = new VirtualMachineRuleEngine();
                    virtualMachine.CreateRule(log);
                    break;

                case VirtualMachineGroup.AvailabilitySets:
                    log.Info("C# RuleEngine: AvailabilitySets Rule engine got picked");
                    IRuleEngine availabilitySet = new AvailabilitySetRuleEngine();
                    availabilitySet.CreateRule(log);
                    break;

                case VirtualMachineGroup.VirtualMachineScaleSets:
                    log.Info("C# RuleEngine: ScaleSets Rule engine got picked");
                    IRuleEngine virtualMachineScaleSet = new ScaleSetRuleEngine();
                    virtualMachineScaleSet.CreateRule(log);
                    break;

                case VirtualMachineGroup.AvailabilityZones:
                    log.Info("C# RuleEngine: AvailabilityZones Rule engine got picked");
                    IRuleEngine availabilityZone = new AvailabilityZoneRuleEngine();
                    availabilityZone.CreateRule(log);
                    break;

                case VirtualMachineGroup.LoadBalancer:
                    log.Info("C# RuleEngine: LoadBalancer Rule engine got picked");
                    IRuleEngine loadBalancer = new LoadBalancerRuleEngine();
                    loadBalancer.CreateRule(log);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}