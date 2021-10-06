<#
.SYNOPSIS
    Runs clean up scripts to remove any files or services that may not be reliability removed when a build fails.
.DESCRIPTION
    Handles the case where the tool that installs and thus owns the files and services is unable perform a clean uninstall.
    This script duplicates logic from tools like Deployment Manager and makes assumption about file locations and names
    but providing a clean environment is more important than minimizing code duplication.
#>
Set-StrictMode -Version "Latest"

Start-Transcript -Path "$Env:SystemDrive\Scripts\cleanup.task.txt" -Force

try {
    # This is not good practice as it changes global state by our execution
    # environment is expected to be a scheduled task so it is acceptable
    $InformationPreference = "Continue"
    $VerbosePreference = "Continue"

    . $PSScriptRoot\remove-expert-servces.ps1
    . $PSScriptRoot\iis-cleanup.ps1

    # Web unit testing can leave chrome.exe hanging around
    Stop-Process -Name "chrome" -Verbose -Force -ErrorAction SilentlyContinue

    Get-Service MSSQLSERVER | Restart-Service -Force -Verbose
} finally {
    Stop-Transcript
}