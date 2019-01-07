using AzureChaos.Core.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AzureChaos.Core.Helper
{
    public class ResourceGroupHelper
    {
        public static List<IResourceGroup> GetResourceGroupsInSubscription(TraceWriter log = null)
        {
            var azureClient = new AzureClient(log);
            var azure = azureClient.AzureInstance;
            List<string> blackListedResourceGroupList = azureClient.AzureSettings.Chaos.ExcludedResourceGroupList;
            var resourceGroupList = azure.ResourceGroups.List();
            var resourceGroups = resourceGroupList.ToList();
            if (resourceGroups?.Count <= 0)
            {
                return null;
            }
            
            return blackListedResourceGroupList?.Count > 0
                ? resourceGroups.Where(x => !blackListedResourceGroupList.Contains(x.Id,
                        StringComparer.OrdinalIgnoreCase))
                    .ToList()
                : resourceGroups.ToList();
        }
    }
}