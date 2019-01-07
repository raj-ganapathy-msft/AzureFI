using Microsoft.Azure.WebJobs.Host;

namespace AzureChaos.Core.Interfaces
{
    public interface IRuleEngine
    {
        void CreateRule(TraceWriter log);
    }
}