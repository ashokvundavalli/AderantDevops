<#
.Synopsis
    Builds a patch for the current branch.
.Description
    Builds a patch for the current branch. Driven from the PatchingManifest.xml.
.Example
        Get-ProductZip; Build-ExpertPatch
    Gets the latest product zip from the build server then builds a patch using those binaries.
.Example
        Get-Product; Build-ExpertPatch
    Gets the latest product from the build server then builds a patch using those binaries.
.Example
        Build-ExpertPatch
    Builds the patch using the local binaries.
#>
function Build-ExpertPatch() {
    param (
        [switch]$noget,
        [switch]$noproduct,
        [switch]$Pre803,
        [switch]$noverify,
		[Parameter(Mandatory=$false)][string]$repositoryPatchBranch
    )

    if (-not $noproduct.IsPresent) {
        Get-ProductZip
    }

    [string]$cmd = "xcopy \\dfs.aderant.com\expertsuite\Main\Build.Tools\Current\* /S /Y $($global:ShellContext.PackageScriptsDirectory)"
    if ($Pre803.IsPresent) {
        $cmd = "xcopy \\dfs.aderant.com\expertsuite\Main\Build.Tools\Pre803\* /S /Y $($global:ShellContext.PackageScriptsDirectory)"
        if (-not $noget.IsPresent) {
            New-Item -Path "$($global:ShellContext.PackageScriptsDirectory)\Patching" -ItemType Directory -Force| Out-Null
            $cmd += "; xcopy \\dfs.aderant.com\expertsuite\Main\Build.Tools\Current\Patching\* /S /Y $($global:ShellContext.PackageScriptsDirectory)\Patching"
        }
    }

    if (-not $noget.IsPresent) {
        Invoke-Expression $cmd
    }

    & "$($global:ShellContext.PackageScriptsDirectory)\Patching\BuildPatch.ps1" -noVerify:$noverify.IsPresent -repositoryPatchBranch $repositoryPatchBranch
}