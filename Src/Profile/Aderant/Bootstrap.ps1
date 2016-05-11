$DebugPreference = "Continue"

function Load([string]$currentPath) {
    # Load the build libraries as this has our shared compile function. This function is shared by the desktop and server bootstrap of Build.Infrastructure
    $buildScripts = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($currentPath, "..\..\Build"));

    if (-not (Test-Path $buildScripts)) {
        throw "Cannot find directory: $buildScripts"
        return
    }

    Write-Debug "Build scripts: $buildScripts"

    pushd $buildScripts
    Invoke-Expression ". .\Build-Libraries.ps1"
    popd	       

    CompileAndLoad $buildScripts
}

$p = $MyInvocation.MyCommand.Definition
$currentPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
Load $currentPath     