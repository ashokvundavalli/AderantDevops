<#
Performs any jobs needed to update/refresh or fix drift on a agent host
#>
$InformationPreference = 'Continue'
$VerbosePreference = 'Continue'

Start-Transcript�-Path�"$Env:SystemDrive\Scripts\refresh-agent-host.log"�-Force
try {
    Push-Location $PSScriptRoot

    . $PSScriptRoot\configure-git-for-agent-host.ps1

    & git pull --progress --verbose

    Pop-Location
} finally {
    Stop-Transcript
}