Param(
       [string] [Parameter(Mandatory=$true)] $clientId,
       [string] [Parameter(Mandatory=$true)] $clientSecret,
       [string] [Parameter(Mandatory=$true)] $tenantId,
       [string] [Parameter(Mandatory=$true)] $subscription,
       [string] [Parameter(Mandatory=$true)] $resourceGroupName,
       [string] [Parameter(Mandatory=$true)] $templateFilePath,
       [string] [Parameter(Mandatory=$true)] $templateFileParameter,
       [string] [Parameter(Mandatory=$true)] $logicAppName,
       [string] [Parameter(Mandatory=$true)] $functionAppName,
       [string] [Parameter(Mandatory=$true)] $connectionString,
	   [string] [Parameter(Mandatory=$true)] $crawlerFrequency,
	   [string] [Parameter(Mandatory=$true)] $schedulerFrequency
)

$securePassword = $clientSecret | ConvertTo-SecureString -AsPlainText -Force
$cred = new-object -typename System.Management.Automation.PSCredential `
     -argumentlist $clientId, $securePassword

Add-AzureRmAccount -Credential $cred -Tenant $tenantId -ServicePrincipal

New-AzureRmResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile $templateFilePath -TemplateParameterFile $templateFileParameter -LogicAppName:$logicAppName -functionAppName:$functionAppName -configConnectionString:$connectionString -crawlerFrequency:$crawlerFrequency -schedulerFrequency:$schedulerFrequency