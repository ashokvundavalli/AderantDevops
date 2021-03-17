#Requires -Version 5.1

function CheckModuleVersion {
    # Check for PackageManagement 1.0.0.0
    Import-Module PackageManagement
    $packageManagerVerion = (Get-Module PackageManagement).Version
    if (!$packageManagerVerion) {
        Write-Warning "PackageManagement not detected, please install PackageManagement ver. 1.0.0.1 or later"
        return $false
    }
    if ($packageManagerVerion.ToString().Equals("1.0.0.0")) {
        Write-Warning "PackageManagement Version 1.0.0.0 detected - this version is buggy and may prevent the installation of tools which enhance the developer experience. If you have issues installing tools such as posh-git using Install-Module you can try replacing the version of PackageManagement in C:\Program Files (x86)\WindowsPowerShell\Modules with a newer version from another machine"
        return $false
    }
    return $true
}

function global:Install-PoshGit {
    [CmdletBinding()]
    param(
        [Aderant.Build.BuildOperationContext]
        $Context = (Get-BuildContext -CreateIfNeeded)
    )

    if (-not [System.Environment]::Is64BitProcess) {
        return
    }

    Set-StrictMode -Version 'Latest'
    Use-CallerPreference -Cmdlet $PSCmdlet -SessionState $ExecutionContext.SessionState

    if (-not ($Context.IsDesktopBuild)) {
        Write-Debug "Install-PoshGit skipped - not a desktop"
        return
    }

    if (-not (CheckModuleVersion)) {
        Write-Debug "Install-PoshGit skipped - Buggy PackageManagement infrastructure present"
        return
    }

    try {
        $modules = Get-Module -Name 'posh-git' -ListAvailable

        if ($null -ne $modules) {
            [bool]$uninstalled = $false

            foreach ($module in $modules) {
                # Check if the version of posh-git is recent.
                if ($module.Version.Major -lt 1) {
                    Write-Debug -Message 'Updating posh-git PowerShell module to latest version.'
                    $module | Uninstall-Module -Force
                    $uninstalled = $true
                }
            }

            if ($uninstalled -and -not (Get-Module -Name 'posh-git' -ListAvailable)) {
                # No usable version of posh-git available.
                Install-Module -Name 'posh-git' -Scope CurrentUser
            }
        } else {
            Install-Module -Name 'posh-git' -Scope CurrentUser
        }
    } finally {
        Import-Module -Name 'posh-git' -Global
        $global:ShellContext.PoshGitAvailable = $null -ne (Get-Module -Name 'posh-git')
    }
}