namespace AzureChaos.Core.Models
{
    public class Schedules
    {
        public string ResourceName { get; set; }
        public string ResourceId { get; set; }
        public string ScheduledTime { get; set; }
        public string ChaosOperation { get; set; }
        public bool? IsRollbacked { get; set; }
        public string Status { get; set; }
    }
}
