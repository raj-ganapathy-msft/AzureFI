namespace AzureChaos.Core.Enums
{
    public enum TriggeredEvent
    {
        /// <summary>Default Action type</summary>
        Unknown = 0,

        /// <summary>When it is triggered by Events Functions</summary>
        Events,

        /// <summary>When it is triggered by Rules Functions</summary>
        Rules,

        /// <summary>When it is triggered by Schedulers Functions</summary>
        Schedulers,

        /// <summary>When it is triggered by Executer Functions </summary>
        Executors
    }
}