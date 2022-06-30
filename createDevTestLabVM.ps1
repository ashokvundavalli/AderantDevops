.\Create-LabVirtualMachine.ps1 -ResourceGroupName '<lab resource group name>' -LabName '<lab name>' -userName '<VM administrative username>' -password '<VM admin password>' -VMName '<VM name to create>'

[CmdletBinding()]

Param(
[Parameter(Mandatory = $false)]  $SubscriptionId,
[Parameter(Mandatory = $true)]   $LabResourceGroup,
[Parameter(Mandatory = $true)]   $LabName,
[Parameter(Mandatory = $true)]   $NewVmName,
[Parameter(Mandatory = $true)]   $UserName,
[Parameter(Mandatory = $true)]   $Password
)

pushd $PSScriptRoot

try {
   if ($SubscriptionId -eq $null) {
       $SubscriptionId = (Get-AzContext).Subscription.SubscriptionId
   }

   $API_VERSION = '2016-05-15'
   $lab = Get-AzResource -ResourceId "/subscriptions/$SubscriptionId/resourceGroups/$LabResourceGroup/providers/Microsoft.DevTestLab/labs/$LabName"

   if ($lab -eq $null) {
      throw "Unable to find lab $LabName resource group $LabResourceGroup in subscription $SubscriptionId."
   }

   $virtualNetwork = @(Get-AzResource -ResourceType  'Microsoft.DevTestLab/labs/virtualnetworks' -ResourceName $LabName -ResourceGroupName $lab.ResourceGroupName -ApiVersion $API_VERSION)[0]

   #The preceding command puts the VM in the first allowed subnet in the first virtual network for the lab.
   #If you need to use a specific virtual network, use | to find the network. For example:
   #$virtualNetwork = @(Get-AzResource -ResourceType  'Microsoft.DevTestLab/labs/virtualnetworks' -ResourceName $LabName -ResourceGroupName $lab.ResourceGroupName -ApiVersion $API_VERSION) | Where-Object Name -EQ "SpecificVNetName"

   $labSubnetName = $virtualNetwork.properties.allowedSubnets[0].labSubnetName

   #Prepare all the properties needed for the createEnvironment call.
   # The properties are slightly different depending on the type of VM base.
   # The virtual network setup might also affect the properties.
   
   $parameters = @{
      "name"      = $NewVmName;
      "location"  = $lab.Location;
      "properties" = @{
         "labVirtualNetworkId"     = $virtualNetwork.ResourceId;
         "labSubnetName"           = $labSubnetName;
         "notes"                   = "Windows Server 2019 Datacenter";
         "osType"                  = "windows"
         "expirationDate"          = "2022-12-01"
         "galleryImageReference"   = @{
            "offer"     = "WindowsServer";
            "publisher" = "MicrosoftWindowsServer";
            "sku"       = "2019-Datacenter";
            "osType"    = "Windows";
            "version"   = "latest"
         };
         "size"                    = "Standard_DS2_v2";
         "userName"                = $UserName;
         "password"                = $Password;
         "disallowPublicIpAddress" = $true;
         "dataDiskParameters" = @(@{
           "attachNewDataDiskOptions" = @{
               "diskName" = "adddatadisk"
               "diskSizeGiB" = "1023"
               "diskType" = "Standard"
               }
         "hostCaching" = "ReadWrite"
         })
      }
   }
   
   #The following line has the same effect as invoking the
   # https://azure.github.io/projects/apis/#!/Labs/Labs_CreateEnvironment REST API

   Invoke-AzResourceAction -ResourceId $lab.ResourceId -Action 'createEnvironment' -Parameters $parameters -ApiVersion $API_VERSION -Force -Verbose
}
finally {
  popd
}