#Requires -RunAsAdministrator

Set-StrictMode -Version Latest

$InformationPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

$root = $PSScriptRoot
if (-not $root) {
    $root = $env:TEMP
}

# Should a human run this script, try not to nuke their environment
$isDesktop = $Env:COMPUTERNAME.StartsWith("WS") -or [System.Environment]::UserInteractive

$appDataFolders = @(
    # Some unit tests aren't very unit-y
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
    $Env:TEMP,
    "C:\Temp",
    "C:\Windows\Temp",
    "C:\UIAutomation",
    (Join-Path -Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) -ChildPath 'Temporary ASP.NET Files')
)

function RemoveFolder([string]$removeTarget) {
    if (Test-Path -Path $removeTarget) {
        Write-Information "Deleting files under $removeTarget"

        Push-Location $removeTarget
        Remove-Item * -Verbose -Force -Recurse -ErrorAction SilentlyContinue
        Pop-Location
    } else {
        Write-Information "Not deleting '$removeTarget' because the path did not exist."
    }
}


# Bug fix for when the transcript went to the wrong folder
Remove-Item "$PSScriptRoot\CleanupAgentHostLog.txt" -Verbose
Remove-Item "C:\Scripts\Build.Infrastructure\Src\Scripts\CleanupAgentHostLog.txt" -Verbose


try {
    Set-Location $root

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

        # Clean up databases.
        if ($null -eq $Env:AgentPool -or $Env:AgentPool.Equals('Default') -or $Env:AgentPool.Equals('Database')) {
            if (-not (Get-Module -Name 'SqlServer' -ListAvailable)) {
                # Install the Microsoft SqlServer module if it is not installed.
                Install-Module -Name 'SqlServer' -Scope AllUsers -AllowClobber -Force
            }

            if (-not (Get-Module -Name 'SqlServer')) {
                # Import the Microsoft SqlServer module.
                Import-Module -Name 'SqlServer'
            }

            Get-ChildItem -Path 'SQLSERVER:\sql\localhost\default\databases' | ForEach-Object {
                $_.DropBackupHistory()
                $_.Drop()
            }
        }
    }

    try {
        # Clear the CCM cache
        $resman = new-object -com "UIResource.UIResourceMgr"
        $cacheInfo= $resman.GetCacheInfo()
        $cacheinfo.GetCacheElements() | foreach-object {$cacheInfo.DeleteCacheElement($_.CacheElementID)}
    } catch {
        # The COM object may not exist
    }

    # Trash any Deployment Manager machine wide settings
    Remove-Item -Path "HKLM:\SOFTWARE\Aderant\Development\AENC2" -Force -Verbose -ErrorAction SilentlyContinue
    Remove-Item -Path "HKLM:\SOFTWARE\Aderant\Security\AENC1" -Force -Verbose -ErrorAction SilentlyContinue
    Remove-Item -Path "HKLM:\SOFTWARE\Aderant\Security\AENC2" -Force -Verbose -ErrorAction SilentlyContinue
    Remove-Item -Path "HKLM:\SOFTWARE\Handshake" -Force -Verbose -ErrorAction SilentlyContinue
} finally {
    Set-Location -Path $root
}