function Install-PoshGit()
{
    [CmdletBinding()]
    param(
        [Aderant.Build.BuildOperationContext]       
        $Context = (Get-BuildContext)
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

    # We need Windows 10 or WMF 5 for Install-Module
    if ($host.Version.Major -ge 5) {
        try {
            if (Test-Path $Env:USERPROFILE\Documents\WindowsPowerShell\Modules\posh-git) {
                Import-Module posh-git -Global
                Write-Debug "PoshGit already installed"
                return
            }

            # Optimization, Get-InstalledModule is quite slow so just peek directly
            if (Test-Path $Env:ProgramFiles\WindowsPowerShell\Modules\posh-git) {
                Import-Module posh-git -Global
                Write-Debug "PoshGit already installed"
                return
            }
    
            if (-not (Get-InstalledModule posh-git -ErrorAction SilentlyContinue)) {
                Install-Module posh-git -Scope CurrentUser
            }            
        } finally {
            Import-Module posh-git -Global            
            $global:GitPromptSettings.EnableWindowTitle = $false            
            $ShellContext.PoshGitAvailable = (Get-Module posh-git) -ne $null
        }
    } else {
        Write-Host "You do not have Windows 10 or PowerShell 5. Windows 10 provides a much improved PowerShell experience." -ForegroundColor Yellow
    }
}


function CheckModuleVersion()
{
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