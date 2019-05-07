function global:Clear-ProfileCache {
<#
.Synopsis
    Clears the Aderant PowerShell profile cache.
.Description
    Clears the Aderant PowerShell profile cache. This is useful in the event the environment variables have been updated as the cache does not update dynamically.
#>
    begin {
        Set-StrictMode -Version 'Latest'
        $InformationPreference = 'Continue'
    }

    process {
        if ([string]::IsNullOrWhiteSpace($ShellContext.CacheDirectory)) {
            Write-Information -MessageData '$ShellContext.CacheDirectory variable is invalid.'
            return
        }

        if (Test-Path -Path $ShellContext.CacheDirectory) {
            Write-Information -MessageData "Removing profile cache directory: '$($ShellContext.CacheDirectory)'."
            Remove-Item -Path $ShellContext.CacheDirectory -Recurse -Force
        } else {
            Write-Information -MessageData "Profile cache directory: '$($ShellContext.CacheDirectory)' does not exist."
        }
    }
}