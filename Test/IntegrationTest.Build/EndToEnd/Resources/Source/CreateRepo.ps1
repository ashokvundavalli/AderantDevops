Set-StrictMode -Version 'Latest'
if ($null -eq $DeploymentItemsDirectory) {
    throw '$DeploymentItemsDirectory not defined'
}

Write-Information "PSScriptRoot: $PSScriptRoot"
Write-Information "DeploymentItemsDirectory: $DeploymentItemsDirectory"
Write-Information ("Current Directory: " + ([System.Environment]::CurrentDirectory))

& git -C $DeploymentItemsDirectory init

Add-Content -LiteralPath "$DeploymentItemsDirectory\.gitignore" -value @"
[Bb]in/
[Oo]bj/
"@

& git -C $DeploymentItemsDirectory add .
& git -C $DeploymentItemsDirectory add ModuleA/Build/TFSBuild.rsp -f
& git -C $DeploymentItemsDirectory add ModuleB/Build/TFSBuild.rsp -f
& git -C $DeploymentItemsDirectory commit -m "Added all files"
