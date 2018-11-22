Start-Transcript -Path ".\CleanupAgentHostLog.txt" -Force

$directoriesToRemove = @(
    # Some unit tests aren't very unit-ty
    "$env:APPDATA\Aderant",
    "$env:LOCALAPPDATA\Aderant",

    # NuGet junk drawer
    "$env:APPDATA\NuGet",
    "$env:LOCALAPPDATA\NuGet",
    "$env:USERPROFILE\.nuget",

    # Shadow copy cache
    "$env:LOCALAPPDATA\assembly",

    "$env:USERPROFILE\Download",

    # VSIX extensions installed by the VS SDK targets
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\12.0Exp\Extensions\Aderant",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\14.0Exp\Extensions\Aderant",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\15.0Exp\Extensions\Aderant",

    $env:TEMP,

    # Browser and INET stack cache
    "$env:LOCALAPPDATA\Microsoft\Windows\INetCache"
)

$machineWideDirectories = @(
    "C:\Temp",
    "C:\Windows\Temp", 
    
    ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory() + "Temporary ASP.NET Files"),
        
    "$Env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files"
)

$whoAmI = $env:USERNAME
$serviceAccounts = @("$env:USERNAME", "service.tfsbuild.ap", "tfsbuildservice$")

foreach ($dir in $directoriesToRemove) {
    $removeTarget = $dir

    foreach ($name in $serviceAccounts) {
        $removeTarget = $removeTarget.Replace($whoAmI, $name)

        if (Test-Path $removeTarget) {
            Write-Output "Deleting files under $removeTarget"
            Remove-Item $removeTarget -Verbose -Force -Recurse -ErrorAction SilentlyContinue
        } else {
            Write-Output "Not deleting $removeTarget"
        }
    }
}

# Should a human run this script, don't nuke their environment
if (-not [System.Environment]::UserInteractive) {
    Get-PSDrive -PSProvider FileSystem | Select-Object -Property Root | ForEach-Object {$machineWideDirectories += $_.Root + "ExpertShare"}

    # Yay for people who check in PostBuild events :)
    machineWideDirectories += "C:\tfs"
}

foreach ($dir in $machineWideDirectories) {
    if (Test-Path $dir) {
        Push-Location $dir
        Write-Output "Deleting files under $dir"
        Remove-Item * -Verbose -Force -Recurse -ErrorAction SilentlyContinue
        Pop-Location
    }
}

# Clean up databases
if ($null -eq $env:AgentPool -or $env:AgentPool.Equals('Default')) {
    Import-Module SqlServer
    Set-Location sqlserver:\
    Set-Location sql\localhost\default\databases
    Get-ChildItem | ForEach-Object { $_.DropBackupHistory(); $_.Drop() }
}