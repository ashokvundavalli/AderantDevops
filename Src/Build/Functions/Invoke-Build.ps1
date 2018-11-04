[string]$indent1 = "  "
[string]$indent2 = "        "

function Get-Branch {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$root
    )

    begin {
        [string]$rspFile = [System.IO.Path]::Combine($root, "Build\TFSBuild.rsp")
        # ToDo: Change 'monotest' to 'master' once the monotest has been merged.
        [string]$branch = 'monotest'
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
        return (Invoke-WebRequest -Uri "http://tfs.$($env:USERDNSDOMAIN.ToLower()):8080/tfs/ADERANT/44f228f7-b636-4bd3-99ee-eb2f1570d768/316b9ba9-3a49-47b2-992e-a9a2a2835b3f/_api/_versioncontrol/itemContent?repositoryId=e6138670-7236-4e80-aa7c-6417eea253f5&path=%2FBuild%2F$($manifest)&version=GB$($branch)&contentOnly=false&__v=5" -UseBasicParsing -UseDefaultCredentials).Content
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

    $global:BranchConfigPath = Read-Host -Prompt 'Please supply a valid path to the ExpertManifest.xml and BranchConfig.xml files'
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
            # ToDo: Change 'monotest' to 'master' once the BranchConfig.xml file exists.
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
    $path = [Aderant.Build.PathUtility]::GetDirectoryNameOfFileAbove($searchDirectory, ".git", $null, $true)
    $context.Variables["_GitDir"] = "$path\.git"

    return $path
}

function CreateToolArgumentString($context, $remainingArgs) {
    Set-StrictMode -Version Latest

    $set = [System.Collections.Generic.HashSet[string]]::new()          

    & {
        if ($context.IsDesktopBuild) {
            $set.Add("/p:IsDesktopBuild=true")
        } else {
            $set.Add("/p:IsDesktopBuild=false")
            $set.Add("/clp:PerformanceSummary")
        }

        if ($MinimalConsoleLogging.IsPresent) {
            $set.Add("/NoConsoleLogger")
        }

        if ($context.BuildMetadata -ne $null) {
            if ($context.BuildMetadata.DebugLoggingEnabled) {
                $set.Add("/flp:Verbosity=Diag")
            } else {
                $set.Add("/flp:Verbosity=Normal")
            }
        }
        
        # Don't show the logo and do not allow node reuse so all child nodes are shut down once the master node has completed build orchestration.
        $set.Add("/nologo")
        $set.Add("/nr:false")

        # Multi-core build
        if ($MaxCpuCount -gt 0) {            
            $set.Add("/m:" + $MaxCpuCount.ToString())
        } else {
            $set.Add("/m:" + [Math]::Max(1, [System.Environment]::ProcessorCount - 2).ToString())
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
            $set.Add("/p:RetrievePrebuilts=false")    
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
            $set.Add("/p:RunPackageProduct=$($PackageProduct.IsPresent)")
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

            Write-Host "Calculating changes between $sourceBranch and $targetBranch"
        }
    }    

    $context.SourceTreeMetadata = Get-SourceTreeMetadata -SourceDirectory $repositoryPath -SourceBranch $sourceBranch -TargetBranch $targetBranch -IncludeLocalChanges:$context.IsDesktopBuild

    Write-Host "$indent1 New commit: $($context.SourceTreeMetadata.NewCommitDescription)"
    Write-Host "$indent1 Old commit: $($context.SourceTreeMetadata.OldCommitDescription)"
    if ($context.SourceTreeMetadata.CommonAncestor) {
        Write-Host "$indent1 CommonAncestor: $($context.SourceTreeMetadata.CommonAncestor)"
    }
   
    if ($context.SourceTreeMetadata.Changes -ne $null -and $context.SourceTreeMetadata.Changes.Count -gt 0) {
        Write-Host ""
        Write-Host "$indent1 Changes..."    
        foreach ($change in $context.SourceTreeMetadata.Changes) {
            if ($change.Status -ne "Untracked") {
                Write-Host "$indent2 $($change.Path):$($change.Status)"
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
        $testedPath = ''
        if (Test-Path($path)) {
            $testedPath = Resolve-Path $path
        } elseif (-not [string]::IsNullOrWhiteSpace($rootPath)) {
            $currentDirPath = Join-Path -Path $rootPath -ChildPath $path

            if (Test-Path -Path $currentDirPath) {
                $testedPath = $currentDirPath
            }
        }

        if ($testedPath -eq '' -And $includePaths) {            
            $includePaths.ForEach({
                $currentPath = $_
                $currentPath = Join-Path -Path $currentPath -ChildPath $path
                if (Test-Path($currentPath)) {
                    $testedPath = $currentPath
                }
            })
        }

        if ($testedPath -ne '') {
            $resolvedPaths += Resolve-Path $testedPath
        } else {
            Write-Error "Can't resolve path: $path" 
        }
    }

    return $resolvedPaths
}

function AssignIncludeExclude {    
    param(
        [Parameter(Mandatory=$false)][string[]]$include,
        [Parameter(Mandatory=$false)][string[]]$exclude,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$rootPath
    )
    
    if ($null -ne $include) {
        $context.Include = ExpandPaths $include
    }

    if ($null -ne $exclude) {			
        $context.Exclude = ExpandPaths -paths $exclude -rootPath $rootPath -includePaths $context.Include            
    }    
}

function AssignSwitches() {
    $switches = $context.Switches
    
    $switches.Branch = $Branch.IsPresent
    $switches.Downstream = $Downstream.IsPresent
    $switches.Transitive = $Transitive.IsPresent
    $switches.Clean = $Clean.IsPresent
    $switches.Release = $Release.IsPresent    
    $switches.Resume = $Resume.IsPresent
    $switches.SkipCompile = $SkipCompile.IsPresent
    $switches.ChangedFilesOnly = $ChangedFilesOnly.IsPresent

    if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("WhatIf")) {
        $switches.WhatIf = $true
    }

    if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("Verbose")) {
        $context.BuildMetadata.DebugLoggingEnabled = $true
        Write-Output "DebugLoggingEnabled"
    }

    $context.Switches = $switches
}

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
        
        [Parameter(HelpMessage = "Destroys all intermediate objects.
Returns the source tree to a pristine state.
Should not be used as it prevents incremental builds which increases build times.")]
        [switch]$Clean,

        [Parameter()]
        [switch]$Release,        

        [Parameter(HelpMessage = "Resumes the build from the last failure point.")]
        [switch]$Resume,

        [Parameter()]
        [switch]$SkipCompile,

        [Parameter(HelpMessage = "Includes the product packaging steps. This will produce the package which can be used to install the product.")]
        [switch]$PackageProduct,

        [Parameter(HelpMessage = "Disables the use of the build cache.")]
        [switch]$NoBuildCache,

        #[Parameter]
        #[switch]$integration,

        #[Parameter]
        #[switch]$automation,        

        [Parameter(HelpMessage = "Displays HTML code coverage report.")]
        [switch]$DisplayCodeCoverage,     

        [Parameter(HelpMessage = "Runs the target with the provided name")]        
        [string]$Target = "BuildAndPackage",

        [Parameter(HelpMessage = "Includes solutions and projects found under these paths into the build tree. Supports wildcards.")]
        [string[]]$Include = $null,

        [Parameter(HelpMessage = "Excludes solutions and projects found under these paths into the build tree. Supports wildcards.")]
        [string[]]$Exclude = $null,

        [Parameter(HelpMessage = "Only files that have modifications are considered.")]
        [Alias("JustMyChanges")]
        [switch]$ChangedFilesOnly,

        [Parameter(HelpMessage = "Disables the text transformation process.")]        
        [switch]$NoTextTemplateTransform,

        [Parameter(HelpMessage = " Specifies the maximum number of concurrent processes to sbuild with.")]        
        [int]$MaxCpuCount,

        [Parameter(HelpMessage = "Disables fetching of dependencies. Used to bypass the default behaviour of keeping you up to date.")]        
        [switch]$NoDependencyFetch,

        [Parameter(HelpMessage = "Instructs the console logger to be quiet.")]        
        [switch]$MinimalConsoleLogging,
        
        [Parameter(ValueFromRemainingArguments)]
        [string[]]$RemainingArgs
    )

    begin {
        Set-StrictMode -Version Latest
        $ErrorActionPreference = 'Stop'
    }

    process {
        if ($Clean.IsPresent) {
            Write-Host "If you're reading this, you have specified 'Clean'." -ForegroundColor Yellow 
            Write-Host "Clean should not be used as it prevents incremental builds which increases build times."        

            if (-not($PSCmdlet.ShouldContinue("Continue cleaning", ""))) {
                return
            }        
        }
        
        [Aderant.Build.BuildOperationContext]$context = Get-BuildContext -CreateIfNeeded
         
        [string]$repositoryPath = $null
        if (-not [string]::IsNullOrEmpty($ModulePath)) {
            $repositoryPath = $ModulePath
        } else {
            $repositoryPath = (Get-Location).Path
        }

        $context.BuildSystemDirectory = "$PSScriptRoot\..\..\..\"

        AssignIncludeExclude -include $Include -exclude $Exclude -rootPath $repositoryPath               
        
        [string]$root = FindGitDir -context $context -searchDirectory $repositoryPath
        GetSourceTreeMetadata -context $context -repositoryPath $root
        if ([string]::IsNullOrWhiteSpace($root)) {
            $root = $repositoryPath
        }

        AssignSwitches
        ApplyBranchConfig -context $context -root $root -enableConfigDownload:$EnableConfigDownload.IsPresent
        FindProductManifest -context $context -root $root -enableConfigDownload:$EnableConfigDownload.IsPresent

        if (-not $NoBuildCache.IsPresent) {
            GetBuildStateMetadata $context
        }

        PrepareEnvironment

        $context.StartedAt = [DateTime]::UtcNow
        $context.LogFile = "$repositoryPath\build.log"

        $contextEndpoint = [DateTime]::UtcNow.ToFileTimeUtc().ToString()    

        $contextService = [Aderant.Build.PipelineService.BuildPipelineServiceHost]::new()
        $contextService.StartListener($contextEndpoint)
        Write-Debug "Service running on uri: $($contextService.ServerUri)"
        $contextService.Publish($context)

        $succeeded = $false

        $currentColor = $host.UI.RawUI.ForegroundColor 
        try {         
            $args = CreateToolArgumentString $context $RemainingArgs

            # When WhatIf specified just determine what would be built
            if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("WhatIf")) {
                $Target = "CreatePlan"
            }        

            Run-MSBuild "$($context.BuildScriptsDirectory)ComboBuild.targets" "/target:$($Target) /verbosity:normal /fl /flp:logfile=$($context.LogFile);Encoding=UTF-8 /p:ContextEndpoint=$contextEndpoint $args"

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
        } finally {
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

            if ($global:LASTEXITCODE -gt 0 -or -not $succeeded -or $context.BuildStatus -eq "Failed") {            
                Write-Host "[" -NoNewline
                Write-Host ($status.ToUpper()) -NoNewline -ForegroundColor Red
                Write-Host "]"
                Write-Host " $reason" -ForegroundColor Red

                if (-not $context.IsDesktopBuild) {
                    throw "Build did not succeed: $($context.BuildStatusReason)"
                }
            } else {            
                Write-Host "[" -NoNewline
                Write-Host ($status.ToUpper()) -NoNewline -ForegroundColor Green
                Write-Host "]"
                Write-Host " $reason" -ForegroundColor Gray    
            }

            if ($contextService -ne $null) {
                $contextService.Dispose()
            }
        }
    }
}

Set-Alias -Name bm -Value global:Invoke-Build2 -Scope 'Global'