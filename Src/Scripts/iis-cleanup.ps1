Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Continue'
$InformationPreference = 'Continue'

Start-Transcript -Path "$Env:SystemDrive\Scripts\IISCleanupLog.txt" -Force

if (-not (Get-Module -Name 'IISAdministration' -ListAvailable)) {
    Install-Module -Name 'IISAdministration' -AllowClobber -Force
}

if (-not (Get-Module -Name 'IISAdministration')) {
    Import-Module -Name 'IISAdministration'
}

if (-not (Get-Module -Name 'ApplicationServer' -ListAvailable)) {
    Write-Warning -Message 'AppFabric ApplicationServer module is not installed.'
    return
}

if (-not (Get-Module -Name 'ApplicationServer')) {
    Import-Module -Name 'ApplicationServer'
}

try {
    $expertWebApplications = Get-ASApplication -SiteName 'Default Web Site'

    foreach ($webApp in $expertWebApplications) {
        if (-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
            if ($webApp.ApplicationName) {
                $iisPath = $webApp.IISPath
                Remove-Item -Path $iisPath -Force
                Write-Information -MessageData "Removed web application: '$iisPath'."
                # The WebConfigurationLocation may not exist for some paths.
                Remove-WebConfigurationLocation -Name $webApp.IISPath -WarningAction 'SilentlyContinue'
            }
        }
    }

    if ($null -ne $Env:AgentPool -and $Env:AgentPool -eq 'Test') {
        try {
            # Attempt to start app pools.
            Start-WebAppPool -Name Expert*
        } catch {
            # Ignore any errors - app pools may not exist.
        }
    } else {
        $applicationPools = Get-IISAppPool | Where-Object { $_.Name -match 'Expert' }

        foreach ($applicationPool in $applicationPools) {
            Write-Information -MessageData "Removing application pool: '$($applicationPool.Name)'."
            $applicationPool.Delete()
        }
    }
} finally {
    Stop-Transcript
}