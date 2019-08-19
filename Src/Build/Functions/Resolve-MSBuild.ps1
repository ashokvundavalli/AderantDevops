[OutputType([string])]
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Bitness
)

Set-Alias Resolve-MSBuild (Join-Path $PSScriptRoot Resolve-MSBuild.ps1) -Scope Global

function Get-MSBuildPath {
    [OutputType([string])]
    [CmdletBinding()]
    param(
        [string]$Version,
        [string]$Bitness
    )

    if ([System.IntPtr]::Size -eq 4 -and $Bitness -eq 'x86') {
        return "MSBuild\$Version\Bin\MSBuild.exe"
    } else {
        return "MSBuild\$Version\Bin\amd64\MSBuild.exe"
    }
}

function FindBuildEnginePath {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$Version,
        [string]$Bitness
    )
    $pathsToTry = @("2019", "2017")

    $programFiles = ${env:ProgramFiles(x86)}

    $items = @(
        foreach ($folder in $pathsToTry) {
            Get-Item -ErrorAction SilentlyContinue @(
                "$programFiles\Microsoft Visual Studio\$folder\*\$(Get-MSBuildPath Current $Bitness)"
                "$programFiles\Microsoft Visual Studio\$folder\*\$(Get-MSBuildPath $Version $Bitness)"
                "$programFiles\$(Get-MSBuildPath $($Version) $Bitness)"
            )
        }
    )

    if ($items.Count -ge 2) {
        $byVersion = {[System.Version]$_.VersionInfo.FileVersionRaw}
        $byProduct = {
            switch -Wildcard ($_.FullName) {
                *\Enterprise\* {4}
                *\Professional\* {3}
                *\Community\* {2}
                *\BuildTools\* {1}
                default {0}
            }
        }
        $items = $items | Sort-Object $byVersion, $byProduct
    }

    [System.IO.Path]::GetDirectoryName($items[-1].FullName)
}

function Get-MSBuildOldVersion($Version, $Bitness) {
    if ([System.IntPtr]::Size -eq 8 -and $Bitness -eq 'x86') {
        $key = "HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\MSBuild\ToolsVersions\$Version"
    } else {
        $key = "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSBuild\ToolsVersions\$Version"
    }
    $rp = [Microsoft.Win32.Registry]::GetValue($key, 'MSBuildToolsPath', '')
    if ($rp) {
        return $rp
    }
}

try {
    if ([string]::IsNullOrEmpty($Version)) {
        return
    }

    $ErrorActionPreference = 'Stop'

    $v16 = [Version]'16.0'
    $v15 = [Version]'15.0'
    $vMax = [Version]'9999.0'
    if (!$Version) {$Version = '*'}
    $vRequired = if ($Version -eq '*') {$vMax} else {[Version]$Version}

    if ($vRequired -eq $v16 -or $vRequired -eq $v15) {
        if ($path = FindBuildEnginePath $Version $Bitness) {
            return $path
        }
    } elseif ($vRequired -lt $v15) {
        if ($path = Get-MSBuildOldVersion $Version $Bitness) {
            return $path
        }
    } elseif ($vRequired -eq $vMax) {
        if ($path = FindBuildEnginePath "*" $Bitness) {
            return $path
        }
    }

    throw 'The specified version is not found.'
} catch {
    Write-Error "Cannot resolve MSBuild $Version : $_"
}