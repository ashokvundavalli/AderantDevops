###############################################################################################################
# Used by profilesetup.cmd
# Creates a file under %userprofile%\Documents\WindowsPowerShell using this as a template.
###############################################################################################################
$args = [System.Environment]::GetCommandLineArgs()
if ($args -like "*AppFabric 1.1*") {
    Write-Host "AppFabric installer detected. Aborting $PSCommandPath"
    return
}

Write-Host "Importing Aderant Module"
Import-Module PROFILE_PATH -Force -DisableNameChecking

<#
#If you want to add in debugging as default switch these setting
$DebugPreference = "Continue"
#>
$DebugPreference = "SilentlyContinue"

# set up build environment
Set-Environment

# navigate to branch root folder
cd $script:BranchLocalDirectory

#show some help
Help