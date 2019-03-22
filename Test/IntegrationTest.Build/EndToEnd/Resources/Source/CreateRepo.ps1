Set-StrictMode -Version 'Latest'
if ($null -eq $DeploymentItemsDirectory) {
    throw '$DeploymentItemsDirectory not defined'
}

Set-Location $DeploymentItemsDirectory

Write-Information "PSScriptRoot: $PSScriptRoot"
Write-Information "DeploymentItemsDirectory: $DeploymentItemsDirectory"
Write-Information ("Current Directory: " + ([System.Environment]::CurrentDirectory))

if (-not (Test-Path $DeploymentItemsDirectory)) {
    Write-Error "Directory $DeploymentItemsDirectory does not exist"
    return
}

& git init

Add-Content -Path ".gitignore" -value @"
[Bb]in/
[Oo]bj/
"@

& git add .
& git add ModuleA/Build/TFSBuild.rsp -f
& git add ModuleB/Build/TFSBuild.rsp -f
& git commit -m "Added all files"
