#Requires -RunAsAdministrator
Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Import extensibility functions.
$imports = @(
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Build\Functions') -Filter '*.ps1'),
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Functions') -Filter '*.ps1'),
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Modules') -Filter '*.psm1')
)

foreach ($directory in $imports) {
    foreach ($file in $directory) {
        if ($file.Name -eq 'Initialize-BuildEnvironment.ps1') {
            continue
        }

        if ($file.Extension -eq '.ps1') {
            . $file.FullName
            continue
        }

        if ($file.Extension -eq '.psm1') {
            if ($DebugPreference -eq 'SilentlyContinue') {
                Import-Module $file.FullName -DisableNameChecking
            } else {
                Import-Module $file.FullName
            }
        }
    }
}

$script:ShellContext = $null

function Initialize-Module {
    . "$PSScriptRoot\ShellContext.ps1"

    $script:ShellContext = [ShellContext]::new()
    $MyInvocation.MyCommand.Module.PrivateData.ShellContext = $script:ShellContext

    [string]$formatDataFile = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..\..\Build\Functions\Formats\SourceTreeMetadata.format.ps1xml'))

    $updateFormatData = {
        Update-FormatData -PrependPath $formatDataFile
    }

    DoActionIfNeeded -action $updateFormatData -file $formatDataFile
}

Initialize-Module

[string]$global:BranchConfigPath = [string]::Empty
[string]$ShellContext.BranchName = [string]::Empty
[string]$script:BranchLocalDirectory = [string]::Empty
[string]$global:BranchServerDirectory = [string]::Empty
[string]$global:BranchModulesDirectory = [string]::Empty
[string]$global:BranchBinariesDirectory = [string]::Empty
[string]$global:BranchExpertSourceDirectory = [string]::Empty
[string]$global:PackageScriptsDirectory = [string]::Empty
[string]$global:ProductManifestPath = [string]::Empty
[PSModuleInfo[]]$script:loadedModuleFeatures = $null

[string[]]$titles = @(
    'Reticulating Splines',
    'Attempting to Lock Back-Buffer',
    'Calculating Inverse Probability Matrices',
    'Compounding Inert Tessellations',
    'Decomposing Singular Values',
    'Dicing Models',
    'Extracting Resources',
    'Obfuscating Quigley Matrix',
    'Fabricating Imaginary Infrastructure',
    'Activating Deviance Threshold',
    'Simulating Program Execution',
    'Abstracting Loading Procedures',
    'Unfolding Helix Packet',
    'Iterating Chaos Array',
    'Calculating Native Restlessness',
    'Filling in the Blanks',
    'Mitigating Time-Stream Discontinuities',
    'Blurring Reality Lines',
    'Reversing the Polarity of the Neutron Flow',
    'Dropping Expert Database',
    'Formatting C:\',
    'Replacing Coffee Machine',
    'Duplicating Offline Cache',
    'Replacing Headlight Fluid',
    'Dialing Roper Hotline'
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

        if ($propertyName -eq 'DevBranchFolder') {
            Clear-Host
            [string]$path = SetPathVariable -question 'What would you like your default path set to when you open PowerShell? e.g. C:\Source' -propertyName $propertyName

            if (-not [System.IO.Directory]::Exists($path)) {
                New-Item -ItemType 'Directory' -Path $path -Force
            }

            return $path
        }

        # Environment variable was not set, default it.
        [Environment]::SetEnvironmentVariable("Expert$propertyName", $defaultValue, "User")
        return $defaultValue
    }
}

<#
    Default Path and Binaries Directory information.
#>
function Set-DefaultPaths {
    Write-Debug 'Setting information from your defaults.'

    $script:BranchLocalDirectory = (GetDefaultValue -propertyName 'DevBranchFolder')
    $ShellContext.BranchBinariesDirectory = 'C:\AderantExpert\Binaries'
    $ShellContext.BranchLocalDirectory = $script:BranchLocalDirectory

    if ($ShellContext.IsTfvcModuleEnabled) {
        $ShellContext.BranchModulesDirectory = (Join-Path -Path $script:BranchLocalDirectory -ChildPath "\Modules")
    } else {
        $ShellContext.BranchModulesDirectory = $script:BranchLocalDirectory
    }
}

<#
Set-ExpertSourcePath is called on startup.  It sets $ShellContext.BranchExpertVersion
Pre-8.0 environments still use the old folder structure where everything was in the binaries folder, so BranchExpertSourceDirectory is set
according to the setting in the ExpertManifest.xml file.
#>
function Set-ExpertSourcePath {
    if (Test-Path $ShellContext.ProductManifestPath) {
        [xml]$manifest = Get-Content $ShellContext.ProductManifestPath
        [string]$branchExpertVersion = $manifest.ProductManifest.ExpertVersion

        if ($branchExpertVersion.StartsWith("8")) {
            $global:BranchExpertSourceDirectory = Join-Path -Path $script:BranchLocalDirectory -ChildPath "\Binaries\ExpertSource"

            if (-not (Test-Path -Path $global:BranchExpertSourceDirectory)) {
                [System.IO.Directory]::CreateDirectory($global:BranchExpertSourceDirectory) | Out-Null
            }
        } else {
            $global:BranchExpertSourceDirectory = $ShellContext.BranchBinariesDirectory
        }
    }
}

function Find-InstallLocation ($programName) {
    <#
    .SYNOPSIS
        Find the Install Location of programs.
    #>
        $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')
        foreach ($installKey in $key.GetSubKeyNames()) {
            $productKey = $key.OpenSubKey($installKey)
    
            foreach ($productEntry in $productKey.GetValueNames()) {
                if ($productEntry -eq "DisplayName") {
                    if ($productKey.GetValue($productEntry) -eq "Expert Deployment Manager") {
                        return $productKey.GetValue("InstallLocation")
                    }
                }
            }
        }
    }

function Set-ExpertVariables {
    <#
    .SYNOPSIS
        Set Expert specific variables.
    #>
    [string]$script:installPath = Find-InstallLocation -programName 'Expert Deployment Manager'

    if ($script:installPath) {
        $script:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { Join-Path -Path $script:installPath -ChildPath 'DeploymentEngine.exe' }
        $script:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { Join-Path -Path $script:installPath -ChildPath 'DeploymentManager.exe' }
    } else {
        $pathToDeploymentEngine = 'C:\AderantExpert\Install\DeploymentEngine.exe'
        $pathToDeploymentManager = 'C:\AderantExpert\Install\DeploymentManager.exe'

        $script:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { $pathToDeploymentEngine }
        $script:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { $pathToDeploymentManager }

        if (-not (Test-Path $script:ShellContext.DeploymentManager)) {
            Write-Warning "Please ensure that DeploymentManager.exe is located at: $($pathToDeploymentManager)"
        }
    }
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

        if ($null -ne $script:loadedModuleFeatures) {
            foreach ($currentModuleFeature in $script:loadedModuleFeatures) {
                if (Get-Module | Where-Object -Property Name -eq $currentModuleFeature.Name) {
                    Remove-Module $currentModuleFeature
                }
            }
            $script:loadedModuleFeatures = $null
        }

        $ShellContext.IsGitRepository = $true

        if ([System.IO.Path]::IsPathRooted($name)) {
            $script:ShellContext.CurrentModulePath = $name
            $script:ShellContext.CurrentModuleName = ([System.IO.DirectoryInfo]::new($script:ShellContext.CurrentModulePath)).Name
            Write-Debug "CurrentModuleName set to: $($script:ShellContext.CurrentModuleName)"

            Write-Debug "Setting repository: $name"

            if (-not (Get-Module -Name 'Git')) {
                Import-Module "$PSScriptRoot\Git.psm1" -Global
            }

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

            if ((IsGitRepository $ShellContext.CurrentModulePath) -or (IsGitRepository ([System.IO.DirectoryInfo]::new($ShellContext.CurrentModulePath).Parent.FullName))) {
                ImportFeatureModules $ShellContext.CurrentModulePath
                Enable-GitPrompt
            } else {
                $ShellContext.IsGitRepository = $false
            }

        } else {
            $ShellContext.CurrentModuleName = $name

            Write-Debug "Current module [$ShellContext:CurrentModuleName]"
            $ShellContext.CurrentModulePath = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath $ShellContext.CurrentModuleName

            Set-Location $ShellContext.CurrentModulePath

            $ShellContext.IsGitRepository = $false
        }

        if ((Test-Path $ShellContext.CurrentModulePath) -eq $false) {
            Write-Warning "the module [$($ShellContext.CurrentModuleName)] does not exist, please check the spelling."
            $ShellContext.CurrentModuleName = ""
            $ShellContext.CurrentModulePath = ""
            return
        }

        Write-Debug "Current module path [$($ShellContext.CurrentModulePath)]"
        $ShellContext.CurrentModuleBuildPath = Join-Path -Path $ShellContext.CurrentModulePath -ChildPath "Build"
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

function ImportFeatureModules([string]$path) {
    [string]$currentModuleBuildDirectory = "$path\Build"

    if (Test-Path $currentModuleBuildDirectory) {
        $featureModules = Get-ChildItem -Path $currentModuleBuildDirectory -File -Filter 'Feature*.psm1'

        if ($null -eq $featureModules) {
            return
        }

        foreach ($featureModule in $featureModules) {
            if($featureModule -and $featureModule.FullName) {
                ImportFeatureModule $featureModule.FullName
            }
        }

    }
}

function ImportFeatureModule([string]$featureModule) {
    Import-Module -Name $featureModule -Scope Global -WarningAction SilentlyContinue
    $currentModuleFeature = Get-Module | Where-Object -Property Path -eq $featureModule
    if ($null -eq $script:loadedModuleFeatures) {
        $script:loadedModuleFeatures = @()
    }
    $script:loadedModuleFeatures += $currentModuleFeature

    Write-Host "`r`nImported module: $($currentModuleFeature.Name)" -ForegroundColor Cyan
    Get-Command -Module $currentModuleFeature.Name
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

    # Take VSIX out of local source directory
    if ($local) {
        Write-Host "Attempting to install $($info.ProductManifestName) from local source directory."
        $vsixFile = [System.IO.Path]::Combine($ShellContext.BranchServerDirectory, $info.ExtensionFile)
    } else { # Take VSIX from drop folder
        Write-Host "Attempting to install $($info.ProductManifestName) from drop folder."
        $localInstallDirectory = [System.IO.Path]::Combine($script:BranchLocalDirectory, $info.ProductManifestName + ".Install")

        [xml]$manifest = Get-Content $ShellContext.ProductManifestPath
        [System.Xml.XmlNode]$module = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object { $_.Name.Contains($info.ProductManifestName)}

        $dropPathVSIX = (GetPathToBinaries $module $ShellContext.BranchServerDirectory)

        if (-not (Test-Path $localInstallDirectory)) {
            New-Item $localInstallDirectory -ItemType directory
        } else {
            Remove-Item -Path "$localInstallDirectory\*" -Recurse -Force 
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
        if ($Initialize.IsPresent) {
            Set-DefaultPaths
            Add-GitCommandIntercept
        }

        $ShellContext.PackageScriptsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($ShellContext.BuildScriptsDirectory, '..\Package'))
        $ShellContext.ProductManifestPath = Join-Path -Path $ShellContext.BranchModulesDirectory -ChildPath "\Build\ExpertManifest.xml"

        Set-ExpertSourcePath
        Set-ExpertVariables
        Set-VisualStudioVersion
    }
}

function Set-VisualStudioVersion {
    $job = Start-JobInProcess -Name "SetVisualStudioVersion" -ScriptBlock {
        Param($path)
            $file = [System.IO.Path]::Combine($path, "vsvars.ps1")
            . $file
    } -ArgumentList $ShellContext.BuildScriptsDirectory

   $null = Register-ObjectEvent $job -EventName StateChanged -Action {
       if ($EventArgs.JobStateInfo.State -ne [System.Management.Automation.JobState]::Completed) {
           Write-Host ("Task has failed: " + $sender.ChildJobs[0].JobStateInfo.Reason.Message) -ForegroundColor Red
       }

       $data = Receive-Job $Sender.Id

       foreach ($item in $data.GetEnumerator()) {
           [System.Environment]::SetEnvironmentVariable($item.Key, $item.Value, [System.EnvironmentVariableTarget]::Process)
       }

       $millisecondsTaken = [int]($Sender.PSEndTime - $Sender.PSBeginTime).TotalMilliseconds
       $Host.UI.RawUI.WindowTitle = "Visual Studio environment ready ($millisecondsTaken ms) $($Env:DevEnvDir)"

       $Sender | Remove-Job -Force

       $EventSubscriber | Unregister-Event -Force
       $EventSubscriber.Action | Remove-Job -Force
   }
}

# sets a value in the global defaults storage
function global:SetDefaultValue {
    param (
        [string]$propertyName,
        [string]$defaultValue
    )

    [Environment]::SetEnvironmentVariable("Expert$propertyName", $defaultValue, "User")
}

function Open-ModuleSolution {
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
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$ModuleName,
        [switch]$getDependencies,
        [switch]$getLatest,
        [switch]$code
    )

    begin {
        [string]$devenv = 'devenv'

        function Get-CurrentModule {
            return Get-ExpertModule -ModuleName $ShellContext.CurrentModuleName
        }
    }

    process {
        $prevModule = $null

        if (($getDependencies) -and -not [string]::IsNullOrEmpty($ModuleName)) {
            if (-not [string]::IsNullOrEmpty($ShellContext.CurrentModuleName)) {
                $prevModule = Get-CurrentModule
            }

            Set-CurrentModule $ModuleName
        }

        [string]$expertSuiteRootPath = [string]::Empty
        [string]$nonExpertSuiteRootPath = [string]::Empty

        if (-not [string]::IsNullOrWhiteSpace($ModuleName)) {
            $expertSuiteRootPath = Join-Path $script:BranchLocalDirectory "ExpertSuite\$ModuleName"
            $nonExpertSuiteRootPath = Join-Path $script:BranchLocalDirectory "$ModuleName"
        } else {
            $ModuleName = $ShellContext.CurrentModuleName
            $expertSuiteRootPath = $ShellContext.CurrentModulePath
            $nonExpertSuiteRootPath = $ShellContext.CurrentModulePath
        }

        if ([string]::IsNullOrWhiteSpace($ModuleName)) {
            Write-Warning "No module specified."
            return
        }

        if ($getDependencies) {
            Write-Host "Getting Dependencies for module: $ModuleName"
            Get-Dependencies
        }

        if ($getLatest) {
            Write-Host "Getting latest source for module: $ModuleName"
            Get-Latest -ModuleName $ModuleName;
        }

        Write-Host "Opening solution for module: $ModuleName"
        $expertSuiteModuleSolutionPath = Join-Path $expertSuiteRootPath "$ModuleName.sln"       
        $devenvPath = ""
        $codePath = ""

        if (Test-Path $expertSuiteModuleSolutionPath) {
            $devenvPath = $expertSuiteModuleSolutionPath
            $codePath = $expertSuiteRootPath
        } else {
            $nonExpertSuiteModuleSolutionPath = Join-Path $nonExpertSuiteRootPath "$ModuleName.sln"
            if (Test-Path $nonExpertSuiteModuleSolutionPath) {
                $devenvPath = $nonExpertSuiteModuleSolutionPath
                $codePath = $nonExpertSuiteRootPath
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($devenvPath)) {
            if ($code) {
                if (Get-Command code -errorAction SilentlyContinue) {
                    Invoke-Expression "code $codePath"
                } else {
                    Write-Host "VS Code could not be found (code)"
                }
            } else {
                Invoke-Expression "& '$devenv' $devenvPath"
            }
        } else {
            [System.IO.FileSystemInfo[]]$candidates = (Get-ChildItem -Filter *.sln -file  | Where-Object {$_.Name -NotMatch ".custom.sln"})
            if ($null -ne $candidates -and $candidates.Count -gt 0) {
                $expertSuiteModuleSolutionPath = Join-Path $expertSuiteRootPath $candidates[0]
                $codePath = $candidates[0].DirectoryName
                if ($code) {
                    if (Get-Command code -errorAction SilentlyContinue) {
                        Invoke-Expression "code $codePath"
                    } else {
                        Write-Host "VS Code could not be found (code)"
                    }
                } else {
                    Invoke-Expression "& '$devenv' $expertSuiteModuleSolutionPath"
                }
            } else {
                "There is no solution file at $expertSuiteModuleSolutionPath"
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

function Add-ModuleExpansionParameter {
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

function Add-BranchExpansionParameter([string]$CommandName, [string]$ParameterName, [switch]$IsDefault) {
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
        Add-BranchExpansionParameter -CommandName Test -ParameterName parameter -IsDefault

        Will add tab expansion of branch names on the newBranch command where the current parameter is the newBranch parameter and this is also the first (default) parameter
    #>
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
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependencies" -ParameterName "SourceModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModuleDependsOn" -ParameterName "TargetModuleName"
Add-ModuleExpansionParameter -CommandName "Get-DownstreamExpertModules" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModule" -ParameterName "ModuleName"
Add-ModuleExpansionParameter -CommandName "Get-ExpertModules" -ParameterName "ModuleNames"
Add-ModuleExpansionParameter –CommandName "Open-ModuleSolution" –ParameterName "ModuleName"
Add-ModuleExpansionParameter –CommandName "Start-Redeployment" –ParameterName "CopyBinariesFrom"
Add-ModuleExpansionParameter -CommandName "Copy-BinToEnvironment" -ParameterName "ModuleNames"
Add-ModuleExpansionParameter -CommandName "CleanupIISCache" -ParameterName "moduleNames"
Add-ModuleExpansionParameter –CommandName "Get-WebDependencies" –ParameterName "ModuleName"

<#
.Synopsis
    Disables the Expert prompt with branch and module information
.Description
    Disable-ExpertPrompt
#>
function Disable-ExpertPrompt() {
    # Copy the current prompt function so we can fall back to it if we're not supposed to handle a command.
    Function global:Prompt {
        $(if (test-path variable:/PSDebugContext) { '[DBG]: ' }
            else { '' }) + 'PS ' + $(Get-Location) `
            + $(if ($nestedpromptlevel -ge 1) { '>>' }) + '> '
    }
}

# Export functions and variables we want external to this script.
$functionsToExport = @(
    [PSCustomObject]@{ function = 'Run-ExpertUITests'; alias = $null; },
    [PSCustomObject]@{ function = 'Run-ExpertSanityTests'; alias = 'rest'; },
    [PSCustomObject]@{ function = 'Run-ExpertVisualTests'; alias = 'revt'; },
    [PSCustomObject]@{ function = 'Build-ExpertModules'; alias = $null; },
    [PSCustomObject]@{ function = 'Build-ExpertPatch'; alias = $null; },
    [PSCustomObject]@{ function = 'Edit-ExpertOwner'; alias = $null; },
    [PSCustomObject]@{ function = 'Clear-ExpertCache'; alias = 'ccache'; },
    [PSCustomObject]@{ function = 'Copy-BinariesFromCurrentModule'; alias = 'cb'; },
    [PSCustomObject]@{ function = 'Disable-ExpertPrompt'; advanced = $true; alias = $null; },
    [PSCustomObject]@{ function = 'Generate-SystemMap'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-CurrentModule'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-Dependencies'; alias = 'gd'; },
    [PSCustomObject]@{ function = 'Get-Database'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-DatabaseServer'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-Product'; alias = $null; },
    [PSCustomObject]@{ function = 'Get-ProductBuild'; alias = 'gpb'; },
#    [PSCustomObject]@{ function = 'Install-LatestSoftwareFactory'; alias = 'usf'; },
#    [PSCustomObject]@{ function = 'Install-LatestVisualStudioExtension'; alias = $null; },
    [PSCustomObject]@{ function = 'Open-ModuleSolution'; alias = 'vs'; },
    [PSCustomObject]@{ function = 'Set-CurrentModule'; alias = 'cm'; },
    [PSCustomObject]@{ function = 'Set-Environment'; advanced = $true; alias = $null; },
    [PSCustomObject]@{ function = 'Start-dbgen'; alias = 'dbgen'; },
    [PSCustomObject]@{ function = 'Start-DeploymentEngine'; alias = 'de'; },
    [PSCustomObject]@{ function = 'Start-DeploymentManager'; alias = 'dm'; },
    [PSCustomObject]@{ function = 'Prepare-Database'; alias = 'dbprep'; },
    [PSCustomObject]@{ function = 'Update-Database'; alias = 'upd'; },
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

#Check-Vsix "NUnit3.TestAdapter" "0da0f6bd-9bb6-4ae3-87a8-537788622f2d" "NUnit.NUnit3TestAdapter"
#Check-Vsix "Aderant.DeveloperTools" "b36002e4-cf03-4ed9-9f5c-bf15991e15e4"

#$ShellContext.LastVsixCheckCommit("", "LastVsixCheckCommit", $ShellContext.CurrentCommit) | Out-Null

Set-Environment -Initialize

Write-Information -MessageData "Type:
    Get-Command -Module 'Aderant'
For a list of commands.$([System.Environment]::NewLine)"

function Test-ExpertPackageFeed {
    $p = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
    new-item -type directory $p
    Push-Location $p

$c = @'
source https://expertpackages.azurewebsites.net/v3/index.json
nuget Aderant.Build.Analyzer
'@

    Set-Content -Path "$p\paket.dependencies" $c
    & $ShellContext.PackagingTool update --verbose

    Pop-Location
}

Export-ModuleMember -Function Test-ExpertPackageFeed
Export-ModuleMember -Variable $script:ShellContext

Set-Location -Path $script:BranchLocalDirectory