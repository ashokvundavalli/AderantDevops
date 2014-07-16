[string]$BranchRoot = ""
[string]$global:BranchName
[string]$global:BranchLocalDirectory
[string]$global:BranchServerDirectory
[string]$global:BranchModulesDirectory
[string]$global:BranchBinariesDirectory
[string]$global:BranchExpertSourceDirectory
[string]$global:BranchExpertVersion
[string]$global:BranchEnvironmentDirectory
[string]$global:BuildScriptsDirectory
[string]$global:PackageScriptsDirectory
[string]$global:ModuleCreationScripts 
[string]$global:ProductManifest
[string]$global:CurrentModuleName
[string]$global:CurrentModulePath
[string]$global:CurrentModuleBuildPath
[string[]]$global:LastBuildBuiltModules
[string[]]$global:LastBuildRemainingModules
[string[]]$global:LastBuildGetLocal
[switch]$global:LastBuildGetDependencies
[switch]$global:LastBuildCopyBinaries
[switch]$global:LastBuildDownstream
[switch]$global:LastBuildGetLatest

<#
Branch information
#>
function Set-BranchPaths {
    #initialise from default setting
    Write-Debug "Setting information for branch from your defaults"    
    $global:BranchLocalDirectory = (GetDefaultValue "DevBranchFolder").ToLower()                  
    $global:BranchName = ResolveBranchName $global:BranchLocalDirectory    
    $global:BranchServerDirectory = (GetDefaultValue "DropRootUNCPath").ToLower()
    $global:BranchModulesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Modules
    $global:BranchBinariesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Binaries
    if((Test-Path $global:BranchBinariesDirectory) -ne $true){ 
        #Ignoring so that we don't randomly create folders
        #[system.io.directory]::CreateDirectory($global:BranchBinariesDirectory)         
    }
    $global:BranchEnvironmentDirectory =Join-Path -Path $global:BranchLocalDirectory -ChildPath \Environment  
    
    if((Test-Path $global:BranchLocalDirectory) -ne $true){
        Write-Host ""
        Write-Host "*********************************************************************************************************************************"
        Write-Warning "The branch directory does not exist. Please call Set-ExpertBranchInfo for initial setup of local directory and branch info"
        Write-Host "*********************************************************************************************************************************"
        Write-Host ""

        throw "Please setup environment"
    }
}

<#
Set-ExpertSourcePath is called on startup and SwitchBranchTo.  It sets $global:BranchExpertVersion and $global:BranchExpertSourceDirectory.
Pre-8.0 environments still use the old folder structure where everything was in the binaries folder, so BranchExpertSourceDirectory is set
according to the setting in the expertmanifest.xml file.
#>
function Set-ExpertSourcePath {
    [xml]$manifest = Get-Content $global:ProductManifest
    $global:BranchExpertVersion = $manifest.ProductManifest.ExpertVersion
    
    if ($global:BranchExpertVersion.StartsWith("8")) {
        $global:BranchExpertSourceDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Binaries\ExpertSource
        if ((Test-Path $global:BranchExpertSourceDirectory) -ne $true){ 
            [System.IO.Directory]::CreateDirectory($global:BranchExpertSourceDirectory) | Out-Null
        }	
    } else {
        $global:BranchExpertSourceDirectory = $global:BranchBinariesDirectory
    }
}

function Set-ScriptPaths{    
    $global:BuildScriptsDirectory = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\Build
    $global:PackageScriptsDirectory = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\Package
    $global:ModuleCreationScripts = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\ModuleCreator
    $global:ProductManifest = Join-Path -Path $global:PackageScriptsDirectory -ChildPath \ExpertManifest.xml
}

<#
    Initialise functions from Build-Libraries.ps1
#>
function Initialise-BuildLibraries{    
    invoke-expression "$BuildScriptsDirectory\Build-Libraries.ps1"    
}
    
    
function ResolveBranchName($branchPath){    
    if(IsMainBanch $branchPath){            
        $name = "MAIN"
    }elseif(IsDevBanch $branchPath){
        $name = $branchPath.Substring($branchPath.LastIndexOf("dev\"))
    }elseif(IsReleaseBanch $branchPath){        
        $name = $branchPath.Substring($branchPath.LastIndexOf("releases\"))
    }
    return $name
}

function IsDevBanch([string]$name){
    return $name.ToLower().Contains("dev")
}

function IsReleaseBanch([string]$name){
    return $name.ToLower().Contains("releases")
}

function IsMainBanch([string]$name){
    return $name.ToLower().Contains("main")
}

# Called from SwitchBranchTo
function Set-ChangedBranchPaths([string]$name){
    #initialise from default setting
    write-host "change branch to $name"                
    
    # container as in dev or release
    $newBranchContainer = ""        
    $previousBranchContainer = ""
    
    # name of branch or MAIN
    $newBranchName = ""
    $previousBranchName = ""   
    
    #was the pervious branch MAIN?        
    [bool]$changeToContainerFromMAIN = $false
    
    # get the new and previous name a container parts
    if((IsDevBanch $global:BranchName) -or (IsReleaseBanch $global:BranchName)){
        $previousBranchContainer = $global:BranchName.Substring(0,$global:BranchName.LastIndexOf("\"))
        $previousBranchName = $global:BranchName.Substring($global:BranchName.LastIndexOf("\")+1)
    }elseif((IsMainBanch $global:BranchName)){        
        $previousBranchName = "MAIN"
        $changeToContainerFromMAIN = $true
    }
    
    if((IsDevBanch $name) -or (IsReleaseBanch $name)){    
        $newBranchContainer = $name.Substring(0,$name.LastIndexOf("\"))     
        $newBranchName = $name.Substring($name.LastIndexOf("\")+1)
    }elseif((IsMainBanch $name)){
        $newBranchName = "MAIN"    
    }    
    
    if($changeToContainerFromMAIN){
        Switch-BranchFromMAINToContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }else{
        Switch-BranchFromContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }    
    #Set common paths
    $global:BranchModulesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Modules" )
    
    $global:BranchBinariesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Binaries" )    
    if((Test-Path $global:BranchBinariesDirectory) -eq $false){    
        New-Item -Path $global:BranchBinariesDirectory -ItemType Directory
    }
    
    $global:BranchEnvironmentDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Environment" )   
    if((Test-Path $global:BranchEnvironmentDirectory) -eq $false){    
        New-Item -Path $global:BranchEnvironmentDirectory -ItemType directory
    }    
}

<#
 we need to cater for the fact MAIN is the only branch and not a container like dev or release
#>
function Switch-BranchFromMAINToContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName){
    #change name and then container and remove extra backslash's 
    $global:BranchName = ($global:BranchName -replace $previousBranchName,$newBranchName)
    $global:BranchName = $newBranchContainer+"\"+$global:BranchName
    #strip MAIN then add container and name    
    $global:BranchLocalDirectory = $global:BranchLocalDirectory.Substring(0,$global:BranchLocalDirectory.LastIndexOf("\")+1)
    $global:BranchLocalDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath( Join-Path  -Path $newBranchContainer -ChildPath $newBranchName))
    #strip MAIN then add container and name            
    $global:BranchServerDirectory = $global:BranchServerDirectory.Substring(0,$global:BranchServerDirectory.LastIndexOf("\")+1) 
    $global:BranchServerDirectory = (Join-Path -Path $global:BranchServerDirectory -ChildPath( Join-Path  -Path $newBranchContainer -ChildPath $newBranchName))
    
    $global:BranchServerDirectory = [System.IO.Path]::GetFullPath($global:BranchServerDirectory)
} 

<#
 we dont have to do anything special if we change from a container to other branch type
#>
function Switch-BranchFromContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName){
    #change name and then container and remove extra backslash's 
    $global:BranchName = ($global:BranchName -replace $previousBranchName,$newBranchName)
    $global:BranchName = ($global:BranchName -replace $previousBranchContainer,$newBranchContainer)
    if(IsMainBanch $global:BranchName){
        $global:BranchName = [System.Text.RegularExpressions.Regex]::Replace($global:BranchName,"[^1-9a-zA-Z_]",""); 
    }
        
    $global:BranchLocalDirectory = ($global:BranchLocalDirectory -replace $previousBranchName,$newBranchName)
    $global:BranchLocalDirectory = (Resolve-Path -Path ($global:BranchLocalDirectory -replace $previousBranchContainer,$newBranchContainer)).Path
                
    $global:BranchServerDirectory = ($global:BranchServerDirectory -replace $previousBranchName,$newBranchName)
    $global:BranchServerDirectory = (Resolve-Path -Path ($global:BranchServerDirectory -replace $previousBranchContainer,$newBranchContainer)).ProviderPath
    
    $global:BranchServerDirectory = [System.IO.Path]::GetFullPath($global:BranchServerDirectory)    
}

function Set-CurrentModule($name){                
    if (!($name)) {        
        if(!($global:CurrentModuleName)){
            Write-Warning "No current module is set"        
            return        
        }else{                                         
            Write-Host "The current module is [$global:CurrentModuleName] on the branch [$global:BranchName]"
            return 
        }
    }        
    Write-Host "Setting information for the module $name"
    $global:CurrentModuleName = $name
    Write-Debug "Current module [$global:CurrentModuleName]"    
    $global:CurrentModulePath = Join-Path -Path $global:BranchModulesDirectory -ChildPath $global:CurrentModuleName
    if((Test-Path $global:CurrentModulePath) -eq $false){
        Write-Warning "the module [$global:CurrentModuleName] does not exist, please check the spelling."
        $global:CurrentModuleName = ""
        $global:CurrentModulePath = ""
        return
    }    
    Write-Debug "Current module path [$global:CurrentModulePath]"
    $global:CurrentModuleBuildPath = Join-Path -Path $global:CurrentModulePath -ChildPath \Build        
}

function global:Write-ToHostEnvironmentDetails{
    Write-Host ""
    Write-Host "-----------------------------"
    Write-Host "Local Branch Information"
    Write-Host "-----------------------------"
    Write-Host "Name :" $global:BranchName
    Write-Host "Path :" $global:BranchLocalDirectory    
    Write-Host ""
    Write-Host "-----------------------------"
    Write-Host "Server Branch Information"
    Write-Host "-----------------------------"
    Write-Host "Path :" $global:BranchServerDirectory
    Write-Host ""
    Write-Host "-----------------------------"
    Write-Host "Current Module Information"   
    Write-Host "-----------------------------"
    Write-Host "Name :" $global:CurrentModuleName
    Write-Host "Path :" $global:CurrentModulePath
    Write-Host ""
}

<#
 gets the folder the current script is executing from
 #>
function Get-ScriptFolder {
     return Split-Path (Get-Variable MyInvocation).Value.ScriptName
} 

<#
 return the connection string to be used for the sitemap builder
#>
function Get-SystemMapConnectionString {
    return (GetDefaultValue "systemMapConnectionString").ToLower()
}

function New-BuildModule($name){
    $shell = ".\create_module.ps1 -ModuleName $name -DestinationFolder $global:BranchModulesDirectory"    
    pushd $global:ModuleCreationScripts
    invoke-expression $shell
    popd
}

<# 
.Synopsis 
    Deploys the database project to your database defined in the environment manifest
.Description
    Deploys the database project, thereby updating your database to the correct definition.
.PARAMETER interactive
    Starts DBGEN in interactive mode
#>
function Update-Database([string]$manifestName, [switch]$interactive) {    
    [string]$fullManifest = ''

    if ($global:BranchExpertVersion.StartsWith("8")) {
        $fullManifest = Join-Path -Path $global:BranchBinariesDirectory -ChildPath 'environment.xml'
    } else {
        if  ($manifestName -eq $null) {
            write "Usage: Update-BranchDatabase <manifestName>"
            return
        } else {
            $fullManifest = Join-Path -Path $global:BranchEnvironmentDirectory -ChildPath "\$manifestName.environment.xml"
        }
    }   

    
    if (Test-Path $fullManifest) {
        Write-Debug "Using manifest: $fullManifest"

        # Update the DBGEN defaults for development churn
        [Xml]$manifest = Get-Content $fullManifest
        $server = $manifest.environment.expertDatabaseServer.serverName
        $db = $manifest.environment.expertDatabaseServer.databaseConnection.databaseName

        $query = @"
begin tran
update CMS_DB_OPTION set OPTION_VALUE = 'Y', LAST_MODIFIED = getdate()
where OPTION_CODE in ('PERMIT_NULLLOSS', 'PERMIT_DATALOSS')
commit
"@

        $command = "sqlcmd -S $server -d $db -E -Q `"$query`""
        Invoke-Expression $command
        
        $shell = "powershell -NoProfile -NoLogo $global:PackageScriptsDirectory\AllowDotNet40Assemblies.ps1"
        Invoke-Expression $shell
        
        $shell = "powershell -NoProfile -NoLogo `"$global:PackageScriptsDirectory\DeployDatabase.ps1`" -environmentManifestPath `"$fullManifest`" -expertSourceDirectory `"$global:BranchExpertSourceDirectory`" -interactive:$" + $interactive
        # Invoke-Expression falls on its face here due to a bug with [switch] - if used the switch argument cannot be converted to a switch parameter
        # which is very annoying
        # http://connect.microsoft.com/PowerShell/feedback/details/742084/powershell-v2-powershell-cant-convert-false-into-swtich-when-using-file-param
        cmd /c $shell
        popd		
    } else {
        Write-Error "No manifest specified at path: $fullManifest"
    }	
}

<# 
.Synopsis 
    Installs the latest version of the Software Factory
.Description
    Will uninstall the previous SoftwareFactory.vsix and then install the latest version from the drop location
#>
function Install-LatestSoftwareFactory() {
    $softwareFactoryVSIXId = 'Aderant.SoftwareFactory.vsix'
    $softwareFactoryVSIX = 'AderantSoftwareFactory.vsix'
    $localSoftwareFactoryInstallDirectory = (Join-Path $global:BranchLocalDirectory \SoftwareFactoryInstall)
    
    [xml]$manifest = Get-Content $global:ProductManifest
    [System.Xml.XmlNode]$softwareFactoryModule = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object{ $_.Name.Contains("SoftwareFactory")}
    
    invoke-expression "$BuildScriptsDirectory\Build-Libraries.ps1"
    $dropPathForSoftwareFactoryVSIX = (GetPathToBinaries $softwareFactoryModule $global:BranchServerDirectory)
    
    #bug that prevents removal unless we force three times
    VSIXInstaller.exe /q /uninstall:$softwareFactoryVSIXId 
    VSIXInstaller.exe /q /uninstall:$softwareFactoryVSIXId
    VSIXInstaller.exe /q /uninstall:$softwareFactoryVSIXId
    
    if(!(Test-Path $localSoftwareFactoryInstallDirectory)) {
        New-Item $localSoftwareFactoryInstallDirectory -ItemType directory
    }else{
        DeleteContentsFrom $localSoftwareFactoryInstallDirectory
    }
    
    CopyContents -copyFrom $dropPathForSoftwareFactoryVSIX -copyTo $localSoftwareFactoryInstallDirectory
                
    pushd $localSoftwareFactoryInstallDirectory
    VSIXInstaller.exe /q $softwareFactoryVSIX
    popd
              
    write "updated $softwareFactoryVSIX"
}

# builds the current module using default parameters
function Start-BuildForBranch {
    $shell = ".\Build-AllModules-Local.ps1"    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd   
}

# builds the current module using default parameters
function Start-BuildForCurrentModule([string]$clean) {
    # Parameter must be a string as we are shelling out which we can't pass [switch] to
    $shell = ".\BuildModule.ps1 -moduleToBuildPath $global:CurrentModulePath -dropRoot $global:BranchServerDirectory -cleanBin $clean"    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd   
}

# gets dependencies for current module using default parameters
function Get-DependenciesForCurrentModule {    
    if (Test-Path $global:BuildScriptsDirectory\LoadDependencies.ps1) {
        $shell = ".\LoadDependencies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory -buildScriptsDirectory $global:BuildScriptsDirectory"    
        pushd $global:BuildScriptsDirectory
        Invoke-Expression $shell
        popd
        return    
    }
    
    $shell = ".\LoadDependancies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory -buildScriptsDirectory $global:BuildScriptsDirectory"    
    pushd $global:BuildScriptsDirectory
    Invoke-Expression $shell
    popd
}


# gets dependencies for current module using default parameters
function Get-LocalDependenciesForCurrentModule {  
    if (Test-Path $global:BuildScriptsDirectory\Load-LocalDependencies.ps1) {
        $shell = ".\Load-LocalDependencies.ps1 -moduleName $global:CurrentModuleName -localModulesRootPath $global:BranchModulesDirectory -serverRootPath $global:BranchServerDirectory"    
        pushd $global:BuildScriptsDirectory
        invoke-expression $shell
        popd
        return    
    }
 
    $shell = ".\Load-LocalDependancies.ps1 -moduleName $global:CurrentModuleName -localModulesRootPath $global:BranchModulesDirectory -serverRootPath $global:BranchServerDirectory"    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd
}

function Copy-BinariesFromCurrentModule  {

    if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {    
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {        
        pushd $global:BuildScriptsDirectory     
        ResolveAndCopyUniqueBinModuleContent -modulePath $global:CurrentModulePath -copyToDirectory $global:BranchExpertSourceDirectory    
        popd  
    }  
    
    # We now need to move/copy the deployment manager files depending on the version we are working on.  There are three different scenarios:
    # 1. 7SP2 and earlier - all files are in Binaries folder.
    # 2. 7SP4 - all deployment files listed in ..\Build.Infrastructure\Src\Package\deploymentManagerFilesList.txt are moved to Binaries\DeploymentManager folder
    #    see GetProduct.ps1 (Function MoveDeploymentManagerFilesToFoler) for details.
    # 3. 8 and later - all deployment files listed in ..\Build.Infrastructure\Src\Package\deploymentManagerFilesList.txt are moved to Binaries\Deployment folder.
    
    MoveDeploymentFiles $global:BranchExpertVersion $global:BranchBinariesDirectory $global:BranchExpertSourceDirectory 
    
    
}

<# 
.Synopsis 
    Runs a GetProduct for the current branch 
.Description
    Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
    This will always return the pdb's. 
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries    
#>
function Get-Product {
    $buildInfrastructure = $global:PackageScriptsDirectory.Replace("Package", "")
    $shell = ".\GetProduct.ps1 -productManifestPath $global:ProductManifest -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString) -buildLibrariesPath $buildInfrastructure"
    pushd $global:PackageScriptsDirectory
    invoke-expression $shell | Out-Host
    popd
}

<# 
.Synopsis 
    Runs a GetProduct for the current branch 
.Description
    Uses the given manifest path
    This will always return the pdb's. 
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries    
#>
function Get-ProductDefinedByGivenManifest([string]$myManifestPath) {

    if(Test-Path $myManifestPath){    
        $shell = ".\GetProduct.ps1 -productManifestPath $myManifestPath -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString)"    
        pushd $global:PackageScriptsDirectory
        invoke-expression $shell | Out-Host
        popd
    }else{
        Write-Error "GetProduct Failed as the given manifest path [$myManifestPath] was not found"
    }
}

<# 
.Synopsis 
    Runs a GetProduct for the current branch but will not contain the pdb's
.Description
    Uses the expertmanifest from the local Build.Infrastructure\Src\Package directory.
    No pdb's returned 
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries    
#>
function Get-ProductNoDebugFiles {
    $shell = ".\GetProduct.ps1 -productManifestPath $global:ProductManifest -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -systemMapConnectionString (Get-SystemMapConnectionString)"    
    pushd $global:PackageScriptsDirectory
    invoke-expression $shell | Out-Host
    popd
}

<# 
.Synopsis 
    Gets the latest product zip from the BuildAll output and unzips to your BranchBinariesDirectory
.Description   
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries    
#>
function Get-ProductZip{
    Write-Host "Getting latest product zip from [$BranchServerDirectory]"
    $zipName = "ExpertBinaries.zip"
    [string]$pathToZip = (PathToLatestSuccessfulPackage -pathToPackages $BranchServerDirectory -packageZipName $zipName)
                      
    $pathToZip = $pathToZip.Trim() 
    DeleteContentsFromExcludingFile -directory $BranchBinariesDirectory "environment.xml"
    Copy-Item -Path $pathToZip -Destination $BranchBinariesDirectory
    $localZip =  (Join-Path $BranchBinariesDirectory $zipName)
    Write-Host "About to extract zip to [$BranchBinariesDirectory]"
    $shellApplication = new-object -com shell.application
    $zipPackage = $shellApplication.NameSpace($localZip)
    $destinationFolder = $shellApplication.NameSpace($global:BranchBinariesDirectory)
    $destinationFolder.CopyHere($zipPackage.Items())
    Write-Host "Finished extracting zip"
}

<# 
.Synopsis 
    Builds a list of modules
.Description   
    Automatically orders the modules according to their dependencies. Automatically handles 
    copying dependencies between the list of modules. Use -getDependencies $true and -copyBinaries $true
    to get dependencies before local dependency management and starting each module build and/or to copy 
    the output to the binaries location.
.PARAMETER workflowModuleNames
    An array of workflow module names
.PARAMETER changeset
    If specified will build all modules in the current changeset. This overrides workflowModuleNames
.PARAMETER getDependencies
    If specified, will call get-depedencies before copying the output from any other specified modules already built 
    and running the build of this module.
.PARAMETER copyBinaries
    If specified, will copy the output of each module build to the binaries location.
.PARAMETER downstream
    If specified will build the sepcified modules and any modules which depend on them
.PARAMETER getLatest
    If specified will get the latest source for the module from TFS before building.
.PARAMETER continue
    If specified will continue the last build starting at a build for the last module that failed
.PARAMETER getLocal
    If specified will get this comma delimited list of dependencies locally instead of from the drop folder
.PARAMETER exclude
    If specified will exclude this comma delimited list of modules from the build
.PARAMETER skipUntil
    A module name that if specified will build the list of modules as normal but skip the ones before the specified module
.EXAMPLE
        Build-ExpertModules
    Will build the current module. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Workflow
    Will build the "Libraries.Workflow" module. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Workflow, Libraries.Foundation, Libraries.Presentation
    Will build the specified modules in the correct order according to their dependencies (Libraries.Foundation, Libraries.Presentation, Libraries.Workflow). 
    The output of each modules will be copied to the dependencies folder of the others before they are built, if are dependent.
    No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules -changeset
    Will build the modules which have files currently checked out for edit. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getLatest
    Will build the modules specified after get the latest source from TFS. No dependencies will be prefetched and the output will not be copied to the binaries folder
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getDependencies -copyBinaries -downstream
    Will build the specified module and any modules which directly or indirectly depend on it.
    The dependencies will be fetched before building and the output will be copied to the binaries folder.  
.EXAMPLE
        Build-ExpertModules Libraries.Foundation -getDependencies -copyBinaries -downstream -skipUntil Libraries.Workflow
    will queue a build from the specified module and any modules which directly or indirectly depend on it but skip actually building any module
    until it reaches Libraries.Workflow. Useful for large builds that have failed somewhere in between and we want to pipck up from where we left off.
    The dependencies will be fetched before building and the output will be copied to the binaries folder.  
    
#>
function Build-ExpertModules {
    param ([string[]]$workflowModuleNames, [switch] $changeset = $false, [switch] $clean = $false, [switch]$getDependencies = $false, [switch] $copyBinaries = $false, [switch] $downstream = $false, [switch] $getLatest = $false, [switch] $continue, [string[]] $getLocal, [string[]] $exclude, [string] $skipUntil)
    $moduleBeforeBuild
    
    $currentWorkingDirectory = Get-Location
    
    if (!$workflowModuleNames){
      if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
         $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name})
         $workflowModuleNames = @($moduleBeforeBuild)
      }
    }
    $builtModules = @{}
    
    if(!$getLocal){
        [string[]]$getLocal = @()
    }
    
    if(!$exclude){
        [string[]]$exclude = @()
    }
    
    #write $workflowModuleNames
    
    if($continue) {
        if(!$global:LastBuildRemainingModules){
            write "No previously failed build found"
            return
        }
        $builtModules = $global:LastBuildBuiltModules
        $workflowModuleNames = $global:LastBuildRemainingModules
        $getDependencies = $global:LastBuildGetDependencies
        $copyBinaries = $global:LastBuildCopyBinaries
        $downstream = $global:LastBuildDownstream
        $getLatest = $global:LastBuildGetLatest
        $getLocal = $global:LastBuildGetLocal
        $exclude = $global:LastBuildExclude
    }
        
    if ($changeset) {
        $workflowModuleNames = Get-ExpertModulesInChangeset
    }
    
    # Set the new last build configuration
    $global:LastBuildGetDependencies = $getDependencies
    $global:LastBuildCopyBinaries = $copyBinaries
    $global:LastBuildDownstream = $downstream
    $global:LastBuildGetLatest = $getLatest
    $global:LastBuildRemainingModules = $workflowModuleNames
    $global:LastBuildGetLocal = $getLocal
    $global:LastBuildExclude = $exclude

    if (!($workflowModuleNames)) {
        write "No modules specified."
        return
    }
    
    if((Test-Path $BranchLocalDirectory) -ne $true){
        write "Branch Root path does not exist: '$BranchLocalDirectory'"
    }    
        
    [Array]$modules = Sort-ExpertModulesByBuildOrder -BranchPath $BranchLocalDirectory -ModuleNames $workflowModuleNames
    
    if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
        Write-Warning "After sorting builds by order the following modules were excluded."
        Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

        Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -PassThru

        $message = "Do you want to continue anyway?"
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($null, $message, $options, 0)
        
        if ($result -ne 0) {
            write "Module(s) not found."                      
            return
        } 
    }        
    
    if ($exclude -eq $null) {
        $exclude = @()
    }

    if ($downstream -eq $true) {
        $downstreamModules = @()
        foreach($module in $modules) {
          $downstreams = Get-DownstreamExpertModules -ModuleName $module.Name -BranchPath $BranchLocalDirectory
          foreach ($downstreamModule in $downstreams){
            $downstreamModules += $downstreamModule
          }
          $downstreamModules += $module
        }
        $modules = $downstreamModules
        $modules = Sort-ExpertModulesByBuildOrder -BranchPath $BranchLocalDirectory -Modules $modules
        $modules = $modules | Where { $_.ModuleType -ne [DependencyAnalyzer.ModuleType]::Test }

        $message = "Do you want to skip modules which cannot be built locally?"
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($null, $message, $options, 0)
        
        if ($result -eq 0) {
            $skipList = @('Applications.Marketing', 'Installs.Marketing', 'Applications.FoundTimeOutlookAddIn', 'Applications.ExpertOutlookAddIn')
            foreach ($m in $skipList) {
                Write-Host "Skipping $m"
                $exclude += $m
            }
        } 
    }
    
    $modules = $modules | Where {$exclude -notcontains $_}
    
    write ""
    write "********** Build Overview *************"
    $count = 0
    $weHaveSkipped = $false
    foreach($module in $modules){
      $count++
      $skipMarkup = ""
      if($skipUntil -eq $module){
             $weHaveSkipped = $true
      }
      if($skipUntil -and $weHaveSkipped -ne $true){
        $skipMarkup = " (skipped)"
      }
      write "$count. $module $skipMarkup"
    }
    write ""
    write ""
    write "Press Ctrl+C to abort"
    Start-Sleep -m 2000

    $weHaveSkipped = $false

    foreach($module in $modules){
      if($skipUntil -eq $module){
             $weHaveSkipped = $true
      }

      # If the user specified skipUntil then we will skip over the modules in the list until we reach the specified one.
      if($skipUntil -and $weHaveSkipped -eq $false){
         Write-Host "************* $module *************"
         Write-Host "   Skipping  "
         # Add the module to the list of built modules 
         if(!$builtModules.ContainsKey($module.Name)){
           $builtModules.Add($module.Name, $module)
           $global:LastBuildBuiltModules = $builtModules
         }
      } else {
          # We either have not specified a skip or we have alread skipped the modules we need to
          Set-CurrentModule $module.Name
          if($getLatest){
            Get-LatestSourceForModule $module.Name -branchPath $BranchLocalDirectory
          }
          if($getDependencies -eq $true){
            Get-DependenciesForCurrentModule
          }
          $dependencies = Get-ExpertModuleDependencies -BranchPath $BranchLocalDirectory -SourceModule $module
          Write-Host "************* $module *************"
          foreach($dependencyModule in $dependencies){
            if(($dependencyModule -and $dependencyModule.Name -and $builtModules -and $builtModules.ContainsKey($dependencyModule.Name)) -or ($getLocal | Where {$_ -eq $dependencyModule})){
              $sourcePath = Join-Path $BranchLocalDirectory Modules\$dependencyModule\Bin\Module

              if($dependencyModule.Name.ToUpper().StartsWith("THIRDPARTY")){
                $sourcePath = Join-Path $BranchLocalDirectory Modules\$dependencyModule\Bin
              }
              
              $targetPath = Join-Path $BranchLocalDirectory Modules\$module
              Copy-Item -Path $sourcePath\* -Destination $targetPath\Dependencies -Force -Recurse -Verbose
            }
          }
      
          # Do the Build
          if ($module.ModuleType -ne [DependencyAnalyzer.ModuleType]::ThirdParty) {
              Start-BuildForCurrentModule $clean
        
              # Check for errors
              if ($LASTEXITCODE -eq 1) {
                  pushd $currentWorkingDirectory
                  throw "Build of $module Failed"
              }
          }
      
          # Add the module to the list of built modules 
          if(!$builtModules.ContainsKey($module.Name)){
            $builtModules.Add($module.Name, $module)
            $global:LastBuildBuiltModules = $builtModules
          }      

          # Copy binaries to drop folder
          if($copyBinaries -eq $true){
            Copy-BinariesFromCurrentModule
          }
        }
      
      [string[]]$global:LastBuildRemainingModules = $global:LastBuildRemainingModules | Where  {$_ -ne $module}
    }
    
    $global:LastBuildRemainingModules = $null
    
  if($moduleBeforeBuild) {
    cm $moduleBeforeBuild
  }
  pushd $currentWorkingDirectory
}


<# 
.Synopsis 
    Builds a patch for the current branch.
.Description
    Builds a patch for the current branch. Driven from the PatchingManifest.xml.
.Example
        Get-ProductZip; Build-ExpertPatch
    Gets the latest product zip from the build server then builds a patch using those binaries.
.Example
        Get-Product; Build-ExpertPatch
    Gets the latest product from the build server then builds a patch using those binaries.
.Example
        Build-ExpertPatch
    Builds the patch using the local binaries.
#>
function Build-ExpertPatch([switch]$noget = $false) {
    pushd $PackageScriptsDirectory; .\AllowDotNet40Assemblies.ps1

    if (!$noget) {
        $cmd = "xcopy \\na.aderant.com\expertsuite\Main\Build.Tools\Current\* /S /Y $PackageScriptsDirectory"
        Invoke-Expression $cmd
    }
    pushd $PackageScriptsDirectory; .\Patching\BuildPatch.ps1
    popd
}


function Get-ExpertModulesInChangeset {
    $expression = "tf status"
    $changesetStrings = Invoke-Expression $expression
    
    #ExpertModule type from DependencyAnalyzer
    $allModules = Get-ExpertModules    

    $changesetModules = @{}    
    
    foreach ($changesetString in $changesetStrings) {
        Write-Debug $changesetString
        foreach ($module in $allModules) {
            $modulePath = "$BranchLocalDirectory\Modules\$module\"
            
            if ($changesetString.IndexOf($modulePath, [System.StringComparison]::InvariantCultureIgnoreCase) -gt -1){
            
                if ($changesetModules.ContainsKey($module.Name) -eq $false){
                    $changesetModules[$module.Name] = $module
                }
            }
        }
    }
    return $changesetModules.Values
}

function Get-LatestSourceForModule {
 param([string[]] $moduleNames, [string] $branchPath)
 
 foreach($moduleName in $moduleNames){
    write "*** Getting latest for $moduleName ****"
    $path = "$branchPath\Modules\$moduleName"
    Invoke-Expression "tf get $path /recursive"
 }
}

<# 
.Synopsis 
    Branches an Expert Module from a source branch to a target branch
.Description   
    Branches the TFS source from TFS into the relevant path in the target branch location
    Updates the ExpertManifest to get the branched module from this branch
    Checking back in to TFS must be done manually.
.PARAMETER moduleName
    The name of the module to branch
.PARAMETER sourceBranch
    The name of the source branch e.g. Main
.PARAMETER targetBranch
    The name of the target branch e.g. Dev\OnTheGo
.EXAMPLE
    Branch-Module -moduleName Services.Query -sourceBranch Main -targetBranch Dev\Validation
    Will branch the module Services.Query from the Main branch to the Dev\Validation branch, update the ExpertManifest.xml file to contain this new reference and any dependent modules.

    The user must manually check in the branched changes    
#>
function Branch-Module([string]$moduleName, [string]$sourceBranch, [string]$targetBranch) {    
    if (!($moduleName)) {
        write "No module specified."
        return
    }

    if (!($sourceBranch)) {
        write "No source branch specified."
        return
    }

    if (!($targetBranch)) {
        write "No target branch specified."
        return
    }

    $sourceTfsPath = "`$/ExpertSuite/" + $sourceBranch.Replace("\", "/").Trim('/') + "/Modules/" + $moduleName
    $targetTfsPath = "`$/ExpertSuite/" + $targetBranch.Replace("\", "/").Trim('/') + "/Modules/" + $moduleName

    Write-Host "Branch Module:            $moduleName"
    Write-Host "Source TFS:               $sourceTfsPath"
    Write-Host "Target TFS:               $targetTfsPath"

    $rootPath = $global:BranchLocalDirectory.Replace($global:BranchName, "")
    if ($global:BranchName.Trim("/").ToUpper() -eq "MAIN"){
        $rootPath = $global:BranchLocalDirectory.Trim("/").Substring(0, $global:BranchLocalDirectory.Length - 4)
    }

    $newBranchPath = $rootPath.Trim("/") + $targetBranch
    Write-Host "Target Branch Directory:  $newBranchPath"

    if ((Test-Path $newBranchPath) -ne $true){
        throw "The target branch path does not exist: $newBranchPath"
    }

    $newModulePath = "$newBranchPath/Modules/$moduleName"

    if ((Test-Path $newModulePath) -eq $true) {
        throw "The target branch path $newModulePath already exists. The branch operation has been aborted"
    }
    Write-Host

    Invoke-Expression "tf get $newBranchPath/Modules /recursive"

    $targetExpertManifestPath = "$newBranchPath\Modules\Build.Infrastructure\Src\Package\ExpertManifest.xml"

    if ((Test-Path $targetExpertManifestPath) -eq $false){
        throw "The target branch expert manifest $targetExpertManifestPath does not exist. The branch operation has been aborted"
    }

    Invoke-Expression "tf branch $sourceTfsPath $targetTfsPath"
    
    Invoke-Expression "tf checkout $targetExpertManifestPath"
    [xml]$doc = Get-Content $targetExpertManifestPath
    
    Write-Host ""
    Write-Host ""
    Write-Host "Updating Dependency Manifest for $moduleName"
    $newModuleManifestPath = [System.IO.Path]::Combine($newModulePath, "Build", "DependencyManifest.xml")	
    Invoke-Expression "tf checkout $newModuleManifestPath"

    Write-Host ""
    Write-Host ""
    Write-Host "Updating Expert Manifest"
   
    New-ExpertManifestForBranch $targetBranch
    
    Write-Host ""
    Write-Host ""
    Write-Host "Updating TFSBuild.proj"
    
    $buildProject = [System.IO.Path]::Combine($newModulePath, "Build", "TFSBuild.proj")
    if (Test-Path $buildProject) {
        Invoke-Expression "tf checkout $buildProject"
        
        $branchUncPath = $targetBranch.Replace("/", "\")
        
        $project = Get-Content $buildProject
        $project = $project -replace "\\\\na.aderant.com\\expertsuite\\main", $BranchServerDirectory
        
        Set-Content $project -Path $buildProject		
    }

    $moduleProject = [System.IO.Path]::Combine($newBranchPath, "Modules", "Modules.proj")
    if (Test-Path $moduleProject) {
        Write-Host ""
        Write-Host ""
        Write-Host "Updating branch TFSBuild.proj"
   
        Invoke-Expression "tf checkout $moduleProject"

        Update-BuildAllProject $moduleProject
    }    

    Write-Host
    Write-Host "New Module Location: $newModulePath"
    Write-Host "Branch Complete"
    Write-Host
}

<# 
.Synopsis 
    Starts DeploymentManager for your current branch
.Description     
    DeploymentManager 
#>
function Start-DeploymentManager {
    $shell = ".\DeploymentManager.exe $fullManifest"
    switch ($global:BranchExpertVersion){
        "7SP4" {
            #SP4 Case where DeploymentManager folder contains DeploymentManager.exe and dependencies.
            pushd $global:BranchBinariesDirectory\DeploymentManager
            Write "75SP4 Starting Deployment Manager in Binaries\DeploymentManager folder..."
        }
        "8" {
            #8.0 case where ExperSource and Deployment folders exist in binaries folder, and DeploymentManager is renamed to Setup.exe.
            $shell = ".\Setup.exe $fullManifest"
            pushd $global:BranchBinariesDirectory
            Write "8.x Starting Deployment Manager (Setup.exe) in Binaries folder..."
        }
        "802" {
            #8.0.2 case where ExperSource folder exists in binaries folder, and DeploymentManager is renamed to Setup.exe.
            $shell = ".\Setup.exe $fullManifest"
            pushd $global:BranchBinariesDirectory
            Write "8.0.2 Starting Deployment Manager (Setup.exe) in Binaries folder..."
        }
        default {
            # SP2 (MatterPlanning), and earlier, where DeploymentManager.exe is in Binaries folder.
            pushd $global:BranchBinariesDirectory
            Write "75SP2 Starting Deployment Manager in Binaries folder..."
        }
    }    
    
<#    
    if (Test-path $global:BranchExpertSourceDirectory){
        #8.0 case where ExperSource and Deployment folders exist in binaries folder, and DeploymentManager is renamed to Setup.exe.
        $shell = ".\Setup.exe $fullManifest"
        pushd $global:BranchBinariesDirectory\Deployment
        Write "8.x Starting Deployment Manager in Binaries\Deployment folder..."
    } else {
        if (Test-path $global:BranchBinariesDirectory\DeploymentManager){
            #SP4 Case where DeploymentManager folder contains DeploymentManager.exe and dependencies.
            pushd $global:BranchBinariesDirectory\DeploymentManager
            Write "75SP4 Starting Deployment Manager in Binaries\DeploymentManager folder..."
        } else {
            # SP2 (MatterPlanning) case where DeploymentManager.exe is in Binaries folder.
            pushd $global:BranchBinariesDirectory
            Write "75SP2 Starting Deployment Manager in Binaries folder..."
        }
    }
#>    
    Invoke-Expression $shell                 
    popd
}

<# 
.Synopsis 
    Run DeploymentEngine for your current branch
.Description     
    DeploymentEngine -action deploy -manifestName FileOpening
    DeploymentEngine -action remove -manifestName FileOpening
    Parameter $action, the action you want the deployment engine to take
    Parameter $manifestName name of the environment manifest you are doing the deployment action for. This will resolve as <manifestName>.environment.xml
#>
function Start-DeploymentEngine {
    param ([string]$action, [string]$manifestName)
    
    [string]$fullManifest = ''

    if ($global:BranchExpertVersion -eq "8" -or $global:BranchExpertVersion -eq "802") {
        if  ($action -eq $null) {
            write "Usage: Start-DeploymentEngine <action>"
            return	
        }
        else {
            $fullManifest = Join-Path -Path $global:BranchBinariesDirectory -ChildPath 'environment.xml'
        }

    } else {
        if  ($action -eq $null -or $manifestName -eq $null) {
            write "Usage: Start-DeploymentEngine <action> <manifestName>"
            return
        } else {
            $fullManifest = Join-Path -Path $global:BranchEnvironmentDirectory -ChildPath "\$manifestName.environment.xml"
        }
    }
        
    if (test-path $fullManifest) {        
        switch ($global:BranchExpertVersion) {
            "7SP4" {
                #SP4 Case where DeploymentManager folder contains DeploymentManager.exe and dependencies.
                pushd $global:BranchBinariesDirectory\DeploymentManager
                Write "75SP4 Starting Deployment Engine in Binaries\DeploymentManager folder..."
            }
            "8" {
                #8.0 case where ExperSource and Deployment folders exist in binaries folder, and DeploymentManager is renamed to Setup.exe.
                pushd $global:BranchBinariesDirectory\Deployment
                Write "8.x Starting Deployment Engine in Binaries\Deployment folder..."
            }
            "802" {
                #8.0.2 case where ExperSource exists and DeploymentManager is renamed to Setup.exe.
                pushd $global:BranchExpertSourceDirectory
                Write "8.0.2 Starting Deployment Engine in Binaries folder..."
            }
            default {
                # SP2 (MatterPlanning), and earlier, case where DeploymentManager.exe is in Binaries folder.
                pushd $global:BranchBinariesDirectory
                Write "75SP2 (or earlier) Starting Deployment Engine in Binaries folder..."
            }
        }
        $shell = ".\DeploymentEngine.exe $action $fullManifest" 
        Invoke-Expression $shell                 
        popd

    } else {
        Write-Warning "Manifest file was not found at [$fullManifest]"
    }                 
}

#sets up visual studio environment, called from Profile.ps1 when starting PS.
function Set-Environment() { 
    Set-BranchPaths 
    Set-ScriptPaths
    Set-ExpertSourcePath
    Initialise-BuildLibraries
    Set-VisualStudioVersion
    Write-ToHostEnvironmentDetails
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo($newBranch, [switch] $SetAsDefault){
    Set-ChangedBranchPaths $newBranch
    Set-ScriptPaths
    Set-ExpertSourcePath
    Initialise-BuildLibraries
    Set-CurrentModule $global:CurrentModuleName
    Write-ToHostEnvironmentDetails
    cd $global:BranchLocalDirectory
    
    if($SetAsDefault) {
        SetDefaultValue dropRootUNCPath $BranchServerDirectory
        SetDefaultValue devBranchFolder $BranchLocalDirectory
    }
}

function Set-VisualStudioVersion() {
    $legacyFile = [System.IO.Path]::Combine($global:BuildScriptsDirectory, "vsvars2010.ps1");

    if (Test-Path $legacyFile) {
        Write-Host "`Using legacy Visual Studio Command Prompt variables script." -ForegroundColor Yellow
        &($legacyFile) 
        return
    } 

    $file = [System.IO.Path]::Combine($global:BuildScriptsDirectory, "vsvars.ps1");
    if (Test-Path $file) {        
        &($file)
    }
}


# Local Dependency Manifest to enable local get 
function global:LocalDependencyManifest {
    return (GetDefaultValue "DependencyManifestOverride")    
}

# gets a value from the global defaults storage, or creates a default
function global:GetDefaultValue {
    param ( 
        [string]$propertyName,
        [string]$defaultValue
    )
    
    Write-Debug "Asked for default for: $propertyName with default ($defaultValue)"

    if ([Environment]::GetEnvironmentVariable("Expert$propertyName", "User") -ne $null) {
        return [Environment]::GetEnvironmentVariable("Expert$propertyName", "User")
    }
    
    if ($propertyName -eq "DevBranchFolder") {
        return SetPathVariable "Where is your local path to the MAIN branch? e.g C:\tfs\ExpertSuite\Main" $propertyName        
    }
    
    if ($propertyName -eq "DropRootUNCPath") {        
        return SetPathVariable "Where is the MAIN branch drop path? For e.g \\na.aderant.com\ExpertSuite\Main" $propertyName
    }
    
    if ($propertyName -eq "SystemMapConnectionString") {
        $title = "SystemMapBuilder Setup"
        $message = "Do you want to use the Expert ITE for SystemMap generation?"
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($title, $message, $options, 0)
        
        if ($result -eq 0) {
            $connection = "/pdbs:svsql303\mssql10 /pdbd:VMAKLITEGG /pdbid:cmsdbo /pdbpw:cmsdbo"
            SetDefaultValue $propertyName $connection
            return $connection
        } else {
            $server = Read-Host "What is the database server?"
            $database = Read-Host "What is the database to use?"
            $login = Read-Host "What is the database login?"
            $password = Read-Host "What is the login password?"
            
            $connection = "/pdbs:$server /pdbd:$database /pdbid:$login /pdbpw:$password"
            SetDefaultValue $propertyName $connection
            return $connection            
        }
    }
    
    # Environment variable was not set, default it.	
    [Environment]::SetEnvironmentVariable("Expert$propertyName", $defaultValue, "User")
    return $defaultValue     
}

function SetPathVariable($question, $propertyName) {
    $result = $null
    while ([string]::IsNullOrEmpty($result)) {        
        $result = Read-Host $question 
        
        if (-not ([string]::IsNullOrEmpty($result))) {
           if (Test-Path $result) {
                SetDefaultValue $propertyName $result                
                return $result
            } else {
                Write-Warning "The given path $result does not exist"                
                $result = $null
            }
        }
    }
}

<# 
.Synopsis 
    Sets the default branch information.
.Description   
    Sets the default branch information. This was formerly held in the defaults.xml file. After initially setting this information
    you should use the Switch-Branch command with the -SetAsDefault parameter to update it.
.PARAMETER devBranchFolder
    The full path to the development branch
.PARAMETER dropUncPath
    The full unc path to the network drop folder for the branch
.EXAMPLE
        Set-ExpertBranchInfo -devBranchFolder c:\ExpertSuite\Dev\Msg2 -dropUncPath C:\expertsuite\Dev\Msg2
     Will set the default branch information to the Dev\Msg2 branch
    
#>
function Set-ExpertBranchInfo([string] $devBranchFolder, [string] $dropUncPath) {
    if((Test-Path $devBranchFolder) -ne $true){
        Write-Error "The path $devBranchFolder does not exist"
    }
     
    if((Test-Path $dropUncPath) -ne $true){
        Write-Error "The path $dropUncPath does not exist"
    }

    SetDefaultValue DevBranchFolder $devBranchFolder
    SetDefaultValue DropRootUNCPath $dropUncPath
    Set-Environment
    Write-Host ""
    Write-Host "The environment has been configured"
    Write-Host "You should not have to run this command again on this machine"
    Write-Host "In future when changing branches you should use the Switch-Branch command with the -SetAsDefault parameter to make it permanent."
}

# sets a value in the global defaults storage
function global:SetDefaultValue {
    param ( 
        [string]$propertyName,
        [string]$defaultValue
    )
    
    [Environment]::SetEnvironmentVariable("Expert$propertyName", $defaultValue, "User")

}

Function global:Get-PackageLocation(){
    return $global:PackageScriptsDirectory
}

Function global:Get-Packages(){
    return (Join-Path -Path $global:PackageScriptsDirectory -ChildPath \GetPackages.ps1)
}

Function global:Get-PackagesCopy(){
    return (Join-Path -Path $global:PackageScriptsDirectory -ChildPath \GetPackagesCopy.ps1)
}

<# 
.Synopsis 
    Opens the solution for a module in the current branch
.Description   
    Opens a module's main solution in visual studio
.PARAMETER ModuleName
    The name of the module
.EXAMPLE
        Open-ModuleSolution Libraries.Presentation
    Will open the Libraries.Presentation solution in visual studio
    
#>
function Open-ModuleSolution([string] $ModuleName)
{    
    if(!($ModuleName)){
        $ModuleName = $global:CurrentModuleName
    }
    if(!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq ""){
        "No module specified"
        return
    }
    Write-Host "Opening solution for module $ModuleName"
    $moduleSolutionPath = Join-Path $BranchLocalDirectory "Modules\$ModuleName\$ModuleName.sln"
    if(Test-Path $moduleSolutionPath) {
        Invoke-Expression "devenv $moduleSolutionPath"
    } else {
        "There is no solution file at $moduleSolutionPath"
    }
}
<# 
.Synopsis 
    Gets the latest source from TFS
.Description   
    Get the latest source from TFS for a module, or if no module is specified, the current module, or if -Branch is specified, the entire branch
.PARAMETER ModuleName
    The name of the module
.PARAMETER Branch
    Gets latest for the entire branch instead of a particular module
.EXAMPLE
        Get-Latest Libraries.Presentation
    Gets the latest source for Libraries.Presentation from TFS
.EXAMPLE
        Get-Latest
    Gets the latest source for the current module from TFS
.EXAMPLE
        Get-Latest -Branch
    Gets the latest source for the current branch from TFS    
#>
function Get-Latest([string] $ModuleName, [switch] $Branch)
{    
    $sourcePath = $null;
    if($Branch){
        $sourcePath = $global:BranchLocalDirectory
    }
    else {
        if(!($ModuleName)){
            $ModuleName = $global:CurrentModuleName
        }
        if(!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq ""){
            "No module specified"
            return
        }
        $sourcePath = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"
        if((Test-Path $sourcePath) -eq $False) {
            "There is no local path $sourcePath. Make sure you are specifying a module that exists in the current branch"
            return
        }
    }
    
    Invoke-Expression "tf get $sourcePath /recursive"
}

#################################################################################################
## Tab Expansion ################################################################################
#  
# Tab Expansion has changed in PS3, the method to override is now called TabExpansion2 and not
# TabExpansion. However, PS3 assumes a custom implementation of TabExpansion IF you create the function
# and will continue to support it. Because we need to support 2 version of PS, we've enabled execution 
# of selected lines based on $host.Version.Major. This will allow support for TabExpansion with 
# a fall back to the system TabExpansion (for PS2) AND TabExpansion2 with custom TabExpansion (for PS3)
##################################################################################################

# Copy the current Tab Expansion function so we can fall back to 
# it if we're not supposed to handle a command, but only for PS2
# PS3 tab expansion is now catered for by TabExpansion2 function
if ($Host.Version.Major -eq 2) {
    Copy Function:\TabExpansion Function:\tempTabExpansion
}

function TabExpansion([string] $line, [string] $lastword)
{   
    if(!$lastword.Contains(";")){ 
        $aliases = Get-Alias
        $parser = New-Object DependencyAnalyzer.AutoCompletionParser $line, $lastword, $aliases
    
        # Evaluate Branches
        Try{
            foreach($tabExpansionParm in $global:expertTabBranchExpansions){
                if($parser.IsAutoCompletionForParameter($tabExpansionParm.CommandName.ToString(), $tabExpansionParm.ParameterName.ToString(), $tabExpansionParm.IsDefault.IsPresent)){
                    Get-ExpertBranches $lastword | Get-Unique
                }
            }
        }
        Catch 
        {
            [system.exception]
            Write-Host $_.Exception.ToString()
        }
        
        # Evaluate Modules
        Try{
            foreach($tabExpansionParm in $global:expertTabModuleExpansions){
                if($parser.IsAutoCompletionForParameter($tabExpansionParm.CommandName.ToString(), $tabExpansionParm.ParameterName.ToString(), $tabExpansionParm.IsDefault.IsPresent)){
                    $parser.GetModuleMatches($BranchLocalDirectory) | Get-Unique
                }
            }
        }
        Catch 
        {
            [system.exception]
            Write-Host $_.Exception.ToString()
        }
    }
    
    [System.Diagnostics.Debug]::WriteLine("Aderant Build Tools:Falling back to default tab expansion for Last word: $lastword, Line: $line")
    
    # Otherwise, fall back to default TabExpansion function, but only for PS2
    if ($Host.Version.Major -eq 2) {
        TabExpansionBeforeExpert $line $lastword
    }

}

function IsTabOnCommandParameter([string] $line, [string] $lastword, [string] $commandName, [string] $parameterName, [switch] $isDefaultParameter) {
    # Need to select last command if is a line of commands separated by ";"
    # Need to ignore auto-completion of parameters
    # Need to ignore auto-completion of command names
    #return ($lastword.StartsWith("-") -ne True -and $line -ne $commandName -and (($line.Trim() -eq "$commandName $lastword" -and $isDefaultParameter) -or ($line.Trim() -eq "SwitchBranchTo")))
}

# Only do logic for TabExpansion / tempTabExpansion for PS2
if ($Host.Version.Major -eq 2) {
    if (gc function:tempTabExpansion | Select-String "SwitchBranchTo"){ 
        Write-Host "Tab Expansion Already Installed"
        Copy Function:\tempTabExpansion Function:\TabExpansion
    } else{
        Copy Function:\tempTabExpansion Function:\TabExpansionBeforeExpert
        Export-ModuleMember -Function TabExpansion
    }
} else {
    Export-ModuleMember -Function TabExpansion
}

$global:expertTabModuleExpansions = @()
$global:expertTabBranchExpansions = @()

<# 
.Synopsis 
    Adds a parameter to the expert tab expansions for modules
.Description
    Tab Expansion is when pressing tab will auto-complete the value of a parameter. This command allows you to configure autocomplete where a module name or comma separated list of module names is required
.PARAMETER CommandName
    The name of the command (not the alias)
.PARAMETER ParameterName
    The name of the parameter to match
.PARAMETER IsDefault
    Set if this is the default index 0 parameter
.EXAMPLE
    Add-ModuleExpansionParameter -CommandName Build-ExpertModules -ParameterName ModuleNames -IsDefault
    
    Will add tab expansion of module names on the Build-ExpertModules command where the current parameter is the ModuleNames parameter and this is also the first (default) parameter
#>
function Add-ModuleExpansionParameter([string] $CommandName, [string] $ParameterName, [switch] $IsDefault){
    if (!($CommandName)) {
        write "No command name specified."
        return
    }
    if (!($CommandName)) {
        write "No parameter name specified."
        return
    }
    $objNewExpansion = New-Object System.Object
    $objNewExpansion | Add-Member -type NoteProperty -name CommandName -value $CommandName
    $objNewExpansion | Add-Member -type NoteProperty -name ParameterName -value $ParameterName
    $objNewExpansion | Add-Member -type NoteProperty -name IsDefault -value $IsDefault
    $global:expertTabModuleExpansions += $objNewExpansion
}

 
<# 
.Synopsis 
    Adds a parameter to the expert tab expansions for branches
.Description
    Tab Expansion is when pressing tab will auto-complete the value of a parameter. This command allows you to configure autocomplete where a branch name is required
.PARAMETER CommandName
    The name of the command (not the alias)
.PARAMETER ParameterName
    The name of the parameter to match
.PARAMETER IsDefault
    Set if this is the default index 0 parameter
.EXAMPLE
    Add-BranchExpansionParameter -CommandName SwitchBranchTo -ParameterName newBranch -IsDefault
    
    Will add tab expansion of branch names on the newBranch command where the current parameter is the newBranch parameter and this is also the first (default) parameter
    
#>
function Add-BranchExpansionParameter([string] $CommandName, [string] $ParameterName, [switch] $IsDefault){
    if (!($CommandName)) {
        write "No command name specified."
        return
    }
    if (!($CommandName)) {
        write "No parameter name specified."
        return
    }
    $objNewExpansion = New-Object System.Object
    $objNewExpansion | Add-Member -type NoteProperty -name CommandName -value $CommandName
    $objNewExpansion | Add-Member -type NoteProperty -name ParameterName -value $ParameterName
    $objNewExpansion | Add-Member -type NoteProperty -name IsDefault -value $IsDefault
    $global:expertTabBranchExpansions += $objNewExpansion
}

# Add branch auto completion scenarios
Add-BranchExpansionParameter -CommandName "SwitchBranchTo" -ParameterName "newBranch" -IsDefault
Add-BranchExpansionParameter -CommandName "Branch-Module" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Branch-Module" -ParameterName "targetBranch"

Add-BranchExpansionParameter –CommandName "New-ExpertManifestForBranch" –ParameterName "SourceBranch" -IsDefault
Add-BranchExpansionParameter –CommandName "New-ExpertManifestForBranch" –ParameterName "TargetBranch"
#These commands are in AderantTfs.psm1
Add-BranchExpansionParameter -CommandName "Merge-Branch" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Merge-Branch" -ParameterName "targetBranch"

# Add module auto completion scenarios
Add-ModuleExpansionParameter -CommandName "Set-CurrentModule" -ParameterName "name" -IsDefault
Add-ModuleExpansionParameter -CommandName "Branch-Module" -ParameterName "moduleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "workflowModuleNames" -IsDefault
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "getLocal"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "exclude"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "skipUntil"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ProviderModules" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ConsumerModules"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependencies" -ParameterName "SourceModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependsOn" -ParameterName "TargetModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-DownstreamExpertModules" -ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-ExpertModule" -ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-ExpertModules" -ParameterName "ModuleNames" -IsDefault
Add-ModuleExpansionParameter –CommandName "Open-ModuleSolution" –ParameterName "ModuleName" -IsDefault  
Add-ModuleExpansionParameter –CommandName "Get-Latest" –ParameterName "ModuleName" -IsDefault


#########################################
## Prompt ###############################
#########################################

<# 
.Synopsis 
    Enables the Expert prompt with branch and module information
.Description     
    Enable-ExpertPrompt
#>
function Enable-ExpertPrompt() {
    Function global:prompt
    {
        # set the window title to the branch name       
        $host.ui.rawui.WindowTitle = "PS - [" + $global:CurrentModuleName + "] on branch [" + $global:BranchName + "]"

        Write-Host("")
        Write-Host ("Module [") -nonewline
        Write-Host ($global:CurrentModuleName) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] on branch [") -nonewline
        Write-Host ($global:BranchName) -nonewline -foregroundcolor Green
        Write-Host ("]") 

        Write-Host ("PS " + $(get-location) +">") -nonewline 
        return " "
    }
}

<# 
.Synopsis 
    Disables the Expert prompt with branch and module information
.Description     
    Disable-ExpertPrompt
#>
function Disable-ExpertPrompt() {
    # Copy the current prompt function so we can fall back to it if we're not supposed to handle a command
            Function global:prompt
            {
                  $(if (test-path variable:/PSDebugContext) { '[DBG]: ' } 
                  else { '' }) + 'PS ' + $(Get-Location) `
                  + $(if ($nestedpromptlevel -ge 1) { '>>' }) + '> '
            }

}

<# 
.Synopsis 
    Generates a System Map to the expertsource folder of the current branch
.Description
    Generates a System Map using the the default ITE information to the expertsource folder of the current branch
#>
function Generate-SystemMap(){
    $inDirectory = Join-Path $global:BranchBinariesDirectory 'ExpertSource'
    $systemMapConnectionString = (Get-SystemMapConnectionString)
    if($systemMapConnectionString.ToLower().Contains("/pdbs:") -and $systemMapConnectionString.ToLower().Contains("/pdbd:") -and $systemMapConnectionString.ToLower().Contains("/pdbid:") -and $systemMapConnectionString.ToLower().Contains("/pdbpw:")){
        $connectionParts = $systemMapConnectionString.Split(" ")                                            
        Write-Debug "Connection is [$connectionParts]"
        &$inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3]        
    }else{
        Write-Error "Connection string is invalid for use with systemmapbuilder.exe [$systemMapConnectionString]"
    }  
}

function Test-ReparsePoint([string]$path) {
  $file = Get-Item $path -Force -ea 0
  return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}

function Test-ReparsePoint([string]$path) {
  $file = Get-Item $path -Force -ea 0
  return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}

<# 
.Synopsis 
    Determines the location of the Aderant Module.
.Description
    Determines the location of the Aderant Module.
#>
function Get-AderantModuleLocation() {
    $aderantModuleBase = (Get-Module -Name Aderant).ModuleBase
    if (Test-ReparsePoint $aderantModuleBase ){
        # this is a symlink, get the target.
        return Get-SymbolicLinkTarget $aderantModuleBase
        
    } else {
        # this is a normal folder.
        return $aderantModuleBase
    }

}

function Help {
    $AderantModuleLocation = Get-AderantModuleLocation
    Write-Host "Using Aderant Module from : $AderantModuleLocation"
    Write-Host ""
    Write-Host "The following aliases are defined: "
    Write-Host ""
    Write-Host "ed  -> Show the current environment details for the branch your working on."  
    Write-Host ""  
    Write-Host "cm  -> Sets the current module when passed the module name, or tells you what is the current module if not passed the module name"    
    Write-Host "vs  -> Opens the current module in Visual Studio. Specify a module name to open different one."
    Write-Host "nm  -> Creates a new module with the given name in the root of your current branch's modules directory"
    Write-Host "gd  -> Gets the current module dependencies"
    Write-Host "gdl -> Gets the current module dependencies (copy from local bin)"
    Write-Host "cb  -> Copies the binaries from you current module to the branch binaries folder"
    Write-Host "bm  -> Builds the current module, the specified module or a list of modules "
    Write-Host "bb  -> Builds the current branch "
    Write-Host "de  -> Runs deployment engine (For help run get-help de) "
    Write-Host "upd -> Deploys the UPD for the current branch"
    Write-Host "dm  -> Runs deployment manger "
    Write-Host "bp  -> Creates the package "
    Write-Host "bpc -> Creates the package and copies it to binaries "
    Write-Host "usf -> Installs the latest version of the Software Factory"
    Write-Host "branch -> Branches a Module"
    Write-Host "Get-Latest -> Gets the latest source from TFS for the current module. Optionally specify a module name"
    Write-Host "Get-Latest -Branch -> Gets the latest source from TFS for the entire current branch."
    Write-Host "Get-Product    -> Gets the defined product using the default manifest file (For help run get-help Get-Product)"
    Write-Host "Get-ProductNoDebugFiles -> Gets the defined product without the pdb's"
    Write-Host "Get-ProductZip -> Pulls the Zipped version of the Product from the Build.All"
    Write-Host "Switch-Branch -> Switch's your local setup to another branch (eg. Switch-Branch dev\BranchName)" 
    Write-Host "Branch-Module -> Branches a module into a target branch" 
    Write-Host "Generate-SystemMap -> Generates a System Map to the expertsource folder of the current branch"
    Write-Host ""  
    Write-Host "Also note that module and branch names will auto-complete when pressing tab"     
    Write-Host ""  
}

# export functions and variables we want external to this script
set-alias nm New-BuildModule
set-alias cm Set-CurrentModule
set-alias bm Build-ExpertModules
set-alias bb Start-BuildForBranch
set-alias gd Get-DependenciesForCurrentModule
set-alias gdf Get-DependenciesFrom
set-alias gdl Get-LocalDependenciesForCurrentModule
set-alias cb Copy-BinariesFromCurrentModule
set-alias de Start-DeploymentEngine
set-alias upd Update-Database
set-alias dm Start-DeploymentManager
set-alias usf Install-LatestSoftwareFactory
Set-Alias bp Get-Packages
Set-Alias bpc Get-PackagesCopy
Set-Alias ed Write-ToHostEnvironmentDetails
Set-Alias branch Branch-Module
Set-Alias Switch-Branch SwitchBranchTo
Set-Alias vs Open-ModuleSolution

Export-ModuleMember -function Set-Environment
Export-ModuleMember -function Help
Export-ModuleMember -function Write-ToHostEnvironmentDetails

# paths
Export-ModuleMember -variable CurrentModuleName
Export-ModuleMember -variable BranchServerDirectory
Export-ModuleMember -variable BranchLocalDirectory
Export-ModuleMember -variable CurrentModulePath
Export-ModuleMember -variable BranchBinariesDirectory
Export-ModuleMember -variable BranchName
Export-ModuleMember -variable BranchModulesDirectory
Export-ModuleMember -variable ProductManifest

# developer experience
Export-ModuleMember -function Set-CurrentModule
Export-ModuleMember -function Get-CurrentModule
Export-ModuleMember -function Copy-BinariesFromCurrentModule
Export-ModuleMember -function Start-BuildForCurrentModule
Export-ModuleMember -function Start-BuildForBranch
Export-ModuleMember -function Get-DependenciesForCurrentModule
Export-ModuleMember -function Get-LocalDependenciesForCurrentModule
Export-ModuleMember -function Update-Database
Export-ModuleMember -function SwitchBranchTo
Export-ModuleMember -function Get-Product
Export-ModuleMember -function Get-ProductNoDebugFiles
Export-ModuleMember -function Get-ProductZip
Export-ModuleMember -function Build-ExpertModules
Export-ModuleMember -function Set-ExpertBranchInfo
Export-ModuleMember -function Start-DeploymentEngine
Export-ModuleMember -function Start-DeploymentManager
Export-ModuleMember -function New-BuildModule
Export-ModuleMember -function Install-LatestSoftwareFactory
Export-ModuleMember -function Get-ExpertModulesInChangeset
Export-ModuleMember -function Build-ExpertPatch
Export-ModuleMember -function Branch-Module
Export-ModuleMember -function Enable-ExpertPrompt
Export-ModuleMember -function Disable-ExpertPrompt
Export-ModuleMember -function Add-BranchExpansionParameter
Export-ModuleMember -function Add-ModuleExpansionParameter
Export-ModuleMember -function Open-ModuleSolution
Export-ModuleMember -function Get-Latest
Export-ModuleMember -function Generate-SystemMap
Export-ModuleMember -function New-ExpertManifestForBranch
Export-ModuleMember -function Get-AderantModuleLocation

Export-ModuleMember -alias nm
Export-ModuleMember -alias ed
Export-ModuleMember -alias cm
Export-ModuleMember -alias bm
Export-ModuleMember -alias bb
Export-ModuleMember -alias upd
Export-ModuleMember -alias gd
Export-ModuleMember -alias gdf
Export-ModuleMember -alias gdl
Export-ModuleMember -alias de
Export-ModuleMember -alias dm
Export-ModuleMember -alias cb
Export-ModuleMember -alias gdp
Export-ModuleMember -alias usf
Export-ModuleMember -alias branch
Export-ModuleMember -alias Switch-Branch
Export-ModuleMember -alias bpc
Export-ModuleMember -alias bp
Export-ModuleMember -alias vs

Enable-ExpertPrompt