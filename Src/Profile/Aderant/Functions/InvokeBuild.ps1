function global:Invoke-Build {
    [CmdletBinding(DefaultParameterSetName="Build")]
    param (
        [Parameter(ParameterSetName="Build", Position=0)]
        [switch]$changed,

        [Parameter(ParameterSetName="Build", Position=1)]
        [switch]$branch,

        [Parameter(Position=2)]
        [Parameter(ParameterSetName="BuildAll")]
        [switch]$all,

        [Parameter(ParameterSetName="Build", Mandatory=$false, Position=3)]
        [ValidateSet('Direct', 'All', 'None')]
        [DownStreamType]$downStreamType = [DownStreamType]::All,

        [Parameter(Position=4)]
        [switch]$release,

        [Parameter(Position=5)]
        [switch]$clean,

        [Parameter(Position=6)]
        [switch]$package,

        [Parameter(Position=7)]
        [switch]$integration,

        [Parameter(Position=8)]
        [switch]$automation,

        [Parameter(Position=9)]
        [switch]$displayCodeCoverage,

        [Parameter(Position=10)]
        [Parameter(ParameterSetName="Build", Mandatory=$false)]
        [ValidateNotNullOrEmpty()]
        [string]$modulePath = '',

        [Parameter(Position=11)]
        [Parameter(ParameterSetName="Build", Mandatory=$false)]
        [ValidateNotNullOrEmpty()]
        [string]$moduleName = ''
    )

    begin {
        Set-StrictMode -Version Latest

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

        # Get build context
        [Aderant.Build.Context]$context = New-BuildContext

        [ComboBuildType]$comboBuildType = [ComboBuildType]::All

        switch ($true) {
            $changed.IsPresent {
                $comboBuildType = [ComboBuildType]::Changed
                break
            }
            $branch.IsPresent {
                $comboBuildType = [ComboBuildType]::Branch
                break
            }
        }

        $context.ComboBuildType = $comboBuildType

        if ($comboBuildType -ne [ComboBuildType]::All) {
            $context.DownStreamType = $downStreamType
        }

        Write-Host "Combo Build Type: $comboBuildType, downstream search type: $downStreamType" -ForegroundColor DarkGreen

        [string]$flavor = "Debug"

        if ($release.IsPresent) {
            $flavor = "Release"
        }

        Write-Host "Forcing BuildFlavor to: $($flavor.ToUpper())" -ForegroundColor DarkGreen
    }

    process {
        [string]$repositoryPath = ''

        if (-not [string]::IsNullOrEmpty($modulePath)){
            $repositoryPath = $modulePath
        } elseif(-not [string]::IsNullOrEmpty($global:CurrentModulePath)) {
            $repositoryPath = $global:CurrentModulePath

            if (-not [string]::IsNullOrEmpty($moduleName)){
                if (((Test-Path "$repositoryPath\.git") -eq $false) -and ((Test-Path "$repositoryPath\..\.git") -eq $true)) {
                    $repositoryPath = $(Resolve-Path "$repositoryPath\..\$moduleName")
                }

                if (Test-Path "$repositoryPath\$moduleName"){
                    $repositoryPath = $(Resolve-Path $moduleName)
                }
            }
        } else {
            Write-Error 'No valid module path supplied.'
            return
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