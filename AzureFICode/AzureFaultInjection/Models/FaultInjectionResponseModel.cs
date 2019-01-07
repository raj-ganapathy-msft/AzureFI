using AzureChaos.Core.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using System.Collections.Generic;

namespace AzureFaultInjection.Models
{
    public class FaultInjectionResponseModel<T>
    {
        public bool Success { get; set; }

        public string SuccessMessage { get; set; }

        public T Result { get; set; }

        public string ErrorMessage { get; set; }
    }

    public class DisplayConfigResponseModel
    {
        public IEnumerable<SubscriptionInner> SubcriptionList { get; set; }
        public IEnumerable<ResourceGroupInner> ResourceGroups { get; set; }
        public ConfigModel Config { get; set; }
    }
}