<#
Performs any jobs needed to update/refresh or fix drift on a agent host
#>

$InformationPreference = 'Continue'

Start-Transcript -Path ".\RefreshAgentHostLog.txt" -Force

Push-Location $PSScriptRoot

. $PSScriptRoot\configure-git-for-agent-host.ps1

& git pull

. $PSScriptRoot\configure-disk-device-parameters.ps1
. $PSScriptRoot\optimize-drives.ps1
. $PSScriptRoot\Disable-InternetExplorerESC.ps1

Pop-Location

Stop-Transcript