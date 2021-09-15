<#
Performs any jobs needed to update/refresh or fix drift on a agent host
#>

$InformationPreference = 'Continue'
$VerbosePreference = 'Continue'

Start-Transcript -Path ".\RefreshAgentHostLog.txt" -Force

Push-Location $PSScriptRoot

. $PSScriptRoot\configure-git-for-agent-host.ps1

& git pull

Pop-Location

Stop-Transcript