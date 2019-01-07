$(document).ready(function () {
  $('#inputTenantId').attr("title", "Tenant Id that represents the id of the service principal under AAD. This is also called as Directory id");
  $('#inputClientId').attr("title", "Client Id represents the id of an application created to perform fault injection operations. This is also called as application id.");
  $('#inputClientSecret').attr("title", "Secret key represents the keys that are connected to a client id to perform fault injection operations.");
  $('#selectSubscription').attr("title", "Subscription id of the azure account on which fault injection operations are to be executed");
  $('#excludedResourceGroups').attr("title", "Comma separated list of Resource groups excluded from fault injection operations. By default, the target resource group mentioned should be  added into the excluded list.");
  $('#includedResourceGroups').attr("title", "Comma separated list of Resource groups that are be exclusively included in fault injection operations. Ignoring all other settings related to resource groups.");
  $('#isVmEnabled').attr("title", "This property enables/disables the standalone VMs under fault injection operations.");
  $('#vmTerminationPercentage').attr("title", "value lies between 0 to 100. Percentage of VMs on which fault injection operations are performed simultaneously.");
  $('#avSetsEnabled').attr("title", "This property enables/disables the Availability Sets under fault injection operations.");
  $('#faultDomain').attr("title", "This property enables/disables the fault domain of an availability Sets under fault injection operations.");
  $('#updateDomain').attr("title", "This property enables/disables the update domain of an availability Sets under fault injection operations. Either of fault domain or update domain is enabled. Both can’t be enabled simultaneously.");
  $('#isAvZonesEnabled').attr("title", "This property enables/disables the AvZones under fault injection operations.");
  $('#avZoneRegions').attr("title", "");

  $('#vmssEnabled').attr("title", "This property enables/disables the VMSSs under fault injection operations.");

  $('#vmssTerminationPercentage').attr("title", "value lies between 0 to 100. Percentage of VMs in a particular VMSS on which fault injection operations are performed simultaneously.");
  $('#schedulerFrequency').attr("title", "The time frequency in minutes for which the scheduler function will run to create fault injection rules.");
  $('#rollbackFrequency').attr("title", "The time frequency in minutes for which the executor function will run to check the successfully executed rules to perform rollback operation.");
  $('#triggerFrequency').attr("title", "Time frequency in minutes for which the trigger function will run to execute the upcoming rules for the next frequency cycle.");
  $('#crawlerFrequency').attr("title", "Time frequency in minutes for which the crawler function will run to crawl the resources (VM, RGs, VMSS, AvSets).");
  $('#meanTime').attr("title", "This property will ensure that the chaos happened on the particular resource only once within this mean time.");
})