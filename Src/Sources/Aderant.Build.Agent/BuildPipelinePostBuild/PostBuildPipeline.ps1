[CmdletBinding()]
$ErrorActionPreference = 'Stop'

[bool]$limitBuildWarnings = Get-VstsInput -Name 'limitBuildWarnings' -AsBool
[bool]$cleanBuildDirectoryOnCompletion = Get-VstsInput -Name 'cleanBuildDirectoryOnCompletion' -AsBool

& $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "PostBuild" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $Env:BUILD_SOURCESDIRECTORY -LimitBuildWarnings:$limitBuildWarnings

if ($cleanBuildDirectoryOnCompletion) {
    try {
        Push-Location $Env:BUILD_SOURCESDIRECTORY
        & "git" "clean" "-fdx"
    } finally {
        Pop-Location
    }
}