<#
    .SYNOPSIS
    Runs a build based on your current context

    .DESCRIPTION

    .PARAMETER remainingArgs
    A catch all that lets you specify arbitrary parameters to the build engine.
    Useful if you wish to override some property but that property is not exposed as a first class concept.

#>
function Global:Invoke-Build2
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
                
    [Aderant.Build.Context]$context = Get-BuildContext -CreateIfNeeded    
    if ($context -eq $null) {
        $context = New-BuildContext
    }

    $context.BuildSystemDirectory = "$PSScriptRoot\..\..\..\"

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

    function CreateArgumentStringForContext($context) {
         $set = [System.Collections.Generic.HashSet[string]]::new()          

        if ($context.BuildMetadata -ne $null) {
            if ($context.BuildMetadata.DebugLoggingEnabled) {
                $set.Add("/v:diag") | Out-Null
            }

            if ($context.BuildMetadata.IsPullRequest) {
                $set.Add("/v:diag")| Out-Null
            }
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

        return [string]::Join(" ", $set)
    }
    
    [string]$repositoryPath = $null

    if (-not [string]::IsNullOrEmpty($ModulePath)) {
        $repositoryPath = $ModulePath
    } else {
        $repositoryPath = $ShellContext.CurrentModulePath
    }

    $context.StartedAt = [DateTime]::UtcNow
    $contextFileName = Publish-BuildContext $context

    $args = CreateArgumentStringForContext $context

    $passThruArgs = ""
    if ($RemainingArgs) {
        $passThruArgs = [string]::Join(" ", $RemainingArgs)
    }

    Run-MSBuild "$($context.BuildScriptsDirectory)\ComboBuild.targets" "/target:BuildAndPackage /p:ContextFileName=$contextFileName $args $passThruArgs"
 
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