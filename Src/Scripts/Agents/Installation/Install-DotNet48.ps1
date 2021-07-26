#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs .Net Framework 4.8.
.DESCRIPTION
    Downloads and installs the .Net Framework 4.8.
.PARAMETER Restart
    Optional switch parameter. If used, the machine will restart after .Net Framework 4.8 is installed.
#>
[CmdletBinding()]
Param (
    [switch]
    $Restart
)

begin {
    Set-StrictMode -Version 'Latest'
    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'

    [string]$dotNet48OfflineInstallerUrl = "https://go.microsoft.com/fwlink/?linkid=2088631"
    [string]$dotNet48Installer = Join-Path -Path $Env:TEMP -ChildPath ([System.IO.Path]::GetRandomFileName())
    Write-Verbose -Message "Configuration"
    Write-Verbose -Message "dotNet48OfflineInstallerUrl: $dotNet48OfflineInstallerUrl"
    Write-Verbose -Message "dotNet48Installer: $dotNet48Installer"
}

process {
    Write-Information -MessageData 'Checking the installation status of .Net 4.8.'

    if ((Get-ChildItem -Path 'HKLM:SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\' | Get-ItemPropertyValue -Name 'Release') -ge 528040) {
        Write-Information -MessageData ".Net 4.8 (or higher) is already installed."
        return
    }

    Write-Information -MessageData "Downloading the .Net 4.8 installer."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $dotNet48OfflineInstallerUrl -OutFile $dotNet48Installer

    Write-Information -MessageData "Installing .Net 4.8."

    $process = $null

    try {
        $installerArgs = @('/q', '/norestart')
        $process = Start-Process -FilePath $dotNet48Installer -installerArgs $installerArgs -PassThru -NoNewWindow -Wait
        if ($process.ExitCode -ne 0) {
            Write-Error "Dot net installer exited with code: $($process.ExitCode)"
        } else {
            Write-Information -MessageData '.Net Framework 4.8 was installed successfully.'
        }
    } finally {
        if ($null -ne $process) {
            Write-Verbose -Message "Disposing of the installer from memory."
            $process.Dispose()
            $process = $null
        }

        if (Test-Path -Path $dotNet48Installer) {
            Write-Verbose -Message "Removing the installer from disk: $dotNet48Installer."
            Remove-Item -Path $dotNet48Installer -Force
        }
    }

    if ($Restart.IsPresent) {
        Write-Verbose -Message "Restarting $Env:ComputerName."
        Restart-Computer -Force
    } else {
        Write-Warning "You MUST restart $Env:ComputerName in order to use .Net Framework 4.8."
    }
}