<#
.Synopsis
    Runs the application server prequisites
.Description
	Runs the application server prequisites
.PARAMETER prereqRoot
	The directory the application server prerequisites are in.
.PARAMETER isLoadBalanced
	Value indicating if deployment is for a load balanced environment
.PARAMETER appFabricServiceUser
	Username to be used for appfabric
.PARAMETER appFabricServicePassword
	Password for the appFabric service user
.PARAMETER expertServiceUser
	Username to be used for expert services
.PARAMETER windowsSourcePath
	Location of the WinSxs folder
.EXAMPLE
	Install-Prereqs 
		Runs the application server prerequisists at "C:\AderantExpert\ApplicationServerPrerequisites\"
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory=$false)][string]$prereqRoot = "C:\AderantExpert\Install\ApplicationServerPrerequisites\",
    [Parameter(Mandatory=$false)][switch]$isLoadBalanced,
    [Parameter(Mandatory=$false)][string]$appFabricServiceUser = "ADERANT_AP\service.expert.qa",
    [Parameter(Mandatory=$true)][string]$appFabricServicePassword,
    [Parameter(Mandatory=$false)][string]$expertServiceUser = "ADERANT_AP\service.expert.qa",
    [Parameter(Mandatory=$false)][string]$windowsSourcePath = "C:\Windows\WinSxS"
)

process {
    $currentDirectory = (Get-Item -Path ".\" -Verbose).FullName
    Import-Module $prereqRoot\expertapplicationserver.ps1
    
    Write-Verbose "Imported Expert Application Server scripts" -verbose

    cd $prereqRoot
        
    Invoke-Command -NoNewScope -ScriptBlock {install-applicationserverprerequisites -isLoadBalanced $($isLoadBalanced) -appFabricServiceUser $appFabricServiceUser -appFabricServicePassword $appFabricServicePassword -expertServiceUser $expertServiceUser -windowsSourcePath $windowsSourcePath}
        
    cd $currentDirectory
    Write-Verbose "Finished Installing Prereqs" -verbose
}

