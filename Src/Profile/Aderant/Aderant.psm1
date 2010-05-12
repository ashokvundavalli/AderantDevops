[string]$BranchRoot = ""
[string]$global:BranchName
[string]$global:BranchLocalDirectory
[string]$global:BranchServerDirectory
[string]$global:BranchModulesDirectory
[string]$global:BranchBinariesDirectory
[string]$global:BranchEnvironmentDirectory
[string]$global:BuildScriptsDirectory
[string]$global:PackageScriptsDirectory
[string]$global:ModuleCreationScripts
[string]$global:ProductManifest
[string]$global:CurrentModuleName
[string]$global:CurrentModulePath
[string]$global:CurrentModuleBuildPath

<#
Branch information
#>
function Set-BranchPaths{
    #initialise from default setting
    Write-Debug "Setting information for branch from your defaults"    
    $global:BranchLocalDirectory = (GetDefaultValue "devBranchFolder").ToLower()                  
    $global:BranchName = ResolveBranchName $global:BranchLocalDirectory    
    $global:BranchServerDirectory = (GetDefaultValue "dropRootUNCPath").ToLower()
    $global:BranchModulesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Modules
    $global:BranchBinariesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath \Binaries
    if((Test-Path $global:BranchBinariesDirectory) -ne $true){ 
        [system.io.directory]::CreateDirectory($global:BranchBinariesDirectory)         
    }
    $global:BranchEnvironmentDirectory =Join-Path -Path $global:BranchLocalDirectory -ChildPath \Environment       
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
        $name = $branchPath.Substring($branchPath.LastIndexOf("release\"))
    }
    return $name
}

function IsDevBanch([string]$name){
    return $name.ToLower().Contains("dev")
}

function IsReleaseBanch([string]$name){
    return $name.ToLower().Contains("release")
}

function IsMainBanch([string]$name){
    return $name.ToLower().Contains("main")
}

function Set-ChangedBranchPaths([string]$name){
    #initialise from default setting
    write-host "change branch to $name"            
    
    if((IsDevBanch $global:BranchName) -or (IsReleaseBanch $global:BranchName)){
        $previousName = $global:BranchName.Substring($global:BranchName.LastIndexOf("\")+1)
    }
    if((IsDevBanch $name) -or (IsReleaseBanch $name)){
        $name = $name.Substring($name.LastIndexOf("\")+1)
    }        
    $global:BranchName = ($global:BranchName -replace $previousName,$name)
    $global:BranchLocalDirectory = ($global:BranchLocalDirectory -replace $previousName,$name)
    $global:BranchServerDirectory = ($global:BranchServerDirectory -replace $previousName,$name)
    $global:BranchModulesDirectory = ($global:BranchModulesDirectory -replace $previousName,$name)
    $global:BranchBinariesDirectory = ($global:BranchBinariesDirectory -replace $previousName,$name)
    if((Test-Path $global:BranchBinariesDirectory) -ne $true){ 
        [system.io.directory]::CreateDirectory($global:BranchBinariesDirectory)         
    }
    $global:BranchEnvironmentDirectory = ($global:BranchEnvironmentDirectory -replace $previousName,$name)
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
        
        $scriptFolder = Join-Path $global:BranchBinariesDirectory "\Installation\CmsDbScripts"

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
function Start-BuildForCurrentModule {
    $shell = ".\BuildModule.ps1 -moduleToBuildPath $global:CurrentModulePath -dropRoot $global:BranchServerDirectory"    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd   
}

# gets dependencies for current module using default parameters
function Get-DependenciesForCurrentModule {    
    $shell = ".\LoadDependancies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory"    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd
}

# gets dependencies for current module using default parameters
function Get-LocalDependenciesForCurrentModule {   
    $shell = ".\Load-LocalDependancies.ps1 -moduleName $global:CurrentModuleName -localModulesRootPath $global:BranchModulesDirectory -serverRootPath $global:BranchServerDirectory"    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd
}

function Copy-BinariesFromCurrentModule{

    if([string]::IsNullOrEmpty($global:CurrentModulePath)){    
        Write-Warning "The current module is not set so the binaries will not be copied"
    }else{        
        pushd $global:BuildScriptsDirectory     
        ResolveAndCopyUniqueBinModuleContent -modulePath $global:CurrentModulePath -copyToDirectory $global:BranchBinariesDirectory    
        popd  
    }  
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
    $shell = ".\GetProduct.ps1 -productManifestPath $global:ProductManifest -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString)"    
    pushd $global:PackageScriptsDirectory
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
    pushd $global:BranchBinariesDirectory
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
        
    [string]$fullManifest = Join-Path -Path $global:BranchEnvironmentDirectory -ChildPath "\$manifestName.environment.xml"
        
    if (test-path $fullManifest) { 
        Write-Debug "Starting deployment engine..."
        pushd $global:BranchBinariesDirectory
        $shell = ".\DeploymentEngine.exe $action $fullManifest"                  
        Invoke-Expression $shell                 
        popd
    } else {
        Write-Warning "Manifest file was not found at [$fullManifest]"
    }                 
}

#sets up visual studio environment
function Set-Environment() { 
    Set-BranchPaths 
    Set-ScriptPaths
    Initialise-BuildLibraries
    Set-VisualStudioVersion
    Write-ToHostEnvironmentDetails
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo($newBranch){
    Set-ChangedBranchPaths $newBranch
    Set-ScriptPaths
    Initialise-BuildLibraries
    Set-CurrentModule
    Write-ToHostEnvironmentDetails
}

function Set-VisualStudioVersion(){
    $defaultVersion = (GetDefaultValue "visualStudioVersion") 

    if([string]::IsNullOrEmpty($defaultVersion)){        
        Write-Debug "No default for the Visual Studio version given so using 2010"        
        $file = "vsvars2010.ps1" 
    }else{
        $file = "vsvars" + $defaultVersion + ".ps1"       
    }            
    $shell = Join-Path $global:BuildScriptsDirectory $file
    &($shell) 
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
    
    Write-Debug "Asked for default for: $propertyName"
    
    # read the load default file
    [string]$defaultsFile = Join-Path (Get-ScriptFolder) 'Defaults.xml'
    if (test-path $defaultsFile) {
        [xml]$defaults = Get-Content $defaultsFile
        
        $value = $defaults.DefaultPath.$propertyName.Value
        
        Write-Debug "Found default for [$propertyName] the value is [$value]"
        
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
	return $global:PackageScriptsDirectory
}

Function global:Get-Packages(){
	return (Join-Path -Path $global:PackageScriptsDirectory -ChildPath \GetPackages.ps1)
}

Function global:Get-PackagesCopy(){
	return (Join-Path -Path $global:PackageScriptsDirectory -ChildPath \GetPackagesCopy.ps1)
}

function Help {
    Write-Host ""
    Write-Host "The following aliases are defined: "
    Write-Host ""
    Write-Host "ed  -> Show the current environment details for the branch your working on."  
    Write-Host ""  
    Write-Host "cm  -> Sets the current module when passed the module name, or tells you what is the current module if not passed the module name"    
    Write-Host "nm  -> Creates a new module with the given name in the root of your current branch's modules directory"
    Write-Host "gd  -> Gets the current module dependencies"
    Write-Host "gdl -> Gets the current module dependencies (copy from local bin)"
    Write-Host "cb  -> Copies the binaries from you current module to the branch binaries folder"
    Write-Host "bm  -> Builds the current module "
    Write-Host "bb  -> Builds the current branch "
    Write-Host "de  -> Runs deployment engine (For help run get-help de) "
    Write-Host "dm  -> Runs deployment manger "
	Write-Host "bp  -> Creates the package "
	Write-Host "bpc -> Creates the package and copies it to binaries "
    Write-Host "usf -> Installs the latest version of the Software Factory"
    Write-Host "Get-Product    -> Gets the defined product using the default manifest file (For help run get-help Get-Product)"
    Write-Host "SwitchBranchTo -> Switch's your local setup to another branch (eg. SwitchBranchTo dev\BranchName)" 
    Write-Host ""       
}

# export functions and variables we want external to this script
set-alias nm New-BuildModule
set-alias cm Set-CurrentModule
set-alias bm Start-BuildForCurrentModule
set-alias bb Start-BuildForBranch
set-alias gd Get-DependenciesForCurrentModule
set-alias gdl Get-LocalDependenciesForCurrentModule
set-alias cb Copy-BinariesFromCurrentModule
set-alias de Start-DeploymentEngine
set-alias dm Start-DeploymentManager
set-alias usf Install-LatestSoftwareFactory
Set-Alias bp Get-Packages
Set-Alias bpc Get-PackagesCopy
Set-Alias ed Write-ToHostEnvironmentDetails

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
Export-ModuleMember -function SwitchBranchTo
Export-ModuleMember -function Get-Product
Export-ModuleMember -function Start-DeploymentEngine
Export-ModuleMember -function Start-DeploymentManager
Export-ModuleMember -function New-BuildModule
Export-ModuleMember -function Install-LatestSoftwareFactory
Export-ModuleMember -function Execute-CmsDatabaseScripts

Export-ModuleMember -alias nm
Export-ModuleMember -alias ed
Export-ModuleMember -alias cm
Export-ModuleMember -alias bm
Export-ModuleMember -alias bb
Export-ModuleMember -alias gd
Export-ModuleMember -alias gdl
Export-ModuleMember -alias de
Export-ModuleMember -alias dm
Export-ModuleMember -alias cb
Export-ModuleMember -alias gdp
Export-ModuleMember -alias usf