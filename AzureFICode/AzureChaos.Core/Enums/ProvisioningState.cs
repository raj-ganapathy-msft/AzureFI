namespace AzureChaos.Core.Enums
{
    /// <summary>
    /// Defines values for ProvisioningState.
    /// </summary>
    public enum ProvisioningState
    {
        /// <summary>Succeed state of the VM.</summary>
        Succeeded,

        /// <summary>Failed state of the VM.</summary>
        Failed,

        /// <summary>Canceled state of the VM.</summary>
        Canceled,

        /// <summary>In-progress state of the VM.</summary>
        InProgress,

        /// <summary>Deleting state of the VM.</summary>
        Deleting
    }
}