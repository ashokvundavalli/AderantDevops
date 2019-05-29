Set-StrictMode -Version Latest

# Need to load Aderant.Build.dll first as it defines types used in later scripts
. "$PSScriptRoot\..\..\Build\Functions\Initialize-BuildEnvironment.ps1"

# Import extensibility functions
$imports = @(
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath "..\..\Build\Functions") -Filter "*.ps1"),
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath "Functions") -Filter "*.ps1"),
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath "Modules") -Filter "*.psm1")
)

foreach ($directory in $imports) {
    foreach ($file in $directory) {
        if ($file.Extension -eq ".ps1") {
            . $file.FullName
        }

        if ($file.Extension -eq ".psm1") {
            Import-Module $file.FullName -DisableNameChecking
        }
    }
}
Update-FormatData -PrependPath (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Build\Functions\Formats\SourceTreeMetadata.format.ps1xml')

$global:ShellContext = $null

function Initialize-Module {
    . $PSScriptRoot\ShellContext.ps1
    $global:ShellContext = [ShellContext]::new()    
    $MyInvocation.MyCommand.Module.PrivateData.ShellContext = $global:ShellContext
}

Initialize-Module

[string]$global:BranchConfigPath = ""
[string]$ShellContext.BranchName = ""
[string]$global:BranchLocalDirectory = ""
[string]$global:BranchServerDirectory = ""
[string]$global:BranchModulesDirectory = ""
[string]$global:BranchBinariesDirectory = ""
[string]$global:BranchExpertSourceDirectory = ""
[string]$global:BuildScriptsDirectory = $global:ShellContext.BuildScriptsDirectory
[string]$global:PackageScriptsDirectory = ""
[string]$global:ProductManifestPath = ""
[string]$global:InstallPath = ""
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
    "Dialing Roper Hotline"
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

function Set-BinariesDirectory {
    $environmentXml = [System.IO.Path]::Combine($global:BranchLocalDirectory, "environment.xml")

    if (-not (Test-Path $environmentXml)) {
        $global:BranchBinariesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath "\Binaries"
    } else {
        # If the environment xml exists in the DevBranchFolder, set it as the BranchBinariesDirectory
        $global:BranchBinariesDirectory = $global:BranchLocalDirectory
    }

    $ShellContext.BranchBinariesDirectory = $global:BranchBinariesDirectory
}

<#
Branch information
#>
function Set-BranchPaths {
    Write-Debug "Setting information for branch from your defaults"

    Import-Module "$PSScriptRoot\AderantTfs.psm1" -Global

    Set-LocalDirectory
    Set-BinariesDirectory
    $ShellContext.BranchLocalDirectory = $global:BranchLocalDirectory
    $ShellContext.BranchName = ResolveBranchName $global:BranchLocalDirectory
    $ShellContext.BranchServerDirectory = (GetDefaultValue "DropRootUNCPath").ToLower()
    $ShellContext.BranchModulesDirectory = Join-Path -Path $global:BranchLocalDirectory -ChildPath "\Modules"
}

<#
Set-ExpertSourcePath is called on startup and SwitchBranchTo.  It sets $ShellContext.BranchExpertVersion and $ShellContext.BranchServerDirectory.
Pre-8.0 environments still use the old folder structure where everything was in the binaries folder, so BranchExpertSourceDirectory is set
according to the setting in the ExpertManifest.xml file.
#>
function Set-ExpertSourcePath {
    if (Test-Path $ShellContext.ProductManifestPath) {
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
}

function Set-ScriptPaths {
    Write-Debug -Message "BranchModulesDirectory: $($ShellContext.BranchModulesDirectory)"

    if ([System.IO.File]::Exists($ShellContext.BranchModulesDirectory + "\ExpertManifest.xml")) {
        [string]$root = Resolve-Path "$PSScriptRoot\..\..\..\"

        $ShellContext.BuildScriptsDirectory = Join-Path -Path $root -ChildPath "Src\Build"
        $ShellContext.PackageScriptsDirectory = Join-Path -Path $root -ChildPath "Src\Package"
        $ShellContext.ProductManifestPath = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "ExpertManifest.xml"
    } else {
        $ShellContext.BuildScriptsDirectory = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "\Build.Infrastructure\Src\Build"
        $ShellContext.PackageScriptsDirectory = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "\Build.Infrastructure\Src\Package"
        $ShellContext.ProductManifestPath = Join-Path -Path $ShellContext.PackageScriptsDirectory -ChildPath "\ExpertManifest.xml"
    }
}

<#
Find the Install Location of programs
#>

function Find-InstallLocation ($programName) {
    $registryItem = Get-ChildItem HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall | Get-ItemProperty | ?{ $_.PSObject.Properties.Name -match 'DisplayName' -and -not [string]::IsNullOrEmpty($_.DisplayName) -and $_.DisplayName -like $programName }

    if($registryItem){
        return $registryItem.InstallLocation
    } else {
        return $null;
    }
}

<#
Set Expert specific variables
#>

function Set-ExpertVariables {
    $global:InstallPath = Find-InstallLocation -programName 'Expert Deployment Manager'

    if($global:InstallPath){
        $ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { Join-Path -Path $global:InstallPath -ChildPath 'DeploymentEngine.exe' }
        $ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { Join-Path -Path $global:InstallPath -ChildPath 'DeploymentManager.exe' }
    } else {
        $pathToDeploymentEngine = 'C:\AderantExpert\Install\DeploymentEngine.exe'
        $pathToDeploymentManager = 'C:\AderantExpert\Install\DeploymentManager.exe'

        $ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { $pathToDeploymentEngine }
        $ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { $pathToDeploymentManager }

        if (-not (Test-Path $ShellContext.DeploymentManager)) {
            Write-Warning "Please ensure that the DeploymentManager.exe is located at: $($pathToDeploymentManager)"
        }
    }
}

<#
    Initialize functions from Build-Libraries.ps1
#>
function Initialize-BuildLibraries {
    . ($ShellContext.BuildScriptsDirectory + "\Build-Libraries.ps1")
}



function Set-CurrentModule {
    param (
        [Parameter(Mandatory=$false)][string]$name
    )

    begin {
        if ([string]::IsNullOrWhiteSpace($name)) {
            $name = Convert-Path -Path '.'
        }
    }

    process {
        if ($name.StartsWith(".")) { # "." or ".\Framework\"
            $name = Resolve-Path $name
        }

        if ($null -ne $currentModuleFeature) {
            if (Get-Module | Where-Object -Property Name -eq $currentModuleFeature.Name) {
                Remove-Module $currentModuleFeature
            }

            $currentModuleFeature = $null
        }

        if ([System.IO.Path]::IsPathRooted($name)) {
            $global:ShellContext.CurrentModulePath = $name
            $global:ShellContext.CurrentModuleName = ([System.IO.DirectoryInfo]::new($global:ShellContext.CurrentModulePath)).Name
            Write-Debug "CurrentModuleName set to: $($global:ShellContext.CurrentModuleName)"

            Write-Debug "Setting repository: $name"
            Import-Module $PSScriptRoot\Git.psm1 -Global

            Set-Location $ShellContext.CurrentModulePath

            if ([string]::IsNullOrWhiteSpace($global:BranchConfigPath)) {
                [string]$buildDirectory = Join-Path -Path $ShellContext.CurrentModulePath -ChildPath 'Build'
                If (Test-Path -Path $buildDirectory) {
                    [string]$manifest = Join-Path -Path $buildDirectory -ChildPath 'ExpertManifest.xml'
                    [string]$config = Join-Path -Path $buildDirectory -ChildPath 'BranchConfig.xml'

                    if ((Test-Path -Path $manifest) -and (Test-Path -Path $config)) {
                        $global:BranchConfigPath = $buildDirectory
                    }
                }
            }

            if (IsGitRepository $ShellContext.CurrentModulePath) {
                SetRepository $ShellContext.CurrentModulePath
                Enable-GitPrompt
                return
            } elseif (IsGitRepository ([System.IO.DirectoryInfo]::new($ShellContext.CurrentModulePath).Parent.FullName)) {
                Enable-GitPrompt
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
}

function IsGitRepository {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$path
    )

    process {
        $path = (Resolve-Path -Path $path).Path

        if ([System.IO.path]::GetPathRoot($path) -eq $path) {
            return $false
        }

        return @(Get-ChildItem -Path $path -Filter ".git" -Recurse -Depth 1 -Attributes Hidden -Directory).Length -gt 0
    }
}

function SetRepository([string]$path) {
    $ShellContext.IsGitRepository = $true

    [string]$currentModuleBuildDirectory = "$path\Build"

    if (Test-Path $currentModuleBuildDirectory) {
        # We only allow 1 feature*.psm1 file in the \build folder.
        $featureModule = Get-ChildItem -Path $currentModuleBuildDirectory -File -Filter 'Feature*.psm1' | Select-object -First 1

        if($featureModule -and $featureModule.FullName) {
            ImportFeatureModule $featureModule.FullName
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
    Write-Host "Local Information"
    Write-Host "-----------------------------"
    Write-Host "Path :" $global:BranchLocalDirectory
    Write-Host "-----------------------------"
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

function Output-VSIXLog {
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
        [switch]$Initialize
    )

    process {
        if ($ShellContext.IsTfvcModuleEnabled) {
            if ($Initialize.IsPresent) {
                Set-BranchPaths
            }

            Set-ScriptPaths
            Set-ExpertSourcePath
            Set-ExpertVariables
        }

        Initialize-BuildLibraries
        Set-VisualStudioVersion

        OutputEnvironmentDetails

        if ($Initialize.IsPresent) {
            # Setup PowerShell script unit test environment
            Install-Pester
        }
    }
}

function Set-VisualStudioVersion() {
    #$job = Start-Job -Name "Set-VisualStudioVersion" -ScriptBlock {
    #    Param($path)
        $file = [System.IO.Path]::Combine($ShellContext.BuildScriptsDirectory, "vsvars.ps1")
        . $file
    #} -ArgumentList $ShellContext.BuildScriptsDirectory

    #$jobEvent = Register-ObjectEvent $job StateChanged -Action {
    #    $jobEvent | Unregister-Event
    #
    #    $data = Receive-Job $sender.Name
    #
    #    foreach ($item in $data.GetEnumerator()) {
    #        Set-Item -Force -Path "ENV:\$($item.Key)" -Value $item.Value
    #    }
    #
    #    $Host.UI.RawUI.WindowTitle = "Visual Studio environment ready"
    #}
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
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$ModuleName,
        [switch]$getDependencies,
        [switch]$getLatest,
        [switch]$code
    )

    begin {
        [string]$devenv = "devenv"
    }

    process {
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
        $parser = [Aderant.Build.AutoCompletionParser]::new($line, $lastword, $aliases)

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

            $parser.GetModuleMatches($wordToComplete, $ShellContext.CurrentModulePath, $ShellContext.BranchModulesDirectory, $ShellContext.ProductManifestPath) | Get-Unique | ForEach-Object {
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

# Add module auto completion scenarios
Add-ModuleExpansionParameter -CommandName "Set-CurrentModule" -ParameterName "name"
Add-ModuleExpansionParameter -CommandName "Branch-Module" -ParameterName "moduleName"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "getLocal"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "exclude"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModules" -ParameterName "skipUntil"
Add-ModuleExpansionParameter -CommandName "Build-ExpertModulesOnServer" -ParameterName "workflowModuleNames"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesForCurrentModule" -ParameterName "onlyUpdated"
Add-ModuleExpansionParameter -CommandName "Get-DependenciesForCurrentModule" -ParameterName "onlyUpdated"
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
    Function global:Prompt {
        # set the window title to the branch name
        $Host.UI.RawUI.WindowTitle = "PS - [" + $ShellContext.CurrentModuleName + "] on branch [" + $ShellContext.BranchName + "]"

        Write-Host("")
        Write-Host ("Module [") -nonewline
        Write-Host ($ShellContext.CurrentModuleName) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] at [") -nonewline
        Write-Host ($ShellContext.CurrentModulePath) -nonewline -foregroundcolor DarkCyan
        Write-Host ("] on branch [") -nonewline
        Write-Host ($ShellContext.BranchName) -nonewline -foregroundcolor Green
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
    Function global:Prompt {
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
    [PSCustomObject]@{ function = 'Build-ExpertModules'; alias = $null; },
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
    [PSCustomObject]@{ function = 'Get-ExpertModulesInChangeset'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-Database'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-DatabaseServer'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-Latest'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-LocalDependenciesForCurrentModule'; alias = 'gdl'; },
    [PSCustomObject]@{ function = 'Get-Product'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-ProductBuild'; alias = 'gpb'; },
    [PSCustomObject]@{ function = 'Git-Merge'; alias = $null; },
    [PSCustomObject]@{ function = 'Help'; alias = $null; },
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

#Check-Vsix "NUnit3.TestAdapter" "0da0f6bd-9bb6-4ae3-87a8-537788622f2d" "NUnit.NUnit3TestAdapter"
#Check-Vsix "Aderant.DeveloperTools" "b36002e4-cf03-4ed9-9f5c-bf15991e15e4"

#$ShellContext.LastVsixCheckCommit("", "LastVsixCheckCommit", $ShellContext.CurrentCommit) | Out-Null

Set-Environment -Initialize

Write-Host ""
Write-Host "Type " -NoNewLine
Write-Host '"help"' -ForegroundColor Green -NoNewLine
Write-Host " for a command list." -NoNewLine
Write-Host ""