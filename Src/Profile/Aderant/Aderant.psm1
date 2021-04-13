#Requires -RunAsAdministrator
Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Import extensibility functions.
$imports = @(
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Build\Functions') -Filter '*.ps1'),
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Functions') -Filter '*.ps1'),
    (Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Modules') -Filter '*.psd1')
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

        if ($file.Extension -eq '.psd1') {
            if ($DebugPreference -eq 'SilentlyContinue') {
                Import-Module $file.FullName -DisableNameChecking
            } else {
                Import-Module $file.FullName
            }
        }
    }
}

$global:ShellContext = $null

function Initialize-Module {
    . "$PSScriptRoot\ShellContext.ps1"

    $global:ShellContext = [ShellContext]::new()
    $MyInvocation.MyCommand.Module.PrivateData.ShellContext = $global:ShellContext

    [string]$formatDataFile = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..\..\Build\Functions\Formats\SourceTreeMetadata.format.ps1xml'))

    $updateFormatData = {
        Update-FormatData -PrependPath $formatDataFile
    }

    DoActionIfNeeded -action $updateFormatData -file $formatDataFile
}

Initialize-Module

[string]$global:ShellContext.BranchName = [string]::Empty
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
Expert-specific variables
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

function Set-DefaultPaths {
    <#
        Default Path and Binaries Directory information.
    #>

    Write-Debug 'Setting information from your defaults.'

    $script:BranchLocalDirectory = (GetDefaultValue -propertyName 'DevBranchFolder')
    $global:ShellContext.BranchBinariesDirectory = 'C:\AderantExpert\Binaries'
    $global:ShellContext.BranchLocalDirectory = $script:BranchLocalDirectory

    if ($global:ShellContext.IsTfvcModuleEnabled) {
        $global:ShellContext.BranchModulesDirectory = (Join-Path -Path $script:BranchLocalDirectory -ChildPath "\Modules")
    } else {
        $global:ShellContext.BranchModulesDirectory = $script:BranchLocalDirectory
    }
}

function Set-ExpertSourcePath {
    <#
    Set-ExpertSourcePath is called on startup.  It sets $global:ShellContext.BranchExpertVersion
    Pre-8.0 environments still use the old folder structure where everything was in the binaries folder, so BranchExpertSourceDirectory is set
    according to the setting in the ExpertManifest.xml file.
    #>
    if (Test-Path $global:ShellContext.ProductManifestPath) {
        [xml]$manifest = Get-Content $global:ShellContext.ProductManifestPath
        [string]$branchExpertVersion = $manifest.ProductManifest.ExpertVersion

        if ($branchExpertVersion.StartsWith("8")) {
            $global:BranchExpertSourceDirectory = Join-Path -Path $script:BranchLocalDirectory -ChildPath "\Binaries\ExpertSource"

            if (-not (Test-Path -Path $global:BranchExpertSourceDirectory)) {
                [System.IO.Directory]::CreateDirectory($global:BranchExpertSourceDirectory) | Out-Null
            }
        } else {
            $global:BranchExpertSourceDirectory = $global:ShellContext.BranchBinariesDirectory
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
        $global:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { Join-Path -Path $script:installPath -ChildPath 'DeploymentEngine.exe' }
        $global:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { Join-Path -Path $script:installPath -ChildPath 'DeploymentManager.exe' }
    } else {
        $pathToDeploymentEngine = 'C:\AderantExpert\Install\DeploymentEngine.exe'
        $pathToDeploymentManager = 'C:\AderantExpert\Install\DeploymentManager.exe'

        $global:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentEngine -Value { $pathToDeploymentEngine }
        $global:ShellContext | Add-Member -MemberType ScriptProperty -Name DeploymentManager -Value { $pathToDeploymentManager }

        if (-not (Test-Path $global:ShellContext.DeploymentManager)) {
            Write-Warning "Please ensure that DeploymentManager.exe is located at: $($pathToDeploymentManager)"
        }
    }
}

function global:Set-CurrentModule {
    [CmdletBinding()]
    [Alias('cm')]
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
                if (Get-Module -Name $currentModuleFeature.Name) {
                    Remove-Module $currentModuleFeature
                }
            }
            $script:loadedModuleFeatures = $null
        }

        $global:ShellContext.IsGitRepository = $true

        if ([System.IO.Path]::IsPathRooted($name)) {
            $global:ShellContext.CurrentModulePath = $name
            $global:ShellContext.CurrentModuleName = ([System.IO.DirectoryInfo]::new($global:ShellContext.CurrentModulePath)).Name
            Write-Debug "CurrentModuleName set to: $($global:ShellContext.CurrentModuleName)"

            Write-Debug "Setting repository: $name"

            Set-Location $global:ShellContext.CurrentModulePath
            Get-BuildConfigFilePaths -startingDirectory $global:ShellContext.CurrentModulePath -setPathAsGlobalVariable $true | Out-Null

            if ((IsGitRepository $global:ShellContext.CurrentModulePath) -or (IsGitRepository ([System.IO.DirectoryInfo]::new($global:ShellContext.CurrentModulePath).Parent.FullName))) {
                if (-not (Get-Module -Name 'Git')) {
                    Import-Module "$PSScriptRoot\Git.psd1"
                }

                ImportFeatureModules $global:ShellContext.CurrentModulePath
            } else {
                $global:ShellContext.IsGitRepository = $false
            }

        } else {
            $global:ShellContext.CurrentModuleName = $name

            Write-Debug "Current module [$global:ShellContext:CurrentModuleName]"
            $global:ShellContext.CurrentModulePath = Join-Path -Path $global:ShellContext.BranchModulesDirectory -ChildPath $global:ShellContext.CurrentModuleName

            Set-Location $global:ShellContext.CurrentModulePath

            $global:ShellContext.IsGitRepository = $false
        }

        if ((Test-Path $global:ShellContext.CurrentModulePath) -eq $false) {
            Write-Warning "the module [$($global:ShellContext.CurrentModuleName)] does not exist, please check the spelling."
            $global:ShellContext.CurrentModuleName = ""
            $global:ShellContext.CurrentModulePath = ""
            return
        }

        Write-Debug "Current module path [$($global:ShellContext.CurrentModulePath)]"
        $global:ShellContext.CurrentModuleBuildPath = Join-Path -Path $global:ShellContext.CurrentModulePath -ChildPath "Build"
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
    $currentModuleFeature = Get-Module -All | Where-Object -Property Path -eq $featureModule
    if ($null -eq $script:loadedModuleFeatures) {
        $script:loadedModuleFeatures = @()
    }
    $script:loadedModuleFeatures += $currentModuleFeature

    Write-Host "`r`nImported module: $($currentModuleFeature.Name)" -ForegroundColor Cyan
    Get-Command -Module $currentModuleFeature.Name
}

function Install-LatestSoftwareFactory([switch]$local) {
    <#
    .Synopsis
        Installs the latest version of the Software Factory
    .Description
        Will uninstall the previous vsix and then install the latest version from the drop location
    #>

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

function Install-LatestVisualStudioExtension {
    <#
    .Synopsis
        Installs the latest version of the given module
    .Description
        Will uninstall the previous vsix and then install the latest version from the drop location
    .Example
        Install-LatestVisualStudioExtension SDK.Database
        Will install the latest version of the SDK.Database project
    #>

    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [ValidateSet('SDK.Database', 'Libraries.SoftwareFactory')]
        [string]$module,
        [switch]$local
    ) 

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
    begin {
        function OutputVSIXLog {
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
    }

    process {
        # Uninstall the extension
        Write-Host "Uninstalling $($installDetails.ProductManifestName)..."
        $vsix = "VSIXInstaller.exe"
        Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($info.ExtensionName)" -Wait -PassThru | Out-Null

        # Take VSIX out of local source directory
        if ($local) {
            Write-Host "Attempting to install $($info.ProductManifestName) from local source directory."
            $vsixFile = [System.IO.Path]::Combine($global:ShellContext.BranchServerDirectory, $info.ExtensionFile)
        } else { # Take VSIX from drop folder
            Write-Host "Attempting to install $($info.ProductManifestName) from drop folder."
            $localInstallDirectory = [System.IO.Path]::Combine($script:BranchLocalDirectory, $info.ProductManifestName + ".Install")

            [xml]$manifest = Get-Content $global:ShellContext.ProductManifestPath
            [System.Xml.XmlNode]$module = $manifest.ProductManifest.Modules.SelectNodes("Module") | Where-Object { $_.Name.Contains($info.ProductManifestName)}

            $dropPathVSIX = (GetPathToBinaries $module $global:ShellContext.BranchServerDirectory)

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
            $errorsOccurred = OutputVSIXLog

            if (-not $errorsOccurred) {
                Write-Host "Updated $($info.ProductManifestPathName). Restart Visual Studio for the changes to take effect."
            } else {
                Write-Host ""
                $displayName = $info.ExtensionDisplayName
                Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'TOOLS -> Exensions and Updates' check if there is the '$displayName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try this command again."
            }
        }
    }
}

function Set-Environment {
    <#
    .Synopsis
        Sets up visual studio environment, called from Profile.ps1 when starting PS.
    .Description
        Sets up visual studio environment, called from Profile.ps1 when starting PS.
    .PARAMETER initialize
        Sets branch paths and installs Pester.
    #>
    param (
        [switch]$Initialize
    )

    process {
        if ($Initialize.IsPresent) {
            Set-DefaultPaths
            Add-GitCommandIntercept
        }

        $global:ShellContext.PackageScriptsDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($global:ShellContext.BuildScriptsDirectory, '..\Package'))
        $global:ShellContext.ProductManifestPath = Join-Path -Path $global:ShellContext.BranchModulesDirectory -ChildPath "\Build\ExpertManifest.xml"

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
    } -ArgumentList $global:ShellContext.BuildScriptsDirectory

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

function global:Open-ModuleSolution {
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
    [Alias('vs')]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$ModuleName,
        [switch]$getDependencies,
        [switch]$getLatest,
        [switch]$code
    )

    begin {
        [string]$devenv = 'devenv'

        function Get-CurrentModule {
            return Get-ExpertModule -ModuleName $global:ShellContext.CurrentModuleName
        }
    }

    process {
        $prevModule = $null

        if (($getDependencies) -and -not [string]::IsNullOrEmpty($ModuleName)) {
            if (-not [string]::IsNullOrEmpty($global:ShellContext.CurrentModuleName)) {
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
            $ModuleName = $global:ShellContext.CurrentModuleName
            $expertSuiteRootPath = $global:ShellContext.CurrentModulePath
            $nonExpertSuiteRootPath = $global:ShellContext.CurrentModulePath
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
        $devenvPath = [string]::Empty
        $codePath = [string]::Empty

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
        try {
            foreach ($tabExpansionParm in $global:expertTabBranchExpansions) {
                if ($parser.IsAutoCompletionForParameter($tabExpansionParm.CommandName.ToString(), $tabExpansionParm.ParameterName.ToString(), $tabExpansionParm.IsDefault.IsPresent)) {
                    Get-ExpertBranches $lastword | Get-Unique
                }
            }
        } catch {
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
            $parser.GetModuleMatches($wordToComplete, $global:ShellContext.CurrentModulePath, $global:ShellContext.BranchModulesDirectory, $global:ShellContext.ProductManifestPath) | Get-Unique | ForEach-Object {
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

function Disable-ExpertPrompt {
    <#
    .Synopsis
        Disables the Expert prompt with branch and module information
    .Description
        Disable-ExpertPrompt
    #>

    # Copy the current prompt function so we can fall back to it if we're not supposed to handle a command.
    function global:Prompt {
        $(if (test-path variable:/PSDebugContext) { '[DBG]: ' }
            else { '' }) + 'PS ' + $(Get-Location) `
            + $(if ($nestedpromptlevel -ge 1) { '>>' }) + '> '
    }
}

# Paths
Export-ModuleMember -variable CurrentModuleName
Export-ModuleMember -variable BranchServerDirectory
Export-ModuleMember -variable BranchLocalDirectory
Export-ModuleMember -variable CurrentModulePath
Export-ModuleMember -variable BranchBinariesDirectory
Export-ModuleMember -variable BranchName
Export-ModuleMember -variable BranchModulesDirectory
Export-ModuleMember -variable ProductManifestPath

Set-Environment -Initialize

Write-Information -MessageData "Type:
    Get-Command -Module 'Aderant'
For a list of commands.$([System.Environment]::NewLine)"

Set-Location -Path $script:BranchLocalDirectory