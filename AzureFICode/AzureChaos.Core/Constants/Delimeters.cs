namespace AzureChaos.Core.Constants
{
    /// <summary>Storage table is not allowing to store the partition key with flashes.
    /// so "delimeters" used to store the resource id as a partition key by replacing the forward slash into exclamatory and vice versa(when reading from the table).
    /// </summary>
    public static class Delimeters
    {
        public const char Exclamatory = '!';
        public const char ForwardSlash = '/';
        public const char At = '@';
        public const char Underscore = '_';
    }
}