using AzureChaos.Core.Constants;
using AzureChaos.Core.Entity;
using AzureChaos.Core.Enums;
using AzureChaos.Core.Helper;
using AzureChaos.Core.Models;
using AzureChaos.Core.Models.Configs;
using AzureFaultInjection.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Azure.Management.Storage.Fluent;
using Microsoft.Rest.Azure;
using Microsoft.Rest.TransientFaultHandling;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml;
using AzureChaos.Core.Providers;

namespace AzureFaultInjection.Controllers
{
    public class FaultInjectionController : ApiController
    {
        private const string StorageConStringFormat = "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1};EndpointSuffix=core.windows.net";
        private const string CommonName = "azurefi";
        private const string StorageConnectionString = "ConfigStorageConnectionString";
        private const string TenantIdString = "TenantId";
        private const string ApplicationIdString = "ApplicationId";
        private const string ResourceGroupName = "ResourceGroupName";
        private const string StorageAccountName = "StorageAccountName";
        private const string TimerExpressionByMinutes = "0 */{0} * * * *";
        private const string TimerExpressionByHours = "0 0 */{0} * * *";

        [ActionName("gettenantinformation")]
        public Dictionary<string, string> GetTenantInformation()
        {
            return new Dictionary<string, string>() {
                { TenantIdString, ConfigurationManager.AppSettings[TenantIdString]},
                { ApplicationIdString, ConfigurationManager.AppSettings[ApplicationIdString] }
            };
        }


        [ActionName("getschedules")]
        public IEnumerable<Schedules> GetSchedules(string fromDate, string toDate)
        {
            try
            {
                var configItems = ConfigurationManager.AppSettings[StorageConnectionString];

                var entities = GetSchedulesByDate(fromDate, toDate);
                var scheduledRuleses = entities as ScheduledRules[] ?? entities.ToArray();
                var result = scheduledRuleses?.Select(ConvertToSchedule);
                return scheduledRuleses?.Select(ConvertToSchedule);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
       
        [ActionName("getactivities")]
        public IEnumerable<Activities> GetActivities(string fromDate, string toDate)
        {
            try
            {
                /*var scheduledRuleses = entities as ScheduledRules[] ?? entities.ToArray();
                var result = scheduledRuleses?.Select(ConvertToActivity);
                //I have to check if rollback parameters are there if so append it to the result, later change these results according to sorting order
                var schedule = scheduledRuleses?.Where(x => x != null && x.RollbackFinalState != x.RollbackInitialState);
                var rollbackresults = schedule?.Select(ConvertToActivityRollback);
                var results =  result?.Concat(rollbackresults);
                */
                /*var results = result?.Concat(scheduledRuleses?.Select(ConvertToActivityRollback)).Where(x => x != null && 
                                                                                                             x.ChaosCompletedTime != null &&
                                                                                                             x.ChaosStartedTime != null &&
                                                                                                             x.FinalState != null &&
                                                                                                             x.InitialState != null &&
                                                                                                             x.Status != null &&
                                                                                                             x.ResourceName != null).OrderByDescending(x => x.TFChaosCompletedTime);
               */
                /*results = results.ToList();
                results = results.OrderByDescending(x => x.ChaosCompletedTime);
                return results;
                */
                var entities = GetSchedulesByDateForActivities(fromDate, toDate);
                var scheduledRuleses = entities.Select(ConvertToActivity);
                List<Activities> activitieses = new List<Activities>();
                activitieses.AddRange(scheduledRuleses);
                var schedule = entities?.Where(x =>(x.Rolledback == false && x.RollbackExecutionStartTime != null)||(x.Rolledback == true));
                var rollbackresults = schedule?.Select(ConvertToActivityRollback);
                activitieses.AddRange(rollbackresults);
                
                activitieses.Sort((x, y) =>
                {
                    return (x.FIChaosStartedTime.Ticks).CompareTo(y.FIChaosStartedTime.Ticks)*(-1);
                });
                
               return activitieses;
                
                ////x.ChaosStartedTime.CompareTo(y.ChaosStartedTime)
                //return activitieses.OrderByDescending(x => x.FIChaosStartedTime);

                //var results = scheduledRuleses?.Concat(rollbackresults);
                //return results.OrderByDescending(x => x.TFChaosCompletedTime).ToList();

                //return activitieses.OrderByDescending(x => x.FIChaosStartedTime.Ticks);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [ActionName("getazureoperations")]
        public Dictionary<string, string> GetAzureOperations()
        {
            return new Dictionary<string, string>() {
                {AzureFiOperation. PowerCycle.ToString(), "Power Cycle"},
                {AzureFiOperation.Restart.ToString(), "Restart"}
            };
        }
        

        // GET: api/Api
        [ActionName("getsubscriptions")]
        public async Task<FaultInjectionResponseModel<DisplayConfigResponseModel>> GetSubscriptions(string tenantId, string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret) ||
                string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            var subscriptionClient = AzureClient.GetSubscriptionClient(clientId,
                clientSecret,
                tenantId);

            IPage<SubscriptionInner> subscriptionTask = null;
            try
            {
                subscriptionTask = await subscriptionClient.Subscriptions.ListAsync();
            }
            catch (Exception ex)
            {
                return new FaultInjectionResponseModel<DisplayConfigResponseModel>() { ErrorMessage = ex.Message, Success = false };
            }

            var subscriptionList = subscriptionTask?.Select(x => x);

            // Read the existing config from storage account
            string storageConnection = ConfigurationManager.AppSettings[StorageConnectionString];

            var model = new DisplayConfigResponseModel()
            {
                SubcriptionList = subscriptionList
            };
            if (!string.IsNullOrWhiteSpace(storageConnection))
            {
                try
                {
                    var settings = AzureClient.GetAzureSettings(storageConnection);
                    if (settings != null)
                    {
                        model.Config = ConvertAzureSettingsConfigModel(settings);
                    }

                    model.ResourceGroups = await GetResourceGroups(settings.Client.TenantId, settings.Client.ClientId,
                        settings.Client.ClientSecret, settings.Client.SubscriptionId);
                }
                catch (Exception ex)
                {
                    // dont throw exception here, 1st time user does not have the settings data.
                }
            }

            //return response;
            return new FaultInjectionResponseModel<DisplayConfigResponseModel>()
            {
                Result = model,
                Success = true
            };
        }

        [ActionName("getresourcegroups")]
        public async Task<IEnumerable<ResourceGroupInner>> GetResourceGroups(string tenantId, string clientId, string clientSecret,
            string subscription)
        {
            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret) ||
                string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(subscription))
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            var subscriptionId = subscription.Split('/').Last();
            var resourceManagementClient = AzureClient.GetResourceManagementClientClient(clientId,
                clientSecret,
                tenantId, subscriptionId);
            var resourceGroupTask = await resourceManagementClient.ResourceGroups.ListAsync();

            var resourceGroupList = resourceGroupTask?.Select(x => x);

            //return response;
            return resourceGroupList;
        }

        [ActionName("createblob")]
        public FaultInjectionResponseModel<ConfigModel> CreateBlob(ConfigModel model)
        {
            if (model == null)
            {
                throw new HttpRequestWithStatusException("Empty model", new HttpResponseException(HttpStatusCode.BadRequest));
            }

            if (string.IsNullOrWhiteSpace(model.ClientId) ||
                string.IsNullOrWhiteSpace(model.ClientSecret) ||
                string.IsNullOrWhiteSpace(model.TenantId) ||
                string.IsNullOrWhiteSpace(model.Subscription))
            {
                throw new HttpRequestWithStatusException("ClientId/ClientSecret/TenantId/Subscription is empty", new HttpResponseException(HttpStatusCode.BadRequest));
            }

            if (2* model.RollbackFrequency >  model.SchedulerFrequency)
            {
                throw new Exception("Power Cycle Schedule Frequency should be less than half the Scheduler Frequency, Please decrease the Power Cycle Schedule or increase the Scheduler Frequency value");
            }

            if (2 * model.RollbackFrequency <=  model.SchedulerFrequency &&
                model.SchedulerFrequency > (model.CrawlerFrequency + model.MeanTime))
            {
                throw new HttpRequestWithStatusException("Scheduler Frequency should be less than the sum of Crawler Frequency & Mean Time between FI on VM, Please reduce the value of Scheduler Frequency", new HttpResponseException(HttpStatusCode.BadRequest));
            }

                try
            {
                var storageAccountName = ConfigurationManager.AppSettings[StorageAccountName];
                var resourceGroupName = ConfigurationManager.AppSettings[ResourceGroupName];

                if (string.IsNullOrWhiteSpace(storageAccountName) || string.IsNullOrWhiteSpace(resourceGroupName))
                {
                    throw new ArgumentNullException("Storage account name or resource group name is null "
                        + "Storage Param: " + StorageAccountName + " Resource Param: " + ResourceGroupName);
                }

                var azure = AzureClient.GetAzure(model.ClientId,
                    model.ClientSecret,
                    model.TenantId,
                    model.Subscription);

                var resourceGroup = azure.ResourceGroups.GetByName(resourceGroupName);
                if (resourceGroup == null)
                {
                    throw new ArgumentNullException(ResourceGroupName, "Resource group information is empty: " + ResourceGroupName);
                }

                var storageAccounts = azure.StorageAccounts.ListByResourceGroup(resourceGroupName);
                IStorageAccount storageAccountInfo = null;
                if (storageAccounts != null && storageAccounts.Count() > 0)
                {
                    storageAccountInfo = storageAccounts.FirstOrDefault(x =>
                        x.Name.Equals(storageAccountName, StringComparison.OrdinalIgnoreCase));
                }

                if (storageAccountInfo == null)
                {
                    throw new ArgumentNullException(StorageAccountName, "Storage account information is empty: " + storageAccountName);
                }

                var storageKeys = storageAccountInfo.GetKeys();
                string storageConnection = string.Format(StorageConStringFormat,
                    storageAccountName, storageKeys[0].Value);

                InsertOrUpdateAppsettings(StorageConnectionString, storageConnection);
                var storageAccount = CloudStorageAccount.Parse(storageConnection);
                var blockBlob = ApiHelper.CreateBlobContainer(storageAccount);
                var configString = ApiHelper.ConvertConfigObjectToString(model);
                using (var ms = new MemoryStream())
                {
                    LoadStreamWithJson(ms, configString);
                    blockBlob.UploadFromStream(ms);
                }

                var functionAppName = CommonName +
                    GetUniqueHash(model.ClientId + model.TenantId + model.Subscription);

                // TODO: ask ? do we deploy the azure functions, whenever user do any changes in the  config?
                if (!DeployAzureFunctions(model, functionAppName, storageConnection, resourceGroupName))
                {
                    throw new HttpRequestWithStatusException("Azure Functions are not deployed",
                        new HttpResponseException(HttpStatusCode.InternalServerError));
                }


                // add the tenant id and application id into the configuration file to validate the user next time.
                InsertOrUpdateAppsettings(TenantIdString, model.TenantId);
                InsertOrUpdateAppsettings(ApplicationIdString, model.ClientId);

                return new FaultInjectionResponseModel<ConfigModel>()
                {
                    Success = true,
                    SuccessMessage = "Deployment Completed Successfully",
                    Result = model
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static string GetStorageConnectionString(string storageAccountName, string resourceGroup, IAzure azure)
        {
            if (string.IsNullOrWhiteSpace(storageAccountName))
            {
                throw new ArgumentNullException("strorageAccountName", "Storage name is empty");
            }

            var storageAccounts = azure.StorageAccounts.ListByResourceGroup(resourceGroup);
            if (storageAccounts == null || !storageAccounts.Any())
            {
                throw new ItemNotFoundException("Storage account not found");
            }

            var account = storageAccounts.FirstOrDefault(x => x.Name.Equals(storageAccountName, StringComparison.OrdinalIgnoreCase));
            if (account == null || string.IsNullOrWhiteSpace(account.Key))
            {
                throw new ItemNotFoundException("Storage account not found");
            }

            return string.Format(StorageConStringFormat, storageAccountName, account.Key);
        }

        private static ConfigModel ConvertAzureSettingsConfigModel(AzureSettings settings)
        {
            var model = new ConfigModel
            {
                TenantId = settings.Client.TenantId,
                ClientId = settings.Client.ClientId,
                ClientSecret = settings.Client.ClientSecret,
                Subscription = settings.Client.SubscriptionId,
                IsChaosEnabled = settings.Chaos.ChaosEnabled,

                ExcludedResourceGroups = settings.Chaos.ExcludedResourceGroupList,
                AzureFiActions = settings.Chaos.AzureFaultInjectionActions,

                AvZoneRegions = settings.Chaos.AvailabilityZoneChaos.Regions,
                IsAvZoneEnabled = settings.Chaos.AvailabilityZoneChaos.Enabled,

                IsAvSetEnabled = settings.Chaos.AvailabilitySetChaos.Enabled,
                IsFaultDomainEnabled = settings.Chaos.AvailabilitySetChaos.FaultDomainEnabled,
                IsUpdateDomainEnabled = settings.Chaos.AvailabilitySetChaos.UpdateDomainEnabled,

                VmssPercentage = settings.Chaos.ScaleSetChaos.PercentageTermination,
                IsVmssEnabled = settings.Chaos.ScaleSetChaos.Enabled,

                LoadBalancerPercentage = settings.Chaos.LoadBalancerChaos != null ? settings.Chaos.LoadBalancerChaos.PercentageTermination : 60,
                IsLoadbalancerEnabled = settings.Chaos.LoadBalancerChaos != null ? settings.Chaos.LoadBalancerChaos.Enabled : false,

                IsVmEnabled = settings.Chaos.VirtualMachineChaos.Enabled,
                VmPercentage = settings.Chaos.VirtualMachineChaos.PercentageTermination,

                CrawlerFrequency = settings.Chaos.CrawlerFrequency,
                SchedulerFrequency = settings.Chaos.SchedulerFrequency,
                RollbackFrequency = settings.Chaos.RollbackRunFrequency,
                MeanTime = settings.Chaos.MeanTime
            };

            return model;
        }
        private static string GetUniqueHash(string input)
        {
            StringBuilder hash = new StringBuilder();
            MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider();
            byte[] bytes = md5Provider.ComputeHash(new UTF8Encoding().GetBytes(input));

            foreach (var t in bytes)
            {
                hash.Append(t.ToString("x2"));
            }
            return hash.ToString().Substring(0, 24);
        }

        private static bool InsertOrUpdateAppsettings(string key, string value)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(HttpContext.Current.Server.MapPath(@"~/Web.config"));


                // This should find the appSettings node (should be only one):
                XmlNode nodeAppSettings = doc.SelectSingleNode("//appSettings");
                var configNode = nodeAppSettings.SelectSingleNode("//add[@key='" + key + "']");
                if (configNode != null)
                {
                    configNode.Attributes["value"].Value = value;
                }
                else
                {
                    // Create new <add> node
                    XmlNode nodeNewKey = doc.CreateElement("add");

                    // Create new attribute for key=""
                    XmlAttribute attributeKey = doc.CreateAttribute("key");
                    // Create new attribute for value=""
                    XmlAttribute attributeValue = doc.CreateAttribute("value");

                    // Assign values to both - the key and the value attributes:
                    attributeKey.Value = key;
                    attributeValue.Value = value;

                    // Add both attributes to the newly created node:
                    nodeNewKey.Attributes.Append(attributeKey);
                    nodeNewKey.Attributes.Append(attributeValue);
                    // Add the node under the 
                    nodeAppSettings.AppendChild(nodeNewKey);
                }

                doc.Save(HttpContext.Current.Server.MapPath(@"~/Web.config"));
                return true;
            }
            catch (Exception ex)
            {

            }

            return false;
        }

        private static bool DeployAzureFunctions(ConfigModel model, string functionAppName, string storageConnection, string resourceGroupName)
        {
            try
            {
                var scriptfile = HttpContext.Current.Server.MapPath(@"~/DeploymentTemplate/deploymentScripts.ps1");


                RunspaceConfiguration runspaceConfiguration = RunspaceConfiguration.Create();

                Runspace runspace = RunspaceFactory.CreateRunspace(runspaceConfiguration);
                runspace.Open();

                RunspaceInvoke scriptInvoker = new RunspaceInvoke(runspace);

                Pipeline pipeline = runspace.CreatePipeline();

                var templateFilePath = HttpContext.Current.Server.MapPath(@"~/DeploymentTemplate/azuredeploy.json");
                var templateParamPath = HttpContext.Current.Server.MapPath(@"~/DeploymentTemplate/azuredeploy.parameters.json");
                //Here's how you add a new script with arguments
                Command myCommand = new Command(scriptfile);
                myCommand.Parameters.Add(new CommandParameter("clientId", model.ClientId));
                myCommand.Parameters.Add(new CommandParameter("clientSecret", model.ClientSecret));
                myCommand.Parameters.Add(new CommandParameter("tenantId", model.TenantId));
                myCommand.Parameters.Add(new CommandParameter("subscription", model.Subscription));
                myCommand.Parameters.Add(new CommandParameter("resourceGroupName", resourceGroupName));
                myCommand.Parameters.Add(new CommandParameter("templateFilePath", templateFilePath));
                myCommand.Parameters.Add(new CommandParameter("templateFileParameter", templateParamPath));
                myCommand.Parameters.Add(new CommandParameter("logicAppName", functionAppName));
                myCommand.Parameters.Add(new CommandParameter("functionAppName", functionAppName));
                myCommand.Parameters.Add(new CommandParameter("connectionString", storageConnection));
                myCommand.Parameters.Add(new CommandParameter("crawlerFrequency", string.Format(TimerExpressionByMinutes, model.CrawlerFrequency)));
                myCommand.Parameters.Add(new CommandParameter("schedulerFrequency", string.Format(TimerExpressionByMinutes, model.SchedulerFrequency)));
                pipeline.Commands.Add(myCommand);

                // Execute PowerShell script
                var results = pipeline.Invoke();
                var error = pipeline.Error.Read() as Collection<ErrorRecord>;// Streams property is not available

                if (error != null)
                {
                    foreach (ErrorRecord er in error)
                    {
                        Console.WriteLine("[PowerShell]: Error in cmdlet: " + er.Exception.Message);
                    }
                }
                else
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        private static Random random = new Random();

        public static string RandomString()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 9)
                .Select(s => s[random.Next(s.Length)]).ToArray()).ToLower();
        }

        private static void LoadStreamWithJson(Stream ms, string json)
        {
            StreamWriter writer = new StreamWriter(ms);
            writer.Write(json);
            writer.Flush();
            ms.Position = 0;
        }

        private static Schedules ConvertToSchedule(ScheduledRules scheduledRule)
        {
            var triggerData = JsonConvert.DeserializeObject<InputObject>(scheduledRule.TriggerData);

            return new Schedules()
            {

                ResourceName = scheduledRule.ResourceName,
                ScheduledTime = scheduledRule.ScheduledExecutionTime.ToString(),
                ChaosOperation = scheduledRule.FiOperation,
                IsRollbacked = scheduledRule.Rolledback,
                Status = (scheduledRule.FiOperation == "Restart") ? Status.Completed.ToString() : scheduledRule.Rolledback.HasValue && scheduledRule.Rolledback.Value ? scheduledRule.RollbackExecutionStatus : Status.Incomplete.ToString()
            };
        }

        private static Activities ConvertToActivity(ScheduledRules scheduledRule)
        {
            var triggerData = JsonConvert.DeserializeObject<InputObject>(scheduledRule.TriggerData);

            return new Activities()
            {
                FIChaosStartedTime = scheduledRule.ExecutionStartTime.Value,
                ResourceName = scheduledRule.ResourceName,
                ChaosStartedTime = scheduledRule.ExecutionStartTime.HasValue ? scheduledRule.ExecutionStartTime.ToString() : null,
                ChaosCompletedTime = scheduledRule.EventCompletedTime.HasValue ? scheduledRule.EventCompletedTime.ToString(): null,
                ChaosOperation = scheduledRule.FiOperation + " - " + triggerData.Action.ToString(),
                InitialState = scheduledRule.InitialState,
                FinalState = scheduledRule.FinalState,
                Status = scheduledRule.ExecutionStatus,
                Error = scheduledRule.Error,
                Warning = scheduledRule.Warning
            };       
        }


        private static Activities ConvertToActivityRollback(ScheduledRules scheduledRule)
        {
            if (scheduledRule.Rolledback == null )
            //|| !scheduledRule.Rolledback.Value
            {
                return null;
            }

            return new Activities()
            {
                //if(scheduledRule.ExecutionStartTime != null)
                FIChaosStartedTime = scheduledRule.RollbackExecutionStartTime.Value,
                ResourceName = scheduledRule.ResourceName,
                ChaosStartedTime = scheduledRule.RollbackExecutionStartTime.HasValue
                    ? scheduledRule.RollbackExecutionStartTime.ToString()
                    : null,
                ChaosCompletedTime = scheduledRule.RollbackEventCompletedTime.HasValue ? scheduledRule.RollbackEventCompletedTime.ToString() : null,
                ChaosOperation = scheduledRule.FiOperation + " - " + ActionType.Start,
                InitialState = scheduledRule.RollbackInitialState,
                FinalState = scheduledRule.RollbackFinalState,
                Status = scheduledRule.RollbackExecutionStatus,
                Error = scheduledRule.RollbackError,
                Warning = scheduledRule.RollbackWarning
            };
        }

        private static IEnumerable<ScheduledRules> GetSchedulesByDate(string fromDate, string toDate)
        {
            //double fromDateSeconds = 86400;
            //double toDateSeconds = 0;
            /*if (!string.IsNullOrWhiteSpace(fromDate))
            {
                DateTime myFromDate = DateTime.ParseExact(fromDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
                fromDateSeconds = (DateTime.UtcNow - myFromDate).TotalSeconds;
            }

            if (!string.IsNullOrWhiteSpace(toDate))
            {
                DateTime myToDate = DateTime.ParseExact(toDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
                toDateSeconds = (DateTime.UtcNow - myToDate).TotalSeconds;
            }
            */
            DateTimeOffset fromDateTimeOffset = DateTimeOffset.UtcNow.AddDays(-1);
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                fromDateTimeOffset = DateTimeOffset.ParseExact(fromDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            DateTimeOffset toDateTimeOffset= DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(toDate))
            {
                toDateTimeOffset = DateTimeOffset.ParseExact(toDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            var result = ResourceFilterHelper.QueryByFromToDate<ScheduledRules>(fromDateTimeOffset.ToUniversalTime(),
                toDateTimeOffset.ToUniversalTime(),
                "ScheduledExecutionTime",
                StorageTableNames.ScheduledRulesTableName);
            return result?.OrderByDescending(x => x.ScheduledExecutionTime);
        }
        private static IEnumerable<ScheduledRules> GetSchedulesByDateForActivities(string fromDate, string toDate)
        {
            //double fromDateSeconds = 86400;
            //double toDateSeconds = 0;
            /*if (!string.IsNullOrWhiteSpace(fromDate))
            {
                DateTime myFromDate = DateTime.ParseExact(fromDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
                fromDateSeconds = (DateTime.UtcNow - myFromDate).TotalSeconds;
            }

            if (!string.IsNullOrWhiteSpace(toDate))
            {
                DateTime myToDate = DateTime.ParseExact(toDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
                toDateSeconds = (DateTime.UtcNow - myToDate).TotalSeconds;
            }
            */
        
            DateTimeOffset fromDateTimeOffset = DateTimeOffset.UtcNow.AddDays(-1);
            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                fromDateTimeOffset = DateTimeOffset.ParseExact(fromDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            DateTimeOffset toDateTimeOffset = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(toDate))
            {
                toDateTimeOffset = DateTimeOffset.ParseExact(toDate.Split('+')[0], "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            var result = ResourceFilterHelper.QueryByFromToDateForActivities<ScheduledRules>(fromDateTimeOffset.ToUniversalTime(),
              toDateTimeOffset.ToUniversalTime(),
              "ScheduledExecutionTime",
            StorageTableNames.ScheduledRulesTableName);
            
            var results = result.Where(x => x != null &&
                                        x.ExecutionStartTime != null &&
                                        x.InitialState != null &&
                                        x.ExecutionStatus != null &&
                                        x.ResourceName != null); //x is SchedulesRules
            return results?.OrderByDescending(x => x.ScheduledExecutionTime);
            //return result?.OrderByDescending(x => x.ScheduledExecutionTime);
        }
    }
    
}
