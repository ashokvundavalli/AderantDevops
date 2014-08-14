###############################################################################################################
# Used by profilesetup.cmd
# Creates a file under %userprofile%\Documents\WindowsPowerShell using this as a template.
# See: http://ttwiki.ap.aderant.com/wiki/index.php?title=Setup
#
###############################################################################################################

write "Importing Aderant Module"
# import the ADERANT common script module
Import-Module Aderant -Force -DisableNameChecking

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