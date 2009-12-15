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
        write-host "Current module path is '$currentModuleRoot'"
    }else{
        Throw "Please check that your module name [$currentModule] is correct as the path [$currentModuleRoot] is not valid"
    }
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
function Get-BranchRootPath {
    return $branchRoot
}

function New-BuildModule($name){
    $shell = ".\create_module.ps1 -ModuleName $name -DestinationFolder (Get-LocalModulesRootPath) "    
    pushd $moduleScripts
    invoke-expression $shell
    popd
}



function Install-LatestSoftwareFactory(){
    $softwareFactoryVSIXId = 'Aderant.SoftwareFactory.vsix'
    $softwareFactoryVSIX = 'AderantSoftwareFactory.vsix'
    $downloadedSoftwareFactoryPath = (Join-Path (Get-BranchRootPath) SoftwareFactoryInstall)
    $dropPathForSoftwareFactoryVSIX
    [xml]$manifest = Get-Content (Get-ExpertProductManifest)
    [System.Xml.XmlNode]$softwareFactoryModule = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object{ $_.Name.Contains("SoftwareFactory")}
    
    invoke-expression "$buildScripts\Build-Libraries.ps1"
    $dropPathForSoftwareFactoryVSIX = (PathToBinaries $softwareFactoryModule (Get-DropRootPath))
    
    VSIXInstaller.exe /q /uninstall:$softwareFactoryVSIXId 
    
    if(!(Test-Path $downloadedSoftwareFactoryPath)) {
        New-Item $downloadedSoftwareFactoryPath -ItemType directory
    }
    
    Copy-Item -Path $dropPathForSoftwareFactoryVSIX\* -Destination $downloadedSoftwareFactoryPath -Force -Recurse
                
    pushd $downloadedSoftwareFactoryPath
    VSIXInstaller.exe /q $softwareFactoryVSIX
    popd
              
    write "updated $softwareFactoryVSIX"
}

# builds the current module using default parameters
function Start-BuildForCurrentModule {
    $shell = ".\BuildModule.ps1 -moduleToBuildPath (Get-CurrentModulePath) -dropRoot $dropRoot"    
    pushd $buildScripts
    invoke-expression $shell
    popd   
}

# gets dependencies for current module using default parameters
function Get-DependenciesForCurrentModule {    
    $shell = ".\LoadDependancies.ps1 -modulesRootPath (Get-CurrentModulePath) -dropPath $dropRoot"    
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
    ResolveAndCopyUniqueBinModuleContent -modulePath $currentModuleRoot -copyToDirectory $binaries    
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
    $shell = ".\GetProduct.ps1 -productManifestPath $expertManifest -dropRoot $dropRoot -binariesDirectory $binaries -getDebugFiles 1"    
    pushd $packageScripts
    invoke-expression $shell
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

    if (!($action)) {
        write "No action specified."
        return
    }
    
    [string]$fullManifest = "$branchRoot\Environment\$manifestName.environment.xml"
        
    if (test-path $fullManifest) { 
    
    } else {
        write "Manifest file not found - you might get errors."
    }

    write "Action: $action"
    write "Manifest: $manifest"

    if ($action -eq "open") {
        write "Opening deployment manager..."
        &$binaries\DeploymentManager.exe $fullManifest           
    } else {        
        write "Starting deployment engine..."
        pushd $binaries 
        &$binaries\DeploymentEngine.exe $action $fullManifest                    
        popd    
    }
}

#sets up visual studio environment
function Set-Environment() {    
    Set-BranchDefaults
    Set-VisualStudioVersion
}

<#
 Re-set the local working branch
#>
function SwitchBranchTo($branchPath){
    if (!($branchPath)) {
        write "No branch specified - aborting"
        return
    }
    $script:branchRoot = $branchPath
    Set-LocalBranchPaths $branchRoot
    Set-BranchBinaries
    Set-DropRoot $devBranch    
    Set-CurrentModule (Get-CurrentModule)
}

function Set-VisualStudioVersion(){
    $file = "vsvars" + (GetDefaultValue "visualStudioVersion") + ".ps1"    
    $vsVarsFilePath = Join-Path $localModulesRoot "Build.Infrastructure\Src\Build"    
    $shell = Join-Path $vsVarsFilePath $file
    # run the script   
    &($shell) 
}

<#
 Sets up branch details
#>
function Set-BranchDefaults () {                
    $script:branchRoot = Set-BranchRoot                
    Set-LocalBranchPaths $script:branchRoot            
    $script:dropRoot = Set-DropRoot
    Set-BranchBinaries                                 
}

function Set-BranchBinaries(){
    $binariesPath = (Join-Path $script:branchRoot \Binaries)
    $script:binaries = $binariesPath
    write-host "binaries         : $script:binaries"
    [system.io.directory]::CreateDirectory($binariesPath)
}

<#
 Sets the path to the drop server branch root
 e.g. \\na.aderant.com\ExpertSuite\Dev\Workflow
#>
function Set-DropRoot($branchName){

    if(!($branchName)){
        $dropRoot = (GetDefaultValue "dropRootUNCPath")
    }else{
        $dropRoot = Join-Path "\\na.aderant.com\ExpertSuite\Dev" $branchName    
    }    
    write-host "dropRoot         : $dropRoot"
    
    if(Test-Path $dropRoot){
        return $dropRoot
    }else{
        $defaultsLocation = Join-Path (Get-ScriptFolder) "Defaults.xml"
    
        Throw "Please check your defaults at [$defaultsLocation] for dropRootUNCPath, the path [$dropRoot] is not valid"
    }        
}

<#
 Set the local paths and report to user
#>
function Set-LocalBranchPaths($rootPath){    
    $script:buildScripts = Join-Path $rootPath "\Modules\Build.Infrastructure\Src\Build"
    $script:moduleBuildScripts = Join-Path $rootPath "\Build"    
    $script:localModulesRoot = Join-Path $rootPath "\Modules"
    $script:packageScripts = Join-Path $rootPath "\Modules\Build.Infrastructure\Src\Package"
    $script:moduleScripts = Join-Path $rootPath "\Modules\Build.Infrastructure\Src\ModuleCreator"
    $script:defaultsScripts = Join-Path $rootPath "\Modules\Build.Infrastructure\Src"    
    $script:expertManifest = Join-Path $packageScripts "ExpertManifest.xml"    
    $script:devBranch = (new-object System.Io.DirectoryInfo $rootPath).Name    
    ReportBranchPathsToHost    
}

<#
 Write details for local paths to host
#>
function ReportBranchPathsToHost(){
    write-host "devBranch        : $devBranch"
    write-host "branchRoot       : $branchRoot"
    write-host "buildScripts     : $buildScripts"
    write-host "packageScripts   : $packageScripts"        
    write-host "expertManifest   : $expertManifest"
    write-host "localModulesRoot : $localModulesRoot"
}

<#
 Sets the path to your local branch root
 e.g. C:\Source\Workflow
#>
function Set-BranchRoot(){
    $branchRoot = (GetDefaultValue "devBranchFolder")    
            
    if (Test-Path $branchRoot) {
        return $branchRoot        
    }else{
        $defaultsLocation = Join-Path (Get-ScriptFolder) "Defaults.xml"
        throw "Please check your defaults at [$defaultsLocation] for devBranchFolder, the path [$branchRoot] is not valid"
    }            
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
    write "usf -> Installs the latest version of the Software Factory"
    write "Get-Product    -> Gets the defined product using the default manifest file (For help run get-help Get-Product)"
    write "SwitchBranchTo -> Switch's your local setup to another branch"        
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
set-alias usf Install-LatestSoftwareFactory

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

# developer experience
Export-ModuleMember -function Set-CurrentModule
Export-ModuleMember -function Copy-BinariesFromCurrentModule
Export-ModuleMember -function Start-BuildForCurrentModule
Export-ModuleMember -function Get-DependenciesForCurrentModule
Export-ModuleMember -function Get-LocalDependenciesForCurrentModule
Export-ModuleMember -function SwitchBranchTo
Export-ModuleMember -function Get-Product
Export-ModuleMember -function Start-DeploymentEngine
Export-ModuleMember -function New-BuildModule
Export-ModuleMember -function Install-LatestSoftwareFactory

Export-ModuleMember -variable branchRoot

Export-ModuleMember -alias nm
Export-ModuleMember -alias cm
Export-ModuleMember -alias cm?
Export-ModuleMember -alias bm
Export-ModuleMember -alias gd
Export-ModuleMember -alias gdl
Export-ModuleMember -alias de
Export-ModuleMember -alias cb
Export-ModuleMember -alias gdp
Export-ModuleMember -alias usf
