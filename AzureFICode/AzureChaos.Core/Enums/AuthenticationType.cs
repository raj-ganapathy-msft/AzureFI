namespace AzureChaos.Core.Enums
{
    /// <summary>Azure authentication type enum</summary>
    public enum AuthenticationType
    {
        /// <summary>Credentials auth type, will validate the azure by using client id and secret.</summary>
        Credentials = 0,

        /// <summary>Certificate auth type, will validate the azure by client certificate.</summary>
        Certificate = 1
    }
}