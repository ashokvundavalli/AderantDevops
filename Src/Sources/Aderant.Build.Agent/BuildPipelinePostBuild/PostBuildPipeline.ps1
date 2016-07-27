[cmdletbinding()]

$ErrorActionPreference = 'Stop'

& $Env:EXPERT_BUILD_FOLDER\Build\Invoke-Build.ps1 -Task "PostBuild" -File $Env:EXPERT_BUILD_FOLDER\Build\BuildProcess.ps1 -Repository $Env:BUILD_SOURCESDIRECTORY