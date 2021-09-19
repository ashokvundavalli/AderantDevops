Set-StrictMode -Version "Latest"

if ((Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion" -Name "ProductName").ProductName -match "Windows Server") {
    # Windows Server OS
    if (-not (Get-WindowsFeature -Name "Web-Server").Installed) {
        Write-Host "IIS is not installed."
        return
    }
} else {
    # Windows Client OS
    if ((Get-WindowsOptionalFeature -Online -FeatureName "IIS-WebServerRole").State -ne "Enabled") {
        Write-Host "IIS is not installed."
        return
    }
}

if (-not (Get-Module -Name "IISAdministration" -ListAvailable)) {
    Install-Module -Name "IISAdministration" -AllowClobber -Force
}

if (-not (Get-Module -Name "IISAdministration")) {
    Import-Module -Name "IISAdministration"
}

$iisSite = "Default Web Site"
$site = Get-IISSite -Name $iisSite

foreach ($webApp in $site.Applications | Where-Object { $_.Path -like "/Expert_*" }) {
    # The WebConfigurationLocation may not exist for some paths.
    # The value "Name" should match exactly with the XML element in applicationHost.config (so trim the trailing slash)
    Remove-WebConfigurationLocation -Name $webApp.VirtualDirectories[0].ToString().TrimEnd("/")

    Remove-WebApplication -Name $webApp.Path -Site $iisSite -Verbose

    foreach ($virtualDirectory in $webApp.VirtualDirectories) {
        Remove-Item -Path $virtualDirectory.PhysicalPath -Force -Verbose
    }
}

if ($null -ne $Env:AgentPool -and $Env:AgentPool -eq "Test") {
    try {
        # Attempt to start app pools.
        Start-WebAppPool -Name Expert*
    } catch {
        # Ignore any errors - app pools may not exist.
    }
} else {
    $applicationPools = Get-IISAppPool | Where-Object { $_.Name -like "Expert*" }

    foreach ($applicationPool in $applicationPools) {
        Remove-WebAppPool $applicationPool.Name
    }
}