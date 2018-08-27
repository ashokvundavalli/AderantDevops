$indent1 = "  "
$indent2 = "        "

function ApplyBranchConfig($context, $stringSearchDirectory) {
    $configPath = [Aderant.Build.PathUtility]::GetDirectoryNameOfFileAbove($stringSearchDirectory, "branch.config")

    if (-not $configPath) {
        #throw "Branch configuration file not found"
        # TODO: shim
    [xml]$config = "<BranchConfig>
  <DropPaths>
    <!--\\ap.aderant.com\akl\tempswap\☃-->
    <PrimaryDropLocation>\\dfs.aderant.com\packages\_build_cache\v1</PrimaryDropLocation>
    <AlternativeDropLocation></AlternativeDropLocation>    
    <PullRequestDropLocation>\\dfs.aderant.com\ExpertSuite\pulls</PullRequestDropLocation>
    <XamlBuildDropLocation>\\dfs.aderant.com\ExpertSuite\dev\vnext</XamlBuildDropLocation>
  </DropPaths>
</BranchConfig>"
        }

    #[xml]$config = Get-Content -Raw -Path "$configPath\branch.config"

    $context.Drops.PrimaryDropLocation = $config.BranchConfig.DropPaths.PrimaryDropLocation
    $context.Drops.PullRequestDropLocation = $config.BranchConfig.DropPaths.PullRequestDropLocation
    $context.Drops.XamlBuildDropLocation = $config.BranchConfig.DropPaths.XamlBuildDropLocation
}

function FindProductManifest($context, $stringSearchDirectory) {        
    $path = [Aderant.Build.PathUtility]::GetDirectoryNameOfFileAbove($stringSearchDirectory, "ExpertManifest.xml")
    $context.ProductManifestPath = "$path\ExpertManifest.xml"
}

function FindGitDir($context, $stringSearchDirectory) {        
    $path = [Aderant.Build.PathUtility]::GetDirectoryNameOfFileAbove($stringSearchDirectory, ".git", $null, $true)
    $context.Variables["_GitDir"] = "$path\.git"
}

 function CreateToolArgumentString($context, $remainingArgs) {
    # Out-Null stops the return value from Add being left on the pipeline
    $set = [System.Collections.Generic.HashSet[string]]::new()          

    if ($context.BuildMetadata -ne $null) {
        if ($context.BuildMetadata.DebugLoggingEnabled) {
            [void]$set.Add("/v:diag")
        }

        #if ($context.BuildMetadata.IsPullRequest) {
        #    $set.Add("/v:diag") | Out-Null
        #}
    }
        
    # Don't show the logo and do not allow node reuse so all child nodes are shut down once the master node has completed build orchestration.
    [void]$set.Add("/nologo")
    [void]$set.Add("/nr:false")

    # Multi-core build
    [void]$set.Add("/m")

    if ($context.IsDesktopBuild) {
        [void]$set.Add("/p:IsDesktopBuild=true")
    } else {
        [void]$set.Add("/p:IsDesktopBuild=false")
        [void]$set.Add("/clp:PerformanceSummary")
    }

    [void]$set.Add("/p:VisualStudioVersion=14.0")

    if ($context.Switches.SkipCompile) {
        [void]$set.Add("/p:Switches_SkipCompile=true")
    }

    if ($remainingArgs) {
        # Add pass-thru args
        [void]$set.Add([string]::Join(" ", $remainingArgs))
    }

    [void]$set.Add("/p:PrimaryDropLocation=$($context.Drops.PrimaryDropLocation)")
    [void]$set.Add("/p:PullRequestDropLocation=$($context.Drops.PullRequestDropLocation)")
    [void]$set.Add("/p:XamlBuildDropLocation=$($context.Drops.XamlBuildDropLocation)")

    [void]$set.Add("/p:ProductManifestPath=$($context.ProductManifestPath)")

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

    $context.SourceTreeMetadata = Get-SourceTreeMetadata -SourceDirectory $repositoryPath -SourceBranch $sourceBranch -TargetBranch $targetBranch -IncludeLocalChanges:$PendingChanges.IsPresent

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
    $buildState = Get-BuildStateMetadata -BucketIds $ids -DropLocation $context.Drops.PrimaryDropLocation

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

<#
.SYNOPSIS
    Runs a build based on your current context

.DESCRIPTION

.PARAMETER remainingArgs
    A catch all that lets you specify arbitrary parameters to the build engine.
    Useful if you wish to override some property but that property is not exposed as a first class concept.

#>
function global:Invoke-Build2
{    
    [CmdletBinding(DefaultParameterSetName="Build", SupportsShouldProcess=$true)]    
    param (
        [Parameter(ParameterSetName="Build")]
        [switch]$PendingChanges,

        [Parameter(ParameterSetName="Build")]
        [switch]$Branch,

        [Parameter()]
        [switch]$Everything,

        [Parameter()]
        [switch]$Downstream,

        [Parameter()]
        [switch]$Transitive,
        
        [Parameter()]
        [switch]$Clean,

        [Parameter()]
        [switch]$Release,

        [Parameter()]
        [switch]$Package,

        [Parameter()]
        [switch]$Resume,

        [Parameter()]
        [switch]$SkipCompile,

        #[Parameter]
        #[switch]$integration,

        #[Parameter]
        #[switch]$automation,        

        [Parameter()]
        [switch]$DisplayCodeCoverage,
                
        [Parameter(ParameterSetName="Build", Mandatory=$false)]        
        [string]$ModulePath = "",  
        
        [Parameter(ValueFromRemainingArguments)]
        [string[]]$RemainingArgs
    )
         
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

    FindGitDir $context $repositoryPath    
    ApplyBranchConfig $context $repositoryPath
    FindProductManifest $context $repositoryPath
    GetSourceTreeMetadata $context $repositoryPath
    GetBuildStateMetadata $context
    PrepareEnvironment

    $switches = $context.Switches
    $switches.PendingChanges = $PendingChanges.IsPresent
    $switches.Everything = $Everything.IsPresent
    $switches.Downstream = $Downstream.IsPresent
    $switches.Transitive = $Transitive.IsPresent
    $switches.Clean = $Clean.IsPresent
    $switches.Release = $Release.IsPresent
    $switches.Resume = $Resume.IsPresent
    $switches.SkipCompile = $SkipCompile.IsPresent        
    $context.Switches = $switches

    $context.StartedAt = [DateTime]::UtcNow

    $contextFileName = [DateTime]::UtcNow.ToFileTimeUtc().ToString()
    $contextService = [Aderant.Build.PipelineService.BuildPipelineServiceFactory]::new()
    $contextService.StartListener($contextFileName)
    $contextService.Publish($context)

    try {
        $args = CreateToolArgumentString $context $RemainingArgs

        Run-MSBuild "$($context.BuildScriptsDirectory)\ComboBuild.targets" "/target:BuildAndPackage /p:ContextFileName=$contextFileName $args"
 
        if ($LASTEXITCODE -eq 0 -and $displayCodeCoverage.IsPresent) {
            [string]$codeCoverageReport = Join-Path -Path $repositoryPath -ChildPath "Bin\Test\CodeCoverage\dotCoverReport.html"

            if (Test-Path ($codeCoverageReport)) {
                Write-Host "Displaying dotCover code coverage report."
                Start-Process $codeCoverageReport
            } else {
                Write-Warning "Unable to locate dotCover code coverage report."
            }
        }
    } finally {
        $contextService.Dispose()
    }
}