Start-Transcript -Path "$env:SystemDrive\Scripts\IISCleanupLog.txt" -Force

Import-Module WebAdministration
Import-Module ApplicationServer

$expertWebApplications = Get-ASApplication -SiteName 'Default Web Site'
    
foreach ($webApp in $expertWebApplications) {
    if (-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
        if ($webApp.ApplicationName) {
            $iisPath = $webApp.IISPath
            Remove-Item -Path $iisPath
            Write-Output "Removed web application $iisPath"
        }
    }
}