Set-StrictMode -Version Latest

Enum BuildType {
    Staged
    Branch
    All
}

function global:Invoke-Build {
    [CmdletBinding()]
    param (
        [Parameter(Position=0)]
        [Parameter(ParameterSetName="BuildStaged")]
        [switch]$staged,

        [Parameter(Position=1)]
        [Parameter(ParameterSetName="BuildStaged")]
        [switch]$downstream,

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
        [Parameter(ParameterSetName="BuildStaged")]
        [Parameter(ParameterSetName="BuildBranch")]
        [Parameter(ParameterSetName="ModulePath")]
        [ValidateNotNullOrEmpty()]
        [string]$modulePath = "",

        [Parameter(Position=10)]
        [Parameter(ParameterSetName="BuildStaged")]
        [Parameter(ParameterSetName="BuildBranch")]
        [Parameter(ParameterSetName="ModuleName")]
        [ValidateNotNullOrEmpty()]
        [string]$moduleName = ""
    )

    begin {
        . $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1

        [BuildType]$buildType = $null

        switch (true) {
            ($staged.IsPresent) {
                $buildType = [BuildType]::Staged
            }
            ($all.IsPresent) {
                $buildType = [BuildType]::All
            }
            default {
                $buildType = [BuildType]::Branch
            }
        }
    }

    process {
        [string]$flavor = "Debug"

        if ($release.IsPresent) {
            $flavor = "Release"
        }

        Write-Host "Forcing BuildFlavor to be $($flavor.ToUpper())" -ForegroundColor DarkGreen

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

        & $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "$task" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $repositoryPath -ModuleName $moduleName -Clean:$clean.ToBool() -Flavor:$flavor -Integration:$integration.ToBool() -Automation:$automation.ToBool() -SkipPackage:$skipPackage -BuildType ($buildType) -Downstream:$downstream.ToBool()
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