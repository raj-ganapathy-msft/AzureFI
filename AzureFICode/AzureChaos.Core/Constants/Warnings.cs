namespace AzureChaos.Core.Constants
{
    public static class Warnings
    {
        public const string ActionAndStateAreSame =
            "Initial state of the resource and the current chaos are same. Couldnot perform the action.";

        public const string RestartOnStop = "Initial state of the resource is stopped case. Hence couldnot perform a stop operation.";

        public const string ChaosDisabledAfterRules = "Chaos disabled after preparing the schedule rules";

        public const string ProvisionStateCheck = "Resource provision state is in {0}, cannot perform chaos";
    }
}
