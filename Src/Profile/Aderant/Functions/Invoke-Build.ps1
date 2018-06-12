function Global:Invoke-Build
{
    [CmdletBinding(DefaultParameterSetName="Build")]
    param (
        [Parameter(ParameterSetName="Build")]
        [switch]$Changes,

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
                
        [Aderant.Build.Context]$context = New-BuildContext
        $switches = $context.Switches 

        $switches.Everything = $Everything.IsPresent
        $switches.Downstream = $Downstream.IsPresent
        $switches.Transitive = $Transitive.IsPresent
        $switches.Clean = $Clean.IsPresent
        $switches.Release = $Release.IsPresent

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
            $repositorypath = $context.GetCurrentRepository()
        }

        [bool]$skipPackage = $false

        if ((Test-Path "$repositoryPath\.git") -eq $false) {
            $skipPackage = ([System.IO.Directory]::GetFiles($repositoryPath, "*.paket.template", [System.IO.SearchOption]::TopDirectoryOnly)).Length -eq 0
        }

        [string]$task = ""

        if ($package -and -not $skipPackage) {
            $task = "Package"
        }
      
        & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $repositoryPath -Clean:$clean.ToBool() -Flavor:$flavor -Integration:$integration.ToBool() -Automation:$automation.ToBool() -SkipPackage:$skipPackage -ComboBuildType $comboBuildType -DownstreamType $downStreamType
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