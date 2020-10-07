<#
.Synopsis
    Verify that contents of the patch are created correctly.
.Description
    Verify the contents of the patch by checking that commit,PR,changesets are pointing to correct iteration and more.
.Example
        Test-PatchItems
    Verifies the contents based on your current branch and PatchingManifest.xml in your Build.Infrastructure.
#>
function Test-PatchItems() {
    param (
        [switch]$noget,
        [Parameter(Mandatory=$false)][string]$repositoryPatchBranch
    )
    if (-not $noget.IsPresent) {
        New-Item -Path "$($global:ShellContext.PackageScriptsDirectory)\Patching" -ItemType Directory -Force | Out-Null
        Invoke-Expression "xcopy \\dfs.aderant.com\expertsuite\Main\Build.Tools\Current\Patching\* /S /Y $($global:ShellContext.PackageScriptsDirectory)\Patching"
    }

    & "$($global:ShellContext.PackageScriptsDirectory)\Patching\TestPatch.ps1" -repositoryPatchBranch $repositoryPatchBranch
}