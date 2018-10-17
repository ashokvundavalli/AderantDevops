Set-StrictMode -Version Latest

# Import extensibility functions
Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath "..\..\Build\Functions") -Filter "*.ps1" | Where-Object {$_.Extension -eq ".ps1" } | ForEach-Object { . $_.FullName }
Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath "Functions") -Filter "*.ps1" | Where-Object {$_.Extension -eq ".ps1" } | ForEach-Object { . $_.FullName }
Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath "Functions") -Filter "*.psm1" | Where-Object {$_.Extension -eq ".psm1" } | ForEach-Object { Import-Module $_.FullName -DisableNameChecking }
Update-FormatData -PrependPath (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Build\Functions\Formats\SourceTreeMetadata.format.ps1xml')

$script:ShellContext = $null

function Initialize-Module {
    . $PSScriptRoot\ShellContext.ps1
    $script:ShellContext = [ShellContext]::new()

    UpdateOrBuildAssembly $ShellContext.BuildScriptsDirectory $true

    $context = New-BuildContext -Environment "AutoDiscover"
    $MyInvocation.MyCommand.Module.PrivateData.Context = $context
    $MyInvocation.MyCommand.Module.PrivateData.ShellContext = $script:ShellContext
}

Initialize-Module

[string]$global:BranchName = ""
[string]$global:BranchLocalDirectory = ""
[string]$global:BranchServerDirectory = ""
[string]$global:BranchModulesDirectory = ""
[string]$global:BranchBinariesDirectory = ""
[string]$global:BranchExpertSourceDirectory = ""
[string]$global:BuildScriptsDirectory = ""
[string]$global:PackageScriptsDirectory = ""
[string]$global:ProductManifestPath = ""
[string]$global:CurrentModuleName = ""
[string]$global:CurrentModulePath = ""
[PSModuleInfo]$currentModuleFeature = $null
[string[]]$global:LastBuildBuiltModules = @()
[string[]]$global:LastBuildRemainingModules = @()
[string[]]$global:LastBuildGetLocal = @()
[bool]$global:LastBuildGetDependencies = $false
[bool]$global:LastBuildCopyBinaries = $false
[bool]$global:LastBuildDownstream = $false
[bool]$global:LastBuildGetLatest = $false

[string[]]$titles = @(
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
    "Duplicating Offline Cache",
    "Replacing Headlight Fluid",
    "Dailing Roper Hotline"
)

$Host.UI.RawUI.WindowTitle = Get-Random -InputObject $titles

<#
Expert specific variables
#>

# gets a value from the global defaults storage, or creates a default
function global:GetDefaultValue {
    param (
        [string]$propertyName,
        [string]$defaultValue
    )

    begin {
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
    }

    process {
        Write-Debug "Asked for default for: $propertyName with default ($defaultValue)"

        if ($null -ne [Environment]::GetEnvironmentVariable("Expert$propertyName", "User")) {
            return [Environment]::GetEnvironmentVariable("Expert$propertyName", "User")
        }

        if ($propertyName -eq "DevBranchFolder") {
            Clear-Host
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
}

function IsDevBanch([string]$name) {
    return $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsReleaseBanch([string]$name) {
    return $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsMainBanch([string]$name) {
    return $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase)
}

function ResolveBranchName([string]$branchPath) {
    if (IsMainBanch $branchPath) {
        $name = "MAIN"
    } elseif (IsDevBanch $branchPath) {
        $name = $branchPath.Substring($branchPath.LastIndexOf("dev\", [System.StringComparison]::OrdinalIgnoreCase))
    } elseif (IsReleaseBanch $branchPath) {
        $name = $branchPath.Substring($branchPath.LastIndexOf("releases\", [System.StringComparison]::OrdinalIgnoreCase))
    }
    return $name
}

<#
Branch information
#>
function Set-BranchPaths {
    #initialise from default setting
    Write-Debug "Setting information for branch from your defaults"
    $global:BranchLocalDirectory = (GetDefaultValue "DevBranchFolder").ToLower()
    $global:BranchName = ResolveBranchName $global:BranchLocalDirectory
    $ShellContext.BranchServerDirectory = (GetDefaultValue "DropRootUNCPath").ToLower()
    $ShellContext.BranchModulesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath "\Modules"
    $ShellContext.BranchBinariesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath "\Binaries"

    if (-not (Test-Path $global:BranchLocalDirectory)) {
        Write-Host ""
        Write-Host "*********************************************************************************************************************************"
        Write-Warning "The branch directory does not exist. Call Set-ExpertBranchInfo for initial setup of local directory and branch info"
        Write-Host "*********************************************************************************************************************************"
        Write-Host ""

        throw "Please setup environment"
    }
}

<#
Set-ExpertSourcePath is called on startup and SwitchBranchTo.  It sets $ShellContext.BranchExpertVersion and $ShellContext.BranchServerDirectory.
Pre-8.0 environments still use the old folder structure where everything was in the binaries folder, so BranchExpertSourceDirectory is set
according to the setting in the ExpertManifest.xml file.
#>
function Set-ExpertSourcePath {
    [xml]$manifest = Get-Content $ShellContext.ProductManifestPath
    [string]$branchExpertVersion = $manifest.ProductManifest.ExpertVersion

    if ($branchExpertVersion.StartsWith("8")) {
        $global:BranchExpertSourceDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath "\Binaries\ExpertSource"

        if (-not (Test-Path -Path $global:BranchExpertSourceDirectory)) {
            [System.IO.Directory]::CreateDirectory($global:BranchExpertSourceDirectory) | Out-Null
        }
    } else {
        $global:BranchExpertSourceDirectory = $ShellContext.BranchBinariesDirectory
    }
}

function Set-ScriptPaths {
    Write-Debug -Message "BranchModulesDirectory: $($ShellContext.BranchModulesDirectory)"

    if ([System.IO.File]::Exists($ShellContext.BranchModulesDirectory + "\ExpertManifest.xml")) {
        [string]$root = Resolve-Path "$PSScriptRoot\..\..\..\"

        $ShellContext.BuildScriptsDirectory = Join-Path -Path $root -ChildPath "Src\Build"
        Write-Debug -Message "BuildScriptsDirectory: $($ShellContext.BuildScriptsDirectory)"
        $ShellContext.PackageScriptsDirectory = Join-Path -Path $root -ChildPath "Src\Package"
        Write-Debug -Message "PackageScriptsDirectory: $($ShellContext.PackageScriptsDirectory)"
        $ShellContext.ProductManifestPath = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "ExpertManifest.xml"
        Write-Debug -Message "ProductManifestPath: $($ShellContext.ProductManifestPath)"
    } else {
        $ShellContext.BuildScriptsDirectory = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "\Build.Infrastructure\Src\Build"
        Write-Debug -Message "BuildScriptsDirectory: $($ShellContext.BuildScriptsDirectory)"
        $ShellContext.PackageScriptsDirectory = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "\Build.Infrastructure\Src\Package"
        Write-Debug -Message "PackageScriptsDirectory: $($ShellContext.PackageScriptsDirectory)"
        $ShellContext.ProductManifestPath = Join-Path -Path $ShellContext.PackageScriptsDirectory -ChildPath "\ExpertManifest.xml"
        Write-Debug -Message "ProductManifestPath: $($ShellContext.ProductManifestPath)"
    }
}

<#
    Initialise functions from Build-Libraries.ps1
#>
function Initialise-BuildLibraries {
    . ($ShellContext.BuildScriptsDirectory + "\Build-Libraries.ps1")
}

# Called from SwitchBranchTo
function Set-ChangedBranchPaths([string]$name) {
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
        $previousBranchContainer = $global:BranchName.Substring(0, $global:BranchName.LastIndexOf("\"))
        $previousBranchName = $global:BranchName.Substring($global:BranchName.LastIndexOf("\") + 1)
    } elseif ((IsMainBanch $global:BranchName)) {
        $previousBranchName = "MAIN"
        $changeToContainerFromMAIN = $true
    }

    if ((IsDevBanch $name) -or (IsReleaseBanch $name)) {
        $newBranchContainer = $name.Substring(0, $name.LastIndexOf("\"))
        $newBranchName = $name.Substring($name.LastIndexOf("\") + 1)
    } elseif ((IsMainBanch $name)) {
        $newBranchName = "MAIN"
        $newBranchContainer = "\"
    }

    $success = $false
    if ($changeToContainerFromMAIN) {
        $success = Switch-BranchFromMAINToContainer $newBranchContainer $newBranchName $previousBranchName
    } else {
        $success = Switch-BranchFromContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }

    if ($success -eq $false) {
        Write-Host -ForegroundColor Yellow "'$name' branch was not found on this machine."
        return $false
    }

    #Set common paths
    $ShellContext.BranchModulesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Modules" )

    $ShellContext.BranchBinariesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Binaries" )
    if ((Test-Path $ShellContext.BranchBinariesDirectory) -eq $false) {
        New-Item -Path $ShellContext.BranchBinariesDirectory -ItemType Directory
    }

    return $true
}

<#
 we need to cater for the fact MAIN is the only branch and not a container like dev or release
#>
function Switch-BranchFromMAINToContainer($newBranchContainer, $newBranchName, $previousBranchName) {
    #change name and then container and remove extra backslash's
    $globalBranchName = ($global:BranchName -replace $previousBranchName, $newBranchName)
    $globalBranchName = $newBranchContainer + "\" + $globalBranchName

    if ($globalBranchName -eq "\") {
        return $false
    }

    # The strip logic assumes the last slash is the container separator, if the local dir ends with a slash it will break that assumption
    $global:BranchLocalDirectory = $global:BranchLocalDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar)

    #strip MAIN then add container and name
    $globalBranchLocalDirectory = $global:BranchLocalDirectory.Substring(0, $global:BranchLocalDirectory.LastIndexOf("\") + 1)
    $globalBranchLocalDirectory = (Join-Path -Path $globalBranchLocalDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $globalBranchLocalDirectory) -eq $false) {
        return $false
    }

    $global:BranchName = $globalBranchName
    $global:BranchLocalDirectory = $globalBranchLocalDirectory

    #strip MAIN then add container and name
    $ShellContext.BranchServerDirectory = $ShellContext.BranchServerDirectory.Substring(0, $ShellContext.BranchServerDirectory.LastIndexOf("\") + 1)
    $ShellContext.BranchServerDirectory = (Join-Path -Path $ShellContext.BranchServerDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    $ShellContext.BranchServerDirectory = [System.IO.Path]::GetFullPath($ShellContext.BranchServerDirectory)

    return $true
}

<#
 we dont have to do anything special if we change from a container to other branch type
#>
function Switch-BranchFromContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName) {
    #change name and then container and remove extra backslash's
    $globalBranchName = $global:BranchName.replace($previousBranchName, $newBranchName)
    $globalBranchName = $globalBranchName.replace($previousBranchContainer, $newBranchContainer)
    if (IsMainBanch $globalBranchName) {
        $globalBranchName = [System.Text.RegularExpressions.Regex]::Replace($globalBranchName, "[^1-9a-zA-Z_\+]", "");
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

    $ShellContext.BranchServerDirectory = $ShellContext.BranchServerDirectory.Substring(0, $ShellContext.BranchServerDirectory.LastIndexOf($previousBranchContainer));
    $ShellContext.BranchServerDirectory = (Resolve-Path -Path ($ShellContext.BranchServerDirectory + $newBranchContainer + "\" + $newBranchName)).ProviderPath

    $ShellContext.BranchServerDirectory = [System.IO.Path]::GetFullPath($ShellContext.BranchServerDirectory)

    return $true
}

function Set-CurrentModule {
    param (
        [string]$name
    )

    if ([string]::IsNullOrWhiteSpace($name)) {
        if ([string]::IsNullOrWhiteSpace($ShellContext.CurrentModuleName)) {
            Write-Warning "No current module is set"
            return
        } else {
            Write-Host "The current module is [$context.ModuleName] on the branch [$global:BranchName]"
            return
        }
    }

    if ($name -eq ".") {
        $name = Resolve-Path $name
    }

    if ($null -ne $currentModuleFeature) {
        if (Get-Module | Where-Object -Property Name -eq $currentModuleFeature.Name) {            
            Remove-Module $currentModuleFeature
        }

        $currentModuleFeature = $null
    }

    if ([System.IO.Path]::IsPathRooted($name)) {
        $ShellContext.CurrentModulePath = $name
        $ShellContext.CurrentModuleName = ([System.IO.DirectoryInfo]::new($ShellContext.CurrentModulePath)).Name

        Write-Debug "Setting repository: $name"
        Import-Module $PSScriptRoot\Git.psm1 -Global

        if (-not (Test-Path (Join-Path -Path $ShellContext.CurrentModulePath -ChildPath \Build\TFSBuild.*))){
            $ShellContext.CurrentModuleName = ""
        }

        Set-Location $ShellContext.CurrentModulePath

        if (IsGitRepository $ShellContext.CurrentModulePath) {
            SetRepository $ShellContext.CurrentModulePath
            global:Enable-GitPrompt
            return
        } elseif (IsGitRepository ([System.IO.DirectoryInfo]::new($ShellContext.CurrentModulePath).Parent.FullName)) {
            global:Enable-GitPrompt
            return
        } else {
            Enable-ExpertPrompt
        }
    } else {
        $ShellContext.CurrentModuleName = $name

        Write-Debug "Current module [$ShellContext:CurrentModuleName]"
        $ShellContext.CurrentModulePath = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath $ShellContext.CurrentModuleName

        Set-Location $ShellContext.CurrentModulePath

        Enable-ExpertPrompt
    }

    if ((Test-Path $ShellContext.CurrentModulePath) -eq $false) {
        Write-Warning "the module [$($ShellContext.CurrentModuleName)] does not exist, please check the spelling."
        $ShellContext.CurrentModuleName = ""
        $ShellContext.CurrentModulePath = ""
        return
    }

    Write-Debug "Current module path [$($ShellContext.CurrentModulePath)]"
    $ShellContext.CurrentModuleBuildPath = Join-Path -Path $ShellContext.CurrentModulePath -ChildPath "Build"

    $ShellContext.IsGitRepository = $true
}

function IsGitRepository {
    param (
        [string]$path
    )

    if ([System.IO.path]::GetPathRoot($path) -eq $path) {
        return $false
    }

    return @(Get-ChildItem -Path $path -Filter ".git" -Recurse -Depth 1 -Attributes Hidden -Directory).Length -gt 0
}

function SetRepository([string]$path) {
    $ShellContext.IsGitRepository = $true

    [string]$currentModuleBuildDirectory = "$path\Build"

    if (Test-Path $currentModuleBuildDirectory) {
        [string]$featureModule = Get-ChildItem -Path $currentModuleBuildDirectory -Recurse | Where-Object { $_.extension -eq ".psm1" -and $_.Name -match "Feature.*" } | Select-Object -First 1 | Select-Object -ExpandProperty FullName
        if ($featureModule) {
            ImportFeatureModule $featureModule
        }
    }
}

function ImportFeatureModule([string]$featureModule) {
    Import-Module -Name $featureModule -Scope Global -WarningAction SilentlyContinue
    $currentModuleFeature = Get-Module | Where-Object -Property Path -eq $featureModule
    Write-Host "`r`nImported module: $($currentModuleFeature.Name)" -ForegroundColor Cyan
    Get-Command -Module $currentModuleFeature.Name
}

function Get-CurrentModule {
    return Get-ExpertModule -ModuleName $ShellContext.CurrentModuleName
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
    Write-Host "Path :" $ShellContext.BranchServerDirectory
    
    if ($ShellContext.CurrentModuleName -and $ShellContext.CurrentModulePath) {    
        Write-Host ""
        Write-Host "-----------------------------"
        Write-Host "Current Module Information"
        Write-Host "-----------------------------"
        Write-Host "Name :" $ShellContext.CurrentModuleName
        Write-Host "Path :" $ShellContext.CurrentModulePath
        Write-Host ""
    }
}

<#
 return the connection string to be used for the sitemap builder
#>
function Get-SystemMapConnectionString {
    return (GetDefaultValue "systemMapConnectionString").ToLower()
}

function New-BuildModule {
    param (
        [string]$name
    )

    process {
        & "$PSScriptRoot..\..\ModuleCreator\create_module.ps1" -ModuleName $name -DestinationFolder $ShellContext.BranchModulesDirectory
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
    } else {
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
    } else {
        Install-LatestVisualStudioExtensionImpl $installDetails
    }
}

function Install-LatestVisualStudioExtensionImpl($installDetails, [switch]$local) {
    # Uninstall the extension
    Write-Host "Uninstalling $($installDetails.ProductManifestName)..."
    $vsix = "VSIXInstaller.exe"
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null
    Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null

    # Take VSIX out of local source directory
    if ($local) {
        Write-Host "Attempting to install $($info.ProductManifestName) from local source directory."
        $vsixFile = [System.IO.Path]::Combine($ShellContext.BranchServerDirectory, $info.ExtensionFile)
    } else { # Take VSIX from drop folder
        Write-Host "Attempting to install $($info.ProductManifestName) from drop folder."
        $localInstallDirectory = [System.IO.Path]::Combine($global:BranchLocalDirectory, $info.ProductManifestName + ".Install")

        [xml]$manifest = Get-Content $ShellContext.ProductManifestPath
        [System.Xml.XmlNode]$module = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object { $_.Name.Contains($info.ProductManifestName)}

        Invoke-Expression "$ShellContext.BuildScriptsDirectory\Build-Libraries.ps1"
        $dropPathVSIX = (GetPathToBinaries $module $ShellContext.BranchServerDirectory)

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
            Write-Host ""
            $displayName = $info.ExtensionDisplayName
            Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'TOOLS -> Exensions and Updates' check if there is the '$displayName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try this command again."
        }
    }
}

Function Output-VSIXLog {
    $errorsOccurred = $false
    $lastLogFile = Get-ChildItem $env:TEMP | Where-Object { $_.Name.StartsWith("VSIX") } | Sort-Object LastWriteTime | Select-Object -last 1

    if ($null -ne $lastLogFile) {
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

#TODO: Front end with the http build service to cache the results for remote clients 
Register-ArgumentCompleter -CommandName Get-Product -ParameterName "pullRquestId" -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $boundParameters)        

    # TODO: Externalize
    # TODO: Call build service for caching for people in the US
    $stem = "http://tfs:8080/tfs/Aderant/ExpertSuite"
    $results = Invoke-RestMethod -Uri "$stem/_apis/git/pullrequests" -ContentType "application/json" -UseDefaultCredentials

    $ids = $results.value | Select-Object -Property pullRequestId, title

    if (-not $wordToComplete.EndsWith("*")) {
        $wordToComplete += "*"
    }

    $ids | Where-Object -FilterScript { $_.pullRequestId -like $wordToComplete -or $_.title -like $wordToComplete } | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_.pullRequestId, $_.title, [System.Management.Automation.CompletionResultType]::Text, $_.title)
    }
}

<#
.Synopsis
    Sets up visual studio environment, called from Profile.ps1 when starting PS.
.Description
    Sets up visual studio environment, called from Profile.ps1 when starting PS.
.PARAMETER initialize
    Sets branch paths and installs Pester.
#>
function Set-Environment {
    param (
        [switch]$initialize
    )

    process {
        if ($initialize) {
            Set-BranchPaths
        } else {
            Clear-Variable -Name "BranchLocalDirectory" -Scope "Global"
        }

        Set-ScriptPaths
        Set-ExpertSourcePath
        Initialise-BuildLibraries
        Set-VisualStudioVersion

        OutputEnvironmentDetails

        if ($initialize.IsPresent) {
            # Setup PowerShell script unit test environment
            Install-Pester
        }
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

    Set-Environment

    Set-CurrentModule $ShellContext.CurrentModuleName

    Set-Location -Path $global:BranchLocalDirectory

    if ($SetAsDefault) {
        SetDefaultValue dropRootUNCPath $ShellContext.BranchServerDirectory
        SetDefaultValue devBranchFolder $global:BranchLocalDirectory
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
    if ((Test-Path $devBranchFolder) -ne $true) {
        Write-Error "The path $devBranchFolder does not exist"
    }

    if ((Test-Path $dropUncPath) -ne $true) {
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
function Open-ModuleSolution() {
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty][string]$ModuleName,
        [switch]$getDependencies,
        [switch]$getLatest,
        [switch]$code,
        [switch]$seventeen
    )

    begin {
        [string]$devenv = "devenv"
    }

    process {      
        if ($seventeen) {
            [string]$vsSeventeenDirectory = "C:\Program Files (x86)\Microsoft Visual Studio\2017\*\Common7\IDE\devenv.exe"

            if (Test-Path $vsSeventeenDirectory) {
                $devenv = (Get-Item $vsSeventeenDirectory | select-object -First 1).FullName
            } else {
                Write-Host "VS 2017 could not be found ($vsSeventeenDirectory)"
            }
        }

        $prevModule = $null

        if (($getDependencies) -and -not [string]::IsNullOrEmpty($ModuleName)) {
            if (-not [string]::IsNullOrEmpty($ShellContext.CurrentModuleName)) {
                $prevModule = Get-CurrentModule
            }

            Set-CurrentModule $moduleName
        }

        [string]$rootPath = ""

        if (-not [string]::IsNullOrWhiteSpace($ModuleName)) {
            $rootPath = Join-Path $global:BranchLocalDirectory "Modules\$ModuleName"
        } else {                
            $ModuleName = $ShellContext.CurrentModuleName
            $rootPath = $ShellContext.CurrentModulePath
        }

        if ([string]::IsNullOrWhiteSpace($moduleName)) {
            Write-Warning "No module specified."
            return
        }

        if ($getDependencies) {
            Write-Host "Getting Dependencies for module: $ModuleName"
            Get-DependenciesForCurrentModule
        }

        if ($getLatest) {
            Write-Host "Getting latest source for module: $ModuleName"
            Get-Latest -ModuleName $ModuleName;
        }

        Write-Host "Opening solution for module: $ModuleName"
        $moduleSolutionPath = Join-Path $rootPath "$ModuleName.sln"

        if (Test-Path $moduleSolutionPath) {
            if ($code) {
                if (Get-Command code -errorAction SilentlyContinue) {
                    Invoke-Expression "code $rootPath"
                } else {
                    Write-Host "VS Code could not be found (code)"
                }
            } else {
                Invoke-Expression "& '$devenv' $moduleSolutionPath"
            }
        } else {
            [System.IO.FileSystemInfo[]]$candidates = (Get-ChildItem -Filter *.sln -file  | Where-Object {$_.Name -NotMatch ".custom.sln"})
            if ($candidates.Count -gt 0) {
                $moduleSolutionPath = Join-Path $rootPath $candidates[0]

                if ($code) {
                    if (Get-Command code -errorAction SilentlyContinue) {
                        Invoke-Expression "code $rootPath"
                    } else {
                        Write-Host "VS Code could not be found (code)"
                    }
                } else {
                    Invoke-Expression "& '$devenv' $moduleSolutionPath"
                }
            } else {
                "There is no solution file at $moduleSolutionPath"
            }
        }

        if (($null -ne $prevModule) -and (Get-CurrentModule -ne $prevModule)) {
            Set-CurrentModule $prevModule
        }
    }
}

function TabExpansion([string] $line, [string] $lastword) {
    if (-not $lastword.Contains(";")) {
        $aliases = Get-Alias
        $parser = New-Object Aderant.Build.AutoCompletionParser $line, $lastword, $aliases

        # Evaluate Branches
        Try {
            foreach ($tabExpansionParm in $global:expertTabBranchExpansions) {
                if ($parser.IsAutoCompletionForParameter($tabExpansionParm.CommandName.ToString(), $tabExpansionParm.ParameterName.ToString(), $tabExpansionParm.IsDefault.IsPresent)) {
                    Get-ExpertBranches $lastword | Get-Unique
                }
            }
        } Catch {
            [system.exception]
            Write-Host $_.Exception.ToString()
        }        
    }

    [System.Diagnostics.Debug]::WriteLine("Aderant Build Tools:Falling back to default tab expansion for Last word: $lastword, Line: $line")   
}

$global:expertTabBranchExpansions = @()

<#
.Synopsis
    Adds a parameter to the expert tab expansions for modules
.Description
    Tab Expansion is when pressing tab will auto-complete the value of a parameter.
    This command allows you to configure autocomplete where a module name or comma separated list of module names is required
.PARAMETER CommandName
    The name of the command (not the alias)
.PARAMETER ParameterName
    The name of the parameter to match
.EXAMPLE
    Add-ModuleExpansionParameter -CommandName Build-ExpertModules -ParameterName ModuleNames -IsDefault
    Will add tab expansion of module names on the Build-ExpertModules command where the current parameter is the ModuleNames parameter
#>
function Add-ModuleExpansionParameter {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$CommandName,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$ParameterName
    )
       
    Register-ArgumentCompleter -CommandName $CommandName -ParameterName $ParameterName -ScriptBlock {
        param($commandName, $parameterName, $wordToComplete, $commandAst, $boundParameters)

        $parser = [Aderant.Build.AutoCompletionParser]::new($commandName, $parameterName, $commandAst)
                
        # Evaluate Modules
        try {

            $parser.GetModuleMatches($wordToComplete, $ShellContext.BranchModulesDirectory, $ShellContext.ProductManifestPath) | Get-Unique | ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new($_)
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
function Add-BranchExpansionParameter([string]$CommandName, [string]$ParameterName, [switch] $IsDefault) {
    if ([string]::IsNullOrWhiteSpace($CommandName)) {
        Write-Error "No command name specified."
        return
    }
    if ([string]::IsNullOrWhiteSpace($ParameterName)) {
        Write-Error "No parameter name specified."
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
    Function global:prompt {
        # set the window title to the branch name
        $Host.UI.RawUI.WindowTitle = "PS - [" + $ShellContext.CurrentModuleName + "] on branch [" + $global:BranchName + "]"

        Write-Host("")
        Write-Host ("Module [") -nonewline
        Write-Host ($ShellContext.CurrentModuleName) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] at [") -nonewline
        Write-Host ($ShellContext.CurrentModulePath) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] on branch [") -nonewline
        Write-Host ($global:BranchName) -nonewline -foregroundcolor Green
        Write-Host ("]")

        Write-Host ("PS " + $(get-location) + ">") -nonewline
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
    Function global:prompt {
        $(if (test-path variable:/PSDebugContext) { '[DBG]: ' }
            else { '' }) + 'PS ' + $(Get-Location) `
            + $(if ($nestedpromptlevel -ge 1) { '>>' }) + '> '
    }
}

function Test-ReparsePoint([string]$path) {
    $file = Get-Item $path -Force -ea 0

    if ($null -eq $file) {
        return $false
    }

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
    if (Test-ReparsePoint $aderantModuleBase ) {
        # this is a symlink, get the target.
        return Get-SymbolicLinkTarget $aderantModuleBase
    } else {
        # this is a normal folder.
        return $aderantModuleBase
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
                $theHelpList += [PSCustomObject]@{Command = $toExport.alias; alias = $toExport.alias; Synopsis = $help.Synopsis}
            } else {
                $theHelpList += [PSCustomObject]@{Command = $toExport.function; alias = $null; Synopsis = $help.Synopsis}
            }
        }
    }

    if ($searchText) {
        $searchText = "*$searchText*";
        foreach ($func in $theHelpList) {
            $functionName = $func.function
            $aliasName = $func.alias

            if (($functionName -like $searchText) -or ($aliasName -like $searchText)) {
                Write-Host -ForegroundColor Green -NoNewline "$functionName, $aliasName "
                Write-Host (Get-Help $functionName).Synopsis
            }
        }
        return
    }

    $AderantModuleLocation = Get-AderantModuleLocation
    Write-Host "Using Aderant Module from : $AderantModuleLocation"

    $sortedFunctions = $theHelpList | Sort-Object -Property alias -Descending
    $sortedFunctions | Format-Table Command, Synopsis
}

<#
.Synopsis
    Wrapper function that deals with Powershell's peculiar error output when Git uses the error stream.
#>
function Invoke-Git {
    [CmdletBinding()]
    param(
        [parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & {
        [CmdletBinding()]
        param(
            [parameter(ValueFromRemainingArguments = $true)]
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

function Reset-DeveloperShell() {
  Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Build\Functions') -Filter '*.ps1' | ForEach-Object { . $_.FullName }
}

# export functions and variables we want external to this script
$functionsToExport = @(
    [PSCustomObject]@{ function = 'Run-ExpertUITests'; alias = $null; },
    [PSCustomObject]@{ function = 'Run-ExpertSanityTests'; alias = 'rest'; },    
    [PSCustomObject]@{ function = 'Run-ExpertVisualTests'; alias = 'revt'; },    
    [PSCustomObject]@{ function = 'Branch-Module'; alias = 'branch'; },
    [PSCustomObject]@{ function = 'Build-ExpertModules'; alias = 'bm'; },
    [PSCustomObject]@{ function = 'Build-ExpertModulesOnServer'; alias = 'bms'; },
    [PSCustomObject]@{ function = 'Build-ExpertPatch'; alias = $null; },
    [PSCustomObject]@{ function = 'Change-ExpertOwner'; alias = $null; },
    [PSCustomObject]@{ function = 'Clear-ExpertCache'; alias = 'ccache'; },
    [PSCustomObject]@{ function = 'Copy-BinariesFromCurrentModule'; alias = 'cb'; },
    [PSCustomObject]@{ function = 'Disable-ExpertPrompt'; advanced = $true; alias = $null; },
    [PSCustomObject]@{ function = 'Enable-ExpertPrompt'; advanced = $true; alias = $null; },
    [PSCustomObject]@{ function = 'Generate-SystemMap'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-AderantModuleLocation'; advanced = $true; alias = $null; },
    [PSCustomObject]@{ function = 'Get-CurrentModule'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-DependenciesForCurrentModule'; alias = 'gd'; },
    [PSCustomObject]@{ function = 'Get-DependenciesFrom'; alias = 'gdf'; },
    [PSCustomObject]@{ function = 'Get-EnvironmentFromXml'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-ExpertBuildAllVersion'; alias = $null; };
    [PSCustomObject]@{ function = 'Get-ExpertModulesInChangeset'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-Database'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-DatabaseServer'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-Latest'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-LocalDependenciesForCurrentModule'; alias = 'gdl'; },
    [PSCustomObject]@{ function = 'Get-Product'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-ProductNoDebugFiles'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-ProductBuild'; alias = 'gpb'; },
    [PSCustomObject]@{ function = 'Get-ProductZip'; alias = $null; },
    [PSCustomObject]@{ function = 'Git-Merge'; alias = $null; },
    [PSCustomObject]@{ function = 'Help'; alias = $null; },
    [PSCustomObject]@{ function = 'Install-DeploymentManager'; alias = $null; },
    [PSCustomObject]@{ function = 'Install-LatestSoftwareFactory'; alias = 'usf'; },
    [PSCustomObject]@{ function = 'Install-LatestVisualStudioExtension'; alias = $null; },
    [PSCustomObject]@{ function = 'Move-Shelveset'; alias = $null; },
    [PSCustomObject]@{ function = 'New-BuildModule'; alias = $null; },
    [PSCustomObject]@{ function = 'Open-ModuleSolution'; alias = 'vs'; },
    [PSCustomObject]@{ function = 'Set-CurrentModule'; alias = 'cm'; },
    [PSCustomObject]@{ function = 'Set-Environment'; advanced = $true; alias = $null; },
    [PSCustomObject]@{ function = 'Set-ExpertBranchInfo'; alias = $null; },    
    [PSCustomObject]@{ function = 'Start-dbgen'; alias = 'dbgen'; },
    [PSCustomObject]@{ function = 'Start-DeploymentEngine'; alias = 'de'; },
    [PSCustomObject]@{ function = 'Start-DeploymentManager'; alias = 'dm'; },
    [PSCustomObject]@{ function = 'SwitchBranchTo'; alias = 'Switch-Branch'; },
    [PSCustomObject]@{ function = 'Prepare-Database'; alias = 'dbprep'; },
    [PSCustomObject]@{ function = 'Uninstall-DeploymentManager'; alias = $null; },
    [PSCustomObject]@{ function = 'Update-Database'; alias = 'upd'; },
    [PSCustomObject]@{ function = 'Scorch'; alias = $null; },
    [PSCustomObject]@{ function = 'Clean'; alias = $null; },
    [PSCustomObject]@{ function = 'CleanupIISCache'; alias = $null; },
    
    # IIS related functions
    [PSCustomObject]@{ function = 'Hunt-Zombies'; alias = 'hz'},
    [PSCustomObject]@{ function = 'Remove-Zombies'; alias = 'rz'},
    [PSCustomObject]@{ function = 'Get-WorkerProcessIds'; alias = 'wpid'}
)

# Exporting the functions and aliases
foreach ($toExport in $functionsToExport) {
    Export-ModuleMember -function $toExport.function

    if ($toExport.alias) {
        Set-Alias $toExport.alias $toExport.function
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

Export-ModuleMember -Function Get-DependenciesFrom
Export-ModuleMember -Function Reset-DeveloperShell

#TODO move
#. $PSScriptRoot\Feature.Database.ps1

#Measure-Command {
#    Enable-ExpertPrompt
#} "Enable-ExpertPrompt"

#Measure-Command {
#    Check-Vsix "NUnit3.TestAdapter" "0da0f6bd-9bb6-4ae3-87a8-537788622f2d" "NUnit.NUnit3TestAdapter"
#} "NUnit3.TestAdapter install"

#Measure-Command {
#    Check-Vsix "Aderant.DeveloperTools" "b36002e4-cf03-4ed9-9f5c-bf15991e15e4"
#} "Aderant.DeveloperTools install"

#$ShellContext.SetRegistryValue("", "LastVsixCheckCommit", $ShellContext.CurrentCommit) | Out-Null

Set-Environment -initialize

Write-Host ""
Write-Host "Type " -NoNewLine
Write-Host '"help"' -ForegroundColor Green -NoNewLine
Write-Host " for a command list." -NoNewLine
Write-Host ""