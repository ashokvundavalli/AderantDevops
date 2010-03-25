write "------------------------------------------------------------------------------------"

# import the ADERANT common script module
Import-Module Aderant -Force

# set up build environment
Set-Environment

# navigate to branch root folder
cd (Get-FullLocalBranchRootPath)

write "------------------------------------------------------------------------------------"

##
#PSUnit: Setting up PATH environment variable to point to PSUnit framework
##
$PSUnitPath = Join-Path (Get-FullLocalBranchRootPath) "\Modules\Thirdparty.PsUnit\bin" #Modify this path to match your local PowerShell installation path
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

#show some help
Help
