$InformationPreference = 'Continue'
$titleEventSource = "ProgressChanged"

[string]$indent1 = "  "
[string]$indent2 = "        "

function Get-Branch {
    [OutputType([String])]
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$root
    )

    begin {
        [string]$rspFile = [System.IO.Path]::Combine($root, "Build\TFSBuild.rsp")
        [string]$branch = 'master'
    }

    process {
        if (Test-Path -Path $rspFile) {
            [string[]]$content = Get-Content -Path $rspFile

            [string[]]$variable = $content | Where-Object { $_ -match '/p:OriginBranch=' }

            if ($null -ne $variable -and $variable.Length -gt 0) {
                return $branch = $variable[0].Split('=')[1]
            }
        }

        return $branch
    }
}

function Get-BuildManifest {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$manifest,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$branch
    )

    process {
        return (Invoke-WebRequest -Uri "http://tfs:8080/tfs/ADERANT/44f228f7-b636-4bd3-99ee-eb2f1570d768/316b9ba9-3a49-47b2-992e-a9a2a2835b3f/_api/_versioncontrol/itemContent?repositoryId=e6138670-7236-4e80-aa7c-6417eea253f5&path=%2FBuild%2F$($manifest)&version=GB$($branch)&contentOnly=false&__v=5" -UseBasicParsing -UseDefaultCredentials).Content
    }
}

function Get-BuildDirectory {
    if (-not [string]::IsNullOrWhiteSpace($global:BranchConfigPath)) {
        If (Test-Path -Path $global:BranchConfigPath) {
            [string]$manifest = Join-Path -Path $global:BranchConfigPath -ChildPath 'ExpertManifest.xml'
            [string]$config = Join-Path -Path $global:BranchConfigPath -ChildPath 'BranchConfig.xml'

            if ((Test-Path -Path $manifest) -and (Test-Path -Path $config)) {
                return
            }
        }
    }

    $global:BranchConfigPath = Read-Host -Prompt 'Supply a valid path to the ExpertManifest.xml and BranchConfig.xml files'
    Get-BuildDirectory
}

function ApplyBranchConfig($context, [string]$root, [switch]$EnableConfigDownload) {
    $configPath = [System.IO.Path]::Combine($root, "Build\BranchConfig.xml")

    [xml]$config = $null
    if (-not (Test-Path -Path $configPath)) {
        if (-not $EnableConfigDownload.IsPresent) {
            Get-BuildDirectory
            $config = Get-Content -Raw -LiteralPath (Join-Path -Path $global:BranchConfigPath -ChildPath "BranchConfig.xml")
        } else {
            [string]$branch = Get-Branch -root $root

            $config = Get-BuildManifest -manifest 'BranchConfig.xml' -branch $branch
        }
    } else {
        $config = Get-Content -Raw -LiteralPath $configPath
    }

    $context.DropLocationInfo.PrimaryDropLocation = $config.BranchConfig.DropLocations.PrimaryDropLocation
    $context.DropLocationInfo.BuildCacheLocation = $config.BranchConfig.DropLocations.BuildCacheLocation
    $context.DropLocationInfo.PullRequestDropLocation = $config.BranchConfig.DropLocations.PullRequestDropLocation
    $context.DropLocationInfo.XamlBuildDropLocation = $config.BranchConfig.DropLocations.XamlBuildDropLocation
}

function Get-BuildDirectory {
    if (-not [string]::IsNullOrWhiteSpace($global:BranchConfigPath)) {
        If (Test-Path -Path $global:BranchConfigPath) {
            [string]$manifest = Join-Path -Path $global:BranchConfigPath -ChildPath 'ExpertManifest.xml'
            [string]$config = Join-Path -Path $global:BranchConfigPath -ChildPath 'BranchConfig.xml'

            if ((Test-Path -Path $manifest) -and (Test-Path -Path $config)) {
                return
            }
        }
    }

    $global:BranchConfigPath = Read-Host -Prompt 'Please supply a valid path to the ExpertManifest.xml and BranchConfig.xml files'
    Get-BuildDirectory
}

function FindProductManifest($context, [string]$root, [switch]$EnableConfigDownload) {
    [string]$configPath = [System.IO.Path]::Combine($root, 'Build\ExpertManifest.xml')

    if (-not (Test-Path -Path $configPath)) {
        if (-not $EnableConfigDownload.IsPresent) {
            Get-BuildDirectory

            $context.ProductManifestPath = Join-Path -Path $global:BranchConfigPath -ChildPath 'ExpertManifest.xml'
            return
        }

        [string]$branch = Get-Branch -root $root

        $config = Get-BuildManifest -manifest 'ExpertManifest.xml' -branch $branch
        $temp = New-TemporaryFile
        $config | Out-File -File $temp.FullName -Encoding 'UTF8'
        $configPath = $temp.FullName
    }

    $context.ProductManifestPath = $configPath
}

function FindGitDir($context, [string]$searchDirectory) {
    $path = SearchForFile $searchDirectory ".git" $true
    $context.Variables["_GitDir"] = "$path\.git"

    return $path
}

function SearchForFile($startingDirectory, $fileName, $isDirectory = $false) {
    # Canonicalize our starting location
    $lookInDirectory = [System.IO.Path]::GetFullPath($startingDirectory)

    if ($isDirectory) {
        $exists = {
            param($arg)
            return [System.IO.Directory]::Exists($arg)
        }
    } else {
        $exists = {
            param($arg)
            return [System.IO.File]::Exists($arg)
        }
    }

    do {
        # Construct the path that we will use to test against
        $possibleFileDirectory = [System.IO.Path]::Combine($lookInDirectory, $fileName)

        # If we successfully locate the file in the directory that we're
        # looking in, simply return that location. Otherwise we'll
        # keep moving up the tree.
        if ($exists.Invoke($possibleFileDirectory)) {
            # We've found the file, return the directory we found it in
            return $lookInDirectory
        } else {
            # GetDirectoryName will return null when we reach the root
            # terminating our search
            $lookInDirectory = [System.IO.Path]::GetDirectoryName($lookInDirectory)
        }
    } while ($lookInDirectory -ne $null)

    # When we didn't find the location, then return an empty string
    return ""
}

function CreateToolArgumentString($context, $remainingArgs) {
    $set = [System.Collections.Generic.HashSet[string]]::new()

    & {
        $set.Add("/p:BUILD_ROOT=$($context.BuildRoot)")

        if ($context.IsDesktopBuild) {
            $set.Add("/p:IsDesktopBuild=true")
        } else {
            $set.Add("/p:IsDesktopBuild=false")
            $set.Add("/clp:PerformanceSummary")
        }

        if ($MinimalConsoleLogging.IsPresent) {
            $set.Add("/clp:verbosity=minimal")
        }

        if ($null -ne $context.BuildMetadata) {
            if ($context.BuildMetadata.DebugLoggingEnabled) {
                $set.Add("/flp:Verbosity=Diag")
            } else {
                $set.Add("/flp:Verbosity=Normal")
            }
        }

        # Don't show the logo and do not allow node reuse so all child nodes are shut down once the master node has completed build orchestration.
        $set.Add("/nologo")
        $set.Add("/nr:false")
        $set.Add("/clp:FORCENOALIGN=true")

        # Multi-core build
        if ($MaxCpuCount -gt 0) {
            $set.Add("/m:" + $MaxCpuCount.ToString())
        } else {
            $numberOfCores = (Get-CimInstance -Class win32_Processor).NumberOfCores
            $set.Add("/m:" + ([Math]::Max(1, $numberOfCores)))
        }

        if ($NoTextTemplateTransform.IsPresent) {
            $set.Add("/p:T4TransformEnabled=false")
        }

        if ($context.Switches.SkipCompile) {
            $set.Add("/p:switch-skip-compile=true")
        }

        if ($context.Switches.Clean) {
            $set.Add("/p:switch-clean=true")
        }

        if ($NoDependencyFetch.IsPresent) {
            $set.Add("/p:NoDependencyFetch=true")
        }

        if ($RunIntegrationTests.IsPresent) {
            $set.Add("/p:RunIntegrationTests=true")
        }

        if ($remainingArgs) {
            # Add pass-thru args
            [void]$set.Add([string]::Join(" ", $remainingArgs))
        }

        $set.Add("/p:PrimaryDropLocation=$($context.DropLocationInfo.PrimaryDropLocation)")
        $set.Add("/p:BuildCacheLocation=$($context.DropLocationInfo.BuildCacheLocation)")
        $set.Add("/p:PullRequestDropLocation=$($context.DropLocationInfo.PullRequestDropLocation)")
        $set.Add("/p:XamlBuildDropLocation=$($context.DropLocationInfo.XamlBuildDropLocation)")

        $set.Add("/p:ProductManifestPath=$($context.ProductManifestPath)")

        if ($PackageProduct.IsPresent) {
            $set.Add("/p:PackageProduct=$($PackageProduct.IsPresent)")
        }
    } | Out-Null
    # Out-Null stops the return value from Add being left on the pipeline

    return [string]::Join(" ", $set)
}

function GetSourceTreeMetadata($context, $repositoryPath) {
    [string]$sourceBranch = ""
    [string]$targetBranch = ""

    if (-not $context.IsDesktopBuild) {
        $metadata = $context.BuildMetadata
        $sourceBranch = $metadata.ScmBranch;

        if ($metadata.IsPullRequest) {
            $targetBranch = $metadata.PullRequest.TargetBranch

            Write-Information "Calculating changes between $sourceBranch and $targetBranch"
        }
    }

    $context.SourceTreeMetadata = Get-SourceTreeMetadata -SourceDirectory $repositoryPath -SourceBranch $sourceBranch -TargetBranch $targetBranch -IncludeLocalChanges:$context.IsDesktopBuild

    Write-Information "$indent1 Build caching info:"
    Write-Information "$indent1 New commit: $($context.SourceTreeMetadata.NewCommitDescription)"
    Write-Information "$indent1 Old commit: $($context.SourceTreeMetadata.OldCommitDescription)"
    if ($context.SourceTreeMetadata.CommonAncestor) {
        Write-Information "$indent1 CommonAncestor: $($context.SourceTreeMetadata.CommonAncestor)"
    }

    if ($context.IsDesktopBuild) {
        if ($null -ne $context.SourceTreeMetadata.Changes -and $context.SourceTreeMetadata.Changes.Count -gt 0) {
            Write-Information "$indent1 Changes..."
            $i = 0
            foreach ($change in $context.SourceTreeMetadata.Changes) {
                $i++
                if ($change.Status -ne "Untracked") {
                    Write-Information "$indent2 $($change.Path):$($change.Status)"
                }

                if ($i -gt 100) {
                    Write-Information "$indent2 ..."
                    break
                }
            }
        }
    }
}

function GetBuildStateMetadata($context) {
    $stm = $context.SourceTreeMetadata

    if ($null -eq $stm) {
        return
    }

    Write-Host ""
    Write-Host "$indent1 Build Tree"

    foreach ($id in $stm.BucketIds) {
        Write-Host ("$indent2 BucketId: $($id.Tag) -> $($id.Id)")
    }

    [string[]]$ids = @()
    if ($stm.BucketIds.Count -gt 0) {
        $ids = $stm.BucketIds | Select-Object -ExpandProperty Id
    }
    $buildState = Get-BuildStateMetadata -BucketIds $ids -DropLocation $context.DropLocationInfo.BuildCacheLocation

    $context.BuildStateMetadata = $buildState

    foreach ($file in $buildState.BuildStateFiles) {
        Write-Host ("$indent2 Build: $($file.BuildId) -> Bucket: $($file.BucketId.Id)/$($file.BucketId.Tag)")
    }
}

function PrepareEnvironment {
    # Setup environment for JavaScript tests
    $lockDownPath = "HKCU:\SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_LOCALMACHINE_LOCKDOWN"
    if (-not ( Test-Path $lockDownPath)) {
        New-Item -Path "$lockDownPath" -Type Directory -Force | Out-Null
    }

    Set-ItemProperty -Path $lockDownPath -Name "iexplore.exe" -Type "DWORD" -Value 0 | Out-Null

    if ((Test-Path "$lockDownPath\Settings") -eq 0) {
        New-Item -Path "$lockDownPath\Settings" -Type Directory -Force | Out-Null
    }
    Set-ItemProperty -Path "$lockDownPath\Settings" -Name "LOCALMACHINE_CD_UNLOCK" -Value 0 -Force | Out-Null

    # To avoid runtime problems by binding to interesting assemblies, we delete this so MSBuild will always try to bind to our version of WCF and not one found on the computer somewhere
    $wcfPath32 = "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319\AssemblyFoldersEx\WCF Data Services Standalone Assemblies"
    $wcfPath64 = "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319\AssemblyFoldersEx\WCF Data Services Standalone Assemblies"
    if (Test-Path $wcfPath32) {
        Remove-Item -Path $wcfPath32 -Recurse
    }

    if (Test-Path $wcfPath64) {
        Remove-Item -Path $wcfPath64 -Recurse
    }

    Optimize-BuildEnvironment
}

# Expand input paths into array. Try to resolve the path to full.
function ExpandPaths {
    param(
        [Parameter(Mandatory=$true)][string[]]$paths,
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$rootPath,
        [Parameter(Mandatory=$false)][string[]]$includePaths
    )

    $resolvedPaths = @()
    foreach ($path in $paths) {
        # Check current location
        $testedPath = $null
        if (Test-Path($path)) {
            $testedPath = Resolve-Path $path
        } elseif (-not [string]::IsNullOrWhiteSpace($rootPath)) {
            $currentDirPath = Join-Path -Path $rootPath -ChildPath $path

            if (Test-Path -Path $currentDirPath) {
                $testedPath = $currentDirPath
            }
        }

        if ($null -eq $testedPath -And $includePaths) {
            $includePaths.ForEach({
                $currentPath = $_
                $currentPath = Join-Path -Path $currentPath -ChildPath $path
                if (Test-Path ($currentPath)) {
                    $testedPath = $currentPath
                }
            })
        }

        if ($null -ne $testedPath) {
            $resolvedPaths += Resolve-Path $testedPath
        } else {
            Write-Error "Can't resolve path: $path"
        }
    }

    return $resolvedPaths
}

function AssignIncludeExclude {
    param(
        [Parameter(Mandatory=$false)]$include,
        [Parameter(Mandatory=$false)]$exclude,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$rootPath,
        [Parameter(Mandatory=$false)][string]$gitDirectory
    )

    if ($null -eq $include) {
        Write-Debug 'No includes specified - root path set to current directory.'
        $include = @($rootPath)
    } else {
        if (-not [string]::IsNullOrEmpty($gitDirectory) -and $rootPath -ne $gitDirectory) {
            if (-not ($include -contains $rootPath)) {
                Write-Debug 'Including current directory in build.'
                $include += $rootPath
            }
        }
    }

    $context.Include = ExpandPaths $include

    if ($null -ne $exclude) {
        $context.Exclude = ExpandPaths -paths $exclude -rootPath $rootPath -includePaths $context.Include
    }
}

function AssignSwitches() {
    $switches = $context.Switches

    $switches.Branch = $Branch.IsPresent
    $switches.Downstream = $Downstream.IsPresent

    if (-not $context.IsDesktopBuild) {
        $switches.Downstream = $true
    }

    $switches.Transitive = $Transitive.IsPresent
    $switches.Clean = $Clean.IsPresent
    $switches.Release = $Release.IsPresent
    $switches.Resume = $Resume.IsPresent
    $switches.SkipCompile = $SkipCompile.IsPresent
    $switches.ChangedFilesOnly = $ChangedFilesOnly.IsPresent
    $switches.RestrictToProvidedPaths = $RestrictToProvidedPaths.IsPresent
    $switches.ExcludeTestProjects = $ExcludeTestProjects.IsPresent

    if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("WhatIf")) {
        $script:Target = "CreatePlan"
    }

    if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("Verbose")) {
        $context.BuildMetadata.DebugLoggingEnabled = $true
    }

    $context.Switches = $switches
}

function RegisterChangeEvents($service) {
    $inputObject = $contextService.ConsoleAdapter

    Register-ObjectEvent -InputObject $inputObject -SourceIdentifier $titleEventSource -EventName "ProgressChanged" -Action {
        $Host.UI.RawUI.WindowTitle = ($Event.SourceArgs.CurrentOperation + " " +  $Event.SourceArgs.StatusDescription)
    } | Out-Null
}

<#
.SYNOPSIS
    .
.DESCRIPTION
    .
#>
# This file is run by an CI agent. The CI agent PowerShell runner does not subscribe to Write-Information.
function global:Invoke-Build2 {
    [CmdletBinding(DefaultParameterSetName="Build", SupportsShouldProcess=$true)]
    param (
        [Parameter(ParameterSetName="Build", Mandatory=$false, Position=0)]
        [string]$ModulePath = "",

        [Parameter()]
        [switch]$Branch,

        [Parameter()]
        [switch]$Downstream,

        [Parameter()]
        [switch]$Transitive,

        [Parameter()]
        <#
        Returns the source tree to a pristine state by destroying all intermediate objects.
        Generally this parameter should not be used as it prevents incremental builds which increases build times.
        #>
        [switch]$Clean,

        [Parameter()]
        [switch]$Release,

        <#
        Resumes the build from the last failure point.
        #>
        [Parameter()]
        [switch]$Resume,

        <#
        Runs the only directory build targets. Skips all targets that produce assemblies.
        #>
        [Parameter()]
        [switch]$SkipCompile,

        <#
        Includes the product packaging steps. This will produce the package which can be used to install the product.
        #>
        [Parameter()]
        [switch]$PackageProduct,

        <#
        Disables the use of the build cache.
        #>
        [Parameter()]
        [switch]$NoBuildCache,

        [Parameter(HelpMessage = "Enables integration tests.")]
        [switch]$RunIntegrationTests,

        #[Parameter]
        #[switch]$automation,

        [Parameter(HelpMessage = "Displays HTML code coverage report.")]
        [switch]$DisplayCodeCoverage,

        <#
        Runs the target with the provided name.
        #>
        [Parameter()]
        [string]$Target = "BuildAndPackage",

        <#
        Includes solutions and projects found under these paths into the build tree. Supports wildcards and relative paths.
        #>
        [Parameter()]
        [string[]]$Include = $null,

        <#
        Excludes solutions and projects found under these paths into the build tree. Supports wildcards and relative paths.
        #>
        [Parameter()]
        [string[]]$Exclude = $null,

        <#
        Only files that have modifications are considered.
        #>
        [Parameter()]
        [Alias("JustMyChanges")]
        [switch]$ChangedFilesOnly,

        <#
        Disables the text transformation process.
        #>
        [Parameter()]
        [switch]$NoTextTemplateTransform,

        <#
        Specifies the maximum number of concurrent processes to build with.
        #>
        [Parameter()]
        [int]$MaxCpuCount,

        <#
        Disables fetching of dependencies. Used to bypass the default behaviour of keeping you up to date.
        #>
        [Parameter()]
        [switch]$NoDependencyFetch,

        <#
        Instructs the build to not expand the build tree.
        The build will attempt to automatically resolve dependencies between modules by examining
        the projects and manifests which specify dependencies along the provided input paths.
        Should the specified inputs require additional dependencies then they are added to the build tree
        automatically. To suppress this behaviour and restict the build tree to just the paths provided specify this parameter.
        #>
        [Parameter()]
        [Alias("NoExpand")]
        [switch]$RestrictToProvidedPaths,

        <#
        Instructs the build to not build anything identified as a test project.
        #>
        [Parameter()]
        [switch]$ExcludeTestProjects,

        <#
        Instructs the console logger to be quiet.
        #>
        [Parameter()]
        [switch]$MinimalConsoleLogging,

        [Parameter(HelpMessage = "Enables fetching build configuration files from TFS.")]
        [switch]$EnableConfigDownload,

        [Parameter(ValueFromRemainingArguments)]
        [string[]]$RemainingArgs
    )

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'


    if ($Clean.IsPresent) {
        Write-Host "You have specified 'Clean'." -ForegroundColor Yellow
        Write-Information "Clean should not avoided as it prevents incremental builds which increases build times. If you find yourself needing this often please speak to a build engineer."

        if (-not($PSCmdlet.ShouldContinue("Continue cleaning", ""))) {
            return
        }
    }

    [Aderant.Build.BuildOperationContext]$context = Get-BuildContext -CreateIfNeeded

    if ($null -eq $context) {
        throw 'Fatal error. Failed to create context'
    }

    [string]$repositoryPath = $null
    if (-not [string]::IsNullOrEmpty($ModulePath)) {
        $repositoryPath = Resolve-Path $ModulePath
    } else {
        $repositoryPath = (Get-Location).Path
    }

    $context.BuildSystemDirectory = "$PSScriptRoot\..\..\..\"

    [string]$root = FindGitDir -context $context -searchDirectory $repositoryPath
    AssignIncludeExclude -include $Include -exclude $Exclude -rootPath $repositoryPath -gitDirectory $root

    $context.BuildRoot = $root

    GetSourceTreeMetadata -context $context -repositoryPath $root

    AssignSwitches
    ApplyBranchConfig -context $context -root $root -enableConfigDownload:$EnableConfigDownload.IsPresent
    FindProductManifest -context $context -root $root -enableConfigDownload:$EnableConfigDownload.IsPresent

    if (-not $NoBuildCache.IsPresent) {
        GetBuildStateMetadata $context
    }

    PrepareEnvironment

    $context.StartedAt = [DateTime]::UtcNow
    $context.LogFile = "$root\build.log"

    $succeeded = $false

    $currentColor = $host.UI.RawUI.ForegroundColor
    try {
        $contextEndpoint = [DateTime]::UtcNow.ToFileTimeUtc().ToString()

        $contextService = [Aderant.Build.PipelineService.BuildPipelineServiceHost]::new()
        RegisterChangeEvents $contextService

        $contextService.StartService($contextEndpoint)
        Write-Debug "Service running on uri: $($contextService.ServerUri)"
        $contextService.CurrentContext = $context

        $args = CreateToolArgumentString $context $RemainingArgs

        # When WhatIf specified just determine what would be built
        if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("WhatIf")) {
            $Target = "CreatePlan"
        }

        Run-MSBuild "$($context.BuildScriptsDirectory)ComboBuild.targets" "/target:$($Target) /fl /flp:logfile=$($context.LogFile);Encoding=UTF-8 /p:ContextEndpoint=$contextEndpoint $args"

        $succeeded = $true

        if ($LASTEXITCODE -eq 0 -and $displayCodeCoverage.IsPresent) {
            [string]$codeCoverageReport = Join-Path -Path $repositoryPath -ChildPath "Bin\Test\CodeCoverage\dotCoverReport.html"

            if (Test-Path ($codeCoverageReport)) {
                Write-Host "Displaying dotCover code coverage report."
                Start-Process $codeCoverageReport
            } else {
                Write-Warning "Unable to locate dotCover code coverage report."
            }
        }
    } catch {
        $succeeded = $false
        Write-Error $PSItem.Tostring()
    } finally {
        Get-EventSubscriber -SourceIdentifier $titleEventSource | Unregister-Event

        $host.UI.RawUI.ForegroundColor = $currentColor

        if (-not $context.IsDesktopBuild) {
            Write-Host "##vso[task.uploadfile]$($context.LogFile)"
        }

        $context = $contextService.CurrentContext
        $reason = $context.BuildStatusReason
        $status = $context.BuildStatus

        Write-Output ""
        Write-Output ""

        Write-Host " Build: " -NoNewline

        if ($global:LASTEXITCODE -gt 0 -or (-not $succeeded) -or $context.BuildStatus -eq "Failed") {
            Write-Host "[" -NoNewline
            Write-Host ($status.ToUpper()) -NoNewline -ForegroundColor Red
            Write-Host "]"
            Write-Host " $reason" -ForegroundColor Red

            if (-not $context.IsDesktopBuild) {
                throw "Build did not succeed: $($context.BuildStatusReason)"
            }
        } else {
            [System.Environment]::SetEnvironmentVariable("SKIP_BUILD_SYSTEM_COMPILE", "true", [System.EnvironmentVariableTarget]::Process)

            Write-Host "[" -NoNewline
            Write-Host ($status.ToUpper()) -NoNewline -ForegroundColor Green
            Write-Host "]"
            Write-Host " $reason" -ForegroundColor Gray
        }

        if ($null -ne $contextService) {
            $contextService.Dispose()
        }
    }
}

Set-Alias -Name bm -Value global:Invoke-Build2 -Scope 'Global'