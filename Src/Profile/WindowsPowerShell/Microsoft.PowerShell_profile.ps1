###############################################################################################################
# Used by profilesetup.cmd
# Creates a file under %userprofile%\Documents\WindowsPowerShell using this as a template.
###############################################################################################################

Write-Host "Importing Aderant Module"
Import-Module {0} -Force -DisableNameChecking

# if you want to add in debugging as default, switch these settings
#$DebugPreference = "Continue"
$DebugPreference = "SilentlyContinue"

# set up build environment
Set-Environment

# navigate to branch root folder
cd $script:BranchLocalDirectory

# show some help
Help