###############################################################################################################
# This profile file is not generally used as teh WIKI now gets users to createt their own profile files,
# which gives users more flexibility to manage their own Documents/WindowsPowerShell folder.  The wiki used
# to get user to make a symlink to this files folder in the Documents folder.
#
# The PS-Unit stuff is still there 
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

#only setup PsUnit for ISE.
if( $psISE){
    ##
    #PSUnit: Setting up PATH environment variable to point to PSUnit framework
    ##
    $PSUnitPath = Join-Path $script:BranchLocalDirectory "\Modules\Thirdparty.PsUnit\bin" #Modify this path to match your local PowerShell installation path

    if(Test-Path $PSUnitPath){
        $shell = (Join-Path $PSUnitPath  PSUnit.ISE.ps1)
        # run the script   
        &($shell) 

        #Make sure to only append this path once
        if (!($env:path -like "*$PSUnitPath*"))
        {
            $env:path = $env:path + ";$PSUnitPath"
        }
        #PSUnit: Setting PSUNIT_HOME environment variable to point to PSUnit framework
        If(! $(Test-Path -Path "env:PSUNIT_HOME"))
        {
            New-Item -Path "env:" -Name "PSUNIT_HOME" -value $PSUnitPath
        }
    }else{
        Write-Host "PSUnit was not found at [$PSUnitPath]"
    }
}

