#Requires -Version 5.1

function global:Install-PoshGit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNull()]
        [Version]$Version
    )

    begin {
        Set-StrictMode -Version 'Latest'

        function CheckModuleVersion {
            # Check for PackageManagement 1.0.0.0
            [System.Version]$minimumRequiredVersion = [System.Version]::new(1, 0, 0, 1)

            $packageManagementModule = Get-Module -Name 'PackageManagement'
            
            if ($null -ne $packageManagementModule) {
                if ($packageManagementModule.Version -lt $minimumRequiredVersion) {
                    # Remove questionable version of PackageManagement module and attempt to find a suitable version.
                    $packageManagementModule | Remove-Module
                } else {
                    # Acceptable version of PackageManagement module already imported.
                    return $true
                }
            }

            $packageManagementModules = Get-Module -Name 'PackageManagement' -ListAvailable

            if ($null -eq $packageManagementModules) {
                Write-Warning 'PackageManagement module not installed. Please download and install the latest version from: https://www.powershellgallery.com/packages/PackageManagement'
                return $false
            }

            foreach ($module in $packageManagementModules) {
                if ($module.Version -ge $minimumRequiredVersion) {
                    $module | Import-Module
                    return $true
                }
            }           

            Write-Warning -Message 'PackageManagement version 1.0.0.0 or lower detected. Please download and install the latest version from: https://www.powershellgallery.com/packages/PackageManagement'
            return $false
        }
    }

    process {
        Use-CallerPreference -Cmdlet $PSCmdlet -SessionState $ExecutionContext.SessionState

        if (-not (CheckModuleVersion)) {
            Write-Warning -Message 'Install-PoshGit skipped - Required PackageManagement module not present.'
            return
        }

        try {
            $modules = Get-Module -Name 'posh-git' -ListAvailable

            if ($null -ne $modules) {
                [bool]$uninstalled = $false

                foreach ($module in $modules) {
                    # Check if the version of posh-git is recent.
                    if ($module.Version -ne $Version) {
                        Write-Information -MessageData "Uninstalling posh-git as it does not match required version: $($Version.ToString())."
                        $module | Uninstall-Module -Force
                        $uninstalled = $true
                    }
                }

                if ($uninstalled -and -not (Get-Module -Name 'posh-git' -ListAvailable)) {
                    # No usable version of posh-git available.
                    Write-Information -MessageData "Installing posh-git version $($Version.ToString())."
                    Install-Module -Name 'posh-git' -RequiredVersion $Version.ToString() -Scope CurrentUser -Force
                }
            } else {
                Write-Information -MessageData "Installing posh-git version $($Version.ToString())."
                Install-Module -Name 'posh-git' -RequiredVersion $Version.ToString() -Scope CurrentUser -Force
            }
        } finally {
            Import-Module -Name 'posh-git'
            $global:ShellContext.PoshGitAvailable = $null -ne (Get-Module -Name 'posh-git')
        }
    }
}