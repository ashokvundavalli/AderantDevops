<#
Performs any jobs needed to update/refresh or fix drift on a agent host
#>

. $PSScriptRoot\configure-git-for-agent-host.ps1

& git pull