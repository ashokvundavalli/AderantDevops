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
$global:workspace

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
        Write-Warning "The branch directory does not exist. Call Set-ExpertBranchInfo for initial setup of local directory and branch info"
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
function Initialise-BuildLibraries {    
    invoke-expression "$BuildScriptsDirectory\Build-Libraries.ps1"   
}    
    
function ResolveBranchName($branchPath){    
    if (IsMainBanch $branchPath){            
        $name = "MAIN"
    } elseif (IsDevBanch $branchPath){
        $name = $branchPath.Substring($branchPath.LastIndexOf("dev\", [System.StringComparison]::OrdinalIgnoreCase))
    } elseif (IsReleaseBanch $branchPath){        
        $name = $branchPath.Substring($branchPath.LastIndexOf("releases\", [System.StringComparison]::OrdinalIgnoreCase))
    }
    return $name
}

function IsDevBanch([string]$name){
    return $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsReleaseBanch([string]$name){
    return $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsMainBanch([string]$name){
    return $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase)
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
    if ((IsDevBanch $global:BranchName) -or (IsReleaseBanch $global:BranchName)) {
        $previousBranchContainer = $global:BranchName.Substring(0,$global:BranchName.LastIndexOf("\"))
        $previousBranchName = $global:BranchName.Substring($global:BranchName.LastIndexOf("\")+1)
    }elseif((IsMainBanch $global:BranchName)){        
        $previousBranchName = "MAIN"
        $changeToContainerFromMAIN = $true
    }
    
    if ((IsDevBanch $name) -or (IsReleaseBanch $name)){    
        $newBranchContainer = $name.Substring(0,$name.LastIndexOf("\"))     
        $newBranchName = $name.Substring($name.LastIndexOf("\")+1)
    } elseif ((IsMainBanch $name)){
        $newBranchName = "MAIN"    
    }    
    
    if ($changeToContainerFromMAIN){
        Switch-BranchFromMAINToContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    } else {
        Switch-BranchFromContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }    
    #Set common paths
    $global:BranchModulesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Modules" )
    
    $global:BranchBinariesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Binaries" )    
    if((Test-Path $global:BranchBinariesDirectory) -eq $false){    
        New-Item -Path $global:BranchBinariesDirectory -ItemType Directory
    }
    
    $global:BranchEnvironmentDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Environment")   
    if ((Test-Path $global:BranchEnvironmentDirectory) -eq $false){    
        New-Item -Path $global:BranchEnvironmentDirectory -ItemType directory
    }    
}

<#
 we need to cater for the fact MAIN is the only branch and not a container like dev or release
#>
function Switch-BranchFromMAINToContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName) {
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

function Set-CurrentModule($name, [switch]$quiet){                
    if (!($name)) {        
        if(!($global:CurrentModuleName)){
            Write-Warning "No current module is set"        
            return        
        }else{                                         
            Write-Host "The current module is [$global:CurrentModuleName] on the branch [$global:BranchName]"
            return 
        }
    }        
    if (-not ($quiet)) {
        Write-Host "Setting information for the module $name"
    }
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

function Get-CurrentModule() {
    return Get-ExpertModule -ModuleName $global:CurrentModuleName
}

<#
.Synopsis 
    Starts dbgen.exe. This is very similar to Update-Database -interactive. You may want to use that instead.
.Description   
    Starts dbgen.exe found in your expert source directory. This is very similar (but inferior) to Update-Database -interactive. You might want to use that instead.
#>
function Start-dbgen() {
    #Normally found at: C:\CMS.NET\Bin\dbgen.exe
    $dbgen = [System.IO.Path]::Combine($global:BranchExpertSourceDirectory, "dbgen\dbgen.exe")
    Invoke-Expression $dbgen
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
update CMS_DB_OPTION set OPTION_VALUE = '{0}', LAST_MODIFIED = getdate()
where OPTION_CODE in ('PERMIT_NULLLOSS', 'PERMIT_DATALOSS')
commit
"@
        # set PERMIT_NULLLOSS and PERMIT_DATALOSS to true
        $command = "sqlcmd -S $server -d $db -E -Q `"" + [string]::Format($query, "Y") + "`""
        Invoke-Expression $command
        
        $shell = "powershell -NoProfile -NoLogo `"$global:PackageScriptsDirectory\DeployDatabase.ps1`" -environmentManifestPath `"$fullManifest`" -expertSourceDirectory `"$global:BranchExpertSourceDirectory`" -interactive:$" + $interactive
        # Invoke-Expression falls on its face here due to a bug with [switch] - if used the switch argument cannot be converted to a switch parameter
        # which is very annoying
        # http://connect.microsoft.com/PowerShell/feedback/details/742084/powershell-v2-powershell-cant-convert-false-into-swtich-when-using-file-param
        cmd /c $shell

        # reset PERMIT_NULLLOSS and PERMIT_DATALOSS to false
        $command = "sqlcmd -S $server -d $db -E -Q `"" + [string]::Format($query, "N") + "`""
        Invoke-Expression $command
    } else {
        Write-Error "No manifest specified at path: $fullManifest"
    }	
}

<# 
.Synopsis 
    Installs the latest version of the Software Factory
.Description
    Will uninstall the previous vsix and then install the latest version from the drop location
#>
function Install-LatestSoftwareFactory([switch]$local) {  
    if ($local) {
        Install-LatestVisualStudioExtension -module "Libraries.SoftwareFactory" -local
    }
    else {
    Install-LatestVisualStudioExtension -module "Libraries.SoftwareFactory"
}
}

<# 
.Synopsis 
    Installs the latest version of the given module
.Description
    Will uninstall the previous vsix and then install the latest version from the drop location
.Example
    Install-LatestVisualStudioExtension SDK.Database 
    Will install the latest version of the SDK.Database project
#>
function Install-LatestVisualStudioExtension(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateSet('SDK.Database', 'Libraries.SoftwareFactory')]
        [String]$module,
        [switch]$local) {

    $installDetails = $null

    if ($module -eq "SDK.Database") {
        $info = @{}
        $info.ProductManifestName = "SDK.Database"
        $info.ExtensionName = "Aderant.Database.Project"
        $info.ExtensionFile = "Aderant.Database.Package.vsix"	
        $installDetails = New-Object –TypeName PSObject –Prop $info
    }

    if ($module -eq "Libraries.SoftwareFactory") {
        $info = @{}
        $info.ProductManifestName = "SoftwareFactory"
        $info.ExtensionName = "Aderant.SoftwareFactory"
        $info.ExtensionFile = "AderantSoftwareFactory.vsix"	
        $installDetails = New-Object –TypeName PSObject –Prop $info
    }

    if ($local) {
        Install-LatestVisualStudioExtensionImpl $installDetails -local
    }
    else {
    Install-LatestVisualStudioExtensionImpl $installDetails
}
}

function Install-LatestVisualStudioExtensionImpl($installDetails, [switch]$local) {
    
    # uninstall the extension
    Write-Host "Uninstalling $($installDetails.ProductManifestName)..."  
    $vsix = "VSIXInstaller.exe"
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null

    # take VSIX out of local source directory
    if ($local) {
        Write-Host "Attempting to install $($info.ProductManifestName) from local source directory."
        $vsixFile = [System.IO.Path]::Combine($global:BranchExpertSourceDirectory, $info.ExtensionFile)
    }

    # take VSIX from drop folder
    else {
        Write-Host "Attempting to install $($info.ProductManifestName) from drop folder."
    $localInstallDirectory = [System.IO.Path]::Combine($global:BranchLocalDirectory, $info.ProductManifestName + ".Install")
    
    [xml]$manifest = Get-Content $global:ProductManifest
    [System.Xml.XmlNode]$module = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object{ $_.Name.Contains($info.ProductManifestName)}
    
    Invoke-Expression "$BuildScriptsDirectory\Build-Libraries.ps1"
    $dropPathVSIX = (GetPathToBinaries $module $global:BranchServerDirectory)
            
    if (!(Test-Path $localInstallDirectory)) {
        New-Item $localInstallDirectory -ItemType directory
    } else {
        DeleteContentsFrom $localInstallDirectory
    }
    
    CopyContents -copyFrom $dropPathVSIX -copyTo $localInstallDirectory
                    
    $vsixFile = [System.IO.Path]::Combine($localInstallDirectory, $info.ExtensionFile)
    }
    
    if ([System.IO.File]::Exists($vsixFile)) {
        $lastWriteTime = (Get-ChildItem $vsixFile).LastWriteTime
        Write-Host "VSIX updated on $lastWriteTime"
        Write-Host "Installing $($info.ProductManifestName). Please wait..."
        Start-Process -FilePath $vsix -ArgumentList "/quiet $vsixFile" -Wait -PassThru | Out-Null 
        $errorsOccurred = Output-VSIXLog
        if (-not $errorsOccurred) {
        Write-Host "Updated $($info.ProductManifestName). Restart Visual Studio for the changes to take effect."
        } else {
            Write-Host
            Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'TOOLS -> Exensions and Updates' check if there is the 'Software Factory' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try this command again."
    }
}
}

Function Output-VSIXLog {
    $errorsOccurred = $false
    $temp = $env:TEMP
    $lastLogFile = Get-ChildItem $temp | Where { $_.Name.StartsWith("VSIX") } | Sort LastWriteTime | Select -last 1
    if ($lastLogFile -ne $null) {
        $logFileContent = Get-Content $lastLogFile.FullName
        foreach ($line in $logFileContent) {
            if ($line.Contains("Exception")) {
                $errorsOccurred = $true
                Write-Host -ForegroundColor Red $line
                notepad $lastLogFile.FullName
            }
        }
    }
    return $errorsOccurred
}

# builds the current module using default parameters
function Start-BuildForCurrentModule([string]$clean, [bool]$debug) {
    # Parameter must be a string as we are shelling out which we can't pass [switch] to
    $shell = ".\BuildModule.ps1 -moduleToBuildPath $global:CurrentModulePath -dropRoot $global:BranchServerDirectory -cleanBin $clean"
        
    if ($debug) {
        $shell += " -debug"    
    } 

    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd   
}

# gets dependencies for current module using default parameters
function Get-DependenciesForCurrentModule([switch]$onlyUpdated) {    
    if (Test-Path $global:BuildScriptsDirectory\LoadDependencies.ps1) {
        if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {
            Write-Warning "The current module is not set so the binaries will not be copied"
        } else {
            $shell = ".\LoadDependencies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory -buildScriptsDirectory $global:BuildScriptsDirectory"
            if ($onlyUpdated) {
                $shell += " -onlyUpdated"
            }
            pushd $global:BuildScriptsDirectory
             Invoke-Expression $shell
             popd
             return    
        }
    }

    $shell = ".\LoadDependancies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory -buildScriptsDirectory $global:BuildScriptsDirectory"
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
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

function Copy-BinariesFromCurrentModule () {
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
function Get-Product ([switch]$onlyUpdated) {
    $buildInfrastructure = $global:PackageScriptsDirectory.Replace("Package", "")
    $shell = ".\GetProduct.ps1 -productManifestPath $global:ProductManifest -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString) -buildLibrariesPath $buildInfrastructure"
    if($onlyUpdated){
        $shell += " -onlyUpdated"
    }
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
    If specified will build the sepcified modules and any modules which depend on them.
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
    param ([string[]]$workflowModuleNames, [switch] $changeset = $false, [switch] $clean = $false, [switch]$getDependencies = $false, [switch] $copyBinaries = $false, [switch] $downstream = $false, [switch] $getLatest = $false, [switch] $continue, [string[]] $getLocal, [string[]] $exclude, [string] $skipUntil, [switch]$debug)
    $moduleBeforeBuild = $null
    
    $currentWorkingDirectory = Get-Location
    
    if (!$workflowModuleNames) {
      if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
         $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name})
         $workflowModuleNames = @($moduleBeforeBuild)
      }
    }

    $builtModules = @{}
    
    if (!$getLocal) {
        [string[]]$getLocal = @()
    }
    
    if (!$exclude) {
        [string[]]$exclude = @()
    }
    
    if ($continue) {
        if (!$global:LastBuildRemainingModules) {
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
		write ""
		write "Retrieving Expert modules for current changeset ..."
        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModulesWithPendingChanges($global:BranchModulesDirectory)
		write "Done."
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
    
    [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModules($workflowModuleNames)
           
    if ((Test-Path $BranchLocalDirectory) -ne $true) {
        write "Branch Root path does not exist: '$BranchLocalDirectory'"
    }    
        
    [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = Sort-ExpertModulesByBuildOrder -BranchPath $BranchLocalDirectory -Modules $workflowModuleNames    
    
    if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
        Write-Warning "After sorting builds by order the following modules were excluded."
        Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

        (Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -Property Name -PassThru) | Select-Object -Property Name 

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
		write ""
		write "Retrieving downstream modules"

        write ("-> Analyzing " + $module.Name + " ...")
        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:workspace.DependencyAnalyzer.GetDownstreamModules($modules)
     
        $modules = Sort-ExpertModulesByBuildOrder -BranchPath $BranchLocalDirectory -Modules $modules
        $modules = $modules | Where { $_.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::Test }       
		write "Done."
    }
    
    $modules = $modules | Where {$exclude -notcontains $_}
    
    write ""
    write "********** Build Overview *************"
    $count = 0
    $weHaveSkipped = $false
    foreach($module in $modules) {
      $count++
      $skipMarkup = ""
      if ($skipUntil -eq $module) {
             $weHaveSkipped = $true
      }
      if ($skipUntil -and $weHaveSkipped -ne $true){
        $skipMarkup = " (skipped)"
      }
      write "$count. $module $skipMarkup"
    }
    write ""
    write ""
    write "Press Ctrl+C to abort"
    Start-Sleep -m 2000

    $weHaveSkipped = $false

    foreach ($module in $modules) {
      if ($skipUntil -eq $module) {
        $weHaveSkipped = $true
      }

      # If the user specified skipUntil then we will skip over the modules in the list until we reach the specified one.
      if ($skipUntil -and $weHaveSkipped -eq $false) {
         Write-Host "************* $module *************"
         Write-Host "   Skipping  "
         # Add the module to the list of built modules 
         if (!$builtModules.ContainsKey($module.Name)) {
           $builtModules.Add($module.Name, $module)
           $global:LastBuildBuiltModules = $builtModules
         }
      } else {
          # We either have not specified a skip or we have already skipped the modules we need to
          Set-CurrentModule $module.Name
          if ($getLatest){
            Get-LatestSourceForModule $module.Name -branchPath $BranchLocalDirectory
          }

          if ($getDependencies -eq $true){
            Get-DependenciesForCurrentModule
          }

          $dependencies = Get-ExpertModuleDependencies -BranchPath $BranchLocalDirectory -SourceModule $module -IncludeThirdParty $true
          Write-Host "************* $module *************"
          foreach ($dependencyModule in $dependencies) {
            Write-Debug "Module dependency: $dependencyModule"

            if (($dependencyModule -and $dependencyModule.Name -and $builtModules -and $builtModules.ContainsKey($dependencyModule.Name)) -or ($getLocal | Where {$_ -eq $dependencyModule})) {
              $sourcePath = Join-Path $BranchLocalDirectory Modules\$dependencyModule\Bin\Module             

              if ($dependencyModule.ModuleType -eq [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
                # Probe the new style ThirdParty path
                $root = [System.IO.Path]::Combine($BranchLocalDirectory, "Modules", "ThirdParty")
                if ([System.IO.Directory]::Exists($root)) {
                    $sourcePath = [System.IO.Path]::Combine($root, $dependencyModule, "Bin")
                } else {
                    # Fall back to the old style path
                    $root = [System.IO.Path]::Combine($BranchLocalDirectory, "Modules")
                    $sourcePath = [System.IO.Path]::Combine($root, $dependencyModule, "Bin")
                }
              }    

              if (-not [System.IO.Directory]::Exists($sourcePath)) {
                throw "The path $sourcePath does not exist"
              }

              Write-Debug "Local dependency source path: $sourcePath"

              $targetPath = Join-Path $BranchLocalDirectory Modules\$module
              Copy-Item -Path $sourcePath\* -Destination $targetPath\Dependencies -Force -Recurse -Verbose
            }
          }
      
          # Do the Build
          if ($module.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
              Start-BuildForCurrentModule $clean $debug

              pushd $currentWorkingDirectory
                      
              # Check for errors
              if ($LASTEXITCODE -eq 1) {                  
                  throw "Build of $module Failed"
              }
          }
      
          # Add the module to the list of built modules 
          if (!$builtModules.ContainsKey($module.Name)){
            $builtModules.Add($module.Name, $module)
            $global:LastBuildBuiltModules = $builtModules
          }      

          # Copy binaries to drop folder
          if ($copyBinaries -eq $true){
            Copy-BinariesFromCurrentModule
          }
        }
      
      [string[]]$global:LastBuildRemainingModules = $global:LastBuildRemainingModules | Where  {$_ -ne $module}
    }
    
    $global:LastBuildRemainingModules = $null
    
  if ($moduleBeforeBuild) {
    cm $moduleBeforeBuild
  }
  pushd $currentWorkingDirectory
}

<# 
.Synopsis 
    Builds the current module on server.
.Description
    Builds the current module on server.
.Example
     bm -getDependencies -clean ; Build-ExpertModuleOnServer
    If a local build succeeded, a server build will then be kicked off for current module.
#>
function Build-ExpertModuleOnServer([string] $ModuleName) {    
    $sourcePath = $null;

    if (!($ModuleName)){
        $ModuleName = $global:CurrentModuleName;
    }
    if (!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq ""){
        Write-Warning "No module specified.";
        return;
    }
    $sourcePath = $BranchName.replace('\','.') + "." + $ModuleName;

    Write-Warning "Build Attempted on server, if you do not wish to watch this build log you can now press 'CTRL+C' to exit.";
    Write-Warning "Exiting will not cancel your build on server.";
    Write-Warning "...";
    Write-Warning "You can use 'popd' to get back to your previous directory.";
    Write-Warning "You can use 'New-ExpertBuildDefinition' to create a new build definition for current module if non-exist."
    
    Invoke-Expression "tfsbuild start http://tfs:8080/tfs ExpertSuite $sourcePath";
    Invoke-Expression "popd";
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
    if (!$noget) {
        $cmd = "xcopy \\na.aderant.com\expertsuite\Main\Build.Tools\Current\* /S /Y $PackageScriptsDirectory"
        Invoke-Expression $cmd
    }
    pushd $PackageScriptsDirectory; .\Patching\BuildPatch.ps1
    popd
}


function Get-ExpertModulesInChangeset {
   return $global:workspace.GetModulesWithPendingChanges($global:BranchModulesDirectory)
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

    # Setup PowerShell script unit test environment
    InstallPester

    $global:workspace = new-object -TypeName Aderant.Build.Providers.ModuleWorkspace -ArgumentList $global:BranchModulesDirectory,"http://tfs:8080/tfs/ADERANT","ExpertSuite"    
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo($newBranch, [switch] $SetAsDefault) {    
    if ($global:BranchName -Contains $newBranch) {
        Write-Host "The magic unicorn has refused your request."
        return
    }

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
.PARAMETER getDependencies
    This will get the latest dependencies for the selected module before opening it up in visual studio.
.PARAMETER getLatest
    This will get the latest source code for the selected module before opening it up in visual studio.
.EXAMPLE
        Open-ModuleSolution Libraries.Presentation
    Will open the Libraries.Presentation solution in visual studio
    
#>
function Open-ModuleSolution([string] $ModuleName, [switch] $getDependencies, [switch]$getLatest) {
# BUG: usage with both a module name and -getDependencies while you have Build.Infrastructure selected will cause some error text and fail to return you to Build.Infrastructure after its opened up your specified module in VS.
    if (($getDependencies) -and -not [string]::IsNullOrEmpty($ModuleName)) {
        if (-not [string]::IsNullOrEmpty($global:CurrentModuleName)) {
            $prevModule = Get-CurrentModule
        }
        Set-CurrentModule $moduleName
    }
    if(!($ModuleName)){
        $ModuleName = $global:CurrentModuleName
    }
    if(!($ModuleName) -or $ModuleName -eq $null -or $ModuleName -eq ""){
        Write-Host -ForegroundColor Yellow "No module specified"
        return
    }
    if ($getDependencies) {
        Write-Host "Getting Dependencies for module $ModuleName"
        Get-DependenciesForCurrentModule
    }
    if ($getLatest) {
        Write-Host "Getting latest source for module $ModuleName"
        Get-Latest -ModuleName $ModuleName;
    }
    Write-Host "Opening solution for module $ModuleName"
    $moduleSolutionPath = Join-Path $BranchLocalDirectory "Modules\$ModuleName\$ModuleName.sln"
    if(Test-Path $moduleSolutionPath) {
        Invoke-Expression "devenv $moduleSolutionPath"
    } else {
        "There is no solution file at $moduleSolutionPath"
    }
    if (($prevModule) -and (Get-CurrentModule -ne $prevModule)) {
        Set-CurrentModule $prevModule
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

# This will start the UI Test Runner for provisioning automated test environments and starting the tests.
function Start-UITests([switch]$noUpdate, [string]$TestRunnerPath) {
    if ([string]::IsNullOrWhiteSpace($TestRunnerPath)) {
        $TestRunnerPath = "C:\temp\UIAutomation"
    }
    if (-not $noUpdate) {
	$UITestRunnerPath = "\\na.aderant.com\packages\Infrastructure\Automation\UIAutomation\UIAutomation.TestRunner\5.3.1.0"
	$shell = Join-Path $BuildScriptsDirectory Build-Libraries.ps1
	$shell = "& { . $shell; PathToLatestSuccessfulBuild $UITestRunnerPath }"
	$latestSuccessfulBuild = powershell -noprofile -command $shell
        if ( -not (Test-Path $TestRunnerPath)) {
            New-Item $TestRunnerPath -type directory
        }
        Invoke-Expression "robocopy.exe /S /PURGE /NJH /NJS /NS /NC /NP /NFL /NDL /R:5 /W:1 /MT:3 $latestSuccessfulBuild $TestRunnerPath"
	}
    $testTool = Join-Path $TestRunnerPath "UITester.exe"
    Invoke-Expression "$testTool -BranchModulesDirectory $BranchModulesDirectory -BuildScriptsDirectory $BuildScriptsDirectory -PackageScriptsDirectory $PackageScriptsDirectory -environmentmanifestpath C:\ExpertShare\environment.xml -branchserverdirectory $BranchServerDirectory"
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
        $parser = New-Object Aderant.Build.AutoCompletionParser $line, $lastword, $aliases
    
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
Add-BranchExpansionParameter -CommandName "Move-Shelveset" -ParameterName "TargetBranch"
Add-BranchExpansionParameter -CommandName "Move-Shelveset" -ParameterName "SourceBranch"

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
Add-ModuleExpansionParameter -CommandName "Get-DependenciesForCurrentModule" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-Product" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ProviderModules" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ConsumerModules"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependencies" -ParameterName "SourceModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependsOn" -ParameterName "TargetModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-DownstreamExpertModules" -ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-ExpertModule" -ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Get-ExpertModules" -ParameterName "ModuleNames" -IsDefault
Add-ModuleExpansionParameter –CommandName "Open-ModuleSolution" –ParameterName "ModuleName" -IsDefault  
Add-ModuleExpansionParameter –CommandName "Get-Latest" –ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter –CommandName "Start-Redeployment" –ParameterName "CopyBinariesFrom"
Add-ModuleExpansionParameter -CommandName "Copy-BinToEnvironment" -ParameterName "ModuleNames" -IsDefault
Add-ModuleExpansionParameter -CommandName "Open-Directory" -ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "New-ExpertBuildDefinition" -ParameterName "ModuleName" -IsDefault
Add-ModuleExpansionParameter -CommandName "Install-LatestSoftwareFactory" -ParameterName "local"

Add-ModuleExpansionParameter -CommandName "Clean" -ParameterName "moduleNames" -IsDefault
Add-ModuleExpansionParameter -CommandName "Scorch" -ParameterName "moduleNames" -IsDefault

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

<#
.Synopsis 
    cd to the specified directory.
.Description
    Will change your working directory to the specified directory. NOTE: this is similar to odir.
.PARAMETER BuildScripts
    cd to your powershell directory. (often a symlink to BuildScriptsForBranch)
.PARAMETER BuildScriptsForBranch
    cd to your build scripts in Build.Infrastructure.
.PARAMETER Binaries
    cd to your branch binaries directory.
.PARAMETER ExpertSource
    cd to your branch ExpertSource, often in your binaries directory.
.PARAMETER LocalBranch
    cd to your currently selected branch on your local disk.
.PARAMETER ServerBranch
    cd to your currently selected branch on the drop server.
.PARAMETER AllModules
    cd to the Modules directory for your currently selected branch.
.PARAMETER Module
    cd to the currently selected module's directory.
.PARAMETER ModuleBin
    cd to the bin directory for your currently selected module.
.PARAMETER ModuleDependencies
    cd to the dependency directory for your currently selected module.
.PARAMETER ExpertShare
    cd to the ExpertShare for your currently selected branch.
.PARAMETER ExpertLocal
    cd to your expert local directory, normally where the binaries for your services are stored.
.PARAMETER SharedBin
    cd to your sharedbin directory with the shared binaries for your services.
.EXAMPLE
        Change-Directory -ModuleBin
    Will cd to your currently selected module's bin directory.
#>
function Change-Directory(
    [switch]$BuildScripts, [switch]$BuildScriptsForBranch, [switch]$Binaries, [switch]$ExpertSource, [switch]$LocalBranch, [switch]$ServerBranch, [switch]$AllModules,
    [switch]$Module, [switch]$ModuleBin, [switch]$ModuleDependencies,
    [switch]$ExpertShare, [switch]$ExpertLocal, [switch]$SharedBin) {
    
    if (
        -not $BuildScripts -and
        -not $BuildScriptsForBranch -and
        -not $Binaries -and 
        -not $ExpertSource -and 
        -not $LocalBranch -and 
        -not $ServerBranch -and 
        -not $AllModules -and 
        -not $Module -and 
        -not $ModuleBin -and 
        -not $ModuleDependencies -and 
        -not $ExpertShare -and 
        -not $ExpertLocal -and 
        -not $SharedBin) {
        
        Write-Host -ForegroundColor Yellow "Please include at least one location.";
        Write-Host "-BuildScripts, -BuildScriptsForBranch";
        Write-Host "-Binaries, -ExpertSource, -ExpertShare, -ExpertLocal, -SharedBin";
        Write-Host "-AllModules, -Module, -ModuleBin, -ModuleDependencies";
        Write-Host "-LocalBranch, -ServerBranch";
    }

    if ($BuildScripts) {
        $path = [System.IO.Path]::Combine("C:\Users\", [Environment]::UserName);
        $path = [System.IO.Path]::Combine($path, "Documents\WindowsPowerShell");
        cd $path;
    }
    if ($BuildScriptsForBranch) {
        cd $global:BuildScriptsDirectory;
    }
    if ($Binaries) { #product bin
        cd "$global:BranchBinariesDirectory";
    } 
    if ($ExpertSource) {
        cd "$global:BranchExpertSourceDirectory";
    } 
    if ($LocalBranch) {
        cd "$global:BranchLocalDirectory";
    } 
    if ($ServerBranch) {
        cd "$global:BranchServerDirectory";
    }
    if ($AllModules) {
        cd "$global:BranchModulesDirectory";
    } 

    if ($Module) {
        if (Test-Path variable:global:CurrentModulePath) {
            cd "$global:CurrentModulePath";
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ModuleBin) {
        if (Test-Path variable:global:CurrentModulePath) {
            $path = [System.IO.Path]::Combine($global:CurrentModulePath, "Bin");
            cd $path;
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ModuleDependencies) {
        if (Test-Path variable:global:CurrentModulePath) {
            $path = [System.IO.Path]::Combine($global:CurrentModulePath, "Dependencies");
            cd $path;
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected."
        }
    }
    if ($ExpertShare) {
        # C:\ExpertShare
        $path = Get-EnvironmentFromXml "/environment/@networkSharePath";
        cd $path;
    }
    if ($ExpertLocal) {
        # C:\AderantExpert\Local
        $path = Get-EnvironmentFromXml "/environment/servers/server/@expertPath"
        cd $path;
    }
    if ($SharedBin) {
        # C:\AderantExpert\Local\SharedBin
        $expertLocalPath = Get-EnvironmentFromXml("/environment/servers/server/@expertPath");
        $path = [System.IO.Path]::Combine($expertLocalPath, "SharedBin");
        cd $path;
    }
    #TODO: TFS root.
}

<#
.Synopsis 
    Open the specified directory in Exploiter.
.Description
    Will open all specified directories that exist in Explorer.exe
.PARAMETER BuildScripts
    Opens your powershell directory. (often a symlink to BuildScriptsForBranch)
.PARAMETER BuildScriptsForBranch
    Opens your build scripts in Build.Infrastructure.
.PARAMETER Binaries
    Opens your branch binaries directory.
.PARAMETER ExpertSource
    Opens your branch ExpertSource, often in your binaries directory.
.PARAMETER LocalBranch
    Opens your currently selected branch on your local disk.
.PARAMETER ServerBranch
    Opens your currently selected branch on the drop server.
.PARAMETER AllModules
    Opens the Modules directory for your currently selected branch.
.PARAMETER Module
    Opens the currently selected module's directory.
.PARAMETER ModuleBin
    Opens the bin directory for your currently selected module.
.PARAMETER ModuleDependencies
    Opens the dependency directory for your currently selected module.
.PARAMETER ExpertShare
    Opens the ExpertShare for your currently selected branch.
.PARAMETER ExpertLocal
    Opens your expert local directory, normally where the binaries for your services are stored.
.PARAMETER SharedBin
    Opens your sharedbin directory with the shared binaries for your services.
.EXAMPLE
        Open-Directory -ModuleBin -SharedBin
    Will open up both the binary directory of the selected module, and the sharedbin in ExpertLocal.
#>
function Open-Directory(
    [switch]$BuildScripts, [switch]$BuildScriptsForBranch, [switch]$Binaries, [switch]$ExpertSource, [switch]$LocalBranch, [switch]$ServerBranch, [switch]$AllModules,
    [switch]$Module, [switch]$ModuleBin, [switch]$ModuleDependencies,
    [switch]$ExpertShare, [switch]$ExpertLocal, [switch]$SharedBin,
    [string]$ModuleName) {
    
    #TODO: Could add a $paths which enables the user to specify arbritrary paths.
    
    if (
        -not $BuildScripts -and
        -not $BuildScriptsForBranch -and
        -not $Binaries -and 
        -not $ExpertSource -and 
        -not $LocalBranch -and 
        -not $ServerBranch -and 
        -not $AllModules -and 
        -not $Module -and 
        -not $ModuleBin -and 
        -not $ModuleDependencies -and 
        -not $ExpertShare -and 
        -not $ExpertLocal -and 
        -not $SharedBin) {
        
        Write-Host -ForegroundColor Yellow "Please include at least one location.";
        Write-Host "-BuildScripts, -BuildScriptsForBranch";
        Write-Host "-Binaries, -ExpertSource, -ExpertShare, -ExpertLocal, -SharedBin";
        Write-Host "-AllModules, -Module, -ModuleBin, -ModuleDependencies";
        Write-Host "-LocalBranch, -ServerBranch";
    }

    if ($BuildScripts) {
        $path = [System.IO.Path]::Combine("C:\Users\", [Environment]::UserName);
        $path = [System.IO.Path]::Combine($path, "Documents\WindowsPowerShell");
        Explorer($path);
    }
    if ($BuildScriptsForBranch) {
        Explorer($global:BuildScriptsDirectory);
    }
    if ($Binaries) { #product bin
        Explorer("$global:BranchBinariesDirectory");
    } 
    if ($ExpertSource) {
        Explorer("$global:BranchExpertSourceDirectory");
    } 
    if ($LocalBranch) {
        Explorer("$global:BranchLocalDirectory");
    } 
    if ($ServerBranch) {
        Explorer("$global:BranchServerDirectory");
    }
    if ($AllModules) {
        Explorer("$global:BranchModulesDirectory");
    }
    if ($Module -or $ModuleBin -or $ModuleDependencies) {
        if (Test-Path variable:global:CurrentModulePath) {
            if ([string]::IsNullOrWhiteSpace($ModuleName)) {
                $selectedModulePath = $global:CurrentModulePath;
            } else {
                $firstHalf = $global:CurrentModulePath.Substring(0, $global:CurrentModulePath.LastIndexOf("\"));
                $selectedModulePath = [System.IO.Path]::Combine($firstHalf, $ModuleName);
            }
            if (Test-Path variable:selectedModulePath) {
                if ($Module) {
                    Explorer("$selectedModulePath");
                }
                if ($ModuleBin) {
                    $path = [System.IO.Path]::Combine($selectedModulePath, "Bin");
                    Explorer($path);
                }
                if ($ModuleDependencies) {
                    $path = [System.IO.Path]::Combine($selectedModulePath, "Dependencies");
                    Explorer($path);			
                }
            } else {
                Write-Host -ForegroundColor Yellow "You seem to have misspelled the name of the module (or it doesn't exist in the current branch)."
            }
        } else {
            Write-Host -ForegroundColor Yellow "Sorry you do not have a module selected. Please select one first."
        }
    }
    if ($ExpertShare) {
        # C:\ExpertShare
        Explorer(Get-EnvironmentFromXml "/environment/@networkSharePath");
        
    } 
    if ($ExpertLocal) {
        # C:\AderantExpert\Local
        Explorer(Get-EnvironmentFromXml "/environment/servers/server/@expertPath")
    }
    if ($SharedBin) {
        # C:\AderantExpert\Local\SharedBin
        $expertLocalPath = Get-EnvironmentFromXml("/environment/servers/server/@expertPath");
        $path = [System.IO.Path]::Combine($expertLocalPath, "SharedBin");
        Explorer($path);
    }
    #TODO: TFS
}
function Explorer([string]$path, [switch]$quiet) {
    if (Test-Path $path) {
        Invoke-Expression "explorer.exe $path";
        if (-not $quiet) {
            Write-Host "Opened: $path";
        }
    } else {
        Write-Host -ForegroundColor Red -NoNewline "  Directory does not exist for:";
        Write-Host " $path";
    }
}
function Get-EnvironmentFromXml([string]$xpath) {
    #I'd love it if this returned an object model representation such as Environment.expertPath or Environment.networkSharePath
    if ([string]::IsNullOrEmpty($xpath)) {
        Write-Host -ForegroundColor Yellow "You need to specify an xpath expression";
        return $null;
    }	
    if (Test-Path variable:global:BranchBinariesDirectory) {
        $environmentXmlPath = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml");
        [xml]$xml = Get-Content $environmentXmlPath;
        $returnValue = Select-Xml $xpath $xml;
        return $returnValue;
    } else {
        Write-Host -ForegroundColor Yellow "I don't know where your Branch Binaries Directory is.";
    }
    return $null;
}

<#
.Synopsis 
    Start de remove; de deploy; for the current branch
.Description
    Will redeploy your Expert Environment for the current branch. This command beeps twice when it has finished.
.PARAMETER getDependencies
    Will get dependencies for the current selected module.
.PARAMETER GetLatestForBranch
    Will get the latest source code for your current branch.
.PARAMETER GetProduct
    Will retrieve the latest product binaries before starting the new deployment.
.PARAMETER GetProductZip
    Will retrieve the latest build all product zip binaries before starting the new deployment. (if you specify both -GetProduct and -GetProductZip then you will get the zip file)
.PARAMETER upd
    Will run Update-Database before starting the new deployment.
.PARAMETER CopyBinariesFrom
    Copy binaries from the bin folder of these specified modules into expert source. If you combine this with -GetProduct, this will happen AFTER getting the latest product.
.EXAMPLE
        Start-Redeployment -getDependencies -GetLatestForBranch -GetProduct -CopyBinariesFrom Applications.Deployment, Libraries.Deployment
    Will first get latest source code for the branch,
    get dependencies for current module,
    remove your existing deployment,
    get product,
    Copy binaries from Applications.Deployment,
    and finally start your new deployment.
#>
function Start-Redeployment([switch]$GetProduct, [switch]$GetProductZip, [switch]$upd, [switch]$dontKillRunning, [switch]$GetLatestForBranch, [switch]$getDependencies, $CopyBinariesFrom) {
    $start = get-date
    if ($GetLatestForBranch) { 
        Get-Latest -branch
    }
    if ($getDependencies) {
        if(!($global:CurrentModuleName)){
            Write-Warning "No current module is set. Cannot get dependencies"	
        } else {
            Get-DependenciesForCurrentModule
        }
    }
    if (-not $dontKillRunning) {
        $running = Get-Process
        foreach ($item in Get-Item -Filter *.exe -Path C:\ExpertShare\*) {
            foreach($process in $running) {
                if ($process.name -ieq $item.BaseName) {
                    $name = $item.BaseName
                    Write-Host "Attempting to kill $name as it still appears to be running."
                    Stop-Process -Name $item.BaseName -Force
                }
            }
        }
    }
    de remove;
    if ($GetProductZip) {
        Get-ProductZip
    } elseif ($GetProduct) {
        Get-Product
    }
    if (-not [String]::IsNullOrEmpty($CopyBinariesFrom)) {
        $startModule = Get-CurrentModule
        foreach ($module in $CopyBinariesFrom) {
            Set-CurrentModule -quiet $module
            Write-Host -ForegroundColor White -NoNewline "Copying binaries from: "
            Write-Host -ForegroundColor DarkCyan -NoNewline $module
            Copy-BinariesFromCurrentModule
            Write-Host -ForegroundColor White "Done."
        }
        Set-CurrentModule -quiet $startModule
    }
    #Could we add restore DB here?
    if ($upd) {
        Update-Database
    }
    de deploy
    $end = get-date
    $start
    $end
    $end-$start
    Get-Beep; Get-Beep #Audio feedback that we have finished.
}

function Get-Beep() {
    $([char]7)
}

function tryToRemove ($path){
    Try {
        "Removing $path";
        Remove-Item $path -recurse -Force;
    } Catch
       {
        [system.exception]
        "caught a system exception"
    }
}

<# 
.Synopsis 
    Cleans the files from the web modules which are not in the source control.
.Description
    Following files will be deleted in the module:
        Dependencies\* 
        Bin\*
        Src\$ModuleName\bin\*
    Also other files could be removed if scorch flag is on.
.PARAMETER ModuleNames
    An array of module names for which you want to clean.
.PARAMETER Scorch
    Use this switch if you want to do a scorch as well.
.EXAMPLE
    Clean Web.Presentation, Web.Foundation -Scorch
#>
function Clean($moduleNames = $global:CurrentModuleName, [switch]$scorch) {
    foreach ($moduleName in $moduleNames) {
        $path = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"

        tryToRemove $path\Dependencies\*
        tryToRemove $path\Bin\*
        tryToRemove $path\Src\$ModuleName\bin\*        
    }
    if ($Scorch){
        Scorch $moduleNames;
    }
}

<# 
.Synopsis 
    Scorch the given modules.  
.PARAMETER ModuleNames
    An array of module names for which you want to scorch.
.EXAMPLE
    Scorch Web.Presentation, Web.Foundation
#>
function Scorch($moduleNames = $global:CurrentModuleName) {
    foreach ($moduleName in $moduleNames) {
        $path = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"
        invoke-expression "tfpt scorch $path /recursive /noprompt";      
    }
}


function InstallPester() {
    Write-Debug "Installing Pester"

    $dir = "cmd /c rmdir `"$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Pester`""
    Invoke-Expression $dir

    if ($BranchLocalDirectory -ne $null) {
        $expression = "cmd /c mklink /d `"$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Pester`" `"$BranchLocalDirectory\Modules\Build.Infrastructure\Src\Profile\Pester\`""
        Invoke-Expression $expression
    }
}


function Help ($searchText) {

    if($searchText){
        $searchText = "*$searchText*";
        foreach ($func in $helpList) {	
            $functionName = $func.function;
            $aliasName = $func.alias;
            
            if(($functionName -like $searchText)  -or ($aliasName -like $searchText)){
                Write-Host -ForegroundColor Green -NoNewline "$functionName, $aliasName ";
                Write-Host (Get-Help $functionName).Synopsis;
            }
        }   
        return;
    }

    $AderantModuleLocation = Get-AderantModuleLocation
    Write-Host "Using Aderant Module from : $AderantModuleLocation"
    Write-Host ""
    Write-Host "The following aliases are defined: "
    Write-Host ""
    Write-Host "ed  -> Show the current environment details for the branch your working on."  
    Write-Host ""  
    Write-Host "css -> Compile Aderant.*.less for Aderant.*.css and Aderant.*.css.map (if it is a SMB module) for the current module"
    Write-Host "cm  -> Sets the current module when passed the module name, or tells you what is the current module if not passed the module name"    
    Write-Host "cwm	-> Copies Local Web.Foundation and Web.Presentation shared files to current module for web modules"
    Write-Host "vs  -> Opens the current module in Visual Studio. Specify a module name to open different one."
    Write-Host "nm  -> Creates a new module with the given name in the root of your current branch's modules directory"
    Write-Host "gd  -> Gets the current module dependencies"
    Write-Host "gdf -> Gets the current module dependencies from another local module"
    Write-Host "gdl -> Gets the current module dependencies (copy from local bin)"
    Write-Host "gwd -> Gets Web Dependencies for the current module"
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
    Write-Host "merge  -> merges a source branch into a target branch."
    Write-Host "odir   -> Opens the specified Directory. Use without any parameters to get a list or try Get-Help Open-Directory -detailed"
    Write-Host "Move-Shelveset -> Is a shortcut for tfpt unshelve /migrate. If your name contains a space please quote it. Use Get-Help Move-Shelveset"
    Write-Host "redeploy   -> Does a de remove; de deploy for you. Can get latest source code and/or product if you desire. Use Get-Help redeploy."
    Write-Host "Get-Latest -> Gets the latest source from TFS for the current module. Optionally specify a module name."    
    Write-Host "Get-Product    -> Gets the defined product using the default manifest file (For more info run get-help Get-Product)."
    Write-Host "Get-ProductNoDebugFiles -> Gets the defined product without the pdb's"
    Write-Host "Get-ProductZip -> Pulls the Zipped version of the Product from the Build.All"
    Write-Host "Switch-Branch  -> Switch's your local setup to another branch (eg. Switch-Branch dev\BranchName)." 
    Write-Host "Branch-Module  -> Branches a module into a target branch." 
    Write-Host "Generate-SystemMap -> Generates a System Map to the expertsource folder of the current branch."
    Write-Host ""  
    Write-Host "Also note that module and branch names will auto-complete when pressing tab"     
    Write-Host ""  
}

function Show-RandomTip {
    $randomIndex = Get-Random -minimum 0 -maximum ($helpList.length-1);
    $functionToTip = $helpList[$randomIndex];
    $functionToTipName = $functionToTip.function;
    $functionToTipAlias = $functionToTip.alias
    Write-Host -ForegroundColor Green "$functionToTipName, $functionToTipAlias";
    Get-Help $functionToTipName	
    Write-Host -ForegroundColor Green "--------------------------------------------------------------------------------------------------"
}

# export functions and variables we want external to this script
$functionsToExport = @( 
    @{ function='Branch-Module';					alias='branch'},
    @{ function='Build-ExpertModules';					alias='bm'},
    @{ function='Build-ExpertModuleOnServer';				alias='bms'},
    @{ function='Build-ExpertPatch'},
    @{ function='Change-Directory';					alias='cdir'},
    @{ function='Build-ExpertPatch'},
    @{ function='Change-Directory';						alias='cdir'},
    @{ function='Copy-BinariesFromCurrentModule';		alias='cb'},
    @{ function='Copy-SharedFilesToCurrentWebModule';	alias='cwm'},
    @{ function='Disable-ExpertPrompt'},
    @{ function='Enable-ExpertPrompt'},
    @{ function='Generate-SystemMap'},
    @{ function='Get-AderantModuleLocation'},
    @{ function='Get-Beep';						alias='beep'},
    @{ function='Get-CurrentModule'},
    @{ function='Get-DependenciesForCurrentModule';			alias='gd'},
    @{ function='Get-DependenciesFrom';					alias='gdf'},
    @{ function='Get-EnvironmentFromXml'},
    @{ function='Get-ExpertModulesInChangeset'},
    @{ function='Get-Latest'},
    @{ function='Get-LocalDependenciesForCurrentModule';		alias='gdl'},
    @{ function='Get-Packages';						alias='bp'},
    @{ function='Get-PackagesCopy';					alias='bpc'},
    @{ function='Get-Product'},
    @{ function='Get-ProductNoDebugFiles'},
    @{ function='Get-ProductZip'},
    @{ function='Help'},
    @{ function='Install-LatestSoftwareFactory';			alias='usf'},
    @{ function='Install-LatestVisualStudioExtension'},
    @{ function='Move-Shelveset'},
    @{ function='New-BuildModule';					alias='nm'},
    @{ function='New-ExpertManifestForBranch'},
    @{ function='Open-Directory';					alias='odir'},
    @{ function='Open-ModuleSolution';					alias='vs'},
    @{ function='Set-CurrentModule';					alias='cm'},
    @{ function='Set-Environment'},
    @{ function='Set-ExpertBranchInfo'},
    @{ function='Show-RandomTip';					alias='tip'},   
    @{ function='Start-BuildForCurrentModule'},
    @{ function='Start-dbgen';						alias='dbgen'},
    @{ function='Start-DeploymentEngine';				alias='de'},
    @{ function='Start-DeploymentManager';				alias='dm'},
    @{ function='Start-Redeployment';					alias='redeploy'},
    @{ function='SwitchBranchTo';					alias='Switch-Branch'},
    @{ function='Update-Database';					alias='upd'},
    @{ function='Write-ToHostEnvironmentDetails';			alias='ed'}
);	

$helpList =@();

#exporting the functions and aliases
foreach ($toexport in $functionsToExport) {			
    Export-ModuleMember -function $toexport.function
    if($toexport.alias){
        set-alias $toexport.alias  $toexport.function
        Export-ModuleMember -alias $toexport.alias
    }	

    #building up a help list to show in the RandomTip function and also the help function
    $helpList = $helpList + @{ function=$toexport.function; alias = $toexport.alias;} ;		
}

#also include all the csharp commandlets in the help list 
foreach( $csharpCommandlet in Get-Command -Module 'dynamic_code_module_Aderant.Build, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'){
    $helpList = $helpList + @{ function=$csharpCommandlet.Name;};
}

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
Export-ModuleMember -function Get-DependenciesFrom
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
Export-ModuleMember -function Install-LatestVisualStudioExtension
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
Export-ModuleMember -function Open-Directory
Export-ModuleMember -function Change-Directory
Export-ModuleMember -Function Start-dbgen
Export-ModuleMember -function Copy-SharedWebFilesToCurrentModule # Holy shit - The magic unicorn disapproves of this name!
Export-ModuleMember -Function Move-Shelveset
Export-ModuleMember -Function Start-Redeployment
Export-ModuleMember -Function Get-EnvironmentFromXml
Export-ModuleMember -Function Start-UITests

Export-ModuleMember -function Clean
Export-ModuleMember -function Scorch

Export-ModuleMember -function Clean
Export-ModuleMember -function Scorch

Export-ModuleMember -alias nm
Export-ModuleMember -alias ed
Export-ModuleMember -alias cm
Export-ModuleMember -alias cwm
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
Export-ModuleMember -alias odir
Export-ModuleMember -alias cdir
Export-ModuleMember -Alias redeploy
Export-ModuleMember -alias beep
Export-ModuleMember -alias dbgen

Enable-ExpertPrompt
