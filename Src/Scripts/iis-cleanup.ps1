$ErrorActionPreference = "Continue"

Start-Transcript -Path "$env:SystemDrive\Scripts\IISCleanupLog.txt" -Force

Import-Module WebAdministration
Import-Module ApplicationServer

$expertWebApplications = Get-ASApplication -SiteName "Default Web Site"

foreach ($webApp in $expertWebApplications) {
    if (-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
        if ($webApp.ApplicationName) {
            $iisPath = $webApp.IISPath
            Remove-Item -Path $iisPath
            Write-Output "Removed web application $iisPath"
        }
    }
}

if ($null -ne $env:AgentPool -and $env:AgentPool -eq 'Test') {
    try {
        # Attempt to start app pools.
        Start-WebAppPool -Name Expert*
    } catch {
        # Ignore any errors - app pools may not exist.
    }
}