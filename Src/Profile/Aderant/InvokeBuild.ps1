Set-StrictMode -Version Latest

function global:Invoke-Build {
    [CmdletBinding(DefaultParameterSetName="Build")]
    param (
        [Parameter(Position=0)]
        [Parameter(ParameterSetName="Build")]
        [ValidateSet("Changed", "Branch", "All")]
        [ComboBuildType]$comboBuildType = [ComboBuildType]::Branch,

        [Parameter(Position=1)]
        [Parameter(ParameterSetName="Build")]
        [ValidateSet("Direct", "All", "None")]
        [DownStreamType]$downStreamType = [DownStreamType]::All,

        [Parameter(Position=2)]
        [Parameter(ParameterSetName="BuildAll")]
        [switch]$all,

        [Parameter(Position=3)]
        [switch]$release,

        [Parameter(Position=4)]
        [switch]$clean,

        [Parameter(Position=5)]
        [switch]$package,

        [Parameter(Position=6)]
        [switch]$integration,

        [Parameter(Position=7)]
        [switch]$automation,

        [Parameter(Position=8)]
        [switch]$displayCodeCoverage,

        [Parameter(Position=9)]
        [Parameter(ParameterSetName="Build")]
        [Parameter(ParameterSetName="ModulePath")]
        [ValidateNotNullOrEmpty()]
        [string]$modulePath = "",

        [Parameter(Position=10)]
        [Parameter(ParameterSetName="Build")]
        [Parameter(ParameterSetName="ModuleName")]
        [ValidateNotNullOrEmpty()]
        [string]$moduleName = ""
    )

    begin {
        Enum ComboBuildType {
              Changed
              Branch
              All
        }
        Enum DownStreamType {
              Direct
              All
              None
        }

        Write-Host "Combo Build Type: $comboBuildType, downstream search type: $downStreamType " -ForegroundColor DarkGreen

        [string]$flavor = "Debug"

        if ($release.IsPresent) {
            $flavor = "Release"
        }

        Write-Host "Forcing BuildFlavor to be $($flavor.ToUpper())" -ForegroundColor DarkGreen
    }

    process {
        if (-not [string]::IsNullOrEmpty($modulePath)){
            $repositoryPath = $modulePath
        } else {
            $repositoryPath = $global:CurrentModulePath

            if (-not [string]::IsNullOrEmpty($moduleName)){
                if (((Test-Path "$repositoryPath\.git") -eq $false) -and ((Test-Path "$repositoryPath\..\.git") -eq $true)) {
                    $repositoryPath = $(Resolve-Path "$repositoryPath\..\$moduleName")
                }

                if (Test-Path "$repositoryPath\$moduleName"){
                    $repositoryPath = $(Resolve-Path $moduleName)
                }
            }
        }

        [bool]$skipPackage = $false

        if ((Test-Path "$repositoryPath\.git") -eq $false) {
            $skipPackage = ([System.IO.Directory]::GetFiles($repositoryPath, "*.paket.template", [System.IO.SearchOption]::TopDirectoryOnly)).Length -eq 0
        }

        [string]$task = ""

        if ($package -and -not $skipPackage) {
            $task = "Package"
        }
      
        & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $repositoryPath -ModuleName $moduleName -Clean:$clean.ToBool() -Flavor:$flavor -Integration:$integration.ToBool() -Automation:$automation.ToBool() -SkipPackage:$skipPackage -ComboBuildType $comboBuildType -DownstreamType $downStreamType
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