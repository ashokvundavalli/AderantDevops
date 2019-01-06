function Get-AiChan {
    param (
        [string]$agentName
    )

    if ([string]::IsNullOrWhiteSpace($agentName) -or -not $agentName.Contains('_')) {
        return
    }

    [string[]]$components = $agentName.Split('_')

    if ($null -eq $components -or $components.Length -ne 3) {
        return
    }

    [string]$agentPath = '\\SVNAS301.ap.aderant.com\internal\team\DevOps\Agents\ASCII'

    if (-not (Test-Path -Path $agentPath)) {
        return
    }

    [System.IO.FileInfo[]]$files = Get-ChildItem -Path $agentPath -File -Filter "$($components[2])*"

    if ($null -eq $files -or $files.Length -eq 0) {
        return
    }

    [string]$file = $null

    if ($files.Count -eq 1) {
        $file = $files[0].FullName
    } else {
        $file = $files[(Get-Random -Minimum 0 -Maximum $files.Length)].FullName
    }

    if ($null -ne $file -and (Test-Path -Path $file)) {
        Write-Information "Build courtesy of $($agentName)"
        [System.IO.File]::ReadAllLines($file) | Write-Output
    }
}

try {
    if ($null -ne $Env:AGENT_NAME) {
        Get-AiChan -agentName $Env:AGENT_NAME
    }
} catch {
    # Don't upset the build system in the event something goes wrong.
}