$branchRoot = ""

# gets the folder the current script is executing from
function Get-ScriptFolder {
     return Split-Path (Get-Variable MyInvocation).Value.ScriptName
} 

# sets the current module to work with
function Set-CurrentModule($currentModule) {

    if (!($currentModule)) {
        
        if (!($script:currentModule)) {
            write-host "Current module not set."
        } else {
            write-host "Current module set to '$script:currentModule'"
        }
        return
    }
                        
    if(Test-Path ((Join-Path -Path $localModulesRoot -ChildPath $currentModule))){    
        $script:currentModule = $currentModule
        $script:currentModuleRoot = (Join-Path -Path $localModulesRoot -ChildPath $currentModule)    
        $script:currentModuleBuildScripts = (Join-Path -Path $currentModuleRoot -ChildPath \Build\)   
        write-host "Current module set to '$currentModule'"
        write-host ""
        write-host "Current module path is '$currentModuleRoot'"
    }else{
        Throw "Please check that your module name [$currentModule] is correct as the path [$currentModuleRoot] is not valid"
    }
}

<#
    override for branchname
#>
function Reset-BranchNameTo($name) {
    $script:branchName = $name
}

<#
    will set from defaults
#>
function Set-BranchName{

    $fullPath = (GetDefaultValue "dropRootUNCPath").ToLower()
    
    if($fullPath.ToLower().Contains("main")){            
        $script:branchName = "MAIN"
    }elseif($fullPath.ToLower().Contains("dev")){
        $script:branchName = $fullPath.Substring($fullPath.LastIndexOf("dev\"))
    }elseif($fullPath.ToLower().Contains("release")){        
        $script:branchName = $fullPath.Substring($fullPath.LastIndexOf("release\"))
    }
}

<#
    get the branch name
#>
function Get-BranchName{
    return $script:branchName 
}

# returns the current module
function Get-CurrentModulePath {
    return $currentModuleRoot
}

# returns the current module name
function Get-CurrentModule {
    return $currentModule
}

# drop root folder path
function Get-DropRootPath{
    return $dropRoot
}

#local root folder path
function Get-LocalModulesRootPath{
    return $localModulesRoot
}

function Get-ExpertProductManifest{
    return $expertManifest
}

# returns the binaries path
function Get-BinariesPath {
    return $binaries
}

# returns the branch root path 
# e.g C:\source\expertsuite\release\mybranch
function Get-FullLocalBranchRootPath {     
    return (Join-Path (Get-DropPathFor "local") (Get-BranchName))
}

<#
    returns the branch root path 
    e.g na.aderant.com\expertsuite\dev\mybranch
#>
function Get-FullServerBranchRootPath {
    return (Join-Path (Get-DropPathFor "server") (Get-BranchName))     
}


<#
    return the connection string to be used for the sitemap builder
#>
function Get-SystemMapConnectionString {
    return (GetDefaultValue "systemMapConnectionString").ToLower()
}

<#
    returns the base path to drop location
    e.g. \\na.aderant.com\expertsuite\
#>
function Get-DropPathFor($type){
    
    if($type.ToLower().Equals("server")){                   
        $fullPath = (Get-DropRootPath)
        if([string]::IsNullOrEmpty($fullPath)){
            $fullPath = (GetDefaultValue "dropRootUNCPath")
        }
    }elseif($type.ToLower().Equals("local")){
        $fullPath = (Get-LocalModulesRootPath)
        if([string]::IsNullOrEmpty($fullPath)){
            $fullPath = (GetDefaultValue "devBranchFolder")
        }
    }
    
    $fullPathLowerCase = $fullPath.ToLower()
   
    if($fullPathLowerCase.Contains("main")){
        $dropPath = $fullPathLowerCase.Substring(0,$fullPathLowerCase.LastIndexOf("\main")+1)        
    }elseif($fullPathLowerCase.Contains("dev")){
        $dropPath = $fullPathLowerCase.Substring(0,$fullPathLowerCase.LastIndexOf("\dev")+1)
    }elseif($fullPathLowerCase.Contains("release")){        
        $dropPath = $fullPathLowerCase.Substring(0,$fullPathLowerCase.LastIndexOf("\release")+1)
    }
   
   return $dropPath 
}

function New-BuildModule($name){
    $shell = ".\create_module.ps1 -ModuleName $name -DestinationFolder (Get-LocalModulesRootPath) "    
    pushd $moduleScripts
    invoke-expression $shell
    popd
}

function Get-Product-Execute-Sql(
        $database,
        $username, 
        $password,
        $server = "localhost"
    ) {    
        if ($database -eq $null -or $username -eq $null -or $password -eq $null) {
            write "Usage: Get-Product-Execute-Sql <database> <username> <password>"
        } else {            
            Execute-Sql-Scripts $database $username $password $server
    }
}

<# 
.Synopsis 
    Executes sql scripts for cms in the current branch
.Description
    Runs each sql script in the \Binaries\Installation\CmsDbScripts folder
#>
function Execute-CmsDatabaseScripts(
        $database,
        $username, 
        $password,
        $server = "localhost"
    ) {    
        if ($database -eq $null -or $username -eq $null -or $password -eq $null) {
            write "Usage: Execute-CmsDatabaseScripts <database> <username> <password>"
        } else {
        
        $scriptFolder = Join-Path (Get-FullLocalBranchRootPath) "\Binaries\Installation\CmsDbScripts"

        foreach ($file in Get-ChildItem -path $scriptFolder -Filter *Pre.sql) {
            Write-Host $file.fullname
        	sqlcmd -S $server -U $username -P $password -d $database -i $file.fullname
        }
        
        foreach ($file in Get-ChildItem -path $scriptFolder -Filter *.sql) {
            if (!$file.fullname.ToLower().EndsWith("pre.sql")) {
                Write-Host $file.fullname
            	sqlcmd -S $server -U $username -P $password -d $database -i $file.fullname
            }
        }
    }
}

<# 
.Synopsis 
    Installs the latest version of the Software Factory
.Description
    Will uninstall the previous SoftwareFactory.vsix and then install the latest version from the drop location
#>
function Install-LatestSoftwareFactory(){
    $softwareFactoryVSIXId = 'Aderant.SoftwareFactory.vsix'
    $softwareFactoryVSIX = 'AderantSoftwareFactory.vsix'
    $localSoftwareFactoryInstallDirectory = (Join-Path (Get-FullLocalBranchRootPath) SoftwareFactoryInstall)
    
    [xml]$manifest = Get-Content (Get-ExpertProductManifest)
    [System.Xml.XmlNode]$softwareFactoryModule = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object{ $_.Name.Contains("SoftwareFactory")}
    
    invoke-expression "$buildScripts\Build-Libraries.ps1"
    $drop = Get-FullServerBranchRootPath
    $dropPathForSoftwareFactoryVSIX = (PathToBinaries $softwareFactoryModule $drop)
    
    VSIXInstaller.exe /q /uninstall:$softwareFactoryVSIXId 
    
    if(!(Test-Path $localSoftwareFactoryInstallDirectory)) {
        New-Item $localSoftwareFactoryInstallDirectory -ItemType directory
    }else{
        DeleteContentsFrom $localSoftwareFactoryInstallDirectory
    }
    
    Copy-Item -Path $dropPathForSoftwareFactoryVSIX\* -Destination $localSoftwareFactoryInstallDirectory -Force -Recurse
                
    pushd $localSoftwareFactoryInstallDirectory
    VSIXInstaller.exe /q $softwareFactoryVSIX
    popd
              
    write "updated $softwareFactoryVSIX"
}

# builds the current module using default parameters
function Start-BuildForCurrentModule {
    $shell = ".\BuildModule.ps1 -moduleToBuildPath (Get-CurrentModulePath) -dropRoot (Get-FullServerBranchRootPath)"    
    pushd $buildScripts
    invoke-expression $shell
    popd   
}

# gets dependencies for current module using default parameters
function Get-DependenciesForCurrentModule {    
    $shell = ".\LoadDependancies.ps1 -modulesRootPath (Get-CurrentModulePath) -dropPath (Get-FullServerBranchRootPath)"    
    pushd $buildScripts
    invoke-expression $shell
    popd
}

# gets dependencies for current module using default parameters
function Get-LocalDependenciesForCurrentModule {    
    $shell = ".\LoadDependancies.ps1 -modulesRootPath (Get-CurrentModulePath)  -dropPath (Get-LocalModulesRootPath) -localBuild 1"    
    pushd $buildScripts
    invoke-expression $shell
    popd
}

function Copy-BinariesFromCurrentModule{        

    pushd $buildScripts 
    .\Build-Libraries.ps1
    ResolveAndCopyUniqueBinModuleContent -modulePath (Get-CurrentModulePath) -copyToDirectory (Get-BinariesPath)    
    popd    
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
    $shell = ".\GetProduct.ps1 -productManifestPath (Get-ExpertProductManifest) -dropRoot (Get-FullServerBranchRootPath) -binariesDirectory (Get-BinariesPath) -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString)"    
    pushd $packageScripts
    invoke-expression $shell | Out-Host
    popd
}

<# 
.Synopsis 
    Starts DeploymentManager for your current branch
.Description     
    DeploymentManager 
#>
function Start-DeploymentManager {                
    pushd (Get-BinariesPath)
    $shell = ".\DeploymentManager.exe $fullManifest"
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
    
    if ($action -eq $null -or $manifestName -eq $null) {
        write "Usage: Start-DeploymentEngine <action> <manifestName>"
        return
    }    
        
    [string]$fullManifest = Join-Path -Path (Get-FullLocalBranchRootPath) -ChildPath "\Environment\$manifestName.environment.xml"
        
    if (test-path $fullManifest) { 
        write "Starting deployment engine..."
        pushd (Get-BinariesPath) 
        $shell = ".\DeploymentEngine.exe $action $fullManifest"                  
        Invoke-Expression $shell                 
        popd
    } else {
        write "Manifest file was not found at [$fullManifest]"
    }
                 
}

#sets up visual studio environment
function Set-Environment() { 
    Set-BranchName   
    Set-BranchDefaults    
    Set-VisualStudioVersion
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo($branchName){
    if (!($branchName)) {
        write "No branch specified - aborting"
        return
    }    
    Reset-BranchNameTo $branchName
    Set-BranchDefaults   
    Set-CurrentModule (Get-CurrentModule)
}

function Set-VisualStudioVersion(){

   $defaultVersion = (GetDefaultValue "visualStudioVersion") 

   if([string]::IsNullOrEmpty($defaultVersion)){
        write-host ""
        Write-Host "No default for the Visual Studio version given so using 2010"
        write-host ""
        $file = "vsvars2010.ps1" 
   }else{
        $file = "vsvars" + $defaultVersion + ".ps1"       
   }        
    
    $vsVarsFilePath = Join-Path (Get-LocalModulesRootPath) "Build.Infrastructure\Src\Build"    
    $shell = Join-Path $vsVarsFilePath $file
    # run the script   
    &($shell) 
}

<#
 Sets up branch details
#>
function Set-BranchDefaults () {                                                                                    
    Set-LocalBranchPaths                   
    Set-BranchBinaries                                 
}

<#
    (Get-LocalModulesRootPath) \Binaries
    e.g. C:\Source\ExpertSuite\Dev\Product\Binaries
#>
function Set-BranchBinaries(){    
    $script:binaries = (Join-Path (Get-LocalModulesRootPath) \..\Binaries)
    write-host "binaries         : $script:binaries"
    [system.io.directory]::CreateDirectory($binaries)
}

<#
    Set the local paths and report to user
#>
function Set-LocalBranchPaths(){        
    $script:buildScripts = Join-Path (Get-FullLocalBranchRootPath) "\Modules\Build.Infrastructure\Src\Build"
    $script:moduleBuildScripts = Join-Path (Get-FullLocalBranchRootPath) "\Build"    
    $script:localModulesRoot = Join-Path (Get-FullLocalBranchRootPath) "\Modules"
    $script:packageScripts = Join-Path (Get-FullLocalBranchRootPath) "\Modules\Build.Infrastructure\Src\Package"
    $script:moduleScripts = Join-Path (Get-FullLocalBranchRootPath) "\Modules\Build.Infrastructure\Src\ModuleCreator"
    $script:defaultsScripts = Join-Path (Get-FullLocalBranchRootPath) "\Modules\Build.Infrastructure\Src"    
    $script:expertManifest = Join-Path $packageScripts "ExpertManifest.xml"        
    ReportBranchPathsToHost    
}

<#
    Write details for local paths to host
#>
function ReportBranchPathsToHost(){
    write-host "branchName       :"(Get-BranchName)
    write-host "branchRoot       :"(Get-FullLocalBranchRootPath)
    write-host "buildScripts     : $buildScripts"
    write-host "packageScripts   : $packageScripts"        
    write-host "expertManifest   : $expertManifest"
    write-host "localModulesRoot : $localModulesRoot"
}

# Local Dependency Manifest to enable local get 
function global:LocalDependencyManifest {
    return (GetDefaultValue "dependencyManifestOverride")    
}

# gets a value from the global defaults file
function global:GetDefaultValue {
    param ( 
        [string]$propertyName,
        [string]$defaultValue
    )
    
    # read the load default file
    [string]$defaultsFile = Join-Path (Get-ScriptFolder) 'Defaults.xml'
    if (test-path $defaultsFile) {
        [xml]$defaults = Get-Content $defaultsFile
        
        $value = $defaults.DefaultPath.$propertyName.Value
        if ($value -eq $null -or $value -eq '') {
            return $defaultValue 
        } else {
            return $value
        }
        
    } else {
        Throw "Unable to read Defaults.xml file"
    }
}

Function global:Get-PackageLocation(){
	return (Join-Path (Get-LocalModulesRootPath) \Build.Infrastructure\Src\Package)
}

Function global:Get-Packages(){
	return (Join-Path (Get-PackageLocation) GetPackages.ps1)
}

Function global:Get-PackagesCopy(){
	return (Join-Path (Get-PackageLocation) GetPackagesCopy.ps1)
}

function Help {
    write "The following aliases are defined: "
    write ""    
    write "cm  -> Sets the current module "
    write "cm? -> Tell you the current module "
    write "nm  -> Creates a new module with the given name in the root of your current branch's modules directory"
    write "gd  -> Gets the current module dependencies"
    write "gdl -> Gets the current module dependencies (copy from local bin)"
    write "cb  -> Copies the binaries from you current module to the branch binaries folder"
    write "bm  -> Builds the current module "
    write "de  -> Runs deployment engine (For help run get-help de) "
    write "dm  -> Runs deployment manger "
	write "bp  -> Creates the package "
	write "bpc -> Creates the package and copies it to binaries "
    write "usf -> Installs the latest version of the Software Factory"
    write "Get-Product    -> Gets the defined product using the default manifest file (For help run get-help Get-Product)"
    write "SwitchBranchTo -> Switch's your local setup to another branch (eg. SwitchBranchTo dev\VS2010)"        
}

# export functions and variables we want external to this script
set-alias nm New-BuildModule
set-alias cm Set-CurrentModule
set-alias cm? Get-CurrentModule
set-alias bm Start-BuildForCurrentModule
set-alias gd Get-DependenciesForCurrentModule
set-alias gdl Get-LocalDependenciesForCurrentModule
set-alias cb Copy-BinariesFromCurrentModule
set-alias de Start-DeploymentEngine
set-alias dm Start-DeploymentManager
set-alias usf Install-LatestSoftwareFactory
Set-Alias bp Get-Packages
Set-Alias bpc Get-PackagesCopy

Export-ModuleMember -function Set-Environment
Export-ModuleMember -function Help
# paths
Export-ModuleMember -function Get-CurrentModule
Export-ModuleMember -function Get-DropRootPath
Export-ModuleMember -function Get-LocalModulesRootPath
Export-ModuleMember -function Get-CurrentModulePath
Export-ModuleMember -function Get-BinariesPath
Export-ModuleMember -function Get-BranchRootPath
Export-ModuleMember -function Get-ExpertProductManifest
Export-ModuleMember -function Get-FullLocalBranchRootPath

# developer experience
Export-ModuleMember -function Set-CurrentModule
Export-ModuleMember -function Copy-BinariesFromCurrentModule
Export-ModuleMember -function Start-BuildForCurrentModule
Export-ModuleMember -function Get-DependenciesForCurrentModule
Export-ModuleMember -function Get-LocalDependenciesForCurrentModule
Export-ModuleMember -function SwitchBranchTo
Export-ModuleMember -function Get-Product
Export-ModuleMember -function Start-DeploymentEngine
Export-ModuleMember -function Start-DeploymentManager
Export-ModuleMember -function New-BuildModule
Export-ModuleMember -function Install-LatestSoftwareFactory
Export-ModuleMember -function Execute-CmsDatabaseScripts

Export-ModuleMember -alias nm
Export-ModuleMember -alias cm
Export-ModuleMember -alias cm?
Export-ModuleMember -alias bm
Export-ModuleMember -alias gd
Export-ModuleMember -alias gdl
Export-ModuleMember -alias de
Export-ModuleMember -alias dm
Export-ModuleMember -alias cb
Export-ModuleMember -alias gdp
Export-ModuleMember -alias usf


