# Used by profilesetup.cmd
# Creates a file under %USERPROFILE%\Documents\WindowsPowerShell using this as a template.
$args = [System.Environment]::GetCommandLineArgs()
if ($args -like '*AppFabric 1.1*') {
    Write-Host "AppFabric installer detected. Aborting $PSCommandPath."
    return
}

Write-Host 'Importing Aderant Module.'
Import-Module PROFILE_PATH -Force -DisableNameChecking

# To enable debug logging set $DebugPreference to 'Continue'.
$DebugPreference = 'SilentlyContinue'