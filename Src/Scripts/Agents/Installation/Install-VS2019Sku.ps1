#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs the specified VS2019 Sku.
.DESCRIPTION
    Downloads the VS2019 Sku installer and runs it.
#>
[CmdletBinding()]
Param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$VsSku
)

begin {
    Set-StrictMode -Version 'Latest'
    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'
    $VerbosePreference = 'Continue'
    $ProgressPreference = 'SilentlyContinue'

    [string]$filePath = "$Env:TEMP\$VsSku"
    [string]$VsSkuURL = "https://aka.ms/vs/16/release/$VsSku"

    Write-Verbose -Message "Configuration"
    Write-Verbose -Message "filePath: $filePath"
    Write-Verbose -Message "VsSkuURL: $VsSkuURL"
}

process {
    Write-Information -MessageData "Downloading VS test agent."
    Invoke-WebRequest -Uri $VsSkuURL -OutFile $filePath

    Write-Information -MessageData "Installing $VsSku."

    $process = $null

    try {
        $arguments = ('--quiet' )
        $process = Start-Process -FilePath $filePath -ArgumentList $arguments -Wait -PassThru
        Write-Information -MessageData "$VsSku installed successful."

        [int]$exitCode = $process.ExitCode

        if (-not ($exitCode -eq 0 -or $exitCode -eq 3010)) {
            Write-Error -Message "Non-zero exit code returned by the installation process: $exitCode."
        }
    } finally {
        if ($null -ne $process) {
            Write-Verbose -Message "Disposing of the installer from memory."
            $process.Dispose()
            $process = $null
        }

        if (Test-Path -Path $filePath) {
            Write-Verbose -Message "Removing the installer from disk: $filePath."
            Remove-Item -Path $filePath -Force
        }
    }
}