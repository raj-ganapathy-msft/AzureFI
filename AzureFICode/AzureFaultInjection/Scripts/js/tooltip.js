$(document).ready(function () {

  $('#tooltip-tenant-id').attr("title", "Azure Active Directory (AAD) Id used to create Service Principal. This is also called Directory Id.");
  $('#tooltip-client-id').attr("title", "Application Id for fault injection operations.");
  $('#tooltip-client-secret').attr("title", "Secret Key associated with Application Id for fault injection operations.");
  $('#tooltip-subscription').attr("title", "Azure Subscription Id in scope for fault injection operations.");
  $('#tooltip-exclude-resourcegroups').attr("title", "Resource Group(s) to be excluded from fault injection operations.");
  $('#tooltip-vm-percentage').attr("title", "% of single Virtual Machines subjected to fault injection operations simultaneously.");
  $('#tooltip-vmss-percentage').attr("title", "% of instances in a particular Scale Set subjected to fault injection operations simultaneously.");
    $('#tooltip-scheduler-frequency').attr("title", "Time between AzureFI initiating fault operations.   Note: The values should be at least twice the Power Cycle Schedule ");
    $('#tooltip-rollback-frequency').attr("title", "Time between powering off and powering on a Virtual Machine. Note: The value should not be more than half the Scheduler Frequency and the sum of Crawler Frequency and Meantime betweenw FI on a VM ");
    $('#tooltip-crawler-frequency').attr("title", "Time between AzureFI crawl operations to identify new resources in the selected Resource Groups. Note: Scheduler Frequency should be less than the sum of Crawler Frequency and Meantime b/w FI on a VM ");
    $('#tooltip-meantime').attr("title", "Time between a specific Virtual Machine picked for fault injection operations. Note: Scheduler Frequency should be less than the sum of Crawler Frequency and Meantime b/w FI on a VM");
  $('#tooltip-availability-sets').attr("title", "All vm instances belonging to particular fault or update domain subjected to chosen fault operations simultaneously.");
  $('#tooltip-availability-zones').attr("title", "All vm instances in a zone subjected to chosen fault operations simultaneously.");
  $('#tooltip-loadbalancer-percentage').attr("title", "% of vm instances behind a load balancer subjected to fault injection operations simultaneously.");

  //$('[data-toggle="tooltip"]').tooltip();
})