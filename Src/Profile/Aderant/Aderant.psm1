. $PSScriptRoot\Caching.ps1

function Measure-Command() {
[CmdletBinding()]
param (    
    [ScriptBlock] $expression,
    [parameter(Mandatory=$False,ValueFromRemainingArguments=$True)]
    [string] $name
  )
  
   process { 
        Microsoft.PowerShell.Utility\Measure-Command -Expression $expression -OutVariable perf
        
        if ($name) {
          Write-Debug ("Performance: `"$name`" took: " + $perf.TotalMilliseconds + " milliseconds")
        }
    }
}

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
[string]$global:ProductManifestPath
[string]$global:CurrentModuleName
[string]$global:CurrentModulePath
[string]$global:CurrentModuleBuildPath
[PSModuleInfo]$global:CurrentModuleFeature = $null
[string[]]$global:LastBuildBuiltModules
[string[]]$global:LastBuildRemainingModules
[string[]]$global:LastBuildGetLocal
[switch]$global:LastBuildGetDependencies
[switch]$global:LastBuildCopyBinaries
[switch]$global:LastBuildDownstream
[switch]$global:LastBuildGetLatest
$global:workspace

$titles = @(
    "Reticulating Splines",
    "Attempting to Lock Back-Buffer",
    "Calculating Inverse Probability Matrices",
    "Compounding Inert Tessellations",
    "Decomposing Singular Values",
    "Dicing Models",
    "Extracting Resources",
    "Obfuscating Quigley Matrix",
    "Fabricating Imaginary Infrastructure",
    "Activating Deviance Threshold",
    "Simulating Program Execution",
    "Abstracting Loading Procedures",
    "Unfolding Helix Packet",
    "Iterating Chaos Array",
    "Calculating Native Restlessness",
    "Filling in the Blanks",
    "Mitigating Time-Stream Discontinuities",
    "Blurring Reality Lines",
    "Reversing the Polarity of the Neutron Flow",
    "Dropping Expert Database",
    "Formatting C:\",
    "Replacing Coffee Machine",
    "Duplicating Offline Cache"
    "Replacing Headlight Fluid"
)

$Host.UI.RawUI.WindowTitle = Get-Random $titles

function Check-Vsix() {
    [CmdletBinding()]
    param (
        [parameter(Mandatory=$true)][string] $vsixName,
        [parameter(Mandatory=$true)][string] $vsixId,
        [parameter(Mandatory=$false)][string] $idInVsixmanifest = $vsixId)

    Begin {
        [Reflection.Assembly]::Load("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089") | Out-Null             

        function Output-VSIXLog {
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

        function InstallVsix() {
            try {
                $vsixFile = gci -Path $ShellContext.BuildToolsDirectory -File -Filter "$vsixName.vsix" -Recurse | Select-Object -First 1

                if (-not ($vsixFile)) {
                    return
                }

                Write-Host "Installing $vsixName..."

                # uninstall the extension
                Write-Host "Uninstalling $vsixName..."

                $vsInstallPath = $env:VS140COMNTOOLS
                $vsix = "$vsInstallPath..\IDE\VSIXInstaller.exe"

                Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($vsixId)" -Wait -PassThru | Out-Null

                if ($vsixFile.Exists) {
                    Write-Host "Installing VSIX..."

                    Start-Process -FilePath $vsix -ArgumentList "/quiet $($vsixFile.FullName)" -Wait -PassThru | Out-Null
                    $errorsOccurred = Output-VSIXLog

                    if (-not $errorsOccurred) {
                        Write-Host "Updated $($vsixName). Restart Visual Studio for the changes to take effect."
                    } else {
                        Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'Tools -> Extensions and Updates' check if there is the '$vsixName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try again."
                    }
                } else {
                    Write-Host -ForegroundColor Yellow "No $vsixName VSIX found"
                }
            } catch {
                Write-Host "Exception occurred while restoring packages" -ForegroundColor Red
                Write-Host $_ -ForegroundColor Red
            }
        }
    }

    Process {
        Set-StrictMode -Version Latest      

        if (-Not $idInVsixmanifest) {
            $idInVsixmanifest = $vsixId
        }
       
        $version = ""
              
        $currentVsixFile = Join-Path -Path $ShellContext.BuildToolsDirectory -ChildPath "$vsixName.vsix"

        $extensionsFolder = Join-Path -Path $env:LOCALAPPDATA -ChildPath \Microsoft\VisualStudio\14.0\Extensions\
        $developerTools = Get-ChildItem -Path $extensionsFolder -Recurse -Filter "$vsixName.dll" -Depth 1

        $developerTools.ForEach({
            $manifest = Join-Path -Path $_.DirectoryName -ChildPath extension.vsixmanifest
            if (Test-Path $manifest) {
                [xml]$manifestContent = Get-Content $manifest  
                $manifestVersion = $manifestContent.PackageManifest.Metadata.Identity.Version

                $version = [System.Version]::Parse($manifestVersion)
                }  
            })       

        $zipFile = $null
        $reader = $null
        
        if ($version -eq "") {
            Write-Host -ForegroundColor Red " $vsixName for Visual Studio is not installed."
            Write-Host -ForegroundColor Red " If you want it, install them manually from $currentVsixFile"
        } else {
            # Bail out if we have already checked if this version is installed (most often the case)
            $lastVsixCheckCommit = $ShellContext.GetRegistryValue("", "LastVsixCheckCommit")
            if ($lastVsixCheckCommit -ne $null) {
                if ($lastVsixCheckCommit -eq $ShellContext.CurrentCommit) {
                    Write-Debug "CurrentCommit: $($ShellContext.CurrentCommit)"
                    Write-Debug "LastVsixCheckCommit: $($lastVsixCheckCommit)"
                    
                    Write-Host -ForegroundColor DarkGreen "Your $vsixName is up to date."
                    return
                }
            }

            Write-Host " * Found installed version $version"

            if (-not (Test-Path $currentVsixFile)) {
                Write-Host -ForegroundColor Red "Error: could not find file $currentVsixFile"
                return
            }
            
            $zipFile = [System.IO.Compression.ZipFile]::OpenRead($currentVsixFile)
            $rawFiles = $zipFile.Entries      
            
            foreach($rawFile in $rawFiles) {
                if ($rawFile.Name -eq "extension.vsixmanifest") {    
                    try {            
                        $archiveEntryStream = $rawFile.Open()
                    
                        $reader = [System.IO.StreamReader]::new($archiveEntryStream)
                        [xml]$currentManifestContent = $reader.ReadToEnd()
                    
                        $foundVersion = [System.Version]::Parse($currentManifestContent.PackageManifest.Metadata.Identity.Version)
                        Write-Host " * Current version is $foundVersion"
                    
                        if ($foundVersion -gt $version) {
                            Write-Host
                            Write-Host "Updating $vsixName..."
                            InstallVsix
                        } else {
                            Write-Host -ForegroundColor DarkGreen "Your $vsixName is up to date."
                        }
                    } finally {
                        $archiveEntryStream.Dispose()
                        $reader.Dispose()
                    }
                    break               
                }
            }
        }
    }

    End {
        if ($zipFile) {
            $zipFile.Dispose()
        }
        if ($reader) {
            $reader.Dispose()
        }
    }
    
}

<#
Expert specific variables
#>
$ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { "C:\AderantExpert\Install\DeploymentEngine.exe" }
$ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { "C:\AderantExpert\Install\DeploymentManager.exe" }

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
    $global:BranchEnvironmentDirectory =Join-Path -Path $global:BranchLocalDirectory -ChildPath \Environment

    if ((Test-Path $global:BranchLocalDirectory) -ne $true) {
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
    [xml]$manifest = Get-Content $global:ProductManifestPath
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

function Set-ScriptPaths {

    if ([System.IO.File]::Exists("$global:BranchModulesDirectory\ExpertManifest.xml")) {
        $root = Resolve-Path "$PSScriptRoot\..\..\..\"

        $global:BuildScriptsDirectory = Join-Path -Path $root -ChildPath Src\Build
        $global:PackageScriptsDirectory = Join-Path -Path $root -ChildPath Src\Package
        $global:ModuleCreationScripts = Join-Path -Path $root -ChildPath Src\ModuleCreator
        $global:ProductManifestPath = Join-Path -Path $global:BranchModulesDirectory -ChildPath ExpertManifest.xml
    } else {
        $global:BuildScriptsDirectory = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\Build
        $global:PackageScriptsDirectory = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\Package
        $global:ModuleCreationScripts = Join-Path -Path $global:BranchModulesDirectory -ChildPath \Build.Infrastructure\Src\ModuleCreator
        $global:ProductManifestPath = Join-Path -Path $global:PackageScriptsDirectory -ChildPath \ExpertManifest.xml
    }
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
    Write-Host "Change branch to $name"

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
        $newBranchContainer = "\"
    }

    $success = $false
    if ($changeToContainerFromMAIN){
        $success = Switch-BranchFromMAINToContainer $newBranchContainer $newBranchName $previousBranchName
    } else {
        $success = Switch-BranchFromContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }

    if ($success -eq $false) {
        Write-Host -ForegroundColor Yellow "'$name' branch was not found on this machine."
        return $false
    }

    #Set common paths
    $global:BranchModulesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Modules" )

    $global:BranchBinariesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Binaries" )
    if ((Test-Path $global:BranchBinariesDirectory) -eq $false){
        New-Item -Path $global:BranchBinariesDirectory -ItemType Directory
    }

    return $true
}

<#
 we need to cater for the fact MAIN is the only branch and not a container like dev or release
#>
function Switch-BranchFromMAINToContainer($newBranchContainer, $newBranchName, $previousBranchName) {
    #change name and then container and remove extra backslash's
    $globalBranchName = ($global:BranchName -replace $previousBranchName,$newBranchName)
    $globalBranchName = $newBranchContainer+"\"+$globalBranchName

    if ($globalBranchName -eq "\") {
        return $false
    }

    # The strip logic assumes the last slash is the container separator, if the local dir ends with a slash it will break that assumption
    $global:BranchLocalDirectory = $global:BranchLocalDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar)

    #strip MAIN then add container and name
    $globalBranchLocalDirectory = $global:BranchLocalDirectory.Substring(0,$global:BranchLocalDirectory.LastIndexOf("\")+1)
    $globalBranchLocalDirectory = (Join-Path -Path $globalBranchLocalDirectory -ChildPath( Join-Path  -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $globalBranchLocalDirectory) -eq $false) {
        return $false
    }

    $global:BranchName = $globalBranchName
    $global:BranchLocalDirectory = $globalBranchLocalDirectory

    #strip MAIN then add container and name
    $global:BranchServerDirectory = $global:BranchServerDirectory.Substring(0,$global:BranchServerDirectory.LastIndexOf("\")+1)
    $global:BranchServerDirectory = (Join-Path -Path $global:BranchServerDirectory -ChildPath( Join-Path  -Path $newBranchContainer -ChildPath $newBranchName))

    $global:BranchServerDirectory = [System.IO.Path]::GetFullPath($global:BranchServerDirectory)

    return $true
}

<#
 we dont have to do anything special if we change from a container to other branch type
#>
function Switch-BranchFromContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName){
    #change name and then container and remove extra backslash's
    $globalBranchName = $global:BranchName.replace($previousBranchName,$newBranchName)
    $globalBranchName = $globalBranchName.replace($previousBranchContainer,$newBranchContainer)
    if(IsMainBanch $globalBranchName){
        $globalBranchName = [System.Text.RegularExpressions.Regex]::Replace($globalBranchName,"[^1-9a-zA-Z_\+]","");
    }

    if ($globalBranchName -eq "\") {
        return $false
    }

    $globalBranchLocalDirectory = $global:BranchLocalDirectory.Substring(0, $global:BranchLocalDirectory.LastIndexOf($previousBranchContainer));
    $globalBranchLocalDirectory = (Join-Path -Path $globalBranchLocalDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $globalBranchLocalDirectory) -eq $false -or $globalBranchLocalDirectory.EndsWith("ExpertSuite")) {
        return $false
    }

    $global:BranchName = $globalBranchName
    $global:BranchLocalDirectory = $globalBranchLocalDirectory

    $global:BranchServerDirectory = $global:BranchServerDirectory.Substring(0, $global:BranchServerDirectory.LastIndexOf($previousBranchContainer));
    $global:BranchServerDirectory = (Resolve-Path -Path ($global:BranchServerDirectory + $newBranchContainer + "\" + $newBranchName)).ProviderPath

    $global:BranchServerDirectory = [System.IO.Path]::GetFullPath($global:BranchServerDirectory)

    return $true
}

function Set-CurrentModule($name, [switch]$quiet) {
    if ($global:CurrentModuleFeature) {
        if (Get-Module | Where-Object -Property Name -eq $global:CurrentModuleFeature.Name) {
            Write-Host "Removing module: $($global:CurrentModuleFeature.Name)"
            Remove-Module $global:CurrentModuleFeature
        }
        $global:CurrentModuleFeature = $null
    }

    if (!($name)) {
        if (!($global:CurrentModuleName)) {
            Write-Warning "No current module is set"
            return
        } else {
            Write-Host "The current module is [$global:CurrentModuleName] on the branch [$global:BranchName]"
            return
        }
    }
    if (-not ($quiet)) {
        Write-Host "Setting information for the module $name"
    }

    if ($name -eq ".") {
        $name = Resolve-Path $name
    }

    if ([System.IO.Path]::IsPathRooted($name)) {
        $global:CurrentModulePath = $name
        $global:CurrentModuleName = ([System.IO.DirectoryInfo]::new($global:CurrentModulePath)).Name
        Write-Debug "Setting repository: $name"
        Import-Module $PSScriptRoot\AderantGit.psm1

        if (-not (Test-Path (Join-Path -Path $global:CurrentModulePath -ChildPath \Build\TFSBuild.*))){
            $global:CurrentModuleName = ""
        }

        if (IsGitRepository $global:CurrentModulePath) {
            SetRepository $global:CurrentModulePath
            Set-Location $global:CurrentModulePath
            global:Enable-GitPrompt
            return
        } elseif (IsGitRepository ([System.IO.DirectoryInfo]::new($global:CurrentModulePath).Parent.FullName)) {
            global:Enable-GitPrompt
        } else {
            Enable-ExpertPrompt
        }
    } else {
        $global:CurrentModuleName = $name

        Write-Debug "Current module [$global:CurrentModuleName]"
        $global:CurrentModulePath = Join-Path -Path $global:BranchModulesDirectory -ChildPath $global:CurrentModuleName
        Enable-ExpertPrompt
    }

    if ((Test-Path $global:CurrentModulePath) -eq $false) {
        Write-Warning "the module [$global:CurrentModuleName] does not exist, please check the spelling."
        $global:CurrentModuleName = ""
        $global:CurrentModulePath = ""
        return
    }

    Write-Debug "Current module path [$global:CurrentModulePath]"
    $global:CurrentModuleBuildPath = Join-Path -Path $global:CurrentModulePath -ChildPath "Build"

    $ShellContext.IsGitRepository = $true
}

function IsGitRepository([string]$path) {
    if ([System.IO.path]::GetPathRoot($path) -eq $path) {
        return $false
    }
    return @(gci -path $path -Filter ".git" -Recurse -Depth 1 -Attributes Hidden -Directory).Length -gt 0
}

function SetRepository([string]$path) {
    $ShellContext.IsGitRepository = $true

    [string]$currentModuleBuildDirectory = "$path\Build"

    if (Test-Path $currentModuleBuildDirectory) {
        [string]$featureModule = Get-ChildItem -Path $currentModuleBuildDirectory -Recurse | ? { $_.extension -eq ".psm1" -and $_.Name -match "Feature.*" } | Select-Object -First 1 | Select -ExpandProperty FullName
        if ($featureModule) {
            ImportFeatureModule $featureModule
        }
    }
}

function ImportFeatureModule([string]$featureModule) {
    Import-Module -Name $featureModule -Scope Global -WarningAction SilentlyContinue
    $global:CurrentModuleFeature = Get-Module | Where-Object -Property Path -eq $featureModule
    Write-Host "`r`nImported module: $($global:CurrentModuleFeature.Name)" -ForegroundColor Cyan
    Get-Command -Module $global:CurrentModuleFeature.Name
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

function OutputEnvironmentDetails {
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
    
    if ($global:CurrentModuleName -and $global:CurrentModulePath) {    
      Write-Host ""
      Write-Host "-----------------------------"
      Write-Host "Current Module Information"
      Write-Host "-----------------------------"
      Write-Host "Name :" $global:CurrentModuleName
      Write-Host "Path :" $global:CurrentModulePath
      Write-Host ""
    }
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
    Runs dbprepare.exe on the specified database. Defaults to version 81.
.Description
    Runs dbprepare.exe on the specified database. Defaults to version 81.
.PARAMETER database
    MANDATORY: The name of the database to use. 
.PARAMETER saPassword
    MANDATORY: The sa user password for the database server.
.PARAMETER server
    The name of the database server. Defaults to the local machine name.
.PARAMETER instance
    The database serverinstance that the database resides on.
.PARAMETER version
    The version to prep the database to. Defaults to 81.
.PARAMETER interactive
    Starts DBPREPARE in interactive mode
#>
function Prepare-Database (
        [Parameter(Mandatory=$true)]
        [string]$database,
        [Parameter(Mandatory=$true)]
        [string]$saPassword,
        [string]$server = $env:COMPUTERNAME,
        [string]$instance,
        [string]$version = "81",
        [switch]$interactive
        ) {

    [string]$cmsConnection
    if (-Not [string]::IsNullOrWhiteSpace($instance)) {
        $instance = "\$instance"    
        $cmsConnection = "[$server]`r`nVendor=1`r`nLocation=$server$instance`r`nDatabase=$database`r`nTitle=$server.$database`r`nForceManualLogin=0"
        Write-Debug "Running dbprepare against database: $database on server: $server$instance"
    } else {
        $cmsConnection = "[$server]`r`nVendor=1`r`nLocation=$server`r`nDatabase=$database`r`nTitle=$server.$database`r`nForceManualLogin=0"
        Write-Debug "Running dbprepare against database: $database on server: $server"
    }

    # Checks if CMS.INI is present in %APPDATA%\Aderant and create it if not. Appends connection details to CMS.INI if they are not present.
    $cmsPath = "$env:APPDATA\Aderant\CMS.INI"
    if (Test-Path $cmsPath) {
        if ((Select-String -InputObject (Get-Content $cmsPath | Out-String) -SimpleMatch "[$server]") -eq $null) {
            Write-Debug "Adding $server connection to existing CMS.INI file in %APPDATA%\Aderant."
            Add-Content $cmsPath "`r`n$cmsConnection"
        } else {
            # If there is an existing connection with incorrect data, dbprepare will open and wait for user input.
            Write-Debug "$server connection present in existing CMS.INI file in %APPDATA%\Aderant."
        }
    } else {
        Write-Debug "Creating CMS.INI file in %APPDATA%\Aderant with $server connection."
        New-Item $cmsPath -ItemType File -Value $cmsConnection
    }

    $dbPreparePath = "$global:BranchExpertSourceDirectory\dbgen\dbprepare.exe"
    $dbPrepareArgs = "target=$server$instance.$database autostart=1 autoclose=1 installshield=1 login=SA password=`"$saPassword`" ERRORLOG=`"$global:BranchBinariesDirectory\Logs\dbprep.log`" version=$version prep=database,other"

    if (-not $interactive.IsPresent) {
        $dbPrepareArgs = $dbPrepareArgs + " hide=1"
    }

    Write-Host "Starting dbprepare.exe from: $dbPreparePath"
    Write-Host "dbprepare.exe arguments: $dbPrepareArgs"

    Start-Process -FilePath $dbPreparePath -ArgumentList $dbPrepareArgs -Wait -PassThru | Out-Null
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

    Write-Warning "The 'upd' command is currently unavailable. Please use DBGen for now to update your database."

    return

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
    # Paket-TODO: don't perfom this check now, revisit this if necessary when we have successfully switched to Paket
    # (the current check is invalid anyway as it check a wrong SW Factory version in the wrong VS version)
    #if (Check-LatestSoftwareFactory) {
    #    Write-Host "No update necessary, you have the latest version"
    #} else {
        if ($local) {
            Install-LatestVisualStudioExtension -module "Libraries.SoftwareFactory" -local
        }
        else {
            Install-LatestVisualStudioExtension -module "Libraries.SoftwareFactory"
        }
    #}
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
        $info.ExtensionDisplayName = "Aderant Expert Database Project"
        $info.ExtensionName = "Aderant.Database.Project.1b755e8f-3540-4935-b959-e4d33f247bd2"
        $info.ExtensionFile = "Aderant.Database.ProjectSystem.vsix"
        $installDetails = New-Object –TypeName PSObject –Prop $info
    }

    if ($module -eq "Libraries.SoftwareFactory") {
        $info = @{}
        $info.ProductManifestName = "SoftwareFactory"
        $info.ExtensionDisplayName = "Aderant Software Factory"
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

        [xml]$manifest = Get-Content $global:ProductManifestPath
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

    Write-Host $vsixFile

    if ([System.IO.File]::Exists($vsixFile)) {
        $lastWriteTime = (Get-ChildItem $vsixFile).LastWriteTime
        Write-Host "VSIX updated on $lastWriteTime"
        Write-Host "Installing $($info.ProductManifestPathName). Please wait..."
        Start-Process -FilePath $vsix -ArgumentList "/quiet $vsixFile" -Wait -PassThru | Out-Null
        $errorsOccurred = Output-VSIXLog
        if (-not $errorsOccurred) {
        Write-Host "Updated $($info.ProductManifestPathName). Restart Visual Studio for the changes to take effect."
        } else {
            Write-Host
            $displayName = $info.ExtensionDisplayName
            Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'TOOLS -> Exensions and Updates' check if there is the '$displayName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try this command again."
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
function Start-BuildForCurrentModule([string]$clean, [bool]$debug, [bool]$release, [bool]$codeCoverage, [bool]$integration) {
    begin {
        Set-StrictMode -Version 2.0
    }

    process {
        # Parameter must be a string as we are shelling out which we can't pass [switch] to
        [string]$commonArgs = "-moduleToBuildPath $global:CurrentModulePath -dropRoot $global:BranchServerDirectory -cleanBin $clean"

        if ($debug) {
            $commonArgs += " -debug"
        } elseif ($release) {
            $commonArgs += " -release"
        }

        if ($integration) {
            $commonArgs += " -integration"
        }

        if ($codeCoverage) {
            $commonArgs += " -codeCoverage"
        }

        pushd $global:BuildScriptsDirectory
        Invoke-Expression -Command ".\BuildModule.ps1 $($commonArgs)"
        popd
    }
}

<#
.Synopsis
    Retrieves the dependencies required to build the current module
#>
function Get-DependenciesForCurrentModule([switch]$noUpdate, [switch]$showOutdated, [switch]$force) {
    if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        try {
            pushd $global:BuildScriptsDirectory
            & .\LoadDependencies.ps1 -modulesRootPath $global:CurrentModulePath -dropPath $global:BranchServerDirectory -update:(-not $noUpdate) -showOutdated:$showOutdated -force:$force
        } finally {
            popd
        }
    }
}

# gets dependencies for each module using default parameters
function Get-DependenciesForEachModule {
    Get-ExpertModules | % {
        if (-not ($_.Name.StartsWith("ThirdParty.")) `
            -and -not ($_.Name.StartsWith("ThirdParty.")) `
            -and -not ($_.Name -eq "Build.T4Task") `
            -and -not ($_.Name -eq "Thirdparty.ExifJS") `
            -and -not ($_.Name -eq "Thirdparty.Microsoft.Office.Interop") `
            -and -not ($_.Name -eq "Marketing.Help") `
            -and -not ($_.Name -eq "Tests.UIAutomation") `
            -and -not ($_.Name -eq "UIAutomation.Framework") `
            -and -not ($_.Name -eq "Expert.Help") `
            -and -not ($_.Name -eq "Installs.Marketing") `
            -and -not ($_.Name -eq "Internal.Licensing")) {
            Write-Host "Getting dependencies for $_..."
            cm $_.Name; gd
        }
    }
}


# gets dependencies for current module using default parameters
function Get-LocalDependenciesForCurrentModule {
    if (Test-Path $global:BuildScriptsDirectory\Load-LocalDependencies.ps1) {
        $shell = ".\Load-LocalDependencies.ps1 -moduleName $global:CurrentModuleName -localModulesRootPath $global:BranchModulesDirectory -serverRootPath $global:BranchServerDirectory"
        try {
            pushd $global:BuildScriptsDirectory
            invoke-expression $shell
        } finally {
            popd
        }
    }
}

function Copy-BinariesFromCurrentModule() {
    if ([string]::IsNullOrEmpty($global:CurrentModulePath)) {
        Write-Warning "The current module is not set so the binaries will not be copied"
    } else {
        Initialise-BuildLibraries

        pushd $global:BuildScriptsDirectory
        ResolveAndCopyUniqueBinModuleContent -modulePath $global:CurrentModulePath -copyToDirectory $global:BranchExpertSourceDirectory -suppressUniqueCheck $true
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
.PARAMETER onlyUpdated
    Switch to indicate that only updated modules should get pulled in.
.PARAMETER createBackup
    Switch to create a backup of the Binaries folder (named BinariesBackup in the same folder) after successfully retrieving the product.
    This is intended to be used by developers who call Copy-BinariesFromCurrentModules (cb) or Copy-BinToEnvironment and want to have a backup with the original files from the Get-Product call.
.EXAMPLE
    Get-Product -createBackup
#>
function Get-Product ([switch]$onlyUpdated, [switch]$createBackup) {
    $buildInfrastructure = $global:PackageScriptsDirectory.Replace("Package", "")

    & tf.exe vc "get" $global:ProductManifestPath

    pushd $global:PackageScriptsDirectory
    & .\GetProduct.ps1 -ProductManifestPath $global:ProductManifestPath -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -getDebugFiles 1 -systemMapConnectionString (Get-SystemMapConnectionString) -onlyUpdated:$onlyUpdated.ToBool()
    popd

    if ($createBackup) {
        Write-Host "Creating backup of Binaries folder."
        $backupPath = $global:BranchLocalDirectory + "\BinariesBackup"
        if (-not (Test-Path $backupPath)) {
            New-Item -ItemType Directory -Path $backupPath
        }
        Invoke-Expression "robocopy.exe $global:BranchBinariesDirectory $backupPath /MIR /SEC /TEE /R:2 /XD $global:BranchBinariesDirectory\ExpertSource\Customization" | out-null
        Write-Host "Backup complete."
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
    $shell = ".\GetProduct.ps1 -ProductManifestPathPath $global:ProductManifestPath -dropRoot $global:BranchServerDirectory -binariesDirectory $global:BranchBinariesDirectory -systemMapConnectionString (Get-SystemMapConnectionString)"
    pushd $global:PackageScriptsDirectory
    invoke-expression $shell | Out-Host
    popd
}

<#
.Synopsis 
    Displays the BuildAll version in the binaries directory of the current branch.
.Description
    WARNING: If you have done a Get-Product or Get-ProductZip since your last deployment, then it will show the version number of the Get-ProductZip rather than what is deployed.
.PARAMETER copyToClipboard
    If specified the BuildAll version will be copied to the clipboard.
#>
function Get-ProductBuild([switch]$copyToClipboard) {
    $versionFilePath = "$global:BranchBinariesDirectory\BuildAllZipVersion.txt"
    if ([System.IO.File]::Exists($versionFilePath)) {
        if ((Get-Content -Path $versionFilePath) -match "[^\\]*[\w.]BuildAll_[\w.]*[^\\]") {
            Write-Host "Current BuildAll version in $global:BranchName` branch:`r`n"
            Write-Host $Matches[0]
            if ($copyToClipboard) {
                Add-Type -AssemblyName "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
                [System.Windows.Forms.Clipboard]::SetText($Matches[0])
            }
        } else {
            Write-Error "Content of BuildAllZipVersion.txt is questionable."
        }
    } else {
        Write-Error "No BuildAllZipVersion.txt present in $global:BranchBinariesDirectory."
    }
}

<#
.Synopsis
    Gets the latest product zip from the BuildAll output and unzips to your BranchBinariesDirectory
.Description
    The binaries will be loaded into your branch binaries directory. e.g. <your_branch_source>\Binaries
#>
function Get-ProductZip([switch]$unstable) {
    Write-Host "Getting latest product zip from [$BranchServerDirectory]"
    $zipName = "ExpertBinaries.zip"
    [string]$pathToZip = (PathToLatestSuccessfulPackage -pathToPackages $BranchServerDirectory -packageZipName $zipName -unstable $unstable)

    if (-not $pathToZip) {
        return
    }

    Write-Host "Selected " $pathToZip

    $pathToZip = $pathToZip.Trim()
    DeleteContentsFromExcludingFile -directory $BranchBinariesDirectory "environment.xml"
    Copy-Item -Path $pathToZip -Destination $BranchBinariesDirectory
    $localZip =  (Join-Path $BranchBinariesDirectory $zipName)
    Write-Host "About to extract zip to [$BranchBinariesDirectory]"
    
    $zipExe = join-path $ShellContext.BuildToolsDirectory "\7z.exe"
    if (test-path $zipExe) {
        $SourceFile=$localZip
        $Destination=$BranchBinariesDirectory
        &$zipExe x $SourceFile "-o$Destination" -y
    } else {
        Write-Host "Falling back to using Windows zip util as 7-zip does not exist on this system"
        $shellApplication = new-object -com shell.application
        $zipPackage = $shellApplication.NameSpace($localZip)
        $destinationFolder = $shellApplication.NameSpace($global:BranchBinariesDirectory)
        $destinationFolder.CopyHere($zipPackage.Items())     
    }
    
    Write-Host "Finished extracting zip"
    [string]$versionFilePath = Join-Path $global:BranchBinariesDirectory "BuildAllZipVersion.txt"
    echo $pathToZip | Out-File -FilePath $versionFilePath
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
    param ([string[]]$workflowModuleNames, [switch] $changeset = $false, [switch] $clean = $false, [switch]$getDependencies = $false, [switch] $copyBinaries = $false, [switch] $downstream = $false, [switch] $getLatest = $false, [switch] $continue, [string[]] $getLocal, [string[]] $exclude, [string] $skipUntil, [switch]$debug, [switch]$release, [bool]$codeCoverage = $true, [switch]$integration, [switch]$codeCoverageReport)

    begin {
        Set-StrictMode -Version 2.0
    }

    process {
        if ($debug -and $release) {
            Write-Error "You can specify either -debug or -release but not both."
            return
        }

        if ($ShellContext.IsGitRepository) {
            Write-Error "You cannot run this command for a git repository. Use 'bm' or 'Invoke-Build' instead."
            return
        }

        $moduleBeforeBuild = $null

        try {
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

            [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $workflowModuleNames -ProductManifestPath $global:ProductManifestPath

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

                [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:workspace.DependencyAnalyzer.GetDownstreamModules($modules)

                $modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $modules -ProductManifestPath $global:ProductManifestPath
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

                    if ($builtModules -and $builtModules.Count -gt 0) {
                        $dependencies = Get-ExpertModuleDependencies -BranchPath $BranchLocalDirectory -SourceModule $module -IncludeThirdParty $true
                        Write-Host "************* $module *************"

                        foreach ($dependencyModule in $dependencies) {
                            Write-Debug "Module dependency: $dependencyModule"

                            if (($dependencyModule -and $dependencyModule.Name -and $builtModules.ContainsKey($dependencyModule.Name)) -or ($getLocal | Where {$_ -eq $dependencyModule})) {
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
                                CopyContents $sourcePath "$targetPath\Dependencies"
                            }
                        }
                    }

                    # Do the Build
                    if ($module.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::ThirdParty) {
                        Start-BuildForCurrentModule $clean $debug -codeCoverage $codeCoverage -integration $integration.IsPresent

                        pushd $currentWorkingDirectory

                        # Check for errors
                        if ($LASTEXITCODE -eq 1) {
                            throw "Build of $module Failed"
                        } elseif ($LASTEXITCODE -eq 0 -and $codeCoverage -and $codeCoverageReport.IsPresent) {
                            [string]$codeCoverageReport = Join-Path -Path $global:CurrentModulePath -ChildPath "Bin\Test\CodeCoverage\dotCoverReport.html"

                            if (Test-Path ($codeCoverageReport)) {
                                Write-Host "Displaying dotCover code coverage report."
                                Start-Process $codeCoverageReport
                            } else {
                                Write-Warning "Unable to locate dotCover code coverage report."
                            }
                        }
                    }

                    # Add the module to the list of built modules
                    if (!$builtModules.ContainsKey($module.Name)) {
                        $builtModules.Add($module.Name, $module)
                        $global:LastBuildBuiltModules = $builtModules
                    }

                    # Copy binaries to drop folder
                    if ($copyBinaries -eq $true) {
                        Copy-BinariesFromCurrentModule
                    }
                }

                [string[]]$global:LastBuildRemainingModules = $global:LastBuildRemainingModules | Where  {$_ -ne $module}
            }

            $global:LastBuildRemainingModules = $null

            if ($moduleBeforeBuild) {
                cm $moduleBeforeBuild
            }
        } finally {
            pushd $currentWorkingDirectory
            [Console]::TreatControlCAsInput = $false
        }
    }
}

<#
.Synopsis
    Builds the current module on server.
.Description
    Builds the current module on server.
.Example
     bm -getDependencies -clean ; Build-ExpertModulesOnServer -downstream
    If a local build succeeded, a server build will then be kicked off for current module.
#>
function Build-ExpertModulesOnServer([string[]] $workflowModuleNames, [switch] $downstream = $false) {
    $moduleBeforeBuild = $null;
    $currentWorkingDirectory = Get-Location;

    if (!$workflowModuleNames) {
        if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
            $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name});
            $workflowModuleNames = @($moduleBeforeBuild);
        }
    }

    if (!($workflowModuleNames)) {
        write "No modules specified.";
        return;
    }

    [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModules($workflowModuleNames)

    if ((Test-Path $BranchLocalDirectory) -ne $true) {
        write "Branch Root path does not exist: '$BranchLocalDirectory'"
    }

    [Aderant.Build.DependencyAnalyzer.ExpertModule[]] $modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $workflowModuleNames -ProductManifestPath $global:ProductManifestPath

    if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
        Write-Warning "After sorting builds by order the following modules were excluded.";
        Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

        (Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -Property Name -PassThru) | Select-Object -Property Name

        $message = "Do you want to continue anyway?";
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($null, $message, $options, 0)

        if ($result -ne 0) {
            write "Module(s) not found."
            return
        }
    }

    if ($downstream -eq $true) {
        write ""
        write "Retrieving downstream modules"

        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:workspace.DependencyAnalyzer.GetDownstreamModules($modules)

        $modules = Sort-ExpertModulesByBuildOrder -BranchPath $global:BranchModulesDirectory -Modules $modules -ProductManifestPath $global:ProductManifestPath
        $modules = $modules | Where { $_.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::Test }
        write "Done."
    }

    $modules = $modules | Where {$exclude -notcontains $_}

    write ""
    write "********** Build Overview *************"
    $count = 0
    $weHaveSkipped = $false
    foreach($module in $modules) {
        $count++;
        write "$count. $module";
    }
    write "";
    write "";
    write "Press Ctrl+C now to abort.";
    Start-Sleep -m 2000;

    foreach($module in $modules) {
        $sourcePath = $BranchName.replace('\','.') + "." + $module;

        Write-Warning "Build(s) started attempting on server for $module, if you do not wish to watch this build log you can now press 'CTRL+C' to exit.";
        Write-Warning "Exiting will not cancel your current build on server. But it will cancel the subsequent builds if you have multiple modules specified, i.e. -downstream build.";
        Write-Warning "...";
        Write-Warning "You can use 'popd' to get back to your previous directory.";
        Write-Warning "You can use 'New-ExpertBuildDefinition' to create a new build definition for current module if non-exist."

        Invoke-Expression "tfsbuild start http://tfs:8080/tfs ExpertSuite $sourcePath";
    }
    if ($moduleBeforeBuild) {
        cm $moduleBeforeBuild;
    }
    pushd $currentWorkingDirectory;
}

<#
.Synopsis
    Forced stopping a visual studio solution for specific module(s).
.Description
    Forced stopping a visual studio solution for specific module(s).
.Example
    Kill-VisualStudio -killAll, Kill-VisualStudio Web.Case
    Forced shutting down all opened visual studio solutions; forced shutting down visual studio solution of Web.Case.
#>
function Kill-VisualStudio([string[]] $workflowModuleNames, [switch] $killAll = $false) {
    $moduleBeforeBuild = $null;
    $currentWorkingDirectory = Get-Location;

    if (!$workflowModuleNames) {
        if (($global:CurrentModulePath) -and (Test-Path $global:CurrentModulePath)) {
            $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $global:CurrentModulePath | foreach {$_.Name});
            $workflowModuleNames = @($moduleBeforeBuild);
        }
    }

    if($killAll) {
        Stop-Process -processname devenv;
    } else {
        $branchNameEscaped = $BranchName.replace('\','\\');
        if (!($workflowModuleNames)) {
            write "No modules specified.";
            return;
        }
        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:workspace.GetModules($workflowModuleNames);
        $workflowModuleNames = $workflowModuleNames | Where {$exclude -notcontains $_};

        foreach($module in $workflowModuleNames) {
            $filter = "ExecutablePath LIKE '%devenv%' AND CommandLine LIKE '%$module%' AND CommandLine LIKE '%$branchNameEscaped%'";
            if (@(Get-WmiObject Win32_Process -Filter "$filter").count -eq 1) {
                Get-WmiObject Win32_Process -Filter "$filter" | Invoke-WmiMethod -Name Terminate;
                Write-Warning "Succeeded. Visual Studio Solution for $module in $BranchName has been forced shutting down.";
            } else {
                if (@(Get-WmiObject Win32_Process -Filter "$filter").count -eq 0) {
                    Write-Warning "Failed. An error occurred, command found none Visual Studio Solution for $module in $BranchName.";
                } else {
                    Write-Warning "Failed. An error occurred, command found more than 1 Visual Studio Solutions matches the criteria, please use -killAll option if you wish to kill all.";
                }
            }
        }
    }

    if ($moduleBeforeBuild) {
        cm $moduleBeforeBuild;
    }
    pushd $currentWorkingDirectory;
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
function Build-ExpertPatch([switch]$noget = $false, [switch]$noproduct = $false, [switch]$Pre803 = $false) {
    if(!$noproduct) {
        Get-ProductZip
    }
    $cmd = "xcopy \\na.aderant.com\expertsuite\Main\Build.Tools\Current\* /S /Y $PackageScriptsDirectory"
    if ($Pre803) {
        $cmd = "xcopy \\na.aderant.com\expertsuite\Main\Build.Tools\Pre803\* /S /Y $PackageScriptsDirectory"
    }
    if (!$noget) {                
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
    switch ($global:BranchExpertVersion) {
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
        "8.1.0" {
            if (Test-Path $ShellContext.DeploymentManager) {
                $shell = $ShellContext.DeploymentManager                
            } else {
                $shell = $null
                InstallDeployment                                
            }
        }
        default {

        }
    }

    if ($shell) {
        Invoke-Expression $shell
    }
    popd
}

<#
.Synopsis
    Run DeploymentEngine for your current branch
.Description
    Starts DeploymentEngine.exe with the specified command.
.PARAMETER command
    The action you want the deployment engine to take.
.PARAMETER serverName
    The name of the database server.
.PARAMETER databaseName
    The name of the database containing the environment manifest.
.PARAMETER skipPackageImports
    Flag to skip package imports.
.PARAMETER skipHelpDeployment
    Flag to skip deployment of Help.
.EXAMPLE
    DeploymentEngine -action Deploy -serverName MyServer01 -databaseName MyMain
    DeploymentEngine -action Remove -serverName MyServer01 -databaseName MyMain
    DeploymentEngine -action ExportEnvironmentManifest -serverName MyServer01 -databaseName MyMain
    DeploymentEngine -action ImportEnvironmentManifest -serverName MyServer01 -databaseName MyMain
#>
function Start-DeploymentEngine {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateSet("Deploy", "Remove", "ExportEnvironmentManifest", "ImportEnvironmentManifest", "EnableFilestream", "DeploySilent", "RemoveSilent")] [string]$command,
        [Parameter(Mandatory=$false)][string]$serverName,
        [Parameter(Mandatory=$false)][string]$databaseName,
        [Parameter(Mandatory=$false)][switch]$skipPackageImports,
        [Parameter(Mandatory=$false)][switch]$skipHelpDeployment
    )

    process {
        if (-not (Test-Path $ShellContext.DeploymentEngine)) {
            Install-DeploymentManager
        }

        $environmentXml = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")
        
        if ([string]::IsNullOrWhiteSpace($serverName)) {
            $serverName = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($databaseName)) {
            $databaseName = Get-Database
        }

        switch ($true) {
            ($skipPackageImports.IsPresent -and $skipHelpDeployment.IsPresent) {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine -skipPackageImports -skipHelpDeployment
                break
            }
            $skipPackageImports.IsPresent {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine -skipPackageImports
                break
            }
            $skipHelpDeployment.IsPresent {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine -skipHelpDeployment
                break
            }
            default {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine
                break
            }
        }

        if (($command -eq "Deploy" -or $command -eq "Remove") -and $LASTEXITCODE -eq 0) {
            powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command "ExportEnvironmentManifest"  -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine
        }
    }
}

<#
.Synopsis
    Installs DeploymentManager.msi from the current branch binaries directory.
.Description
    Installs Deployment Manager from the .msi located in the current branch.
.EXAMPLE
    Install-DeploymentManager
        Installs Deployment Manager from the current branch binaries directory.
#>
function Install-DeploymentManager {
    & "$global:BranchBinariesDirectory\AutomatedDeployment\InstallDeploymentManager.ps1" -deploymentManagerMsiDirectory $global:BranchBinariesDirectory
}

<#
.Synopsis
    Uninstalls DeploymentManager.msi from the current branch binaries directory.
.Description
    Uninstalls Deployment Manager from the .msi located in the current branch.
.EXAMPLE
    Uninstall-DeploymentManager
        Uninstalls Deployment Manager using the .msi in the current branch binaries directory.
#>
function Uninstall-DeploymentManager {
    & "$global:BranchBinariesDirectory\AutomatedDeployment\UninstallDeploymentManager.ps1" -deploymentManagerMsiDirectory $global:BranchBinariesDirectory
}

#sets up visual studio environment, called from Profile.ps1 when starting PS.
function Set-Environment($initialize = $true) {
    if ($initialize) {
        Set-BranchPaths
    }

    Set-ScriptPaths
    Set-ExpertSourcePath
    Initialise-BuildLibraries
    Set-VisualStudioVersion

    OutputEnvironmentDetails

    $global:workspace = new-object -TypeName Aderant.Build.Providers.ModuleWorkspace -ArgumentList $Global:ProductManifestPath,$global:BranchModulesDirectory,"ExpertSuite"

    if ($initialize) {
        # Setup PowerShell script unit test environment
        InstallPester
    }
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo($newBranch, [switch] $SetAsDefault) {
    if ($global:BranchName -Contains $newBranch) {
        Write-Host -ForegroundColor Yellow "The magic unicorn has refused your request."
        return
    }

    $success = Set-ChangedBranchPaths $newBranch

    if ($success -eq $false) {
        return
    }

    Set-Environment $false

    Set-CurrentModule $global:CurrentModuleName

    cd $global:BranchLocalDirectory

    if ($SetAsDefault) {
        SetDefaultValue dropRootUNCPath $BranchServerDirectory
        SetDefaultValue devBranchFolder $BranchLocalDirectory
    }
}

function Set-VisualStudioVersion() {
    $file = [System.IO.Path]::Combine($global:BuildScriptsDirectory, "vsvars.ps1");
    if (Test-Path $file) {
        &($file)
    }
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
        cls
        return SetPathVariable "Where is your local path to the MAIN branch? e.g C:\tfs\ExpertSuite\Main" $propertyName
    }

    if ($propertyName -eq "DropRootUNCPath") {
        return SetPathVariable "Where is the MAIN branch drop path? For e.g \\dfs.aderant.com\ExpertSuite\Main" $propertyName
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
    Set-Environment $true
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
function Open-ModuleSolution([string] $ModuleName, [switch] $getDependencies, [switch]$getLatest, [switch]$code, [switch]$seventeen) {
    $devenv = "devenv"
    $seventeenDirectory = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\*\Common7\IDE\devenv.exe'
    if($seventeen){
        if(Test-Path $seventeenDirectory){
            $devenv = (Get-Item $seventeenDirectory | select-object -First 1).FullName
        }else{
            Write-Host "VS 2017 could not be found ($seventeenDirectory)"
        }
    }
    if (($getDependencies) -and -not [string]::IsNullOrEmpty($ModuleName)) {
        if (-not [string]::IsNullOrEmpty($global:CurrentModuleName)) {
            $prevModule = Get-CurrentModule
        }
        Set-CurrentModule $moduleName
    }
    $rootPath = Join-Path $BranchLocalDirectory "Modules\$ModuleName"
    if(!($ModuleName)){
        $ModuleName = $global:CurrentModuleName
        $rootPath = $global:CurrentModulePath
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
    $moduleSolutionPath = Join-Path $rootPath "$ModuleName.sln"
    if(Test-Path $moduleSolutionPath) {
        if($code) {
            if(Get-Command code -errorAction SilentlyContinue){
                Invoke-Expression "code $rootPath"
            }else{
                Write-Host "VS Code could not be found (code)"
            }
        }else{
            Invoke-Expression "& '$devenv' $moduleSolutionPath"
        }
    } else {
        $candidates = (gci -Filter *.sln -file  | Where-Object {$_.Name -NotMatch ".custom.sln"})
        if ($candidates.Count -gt 0) {
            $moduleSolutionPath = Join-Path $rootPath $candidates[0]
            if($code) {
                if(Get-Command code -errorAction SilentlyContinue){
                    Invoke-Expression "code $rootPath"
                }else{
                    Write-Host "VS Code could not be found (code)"
                }
            }else{
                Invoke-Expression "& '$devenv' $moduleSolutionPath"
            }
        } else {
            "There is no solution file at $moduleSolutionPath"
        }
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



function TabExpansion([string] $line, [string] $lastword)
{
    if (!$lastword.Contains(";")) {
        $aliases = Get-Alias
        $parser = New-Object Aderant.Build.AutoCompletionParser $line, $lastword, $aliases

        # Evaluate Branches
        Try {
            foreach($tabExpansionParm in $global:expertTabBranchExpansions){
                if ($parser.IsAutoCompletionForParameter($tabExpansionParm.CommandName.ToString(), $tabExpansionParm.ParameterName.ToString(), $tabExpansionParm.IsDefault.IsPresent)) {
                    Get-ExpertBranches $lastword | Get-Unique
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
}

function IsTabOnCommandParameter([string] $line, [string] $lastword, [string] $commandName, [string] $parameterName, [switch] $isDefaultParameter) {
    # Need to select last command if is a line of commands separated by ";"
    # Need to ignore auto-completion of parameters
    # Need to ignore auto-completion of command names
    #return ($lastword.StartsWith("-") -ne True -and $line -ne $commandName -and (($line.Trim() -eq "$commandName $lastword" -and $isDefaultParameter) -or ($line.Trim() -eq "SwitchBranchTo")))
}

Export-ModuleMember -Function TabExpansion

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
.EXAMPLE
    Add-ModuleExpansionParameter -CommandName Build-ExpertModules -ParameterName ModuleNames -IsDefault
    Will add tab expansion of module names on the Build-ExpertModules command where the current parameter is the ModuleNames parameter
#>
function Add-ModuleExpansionParameter([string] $CommandName, [string] $ParameterName){
    if (!($CommandName)) {
        write "No command name specified."
        return
    }
    if (!($ParameterName)) {
        write "No parameter name specified."
        return
    }    
       
    Register-ArgumentCompleter -CommandName $CommandName -ParameterName $ParameterName -ScriptBlock {
        param($commandName, $parameterName, $wordToComplete, $commandAst, $boundParameters)

        $aliases = Get-Alias
        $parser = New-Object Aderant.Build.AutoCompletionParser $commandName, $parameterName, $commandAst
                
        # Evaluate Modules
        try {   
            $parser.GetModuleMatches($wordToComplete, $global:BranchModulesDirectory, $ProductManifestPath) | Get-Unique | ForEach-Object {                    
                [System.Management.Automation.CompletionResult]::new($_)
            } 
            
            # Probe for known Git repositories
            gci -Path "HKCU:\SOFTWARE\Microsoft\VisualStudio\14.0\TeamFoundation\GitSourceControl\Repositories" | % { Get-ItemProperty $_.pspath } |           
                Where-Object { $_.Name -like "$wordToComplete*" -and (Test-Path $_.Path) } | ForEach-Object { 
                [System.Management.Automation.CompletionResult]::new($_.Path) 
            }        
        } catch {
            [System.Exception]           
            Write-Host $_.Exception.ToString()
        }
    }
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
Add-BranchExpansionParameter -CommandName "Merge-Baseless" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Merge-Baseless" -ParameterName "targetBranch"

# Add module auto completion scenarios
Add-ModuleExpansionParameter -CommandName "Set-CurrentModule" -ParameterName "name"
Add-ModuleExpansionParameter -CommandName "Branch-Module" -ParameterName "moduleName"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "getLocal"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "exclude"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "skipUntil"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModulesOnServer" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Kill-VisualStudio" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesForCurrentModule" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-Product" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ProviderModules"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesFrom" -ParameterName "ConsumerModules"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependencies" -ParameterName "SourceModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependsOn" -ParameterName "TargetModuleName"
Add-ModuleExpansionParameter -CommandName "Get-DownstreamExpertModules" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModule" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModules" -ParameterName "ModuleNames"
Add-ModuleExpansionParameter –CommandName "Open-ModuleSolution" –ParameterName "ModuleName"
Add-ModuleExpansionParameter –CommandName "Get-Latest" –ParameterName "ModuleName"
Add-ModuleExpansionParameter –CommandName "Start-Redeployment" –ParameterName "CopyBinariesFrom"
Add-ModuleExpansionParameter -CommandName "Copy-BinToEnvironment" -ParameterName "ModuleNames"
Add-ModuleExpansionParameter -CommandName "Open-Directory" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "New-ExpertBuildDefinition" -ParameterName "ModuleName"

Add-ModuleExpansionParameter -CommandName "Clean" -ParameterName "moduleNames"
Add-ModuleExpansionParameter -CommandName "CleanupIISCache" -ParameterName "moduleNames"

Add-ModuleExpansionParameter -CommandName "Scorch" -ParameterName "moduleNames"

Add-ModuleExpansionParameter –CommandName "Get-WebDependencies" –ParameterName "ModuleName"

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
        $Host.UI.RawUI.WindowTitle = "PS - [" + $global:CurrentModuleName + "] on branch [" + $global:BranchName + "]"

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
.PARAMETER GetLatestForBranch
    Will get the latest source code for your current branch.
.PARAMETER GetProduct
    Will retrieve the latest product binaries before starting the new deployment.
.PARAMETER GetProductZip
    Will retrieve the latest build all product zip binaries before starting the new deployment. (if you specify both -GetProduct and -GetProductZip then you will get the zip file)
.PARAMETER RestoreDB
    Will restore the DACPAC.
.EXAMPLE
        Start-Redeployment -GetLatestForBranch -GetProduct -RestoreDB
    Will first get latest source code for the branch,
    get dependencies for current module,
    remove your existing deployment,
    get product,
    Copy binaries from Applications.Deployment,
    and finally start your new deployment.
#>
function Start-Redeployment([switch]$GetProduct, [switch]$GetProductZip, [switch]$GetProductZipUnstable, [switch]$RestoreDB, [switch]$dontKillRunning, [switch]$GetLatestForBranch) {
    $start = get-date
    if ($GetLatestForBranch) {
        Get-Latest -branch
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
    } elseif ($GetProductZipUnstable) {
        Get-ProductZip -unstable
    } elseif ($GetProduct) {
        Get-Product
    }
    if ($RestoreDB) {
        Restore-ExpertDatabase
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
    if(Test-Path $path){
        Remove-Item $path -recurse -Force;
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
    Cleans the old cache files created by IIS Dynamic Compilation.
.Description
    Following files will be deleted in the module:
        *\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files\*
    Only the files which created X time ago would be removed.
.PARAMETER Days
    Removed all the caches which created/modified $days ago.
.PARAMETER Directory
    Specify the destination of the IIS Cache.
.EXAMPLE
    CleanupIISCache -days 0
#>
function CleanupIISCache {
    param(
        [Parameter(Mandatory=$false)][int] $days = 1,
        [Parameter(Mandatory=$false)][string] $directory = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files'
    )

    $lastWriteTime = (Get-Date).AddDays(-$days)
    Get-ChildItem $directory | Where-Object { $_.LastWriteTime -lt $lastWriteTime} | Remove-Item -Recurse -Force
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
    if ($MyInvocation.ScriptName.EndsWith("_profile.ps1")) {
      # Backwards compatibility quirk
      # In some versions of the shell _Profile.ps1 script we call the function "Help" during start up.
      # If we detect this version, we do not want to show help during the module load for performance reasons so
      # we bail out if the caller is the _Profile.ps1 file
      return
    }
    
    $theHelpList = @()
    
    foreach ($toExport in $functionsToExport) {
      if (-not $toExport.advanced) {
         $ast = (Get-Command $toExport.function).ScriptBlock.Ast
         if ($ast) {        
             $help = $ast.GetHelpContent()
         }
      
         if ($toExport.alias) {
             $theHelpList += [pscustomobject]@{Command=$toExport.alias; Alias=$toExport.Alias; Synopsis=$help.Synopsis}
         } else {
             $theHelpList += [pscustomobject]@{Command=$toExport.function; Alias=$null; Synopsis=$help.Synopsis}
         }
      }
    }

    if ($searchText){
        $searchText = "*$searchText*";
        foreach ($func in $theHelpList) {
            $functionName = $func.function
            $aliasName = $func.alias

            if(($functionName -like $searchText)  -or ($aliasName -like $searchText)){
                Write-Host -ForegroundColor Green -NoNewline "$functionName, $aliasName "
                Write-Host (Get-Help $functionName).Synopsis
            }
        }
        return
    }

    $AderantModuleLocation = Get-AderantModuleLocation
    Write-Host "Using Aderant Module from : $AderantModuleLocation"

    $sortedFunctions = $theHelpList | Sort-Object -Property Alias -Descending
    $sortedFunctions | Format-Table Command, Synopsis
}

<#
.SYNOPSIS
Looks for web applications registered in IIS that do not map to the file system

.PARAMETER virtualPath
Virtual path to search within

.EXAMPLE
Hunt-Zombies -VirtualPath 'Expert_Local'

Finds all the zombie applications in Expert_Local 
#>
function Hunt-Zombies {
    param(
        [Parameter(Mandatory=$false)] [string] $virtualPath = ''
    )
    if(-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Warning "You do not have Administrator rights to run this script`nPlease re-run this script as an Administrator"
        Break
    }

    Import-Module WebAdministration
    Import-Module ApplicationServer

    if([String]::IsNullOrWhitespace($virtualPath)) {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site'
    } else {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site' -VirtualPath $virtualPath
    }
    
    foreach($webApp in $expertWebApplications){
        if(-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
            if($webApp.ApplicationName) {
                $iisPath = $webApp.IISPath
                $filePath = $webApp.PhysicalPath
                Write-Output "Found zombie web application $iisPath, could not find path $filePath."
            }    
        }
    }    
    Write-Output 'Zombie hunt complete.'
}

<#
.SYNOPSIS
Removes web applications registered in IIS that do not map to the file system

.PARAMETER virtualPath
Virtual path to search within

.EXAMPLE
Remove-Zombies -VirtualPath 'Expert_Local'

Removes all the zombie applications in Expert_Local 
#>
function Remove-Zombies {
    param(
        [Parameter(Mandatory=$false)] [string] $virtualPath = ''
    )

    if(-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Warning "You do not have Administrator rights to run this script`nPlease re-run this script as an Administrator"
        Break
    }

    Import-Module WebAdministration
    Import-Module ApplicationServer

    if([String]::IsNullOrWhitespace($virtualPath)) {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site'
    } else {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site' -VirtualPath $virtualPath
    }
    
    foreach($webApp in $expertWebApplications){
        if(-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
            if($webApp.ApplicationName) {
                $iisPath = $webApp.IISPath
                Remove-Item -Path $iisPath
                Write-Output "Removed zombie web application $iisPath"
            }
        }
    }
    Write-Output 'Zombie removal complete.'
}





<#
.Synopsis
    Returns the Database Server\Instance for the current local deployment.
.Description
    Uses Get-EnvironmentFromXml to return the Database Server\Instance for the current local deployment.
#>
function Get-DatabaseServer() {
    if (-Not (Test-Path ([System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")))) {
        $databaseServer = $env:COMPUTERNAME    
        Write-Host "Server instance set to: $databaseServer"
        return $databaseServer
    } else {
        try {
            [string]$databaseServer = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/@serverName"), "[^/]*$").ToString()
            Write-Debug "Database server set to: $databaseServer"
        } catch {
            throw "Unable to get database server from environment.xml"
        }

        [string]$serverInstance = Get-EnvironmentFromXml "/environment/expertDatabaseServer/@serverInstance"
        
        if (-not [string]::IsNullOrWhiteSpace($serverInstance)) {
            [string]$serverInstance = [regex]::Match($serverInstance, "[^/]*$").ToString()
            $databaseServer = "$($databaseServer)\$($serverInstance)"
            Write-Debug "Server instance set to: $serverInstance"
        } else {
            Write-Debug "Unable to get database server instance from environment.xml"
        }

        Write-Host "Server instance set to: $databaseServer"
        return $databaseServer
    }
}

<#
.Synopsis
    Returns the database name for the current local deployment.
.Description
    Uses Get-EnvironmentFromXml to return the the database name for the current local deployment.
#>
function Get-Database() {
    if (-Not (Test-Path ([System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")))) {
        $database = Read-Host -Prompt "No environment.xml found. Please specify a database name"
        return $database
    } else {
        try {
            [string]$database = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/databaseConnection/@databaseName"), "[^/]*$").ToString()
            Write-Host "Database name set to: $database"
        } catch {
            throw "Unable to get database name from environment.xml"
        }

        return $database
    }
}

<#
.SYNOPSIS
Runs UI tests for the current module
.PARAMETER productname
    The name of the product you want to run tests against
.PARAMETER testCaseFilter
    The vstest testcasefilter string to use
.PARAMETER dockerHost
    The dockerHost to run the docker container on
.EXAMPLE
    Run-ExpertUITest -productname "Web.Inquiries" -testCaseFilter "TestCategory=Smoke"
    If Inquiries is the current module, all smoke tests for the inquiries product will be executed
#>
function Run-ExpertUITests {
    param(
        [Parameter(Mandatory=$false)] [string]$productName = "*",
        [Parameter(Mandatory=$false)] [string]$testCaseFilter = "TestCategory=Sanity",
        [Parameter(Mandatory=$false)] [string]$dockerHost = "",
        [Parameter(Mandatory=$false)] [string]$browserName,
        [Parameter(Mandatory=$false)] [switch]$deployment,
        [Parameter(Mandatory=$false)] [switch]$noBuild,
        [Parameter(Mandatory=$false)] [switch]$noDocker
    )
    if (-Not $CurrentModuleName) {
        Write-Error "You must select a module to run this command"
        Break
    }
    if ([string]::IsNullOrWhiteSpace($dockerHost) -and -Not (Get-Command docker -errorAction SilentlyContinue)){
        Write-Error "Docker not installed. Please install Docker for Windows before running this command"
        Break
    }
    if ($productName -eq "*") {
        Write-Host "No project name specified. Running sanity test for all test containers"
    }

    $testOutputPath = "$currentModulePath\Bin\Test\"
    $testAssemblyName = "UIAutomationTest.*$productName*.dll"
    $testAssemblyPath = $testOutputPath + $testAssemblyName
    $runsettingsFile = $testOutputPath + "localexecution.runsettings"
    $runSettings

    if (!$deployment -or $noDocker -or $browserName -or ![string]::IsNullOrWhiteSpace($dockerHost)) {
        $runSettingsParameters
        if ($browserName) {
            $runSettingsParameters += '<Parameter name="BrowserName" value="' + $browserName + '" />
            '
        }
        if (!$deployment) {
            $runSettingsParameters += '<Parameter name="Development" value="true" />
            '
        }
        if ($noDocker) {
            Get-ChildItem "$CurrentModulePath\Packages\**\InitializeSeleniumServer.ps1" -Recurse | Import-Module
            Start-SeleniumServers
            $runSettingsParameters += '<Parameter name="NoDocker" value="true" />
            '
        }
        if (![string]::IsNullOrWhiteSpace($dockerHost)) {
            $runSettingsParameters += '<Parameter name="DockerHost" value="'+$dockerHost+'" />
            '
        }

        $runSettingsContent = '<?xml version="1.0" encoding="utf-8"?>  
        <RunSettings>
            <!-- Parameters used by tests at runtime -->  
            <TestRunParameters>
            ' + $runSettingsParameters +
            '</TestRunParameters>
        </RunSettings>'
        if (Test-Path $runsettingsFile) {
            Remove-Item $runsettingsFile
        }
        Write-Host "Creating custom runsettings file"
        New-Item -Path $runsettingsFile -ItemType "file" -Value $runSettingsContent > $null
        $runSettings = "/Settings:$runsettingsFile"
    }

    if(-Not $noBuild){
        Get-ChildItem -Path ("$currentModulePath\Test\*$productName*\") -Recurse -Filter "UIAutomationTest.*.csproj" | ForEach-Object {$_} {
            msbuild $_
        }
    }

    if (-Not (Test-Path($testAssemblyPath))) {
        Write-Error "Cannot find $testAssemblyPath. Please ensure this module has been built before trying to execute tests"
        Break
    }
    $testAssemblies = Get-ChildItem -Path $testAssemblyPath -Recurse -Filter $testAssemblyName
    vstest.console.exe $testAssemblies /TestCaseFilter:$testCaseFilter /TestAdapterPath:$testOutputPath /Logger:trx $runSettings
}

<#
.SYNOPSIS
    Runs UI sanity tests for the current module
.PARAMETER productName
    The name of the product you want to run tests against
.EXAMPLE
    Run-ExpertSanityTests -productName "Web.Inquiries"
    rest "Web.Inquiries" -deployment
    This will run the UI sanity tests for the Inquiries product against a deployment url
#>
function Run-ExpertSanityTests {
    param(
        [Parameter(Mandatory=$false)] [string]$productName = "*",
        [Parameter(Mandatory=$false)] [string]$dockerHost = "",
        [Parameter(Mandatory=$false)] [string]$browserName,
        [Parameter(Mandatory=$false)] [switch]$deployment,
        [Parameter(Mandatory=$false)] [switch]$noDocker

    )
    Run-ExpertUITests -productName $productName -testCaseFilter "TestCategory=Sanity" -dockerHost:$dockerHost -deployment:$deployment -noDocker:$noDocker -browserName $browserName
}

<#
.SYNOPSIS
    Runs UI visual tests for the current module
.PARAMETER productName
    The name of the product you want to run tests against
.PARAMETER development
    Run against the developer environment
.PARAMETER noDocker
    Don't use docker
.PARAMETER browserName
    Browser to use for the test
.EXAMPLE
    Run-ExpertVisualTests -productName "Web.Inquiries"
    rest "Web.Inquiries" -deployment
    This will run the UI visual tests for the Inquiries product against a deployment url
#>
function Run-ExpertVisualTests {
    param(
        [Parameter(Mandatory=$false)] [string]$productName = "*",
        [Parameter(Mandatory=$false)] [string]$dockerHost = "",
        [Parameter(Mandatory=$false)] [string]$browserName,
        [Parameter(Mandatory=$false)] [switch]$deployment,
        [Parameter(Mandatory=$false)] [switch]$noDocker

    )
    Run-ExpertUITests -productName $productName -testCaseFilter "TestCategory=Visual" -dockerHost:$dockerHost -deployment:$deployment -noDocker:$noDocker -browserName $browserName
}

<#
.Synopsis
    Clears the Expert cache for the specified user.
.Description
    Clears the local and roaming caches for the specified user.
.PARAMETER user
    The user account to clear the Expert cache for.
.PARAMETER environmentName
    Remove the cache for a specific Expert environment.
.PARAMETER removeCMSINI
    Removes the CMS.INI file from AppData\Roaming\Aderant.
.EXAMPLE
    Clear-ExpertCache -user TTQA1 -environmentName ITEGG -removeCMSINI
    This will clear the local and roaming caches for the ITEGG environment for TTQA1 and remove CMS.INI from AppData\Roaming\Aderant.
#>
function Clear-ExpertCache {
    param(
        [Parameter(Mandatory=$false)] [string]$user = [Environment]::UserName,
        [Parameter(Mandatory=$false)] [string]$environmentName,
        [switch]$removeCMSINI
    )

    [string]$cache = "Aderant"
    [string]$localAppData
    [string]$roamingAppData
    
    if (-not [string]::IsNullOrWhiteSpace($environmentName)) {
        $cache = [string]::Concat($cache, "\$environmentName")
    }

    if (-not ($user -match [Environment]::UserName)) {
        $localAppData = "C:\Users\$user\AppData\Local"    
        $roamingAppData = "C:\Users\$user\AppData\Roaming"
    } else {
        $localAppData = $env:LOCALAPPDATA
        $roamingAppData = $env:APPDATA
    }

    if (Test-Path("$localAppData\$cache")) {
        if (-not (Get-Item "$localAppData\$cache").PSIsContainer) {
            Write-Error "Please specify a valid environment name"
            Break
        }
        try {
            Get-ChildItem -Path "$localAppData\$cache" -Recurse | Remove-Item -force -recurse
            if (-not [string]::IsNullOrWhiteSpace($environmentName)) {
                Remove-Item -Path "$localAppData\$cache" -Force
            }
            Write-Host "Successfully cleared $localAppData\$cache"
        } catch {
            Write-Warning "Unable to clear $localAppData\$cache"
        }
    } else {
        Write-Host "No cache present at $localAppData\$cache"
    }

    if (Test-Path("$roamingAppData\$cache")) {
        if (-not (Get-Item "$roamingAppData\$cache").PSIsContainer) {
            Write-Error "Please specify a valid environment name"
            Break
        }
        try {
            if ([string]::IsNullOrWhiteSpace($environmentName)) {
                Get-ChildItem -Path "$roamingAppData\$cache" -Recurse |  Remove-Item -Exclude "CMS.INI" -Force -Recurse
            } else {
                Get-ChildItem -Path "$roamingAppData\$cache" -Recurse | Remove-Item -Force -Recurse
                Remove-Item -Path "$roamingAppData\$cache" -Force
            }
            Write-Host "Successfully cleared $roamingAppData\$cache"
        } catch {
            Write-Error "Unable to clear $roamingAppData\$cache"
        }
    } else {
        Write-Host "No cache present at $roamingAppData\$cache"
    }

    if ($removeCMSINI.IsPresent) {
        if (Test-Path("$roamingAppData\Aderant\CMS.INI")) {
            try {
                Remove-Item -Path "$roamingAppData\Aderant\CMS.INI" -Force
                Write-Host "Successfully removed CMS.INI"
            } catch {
                Write-Error "Unable to remove CMS.INI at $roamingAppData\Aderant"
            }
        } else {
            Write-Host "No CMS.INI file present at $roamingAppData\Aderant"
        }
    }
}

<#
.Synopsis
    Changes the system owner in FWM_ENVIRONMENT.
.Description
    Changes the system owner in FWM_ENVIRONMENT
.PARAMETER owner
    The owner to set ISSYSTEM = 'Y' for.
.PARAMETER serverInstance
    The SQL server\instance the database is on.
.PARAMETER database
    The name of the Expert database.
.EXAMPLE
        Change-ExpertOwner -owner Aderant
    This will change the system owner to Aderant in the Expert database.
#>
function Change-ExpertOwner {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)] [string]$serverInstance,
        [Parameter(Mandatory=$false)] [string]$database,
        [Parameter(Mandatory=$false)] [switch]$force
    )

    dynamicparam {
        [string]$parameterName = "owner"
        $parameterAttribute = New-Object System.Management.Automation.ParameterAttribute
        $parameterAttribute.Position = 0
        $parameterAttribute.Mandatory = $true
        $attributeCollection = New-Object System.Collections.ObjectModel.Collection[System.Attribute]
        $attributeCollection.Add($parameterAttribute)
        $owners = "Aderant", "Clifford, Maximillian & Scott"
        $validateSetAttribute = New-Object System.Management.Automation.ValidateSetAttribute($owners)
        $attributeCollection.Add($validateSetAttribute)
        $runtimeParameter = New-Object System.Management.Automation.RuntimeDefinedParameter($parameterName, [string], $attributeCollection)
        $runtimeParameterDictionary = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        $runtimeParameterDictionary.Add($parameterName, $runtimeParameter)
        return $runtimeParameterDictionary
    }

    begin {
        $owner = $PsBoundParameters[$parameterName]
        if ([string]::IsNullOrEmpty($serverInstance)) {
            $serverInstance = Get-DatabaseServer
        } else {
            Write-Host "Server instance set to: $serverInstance"
        }

        if ([string]::IsNullOrEmpty($database)) {
            $database = Get-Database
        } else {
            Write-Host "Database name set to: $database"
        }

        Write-Host "Expert owner: $owner"
    }

    process {
        if (-Not (Get-Module -ListAvailable -Name Sqlps)) {
            Write-Error "The Sqlps module is not available on this system."
            return
        }

        Import-Module Sqlps -DisableNameChecking
    
        if ($owner -contains "Aderant") {
            [string]$ownerID = "00000000-0000-0000-0000-00000000000A"
        } else {
            [string]$ownerID = "402A1B6F-AAB2-4B32-BEFD-D4C9BB556029"
        }
        
        [string]$sql = "DECLARE @OWNER NVARCHAR(100) = '" + $owner + "';
DECLARE @OWNERID NVARCHAR(40) = '" + $ownerID + "';

IF NOT EXISTS (SELECT TOP 1 * FROM FWM_OWNER WHERE OWNERID = @OWNERID) BEGIN
INSERT INTO FWM_OWNER (OWNERID, NAME, ISSYSTEM) VALUES (@OWNERID, @OWNER, 'Y');
END;

UPDATE FWM_OWNER SET ISSYSTEM = 'Y' WHERE OWNERID = @OWNERID;
UPDATE FWM_OWNER SET ISSYSTEM = 'N' WHERE OWNERID != @OWNERID;
UPDATE HBM_PARMS SET FIRM_NAME = @OWNER;"
    
    if (-not $force.IsPresent) {
        Write-Host "Continue?"
        $answer = Read-Host "Y/N"

        while("Y", "N" -notcontains $answer) {
            $answer = Read-Host "Y/N"
        }

        if ($answer -eq "N") {
            return
        }
    }
    
    try {
            Invoke-Sqlcmd -ServerInstance $serverInstance -Database $database -Query $sql
        } catch {
            Write-Error "Failed to change Expert owner to: $owner for database: $database"
            return
        }
        
        Write-Host "Expert owner set to: $owner" -ForegroundColor Cyan
    }
}

<# 
.Synopsis 
    Get the process Ids and App pool names of all running IIS Worker Processes.  Handy for deciding which w3wp process to attach to in VS.
.Description   
    Get the process Ids and App pool names of all running IIS Worker Processes.  Handy for deciding which w3wp process to attach to in VS.
.PARAMETER all
    Switch - boolean value to return all IIS Worker Processes otherwise just get ones using ExpertApplications_Local App pool (most common thing people debug)
.EXAMPLE
    wpid -all
#>
function Get-WorkerProcessIds(){
    param ([switch] $all = $false)

    $process = "w3wp.exe"
    $processObjects = Get-WmiObject Win32_Process -Filter "name = '$process'" | Select-Object Handle, CommandLine

    ForEach($processObject in $processObjects){
        $commandLine = $processObject.CommandLine.Substring($processObject.CommandLine.IndexOf("`"") + 1)
        $commandLineAndHandle = $commandLine.Substring(0, $commandLine.IndexOf("`"")) + " --> " + $processObject.Handle
        
        If($all -eq $true){
            Write-Host $commandLineAndHandle -ForegroundColor Green
        } Elseif($commandLine.StartsWith("ExpertApplications_")){
            Write-Host $commandLineAndHandle -ForegroundColor Green
        }
    }
}

<#
.Synopsis
    Wrapper function that deals with Powershell's peculiar error output when Git uses the error stream.
#>
function Invoke-Git {
    [CmdletBinding()]
    param(
        [parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )

    & {
        [CmdletBinding()]
        param(
            [parameter(ValueFromRemainingArguments=$true)]
            [string[]]$InnerArgs
        )
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo.Arguments = "$InnerArgs"
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.WorkingDirectory = (Get-Item -Path ".\" -Verbose).FullName
        $process.StartInfo.FileName = "git"
        $process.Start() | Out-Null
        $process.WaitForExit()
        return $process.StandardError.ReadToEnd()

    } -ErrorAction SilentlyContinue -ErrorVariable fail @Arguments

    if ($fail) {
        $fail.Exception
    }

}

<#
.Synopsis
    Starts a merge workflow for all Pull Requests linked to a specific work item.
.Description   
    Takes the bug work item ID that needs to be merged, the merge work item ID that the merge PR should be attached with and the target Git branch name as input.
    It then tries to cherry pick each linked Pull Request of the bug work item ID to a new feature branch off the given target branch, commit it and create a PR.
    If a merge conflict occurs, Visual Studio (Experimental Instance, for performance reasons) opens up automatically for manual conflict resolving.
    After successfully resolving the merge conflict, just close Visual Studio and run this command again. It will remember your last inputs for convenience.
    Once a Pull Request for the merge operation is created, Internet Explorer will open up automatically and show the created PR which is set to auto-complete. 
    Additionally, it will automatically do a squash merge and delete the feature branch.
    The CRTDev group will be associated automatically as an optional reviewer which can be changed manually.
#>
function Git-Merge {

    # setup logging
    $ErrorActionPreference = "SilentlyContinue"
    Stop-Transcript | out-null
    $ErrorActionPreference = 'Stop'

    # create C:\temp folder if it does not exist
    $tempFolderPath = "C:\temp"
    if (!(Test-Path $tempFolderPath)) {
        New-Item -ItemType Directory -Path $tempFolderPath
    }

    $logFilePath = Join-Path $tempFolderPath git-merge_log.txt
    Start-Transcript -path $logFilePath -append

    # variables
    $tfsUrl = "http://tfs:8080/tfs/aderant"
    $tfsUrlWithProject = "http://tfs:8080/tfs/aderant/ExpertSuite"
    $bugId = $null
    $mergeBugId = $null
    $targetBranch = $null
    $tempFolderPath = "C:\temp\gitMerge"
    $gitError = ""
    $needInput = $true

    # close all open Internet Explorer instances that might have been opened as a COM object
    # we close all instances, even those that were opened manually - assuming nobody uses that browser any more :-)
    (New-Object -COM 'Shell.Application').Windows() | Where-Object {
        $_.Name -like '*Internet Explorer*'
    } | ForEach-Object {
        $_.Quit()
    }

    # create temporary working folder if it does not exist
    if (!(Test-Path $tempFolderPath)) {
        Write-Host "`nCreating directory $tempFolderPath" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $tempFolderPath | Out-Null
    }

    # create application settings to save application state (like the previous input so you don't have to provide it again after a conflict or other failure)
    $appInfoFilePath = Join-Path $tempFolderPath ".app-info"
    if (!(Test-Path $appInfoFilePath)) {
        Write-Host "Creating application info file" -ForegroundColor Gray
        New-Item $appInfoFilePath -ItemType File | Out-Null
    }

    $autoCreateMergeBug = $false

    # grab previous input automatically on request
    $appInfo = Get-Content $appInfoFilePath
    if ($appInfo) {
        $inputs = $appInfo.Split(',')
        Write-Host "The previous inputs were"
        Write-Host " * Bug ID:        $($inputs[0])"
        Write-Host " * Merge Bug ID:  $($inputs[1])"
        Write-Host " * Target branch: $($inputs[2])"

        Write-Host "`nDo you want to use these inputs (y/n)?" -ForegroundColor Magenta
        $useInputsAnswer = Read-Host

        if ($useInputsAnswer -eq 'y') {
            $bugId = $inputs[0]
            $mergeBugId = $inputs[1]
            $targetBranch = $inputs[2]
            $needInput = $false

            if ($mergeBugId -eq 'c' -or $mergeBugId -eq "'c'") {
                $autoCreateMergeBug = $true
            }
        }
    }

    # grab input manually
    if ($needInput) {
        while (!$bugId -or $bugId.Length -le 5) {
            if ($bugId -and $bugId.Length -le 5) {
                Write-Host "$bugId is not a valid work item ID" -ForegroundColor Red
            }
            $bugId = Read-Host -Prompt "`nWhich bug ID to you want to merge"
        }
    
        while (!$mergeBugId -or $mergeBugId.Length -le 5 -or $mergeBugId -eq $bugId) {
            $mergeBugId = Read-Host -Prompt "`nWhich merge bug ID to you want the merge operation to be associated with (enter 'c' to automatically create one)"
            if ($mergeBugId -eq 'c' -or $mergeBugId -eq "'c'") {
                $autoCreateMergeBug = $true
                break
            }
            if ($mergeBugId -and $mergeBugId.Length -le 5) {
                Write-Host "$mergeBugId is not a valid work item ID" -ForegroundColor Red
            }
            if ($mergeBugId -eq $bugId) {
                Write-Host "You cannot use the original work item to be associated with the merge operation" -ForegroundColor Red
            }
        }

        while (!$targetBranch -or $targetBranch.Length -le 1) {
            $targetBranch = Read-Host -Prompt "`nWhich Git branch to you want to merge to (e.g. master, patch/81SP1 etc. - CASE SENSTITIVE)"
        }

        Set-Content $appInfoFilePath "$([System.String]::Join(",", @($bugId, $mergeBugId, $targetBranch)))" -Force
    }

    # get the bug work item from TFS
    $getWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($bugId)?`$expand=all&api-version=1.0"
    Write-Host "Invoke-RestMethod -Uri $getWorkItemUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
    $workItem = Invoke-RestMethod -Uri $getWorkItemUri -ContentType "application/json" -UseDefaultCredentials

    # create new IE browser object to show merge PRs (optionally the newly created merge bug)
    $browser = New-Object -ComObject internetexplorer.application

    #retrieve or auto-create the merge work item
    if ($autoCreateMergeBug -eq $true) {

        $assumedIterationPath = "ExpertSuite"
        switch ($targetBranch) {
            'master' {
                $assumedIterationPath += "\\8.2.0.0"
            }
            'patch/81SP1' {
                $assumedIterationPath += "\\8.1.0.2 (HF)"
            }
            'releases/10.8102' {
                $assumedIterationPath += "\\8.1.1 (SP)"
            }    
        }

        # automatically create the merge work item in TFS
        $createWorkItemUri = "$($tfsUrlWithProject)/_apis/wit/workItems/`$Bug?api-version=1.0"
        $createWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.Title",
    "value": "MERGE: $($workItem.fields.'System.Title'.Replace('"', '\"'))"
  },
  {
    "op": "add",
    "path": "/fields/Microsoft.VSTS.TCM.ReproSteps",
    "value": "Merge work item $($workItem.id) into $targetBranch (and the respective TFS branch, if applicable)."
  },
  {
    "op": "add",
    "path": "/fields/System.History",
    "value": "Automatically created via Git-Merge."
  },
  {
    "op": "add",
    "path": "/fields/System.AreaPath",
    "value": "$($workitem.fields.'System.AreaPath'.Replace('\', '\\'))"
  },
  {
    "op": "add",
    "path": "/fields/System.IterationPath",
    "value": "$assumedIterationPath"
  },
  {
    "op": "add",
    "path": "/relations/-",
    "value": {
      "rel": "System.LinkTypes.Hierarchy-Reverse",
      "url": "$($tfsUrl)/_apis/wit/workItems/$($bugId)",
      "attributes": {
        "comment": "Original bug"
      }
    }
  }
]
"@
        Write-Host "Invoke-RestMethod -Uri $createWorkItemUri -Body $createWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
        $createdMergeWorkItem = Invoke-RestMethod -Uri $createWorkItemUri -Body $createWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch
        $mergeBugId = $createdMergeWorkItem.id

        # assign the merge work item to the creator and set it to Active
        $updateWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($createdMergeWorkItem.id)?api-version=1.0"
        $updateWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.AssignedTo",
    "value": "$($createdMergeWorkItem.fields.'System.CreatedBy'.Replace('\', '\\'))"
  },
  {
    "op": "replace",
    "path": "/fields/System.State",
    "value": "Active"
  }
]
"@
        Write-Host "Invoke-RestMethod -Uri $updateWorkItemUri -Body $updateWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
        $updatedMergeWorkItem = Invoke-RestMethod -Uri $updateWorkItemUri -Body $updateWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

        Write-Host "`nAutomatically created merge work item $mergeBugId. Please verify assignee, area & iteration path.`n" -ForegroundColor Yellow
        Read-Host -Prompt "A new IE browser window will now open to load the work item for editing. Press any key to continue"
        $workItemUrl = "$($tfsUrlWithProject)/_workitems?id=$mergeBugId"
        $browser.navigate($workItemUrl)
        $browser.visible = $true
    }

    # get existing merge work item from TFS
    $getMergeWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($mergeBugId)?`$expand=all&api-version=1.0"
    Write-Host "Invoke-RestMethod -Uri $getMergeWorkItemUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
    $mergeWorkItem = Invoke-RestMethod -Uri $getMergeWorkItemUri -ContentType "application/json" -UseDefaultCredentials

    # gather all PRs from the bug work item that need to be merged
    $repositoriesToProcess = @{}
    foreach ($relation in $workItem.relations | Where-Object { $_.rel -eq "ArtifactLink" -and $_.attributes.name -eq "Pull Request" }) {
        $pullRequestUri = $relation.url
        $pullRequestPath = @($pullRequestUri.Split('/'))[5].Replace("%2f", "/").Replace("%2F", "/")
        $pullRequestPathParts = @($pullRequestPath.Split('/'))
    
        $repositoryId = $pullRequestPathParts[1]
        $pullRequestId = $pullRequestPathParts[2]
        $getPullRequestUri = "$($tfsUrl)/_apis/git/repositories/$repositoryId/pullrequests/$pullRequestId"

        # get the pull request object
        Write-Host "Invoke-RestMethod -Uri $getPullRequestUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
        $pullRequest = Invoke-RestMethod -Uri $getPullRequestUri -ContentType "application/json" -UseDefaultCredentials
        if ($pullRequest.status -ne 'completed') {
            continue
        }
        $repositoryFromPR = $pullRequest.repository

        # add the containing repository to the dictionary if it hasn't already been added
        if (-not $repositoriesToProcess.Contains($repositoryFromPR.name)) {
            $pullRequests = New-Object System.Collections.ArrayList
            $pullRequests.Add($pullRequest) | Out-Null
            $repositoriesToProcess.Add($repositoryFromPR.name, $pullRequests)
        } else {
            $repositoriesToProcess[$repositoryFromPR.name].Add($pullRequest)
        }
    }

    # write a summary of what is gonna happen
    Write-Host "`nMerging bug $($workItem.id) into branch $($targetBranch)" -ForegroundColor Green
    Write-Host "`-> $($workItem.fields.'System.Title')`n" -ForegroundColor Green
    Write-Host "Merge bug: $($mergeWorkItem.id) - $($mergeWorkItem.fields.'System.Title')"
    foreach ($currentRepositoryName in $repositoriesToProcess.Keys) {
        $repository = $repositoriesToProcess[$currentRepositoryName]
        Write-Host "`n$currentRepositoryName PRs:"
        foreach ($pullRequest in $repository) {
            Write-Host " * $($pullRequest.targetRefName.Substring(11)) - PR $($pullRequest.pullRequestId) - $($pullRequest.title)"
        }
    }
    $hasChangesets = $false
    $changeSetInfos = ""
    $allTfvcBranchPaths = [System.Collections.ArrayList]@()
    foreach ($relation in $workItem.relations | Where-Object { $_.rel -eq "ArtifactLink" -and $_.attributes.name -eq "Fixed in Changeset" }) {
        if (-not $hasChangesets) {
            $hasChangesets = $true
            Write-Host "`nTFVC commits (need to be merged manually):" -ForegroundColor Yellow
        }
        $splitUrl = $relation.url.Split('/')
        $changeSetId = $splitUrl[$splitUrl.Count - 1]
        $getChangesetUri = "$($tfsUrl)/_apis/tfvc/changesets/$($changeSetId)?includeDetails=true&api-version=1.0"

        #get the work items from TFS
        $changeSet = Invoke-RestMethod -Uri $getChangesetUri -ContentType "application/json" -UseDefaultCredentials
        $getChangesUri = $changeSet._links.changes.href
        $changes = Invoke-RestMethod -Uri $getChangesUri -ContentType "application/json" -UseDefaultCredentials
        $tfvcBranchPaths = [System.Collections.ArrayList]@()
        foreach ($change in $changes.value) {
            $path = ""
            $pathParts = $change.item.path.Split('/')
            foreach ($part in $pathParts) {
                if ($part -eq "$" -or $part -eq "ExpertSuite") {
                    continue
                }
                if ($part -eq "Modules") {
                    break
                }
                $path += "$part/"
            }
            $path = $path.Substring(0, $path.Length - 1)
            if (-not $tfvcBranchPaths.Contains($path)) {
                $tfvcBranchPaths.Add($path) | Out-Null
            }
            if (-not $allTfvcBranchPaths.Contains($path)) {
                $allTfvcBranchPaths.Add($path) | Out-Null
            }
        }
        $changeSetInfo = " * $tfvcBranchPaths - CS $changeSetId - $($relation.attributes.comment)"
        $changeSetInfos += "$changeSetInfo`n"
        Write-Host $changeSetInfo
    }

    foreach ($branch in $allTfvcBranchPaths) {
        Write-Host "`nCreate an additional merge work item for merging the TFVC changesets from $branch to its parent branch (y/n)?" -ForegroundColor Magenta
        $additionalMergeBugAnswer = Read-Host
        if ($additionalMergeBugAnswer -eq 'y') {

            $getBranchUri = "$($tfsUrl)/_apis/tfvc/branches/`$/ExpertSuite/$branch/Modules?includeParent=true&api-version=1.0-preview.1"
            Write-Host "Invoke-RestMethod -Uri $getBranchUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
            $branchInfo = Invoke-RestMethod -Uri $getBranchUri -ContentType "application/json" -UseDefaultCredentials
            $parentBranch = $branchInfo.parent[0].path.Replace("$/ExpertSuite/", "").Replace("/Modules", "")

            $assumedIterationPath = "ExpertSuite"
            switch ($parentBranch) {
                'Releases/811x' {
                    $assumedIterationPath += "\\8.1.1 (SP)"
                }
                'Releases/81x' {
                    $assumedIterationPath += "\\8.1.0"
                }
                'Releases/803x' {
                    $assumedIterationPath += "\\8.0.3 (SP)"
                }
                'Releases/802x' {
                    $assumedIterationPath += "\\8.0.2 (SP)"
                }
                'Releases/801x' {
                    $assumedIterationPath += "\\8.0.1(SP)" # this is not a typo, this iteration path is indeed missing a space...
                }
            }

            # automatically create the merge work item in TFS
            $createAdditionalWorkItemUri = "$($tfsUrlWithProject)/_apis/wit/workItems/`$Bug?api-version=1.0"
            $createAdditionalWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.Title",
    "value": "MERGE: $($workItem.fields.'System.Title'.Replace('"', '\"'))"
  },
  {
    "op": "add",
    "path": "/fields/Microsoft.VSTS.TCM.ReproSteps",
    "value": "Merge work item $($workItem.id) into TFVC branch $parentBranch."
  },
  {
    "op": "add",
    "path": "/fields/System.History",
    "value": "Automatically created via Git-Merge."
  },
  {
    "op": "add",
    "path": "/fields/System.AreaPath",
    "value": "$($workitem.fields.'System.AreaPath'.Replace('\', '\\'))"
  },
  {
    "op": "add",
    "path": "/fields/System.IterationPath",
    "value": "$assumedIterationPath"
  },
  {
    "op": "add",
    "path": "/relations/-",
    "value": {
      "rel": "System.LinkTypes.Hierarchy-Reverse",
      "url": "$($tfsUrl)/_apis/wit/workItems/$($bugId)",
      "attributes": {
        "comment": "Original bug"
      }
    }
  }
]
"@
            Write-Host "Invoke-RestMethod -Uri $createAdditionalWorkItemUri -Body $createAdditionalWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
            $additionallyCreatedMergeWorkItem = Invoke-RestMethod -Uri $createAdditionalWorkItemUri -Body $createAdditionalWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

            # assign the additional merge work item to the creator
            $updateAdditionalWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($additionallyCreatedMergeWorkItem.id)?api-version=1.0"
            $updateAdditionalWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.AssignedTo",
    "value": "$($additionallyCreatedMergeWorkItem.fields.'System.CreatedBy'.Replace('\', '\\'))"
  }
]
"@
            Write-Host "Invoke-RestMethod -Uri $updateAdditionalWorkItemUri -Body $updateAdditionalWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
            $updatedAdditionalMergeWorkItem = Invoke-RestMethod -Uri $updateAdditionalWorkItemUri -Body $updateAdditionalWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

            Write-Host "`nAutomatically created additional merge work item $($additionallyCreatedMergeWorkItem.id). Please verify assignee, area & iteration path.`n" -ForegroundColor Yellow
            Read-Host -Prompt "A new IE browser window will now open to load the work item for editing. Press any key to continue"
            $workItemUrl = "$($tfsUrlWithProject)/_workitems?id=$($additionallyCreatedMergeWorkItem.id)"
            $browser.navigate($workItemUrl)
            $browser.visible = $true
        }
    }

    Write-Host "`nProceed with merging (y/n)?" -ForegroundColor Magenta
    $proceedAnswer = Read-Host

    if ($proceedAnswer -ne 'y') {
        Write-Host "Aborting."
        return
    }


    # create folder for git repo to clone if it does not exist
    $gitReposPath = Join-Path $tempFolderPath $bugId
    if (!(Test-Path $gitReposPath)) {
        Write-Host "`nCreating directory $gitReposPath" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $gitReposPath | Out-Null
    }

    try {

        foreach ($currentRepositoryName in $repositoriesToProcess.Keys) {

            $repository = $repositoriesToProcess[$currentRepositoryName]
            $repositoryId = $repository[0].repository.id

            Write-Host "`nProcessing $currentRepositoryName" -ForegroundColor Green

            $featureBranch = $targetBranch + "-for-merging-$bugId"

            cd $gitReposPath

            $currentRepositoryPath = Join-Path $gitReposPath $currentRepositoryName

            $isInitialRun = $false
            if (!(Test-Path $currentRepositoryPath)) {
                # clone the repository and checkout a new feature branch
                Write-Host "Cloning repository" -ForegroundColor Gray
                Write-Host "git clone $($repository[0].repository.remoteUrl)" -ForegroundColor Blue
                Invoke-Git clone $repository[0].repository.remoteUrl
                $isInitialRun = $true
                Write-Host "Done" -ForegroundColor Gray
            }

            cd $currentRepositoryPath

            if ($isInitialRun) {
                Write-Host "Creating feature branch" -ForegroundColor Gray
                Write-Host "git checkout -b $featureBranch origin/$targetBranch" -ForegroundColor Blue
                Invoke-Git checkout -b $featureBranch origin/$targetBranch
            }

            $pullRequestDescription = ""

            # cherry-pick the commits of every associated PR in this repository
            foreach ($pullRequest in $repository) {

                $pullRequestDescription += "Merging $($pullRequest.title)`n"

                $conflictInfoFilePath = Join-Path (Join-Path $currentRepositoryPath "..\") "$currentRepositoryName.conflicts"
                $processedInfoFilePath = Join-Path (Join-Path $currentRepositoryPath "..\") "$currentRepositoryName.processed"
                if (!(Test-Path $processedInfoFilePath)) {
                    Write-Host "Creating processed info file" -ForegroundColor Gray
                    New-Item $processedInfoFilePath -ItemType File | Out-Null
                }

                $lastMergeCommitId = $pullRequest.lastMergeCommit.commitId

                $processedInfo = Get-Content $processedInfoFilePath
                if (!$processedInfo -or -not (Get-Content $processedInfoFilePath).Contains($pullRequest.pullRequestId)) {

                    if (!(Test-Path $conflictInfoFilePath) -or -not (Get-Content $conflictInfoFilePath).Contains($pullRequest.pullRequestId)) {
                        Write-Host "Cherry-picking $($lastMergeCommitId.Substring(0,7))" -ForegroundColor Gray
                        Write-Host "git cherry-pick $lastMergeCommitId" -ForegroundColor Blue
                        $gitError = Invoke-Git cherry-pick $lastMergeCommitId
                    }

                    if ($gitError.Contains('is a merge but no -m option was given')) {
                        Write-Host "Cherry-picking $($lastMergeCommitId.Substring(0,7)) with merge option" -ForegroundColor Gray
                        Write-Host "git cherry-pick -m 1 $lastMergeCommitId" -ForegroundColor Blue
                        $gitError = Invoke-Git cherry-pick -m 1 $lastMergeCommitId
                    }

                    if ($gitError.StartsWith('error: could not apply')) {
                        # merge conflict occurred
                        if (!(Test-Path $conflictInfoFilePath)) {
                            Write-Host "Creating conflict info file" -ForegroundColor Gray
                            New-Item $conflictInfoFilePath -ItemType File | Out-Null
                        }
                        Write-Host "Updating conflict info file" -ForegroundColor Gray
                        $conflictInfo = Get-Content $conflictInfoFilePath
                        if (!$conflictInfo -or -not $conflictInfo.Contains($pullRequest.pullRequestId)) {
                            Add-Content $conflictInfoFilePath "$($pullRequest.pullRequestId)" | Out-Null
                        }
                        $solutionFilePath = Join-Path $currentRepositoryPath "$currentRepositoryName.sln"
                        Read-Host -Prompt "A new instance of Visual Studio will now open for manual conflict resolving. Press any key to continue"
                        Write-Host "Opening $solutionFilePath for manual conflict resolving" -ForegroundColor Gray
                        Start-Process devenv -ArgumentList "$solutionFilePath /RootSuffix Exp"
                        throw "Please resolve the merge conflict and run this command again."
                    }

                    Write-Host "git commit -m ""Cherry picked commit of PR $($pullRequest.pullRequestId)"" --allow-empty" -ForegroundColor Blue
                    $gitError = Invoke-Git commit -m """Cherry picked commit of PR $($pullRequest.pullRequestId)""" --allow-empty
        
                    if ($gitError) {
                        Write-Host $gitError
                        $solutionFilePath = Join-Path $currentRepositoryPath "$currentRepositoryName.sln"
                        Read-Host -Prompt "A new instance of Visual Studio will now open for manual conflict resolving. Press any key to continue"
                        Write-Host "Opening $solutionFilePath for manual conflict resolving" -ForegroundColor Gray
                        Start-Process devenv -ArgumentList "$solutionFilePath /RootSuffix Exp"
                        throw "Please resolve the merge conflict and run this command again."
                    }

                    Add-Content $processedInfoFilePath "$($pullRequest.pullRequestId)" | Out-Null
                }
            }

            #publish feature branch
            Write-Host "Pushing changes to feature branch" -ForegroundColor Gray
            Write-Host "git push origin $featureBranch" -ForegroundColor Blue
            $gitError = Invoke-Git push origin $featureBranch   

            if($gitError.Contains("Everything up-to-date")) {
                Write-Host "No more changes to push to origin for repository $currentRepositoryName, skipping PR creation"
            } else {
                Invoke-Git notes add -m """Merged: $bugId"""
                Invoke-Git push origin refs/notes/commits        
              
                $createPullRequestUri = "$($tfsUrl)/_apis/git/repositories/$repositoryId/pullrequests?api-version=3.0"
                $createPullRequestBody = @"
{
    "sourceRefName": "refs/heads/$featureBranch",
    "targetRefName": "refs/heads/$targetBranch",
    "title": "Merge of $bugId into $targetBranch - $($workItem.fields.'System.Title'.Replace('"', '\"'))",
    "description": "$pullRequestDescription",
    "reviewers": [
    {
        "id": "f9c35c2b-e4c7-4940-a045-04e49a8381cc"
    }
    ]
}
"@

                #create the pull request
                Write-Host "Creating a pull request from $featureBranch to $targetBranch" -ForegroundColor Gray
                Write-Host "Invoke-RestMethod -Uri $createPullRequestUri -Body $createPullRequestBody -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
                $createdPullRequest = Invoke-RestMethod -Uri $createPullRequestUri -Body $createPullRequestBody -ContentType "application/json" -UseDefaultCredentials -Method Post


                $modifyPullRequestUri = "$($tfsUrl)/_apis/git/repositories/$repositoryId/pullrequests/$($createdPullRequest.pullRequestId)?api-version=3.0"
                $modifyPullRequestBody = @"
{
    "completionOptions": {
        "deleteSourceBranch": "true",
        "mergeCommitMessage": "Merge of $bugId into $targetBranch",
        "squashMerge": "true"
    }
}
"@

                #update the pull request (setting auto-complete etc.)
                Write-Host "Setting auto-complete for pull request $($createdPullRequest.pullRequestId)" -ForegroundColor Gray
                Write-Host "Invoke-RestMethod -Uri $modifyPullRequestUri -Body $modifyPullRequestBody -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
                $updatedPullRequest = Invoke-RestMethod -Uri $modifyPullRequestUri -Body $modifyPullRequestBody -ContentType "application/json" -UseDefaultCredentials -Method Patch

                $linkWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($mergeBugId)?api-version=3.0"
                $linkWorkItemBody = @"
[
    {
    "op": 0,
    "path": "/relations/-",
    "value": {
        "attributes": {
        "name": "Pull Request"
        },
        "rel": "ArtifactLink",
        "url": "$($updatedPullRequest.artifactId)"
    }
    }
]
"@

                #update the merge bug id by linking the newly created PR to it
                Write-Host "Linking for pull request $($createdPullRequest.pullRequestId) to work item $mergeBugId" -ForegroundColor Gray
                Write-Host "Invoke-RestMethod -Uri $linkWorkItemUri -Body $linkWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials" -ForegroundColor Blue
                $updatedWorkItem = Invoke-RestMethod -Uri $linkWorkItemUri -Body $linkWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

                Read-Host -Prompt "Successfully created pull request. A new IE browser window will now open to review the PR and edit optional reviewers. Press any key to continue"
                if ($browser.visible -eq $false) {
                    $browser.navigate($updatedPullRequest.repository.remoteUrl + "/pullrequest/" + $createdPullRequest.pullRequestId)
                    $browser.visible = $true
                } else {
                    $browser.navigate2($updatedPullRequest.repository.remoteUrl + "/pullrequest/" + $createdPullRequest.pullRequestId, "", "_blank")
                }
            }
        }
    } finally {
    }

    Write-Host "`nSuccessfully merged all Pull Requests linked to work item $bugId."
    if ($changeSetInfos.Length -gt 0) {
        Write-Host "`nPlease remember to manually merge the following linked TFVC changesets:" -ForegroundColor Yellow
        Write-Host $changeSetInfos
    }

    try {
        # clean up (take into account symlinks hence don't use Remove-Item as it would delete everything it finds in the symlink folders as well)
        cd C:\Temp\gitMerge
        (cmd /c del /f /s /q $gitReposPath) | Out-Null
        (cmd /c rmdir /s /q $gitReposPath) | Out-Null
        while (Test-Path $gitReposPath) {
            (cmd /c rmdir /s /q $gitReposPath) | Out-Null # do it again as rmdir deletes the directories one by one
        }
    } catch {
        [System.Exception]           
        Write-Host $_.Exception.ToString()
        Write-Host "Could not automatically delete $gitReposPath. You need to clean it up manually." -ForegroundColor Red
    }

    Read-Host -Prompt "Press any key to end this script"

    Write-Host "`n`nDONE" -ForegroundColor Yellow
    Stop-Transcript
}

function Get-ExpertBuildAllVersion () {
    $path = -join($BranchBinariesDirectory, "\BuildAllZipVersion.txt");
    if(-Not(Test-Path $path)){
        "BuildAllZipVersion.txt doesn't exist. your binary folder is probably not from a BuildAll";
        return;
    }

    $version = select-string -Path $path -Pattern "BuildAll_[0-9]+.[0-9]+";
    "$($version.Matches.Value)"
}

# export functions and variables we want external to this script
$functionsToExport = @(
    [pscustomobject]@{ function='Run-ExpertUITests';},
    [pscustomobject]@{ function='Run-ExpertSanityTests';                      alias='rest'},    
    [pscustomobject]@{ function='Run-ExpertVisualTests';                      alias='revt'},    
    [pscustomobject]@{ function='Branch-Module';                              alias='branch'},
    [pscustomobject]@{ function='Build-ExpertModules';                        alias='bm'},
    [pscustomobject]@{ function='Build-ExpertModulesOnServer';                alias='bms'},
    [pscustomobject]@{ function='Build-ExpertPatch';},
    [pscustomobject]@{ function='Change-Directory';                           alias='cdir'},
    [pscustomobject]@{ function='Change-ExpertOwner';},
    [pscustomobject]@{ function='Clear-ExpertCache';                          alias='ccache'},
    [pscustomobject]@{ function='Copy-BinariesFromCurrentModule';             alias='cb'},
    [pscustomobject]@{ function='Disable-ExpertPrompt';                       advanced=$true},
    [pscustomobject]@{ function='Enable-ExpertPrompt';                        advanced=$true},
    [pscustomobject]@{ function='Generate-SystemMap'},
    [pscustomobject]@{ function='Get-AderantModuleLocation';                  advanced=$true},
    [pscustomobject]@{ function='Get-Beep';                                   alias='beep'},
    [pscustomobject]@{ function='Get-CurrentModule'},
    [pscustomobject]@{ function='Get-DependenciesForCurrentModule';           alias='gd'},
    [pscustomobject]@{ function='Get-DependenciesForEachModule';              alias='gde'},
    [pscustomobject]@{ function='Get-DependenciesFrom';                       alias='gdf'},
    [pscustomobject]@{ function='Get-EnvironmentFromXml'},
    [pscustomobject]@{ function='Get-ExpertBuildAllVersion'};
    [pscustomobject]@{ function='Get-ExpertModulesInChangeset'},
    [pscustomobject]@{ function='Get-Database'},
    [pscustomobject]@{ function='Get-DatabaseServer'},
    [pscustomobject]@{ function='Get-Latest'},
    [pscustomobject]@{ function='Get-LocalDependenciesForCurrentModule';      alias='gdl'},
    [pscustomobject]@{ function='Get-Product'},
    [pscustomobject]@{ function='Get-ProductNoDebugFiles'},
    [pscustomobject]@{ function='Get-ProductBuild';                           alias='gpb'},
    [pscustomobject]@{ function='Get-ProductZip'},
    [pscustomobject]@{ function='Git-Merge';},
    [pscustomobject]@{ function='Help'},
    [pscustomobject]@{ function='Install-DeploymentManager'},
    [pscustomobject]@{ function='Install-LatestSoftwareFactory';              alias='usf'},
    [pscustomobject]@{ function='Install-LatestVisualStudioExtension'},
    [pscustomobject]@{ function='Kill-VisualStudio';                          alias='kvs'},
    [pscustomobject]@{ function='Move-Shelveset';},
    [pscustomobject]@{ function='New-BuildModule';},
    [pscustomobject]@{ function='Open-Directory';                             alias='odir'},
    [pscustomobject]@{ function='Open-ModuleSolution';                        alias='vs'},
    [pscustomobject]@{ function='Set-CurrentModule';                          alias='cm'},
    [pscustomobject]@{ function='Set-Environment';                            advanced=$true},
    [pscustomobject]@{ function='Set-ExpertBranchInfo';},    
    [pscustomobject]@{ function='Start-dbgen';                                alias='dbgen'},
    [pscustomobject]@{ function='Start-DeploymentEngine';                     alias='de'},
    [pscustomobject]@{ function='Start-DeploymentManager';                    alias='dm'},
    [pscustomobject]@{ function='Start-Redeployment';                         alias='redeploy'},
    [pscustomobject]@{ function='SwitchBranchTo';                             alias='Switch-Branch'},
    [pscustomobject]@{ function='Prepare-Database';                           alias='dbprep'},
    [pscustomobject]@{ function='Uninstall-DeploymentManager'},
    [pscustomobject]@{ function='Update-Database';                            alias='upd'},
    [pscustomobject]@{ function='Scorch';},
    [pscustomobject]@{ function='Clean';},
    [pscustomobject]@{ function='CleanupIISCache';},
    
    # IIS related functions
    [pscustomobject]@{ function='Hunt-Zombies';                               alias='hz'},
    [pscustomobject]@{ function='Remove-Zombies';                             alias='rz'},
    [pscustomobject]@{ function='Get-WorkerProcessIds';                       alias='wpid'}
)

$helpList = @()

# Exporting the functions and aliases
foreach ($toExport in $functionsToExport) {
    Export-ModuleMember -function $toExport.function

    if ($toExport.alias) {
        Set-Alias $toExport.alias  $toExport.function
        Export-ModuleMember -Alias $toExport.alias
    }
}

# paths
Export-ModuleMember -variable CurrentModuleName
Export-ModuleMember -variable BranchServerDirectory
Export-ModuleMember -variable BranchLocalDirectory
Export-ModuleMember -variable CurrentModulePath
Export-ModuleMember -variable BranchBinariesDirectory
Export-ModuleMember -variable BranchName
Export-ModuleMember -variable BranchModulesDirectory
Export-ModuleMember -variable ProductManifestPath

. $PSScriptRoot\Feature.Database.ps1

Measure-Command {
  Enable-ExpertPrompt
} "Enable-ExpertPrompt"

Measure-Command {
  Check-Vsix "NUnit3.TestAdapter" "0da0f6bd-9bb6-4ae3-87a8-537788622f2d" "NUnit.NUnit3TestAdapter"
} "NUnit3.TestAdapter install"

Measure-Command {
  Check-Vsix "Aderant.DeveloperTools" "b36002e4-cf03-4ed9-9f5c-bf15991e15e4"
} "Aderant.DeveloperTools install"


$ShellContext.SetRegistryValue("", "LastVsixCheckCommit", $ShellContext.CurrentCommit) | Out-Null

Set-Environment

Write-Host ''
Write-Host 'Type ' -NoNewLine
Write-Host '"help"' -ForegroundColor Green -NoNewLine
Write-Host " for a command list." -NoNewLine
Write-Host ''