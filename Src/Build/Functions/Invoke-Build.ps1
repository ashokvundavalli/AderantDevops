﻿# This file is run by an CI agent. The CI agent PowerShell runner does not subscribe to Write-Information.

$indent1 = "  "
$indent2 = "        "

function ApplyBranchConfig($context, $root) {
    $configPath = [System.IO.Path]::Combine($root, "Build\BranchConfig.xml")    

    if (-not $configPath) {
        throw "Branch configuration file not found at path $($configPath)"
    }

    [xml]$config = Get-Content -Raw -LiteralPath $configPath

    $context.DropLocationInfo.PrimaryDropLocation = $config.BranchConfig.DropLocations.PrimaryDropLocation
    $context.DropLocationInfo.BuildCacheLocation = $config.BranchConfig.DropLocations.BuildCacheLocation
    $context.DropLocationInfo.PullRequestDropLocation = $config.BranchConfig.DropLocations.PullRequestDropLocation
    $context.DropLocationInfo.XamlBuildDropLocation = $config.BranchConfig.DropLocations.XamlBuildDropLocation
}

function FindProductManifest($context, $stringSearchDirectory) {
    $configPath = [System.IO.Path]::Combine($root, "Build\ExpertManifest.xml")
    $context.ProductManifestPath = $configPath
}

function FindGitDir($context, $stringSearchDirectory) {        
    $path = [Aderant.Build.PathUtility]::GetDirectoryNameOfFileAbove($stringSearchDirectory, ".git", $null, $true)
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
            $set.Add("/m:$MaxCpuCount")
        } else {
            $set.Add("/m")
        }

        if ($NoTextTemplateTransform.IsPresent) {
            $set.Add("/p:T4TransformEnabled=false")
        }      

        #$set.Add("/p:VisualStudioVersion=14.0")

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
            $set.Add([string]::Join(" ", $remainingArgs))
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
    $sourceBranch = ""
    $targetBranch = ""

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
            Write-Host "$indent2 $($change.Path):$($change.Status)"
        }
    }
}

function GetBuildStateMetadata($context) {    
    $stm = $context.SourceTreeMetadata

    Write-Host ""
    Write-Host "$indent1 Build Tree"
    foreach ($id in $stm.BucketIds) {
        Write-Host ("$indent2 BucketId: $($id.Tag) -> $($id.Id)")
    }   

    $ids = $stm.BucketIds | Select-Object -ExpandProperty Id    
    $buildState = Get-BuildStateMetadata -BucketIds $ids -DropLocation $context.DropLocationInfo.BuildCacheLocation

    $context.BuildStateMetadata = $buildState

    foreach ($file in $buildState.BuildStateFiles) {
        Write-Host ("$indent2 Build: $($file.BuildId) -> Bucket: $($file.BucketId.Id)/$($file.BucketId.Tag)")
    }    
}

function PrepareEnvironment {
  # Setup environment for JavaScript tests
  Set-ItemProperty -Path "HKCU:\SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_LOCALMACHINE_LOCKDOWN" -Name "iexplore.exe" -Type "DWORD" -Value 0

  $lockDownPath = "HKCU:\Software\Policies\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_LOCALMACHINE_LOCKDOWN"
  if ((Test-Path "$lockDownPath") -eq 0)
  {
    New-Item -Path "$lockDownPath\Settings" -Type Directory -Force
    New-ItemProperty -Path "$lockDownPath\Settings" -Name "LOCALMACHINE_CD_UNLOCK" -Value 0
  }
  elseif ((Test-Path "$lockDownPath") -eq 1)
  {
    Set-ItemProperty -Path "$lockDownPath\Settings" -Name "LOCALMACHINE_CD_UNLOCK" -Value 0
  }  

  # To avoid runtime problems by binding to interesting assemblies, we delete this so MSBuild will always try to bind to our version of WCF and not one found on the computer somewhere
  $wcfPath32 = "HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v4.0.30319\AssemblyFoldersEx\WCF Data Services Standalone Assemblies"
  $wcfPath64 = "HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319\AssemblyFoldersEx\WCF Data Services Standalone Assemblies"
  if (Test-Path $wcfPath32) {  
    Remove-Item -Path $wcfPath32 -Recurse
  }

  if (Test-Path $wcfPath64) {  
    Remove-Item -Path $wcfPath64 -Recurse
  }  
}

function ExpandPaths([string[]]$paths) {
    $resolvedPaths = @() 
    foreach ($path in $paths) {
        $resolvedPaths += Resolve-Path $path
    }
    return $resolvedPaths
}

function AssignIncludeExclude() {
    if ($Include) {
        $context.Include = ExpandPaths $Include

        Write-Output "These paths will be included:"
        $context.Include.ForEach({ Write-Output $_})
    }

    if ($Exclude) {
        $context.Exclude = ExpandPaths $Exclude
        $context.Exclude.ForEach({ Write-Output $_})
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

function global:Invoke-Build2
{
    [CmdletBinding(DefaultParameterSetName="Build", SupportsShouldProcess=$true)]    
    param (
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

        [Parameter()]
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

        [Parameter()]
        [switch]$DisplayCodeCoverage,
                
        [Parameter(ParameterSetName="Build", Mandatory=$false)]        
        [string]$ModulePath = "",

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
        
        [Parameter(ValueFromRemainingArguments)]
        [string[]]$RemainingArgs
    )

    if ($Clean) {
        Write-Host "If you're reading this, you have specified 'Clean'." -ForegroundColor Yellow 
        Write-Host "Clean should not be used as it prevents incremental builds which increases build times."        

        if (-not($PSCmdlet.ShouldContinue("Continue cleaning", ""))) {
            return
        }        
    } 
         
    Set-StrictMode -Version Latest
    $ErrorActionPreference = "Stop" 
        
    [Aderant.Build.BuildOperationContext]$context = Get-BuildContext -CreateIfNeeded
         
    [string]$repositoryPath = $null
    if (-not [string]::IsNullOrEmpty($ModulePath)) {
        $repositoryPath = $ModulePath
    } else {
        $repositoryPath = $ShellContext.CurrentModulePath
    }

    $context.BuildSystemDirectory = "$PSScriptRoot\..\..\..\"

    AssignIncludeExclude    

    $root = FindGitDir $context $repositoryPath
    AssignSwitches
    ApplyBranchConfig $context $root
    FindProductManifest $context $root
    GetSourceTreeMetadata $context $root

    if (-not $NoBuildCache.IsPresent) {
        GetBuildStateMetadata $context
    }

    PrepareEnvironment  

    $context.StartedAt = [DateTime]::UtcNow
    $context.LogFile = "$repositoryPath\build.log"

    $contextEndpoint = [DateTime]::UtcNow.ToFileTimeUtc().ToString()    

    $contextService = [Aderant.Build.PipelineService.BuildPipelineServiceHost]::new()
    $contextService.StartListener($contextEndpoint)    
    $contextService.Publish($context)

    $succeded = $false

    $currentColor = $host.UI.RawUI.ForegroundColor 
    try {         
        $args = CreateToolArgumentString $context $RemainingArgs

        # When WhatIf specified just determine what would be built
        if ($PSCmdLet.MyInvocation.BoundParameters.ContainsKey("WhatIf")) {
            $Target = "CreatePlan"
        }        

        Run-MSBuild "$($context.BuildScriptsDirectory)\ComboBuild.targets" "/target:$($Target) /verbosity:normal /fl /flp:logfile=$($context.LogFile) /p:ContextEndpoint=$contextEndpoint $args"

        $succeded = $true

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
        $succeded = $false        
    } finally {        
        Write-Host "##vso[task.uploadfile]$($context.LogFile)"        

        $host.UI.RawUI.ForegroundColor = $currentColor

        $context = $contextService.CurrentContext
        $reason = $context.BuildStatusReason
        $status = $context.BuildStatus

        Write-Output ""
        Write-Output ""

        Write-Host " Build: " -NoNewline

        if ($global:LASTEXITCODE -gt 0 -or -not $succeded -or $context.BuildStatus -eq "Failed") {            
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