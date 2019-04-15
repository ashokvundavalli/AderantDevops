$root = $PSScriptRoot
if (-not $root) {
    $root = $env:TEMP
}

# Should a human run this script, try not to nuke their environment
$isDesktop = $Env:COMPUTERNAME.StartsWith("WS") -or [System.Environment]::UserInteractive

Set-Location $root
Microsoft.PowerShell.Host\Start-Transcript -Path ".\CleanupAgentHostLog.txt" -Force

Set-StrictMode -Version Latest

$appDataFolders = @(
    # Some unit tests aren't very unitty
    "Aderant",

    # NuGet junk drawer
    "NuGet",
    ".nuget",

    # Shadow copy cache
    "assembly",

    "Temp",

    "Microsoft\VisualStudio\12.0Exp\Extensions\Aderant",
    "Microsoft\VisualStudio\14.0Exp\Extensions\Aderant",
    "Microsoft\VisualStudio\15.0Exp\Extensions\Aderant",

    # Browser and INET stack cache
    "Microsoft\Windows\INetCache"
)

$userProfileFolders = @(
    "Downloads",
    ".nuget"
)

$machineWideDirectories = @(
    $env:TEMP,
    "C:\Temp",
    "C:\Windows\Temp",
    "C:\UIAutomation",

    ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory() + "Temporary ASP.NET Files"),

    "$Env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files"
)


if ($null -eq $env:AgentPool -or $env:AgentPool -eq 'Default') {
    $results = Get-PSDrive -PSProvider FileSystem  | Where-Object { $null -eq $_.DisplayRoot -or -not $_.DisplayRoot.StartsWith("\\") } | ForEach-Object { [System.IO.Path]::Combine($_.Root, "ExpertShare") }
    $machineWideDirectories += $results

    $defaultAgentDirectories = @(
        "C:\CMS.net",

        # Yay for checked in PostBuild events :)
        "C:\ExpertShare",
        "C:\tfs\"
    )

    $machineWideDirectories += $defaultAgentDirectories
}

function RemoveFolder([string]$removeTarget) {
    if (Test-Path $removeTarget) {
        Write-Information "Deleting files under $removeTarget"

        Remove-Item $removeTarget -Verbose -Force -Recurse -ErrorAction SilentlyContinue
    } else {
        Write-Information "Not deleting $removeTarget"
    }
}

$currentUserDirectory = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::UserProfile)
$profileHome = [System.IO.Directory]::GetParent($currentUserDirectory)

$serviceAccounts = @("$env:USERNAME", "service.tfsbuild.ap", "tfsbuildservice$", "ExpertService$")
foreach ($name in $serviceAccounts) {
    foreach ($folder in $appDataFolders) {
        RemoveFolder ([System.IO.Path]::Combine($profileHome, $name, "AppData\Local", $folder))
        RemoveFolder ([System.IO.Path]::Combine($profileHome, $name, "AppData\Roaming", $folder))
    }

    foreach ($folder in $userProfileFolders) {
        RemoveFolder ([System.IO.Path]::Combine($profileHome, $name, $folder))
    }
}

if (-not $isDesktop) {
    foreach ($dir in $machineWideDirectories) {
        if (Test-Path $dir) {
            RemoveFolder $dir
        }
    }

    # Clean up databases
    if ($null -eq $env:AgentPool -or $env:AgentPool.Equals('Default')) {
        Import-Module SqlServer
        Set-Location sqlserver:\
        Set-Location sql\localhost\default\databases
        Get-ChildItem | ForEach-Object { $_.DropBackupHistory(); $_.Drop() }
    }
}

Set-Location $root
Microsoft.PowerShell.Host\Stop-Transcript