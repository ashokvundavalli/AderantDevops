function Global:Invoke-Build
{
    [CmdletBinding(DefaultParameterSetName="Build")]
    param (
        [Parameter(ParameterSetName="Build")]
        [switch]$changes,

        [Parameter(ParameterSetName="Build")]
        [switch]$branch,

        [Parameter()]
        [switch]$everything,

        [Parameter()]
        [switch]$downstream,

        [Parameter()]
        [switch]$transitive,
        
        [Parameter()]
        [switch]$clean,

        [Parameter()]
        [switch]$release,

        [Parameter()]
        [switch]$package,

        #[Parameter]
        #[switch]$integration,

        #[Parameter]
        #[switch]$automation,

        [Parameter]
        [switch]$DisplayCodeCoverage,
                
        [Parameter(ParameterSetName="Build", Mandatory=$false)]        
        [string]$ModulePath = ""
    )

    begin {        
        Set-StrictMode -Version Latest
        $ErrorActionPreference = "Stop"
                
        [Aderant.Build.Context]$context = Get-BuildContext
        $switches = $context.Switches 

        $switches.Everything = $everything.IsPresent
        $switches.Downstream = $downstream.IsPresent
        $switches.Transitive = $transitive.IsPresent
        $switches.Clean = $clean.IsPresent
        $switches.Release = $release.IsPresent

        $context.Switches = $switches
    }

    process {
        $service = $context.GetService("Aderant.Build.Services.IFileSystem")

        [string]$repositoryPath = $null

        #if (-not [string]::IsNullOrEmpty($modulePath)){
        #    $repositoryPath = $modulePath
        #} elseif(-not [string]::IsNullOrEmpty($global:CurrentModulePath)) {
        #    $repositoryPath = $global:CurrentModulePath

        #    if (-not [string]::IsNullOrEmpty($moduleName)){
        #        if (((Test-Path "$repositoryPath\.git") -eq $false) -and ((Test-Path "$repositoryPath\..\.git") -eq $true)) {
        #            $repositoryPath = $(Resolve-Path "$repositoryPath\..\$moduleName")
        #        }

        #        if (Test-Path "$repositoryPath\$moduleName"){
        #            $repositoryPath = $(Resolve-Path $moduleName)
        #        }
        #    }
        #} else {
        #    Write-Error 'No valid module path supplied.'
        #    return
        #}

        if (-not [string]::IsNullOrEmpty($modulePath)) {
            $repositoryPath = $modulePath
        } else {
            $repositoryPath = $ShellContext.CurrentModulePath
        }

        $contextFileName = Publish-BuildContext $context

        $builder = $context.CreateArgumentBuilder("MSBuild")

        Run-MSBuild "$($context.BuildScriptsDirectory)\Aderant.ComboBuild.targets" "/target:BuildAndPackage /p:ContextFileName=$contextFileName"
    }

    end {
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
}