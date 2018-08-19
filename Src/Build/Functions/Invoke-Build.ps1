$indent1 = "  "
$indent2 = "        "

function ApplyBranchConfig($context, $stringSearchDirectory) {
    $configPath = [Aderant.Build.PathUtility]::GetDirectoryNameOfFileAbove($stringSearchDirectory, "branch.config")

    if (-not $configPath) {
        #throw "Branch configuration file not found"
        # TODO: shim
    [xml]$config = "<BranchConfig>
  <Artifacts>
    <!--\\ap.aderant.com\akl\tempswap\☃-->
    <PrimaryDropLocation>\\dfs.aderant.com\ExpertSuite\_TEMP_</PrimaryDropLocation>
    <AlternativeDropLocation></AlternativeDropLocation>
    <PullRequestDropLocation>\\dfs.aderant.com\ExpertSuite\pulls</PullRequestDropLocation>
  </Artifacts>
</BranchConfig>"
        }

    #[xml]$config = Get-Content -Raw -Path "$configPath\branch.config"
    $context.PrimaryDropLocation = $config.BranchConfig.Artifacts.PrimaryDropLocation
    $context.PullRequestDropLocation = $config.BranchConfig.Artifacts.PullRequestDropLocation
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
            $set.Add("/v:diag") | Out-Null
        }

        #if ($context.BuildMetadata.IsPullRequest) {
        #    $set.Add("/v:diag") | Out-Null
        #}
    }
        
    # Don't show the logo and do not allow node reuse so all child nodes are shut down once the master node has completed build orchestration.
    $set.Add("/nologo") | Out-Null
    $set.Add("/nr:false") | Out-Null

    # Multi-core build
    $set.Add("/m")| Out-Null

    if ($context.IsDesktopBuild) {
        $set.Add("/p:IsDesktopBuild=true") | Out-Null
    } else {
        $set.Add("/p:IsDesktopBuild=false") | Out-Null
        $set.Add("/clp:PerformanceSummary") | Out-Null
    }

    $set.Add("/p:VisualStudioVersion=14.0") | Out-Null

    if ($context.Switches.SkipCompile) {
        $set.Add("/p:Switches_SkipCompile=true") | Out-Null
    }

    if ($remainingArgs) {
        # Add pass-thru args
        $set.Add([string]::Join(" ", $remainingArgs)) | Out-Null
    }

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

    $context.SourceTreeMetadata = Get-SourceTreeMetadata -SourceDirectory $repositoryPath -SourceBranch $sourceBranch -TargetBranch $targetBranch

    Write-Host "$indent1 New commit: $($context.SourceTreeMetadata.NewCommitDisplay)"
    Write-Host "$indent1 Old commit: $($context.SourceTreeMetadata.OldCommitDisplay)"
    Write-Host "$indent1 CommonAncestor: $($context.SourceTreeMetadata.CommonAncestor)"    
   
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
    $buildState = Get-BuildStateMetadata -BucketIds $ids -DropLocation $context.PrimaryDropLocation

    $context.BuildStateMetadata = $buildState

    foreach ($file in $buildState.BuildStateFiles) {
        Write-Host ("$indent2 Build: $($file.BuildId) -> Bucket: $($file.BucketId.Id)")
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

    ApplyBranchConfig $context $repositoryPath

    FindGitDir $context $repositoryPath

    GetSourceTreeMetadata $context $repositoryPath
    GetBuildStateMetadata $context

    return

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
    $contextFileName = Publish-BuildContext $context

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
}