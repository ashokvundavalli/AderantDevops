[cmdletbinding()]

[bool]$limitBuildWarnings = Get-VstsInput -Name 'limitBuildWarnings' -AsBool

$ErrorActionPreference = 'Stop'

& $Env:EXPERT_BUILD_DIRECTORY\Build\Invoke-Build.ps1 -Task "PostBuild" -File $Env:EXPERT_BUILD_DIRECTORY\Build\BuildProcess.ps1 -Repository $Env:BUILD_SOURCESDIRECTORY -LimitBuildWarnings:$limitBuildWarnings