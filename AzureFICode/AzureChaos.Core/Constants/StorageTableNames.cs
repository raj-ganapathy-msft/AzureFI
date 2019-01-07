namespace AzureChaos.Core.Constants
{
    /// <summary>
    /// Consists of various storage table names used accross the project
    /// </summary>
    public class StorageTableNames
    {
        public const string ResourceGroupCrawlerTableName = "tblchaosresourcegroup";
        public const string VirtualMachineCrawlerTableName = "tblchaosvirtualmachines";
        public const string AvailabilitySetCrawlerTableName = "tblchaosavailabilityset";
        public const string VirtualMachinesScaleSetCrawlerTableName = "tblchaosscalesets";
        public const string AvailabilityZoneCrawlerTableName = "tblchaosavailabilityzone";
        public const string ScheduledRulesTableName = "tblchaosscheduledrules";
        public const string LoadBalancerCrawlerTableName = "tblchaosloadbalancergroup";
    }
}