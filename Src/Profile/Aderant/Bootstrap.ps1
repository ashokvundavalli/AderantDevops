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

# We can't use $PSScriptRoot here until we move to Git and get rid of symlinks
$actualPath = GetSymbolicLinkTarget (Split-Path -Parent $MyInvocation.MyCommand.Definition)   

$shellProperties = New-Object -TypeName PSObject
$shellProperties | Add-Member -MemberType ScriptProperty -Name BuildScriptsDirectory -Value { [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($actualPath, "..\..\Build")) }
$shellProperties | Add-Member -MemberType ScriptProperty -Name BuildToolsDirectory -Value { [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($actualPath, "..\..\Build.Tools")) }
$shellProperties | Add-Member -MemberType ScriptProperty -Name PackagingTool -Value { [System.IO.Path]::Combine($This.BuildScriptsDirectory, "paket.exe") }

Write-Debug $shellProperties