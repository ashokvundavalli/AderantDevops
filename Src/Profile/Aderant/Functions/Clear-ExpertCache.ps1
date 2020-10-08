function global:Clear-ExpertCache {
    <#
    .Synopsis
        Clears the Expert cache for the specified user.
    .Description
        Clears the local and roaming caches for the specified user.
    .PARAMETER user
        The user account to clear the Expert cache for.
    .PARAMETER environmentName
        Remove the cache for a specific Expert environment.
    .PARAMETER removeCMSINI
        Removes the CMS.INI file from AppData\Roaming\Aderant.
    .EXAMPLE
        Clear-ExpertCache -user TTQA1 -environmentName ITEGG -removeCMSINI
        This will clear the local and roaming caches for the ITEGG environment for TTQA1 and remove CMS.INI from AppData\Roaming\Aderant.
    #>
    [CmdletBinding()]
    [Alias('ccache')]
    param(
        [Parameter(Mandatory = $false)] [string]$user = [Environment]::UserName,
        [Parameter(Mandatory = $false)] [string]$environmentName,
        [switch]$removeCMSINI
    )

    [string]$cache = "Aderant"
    [string]$localAppData = [string]::Empty
    [string]$roamingAppData = [string]::Empty

    Write-Warning '***'
    Write-Warning 'If the product has caching issues, please raise a bug in TFS.'
    Write-Warning '***'

    if (-not [string]::IsNullOrWhiteSpace($environmentName)) {
        $cache = [string]::Concat($cache, "\$environmentName")
    }

    if (-not ($user -match [Environment]::UserName)) {
        $localAppData = "C:\Users\$user\AppData\Local"
        $roamingAppData = "C:\Users\$user\AppData\Roaming"
    } else {
        $localAppData = $env:LOCALAPPDATA
        $roamingAppData = $env:APPDATA
    }

    if (Test-Path -Path "$localAppData\$cache") {
        if (-not (Get-Item "$localAppData\$cache").PSIsContainer) {
            Write-Error "Please specify a valid environment name"
            Break
        }
        try {
            Get-ChildItem -Path "$localAppData\$cache" -Recurse | Remove-Item -force -recurse
            if (-not [string]::IsNullOrWhiteSpace($environmentName)) {
                Remove-Item -Path "$localAppData\$cache" -Force
            }
            Write-Host "Successfully cleared $localAppData\$cache"
        } catch {
            Write-Warning "Unable to clear $localAppData\$cache"
        }
    } else {
        Write-Host "No cache present at $localAppData\$cache"
    }

    if (Test-Path -Path "$roamingAppData\$cache") {
        if (-not (Get-Item "$roamingAppData\$cache").PSIsContainer) {
            Write-Error "Please specify a valid environment name"
            Break
        }
        try {
            if ([string]::IsNullOrWhiteSpace($environmentName)) {
                Get-ChildItem -Path "$roamingAppData\$cache" -Recurse |  Remove-Item -Exclude "CMS.INI" -Force -Recurse
            } else {
                Get-ChildItem -Path "$roamingAppData\$cache" -Recurse | Remove-Item -Force -Recurse
                Remove-Item -Path "$roamingAppData\$cache" -Force
            }
            Write-Host "Successfully cleared $roamingAppData\$cache"
        } catch {
            Write-Error "Unable to clear $roamingAppData\$cache"
        }
    } else {
        Write-Host "No cache present at $roamingAppData\$cache"
    }

    if ($removeCMSINI.IsPresent) {
        if (Test-Path -Path "$roamingAppData\Aderant\CMS.INI") {
            try {
                Remove-Item -Path "$roamingAppData\Aderant\CMS.INI" -Force
                Write-Host "Successfully removed CMS.INI"
            } catch {
                Write-Error "Unable to remove CMS.INI at $roamingAppData\Aderant"
            }
        } else {
            Write-Host "No CMS.INI file present at $roamingAppData\Aderant"
        }
    }
}