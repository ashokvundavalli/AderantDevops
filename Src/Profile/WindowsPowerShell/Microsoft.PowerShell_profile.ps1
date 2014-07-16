###############################################################################################################
# This profile file is not generally used as the WIKI now gets users to create their own profile files,
# which gives users more flexibility to manage their own Documents/WindowsPowerShell folder.  The wiki used
# to get user to make a symlink to this files folder in the Documents folder.
#
###############################################################################################################

write "Importing Aderant Module"
# import the ADERANT common script module
Import-Module Aderant -Force

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