<#
.Synopsis
       Finds the specified or latest MSBuild.
#>

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

    if ($Bitness -eq 'x86') {
        return [System.IO.Path]::Combine("MSBuild", $Version, "Bin", "MSBuild.exe")
    } else {
        return [System.IO.Path]::Combine("MSBuild", $Version, "Bin", "amd64", "MSBuild.exe")
    }
}

function CompileVisualStudioLocationHelper {
    if (([System.Management.Automation.PSTypeName]'VisualStudioConfiguration.VisualStudioLocationHelper').Type) {
        # The type is already defined so bail out
        return
    }

    Add-Type -Path ([System.IO.Path]::Combine($PSScriptRoot, "VisualStudioConfiguration.cs"))
}

function FindBuildEnginePath {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [string]$Version,
        [string]$Bitness
    )

    CompileVisualStudioLocationHelper

    $studios = [VisualStudioConfiguration.VisualStudioLocationHelper]::GetInstances()

    if ($Version -ne "*") {
        $studios = $studios | Where-Object { $_.Version.Major.ToString() -eq ([Version]$Version).Major }
    }

    $items = @{}
    $items[''] = $null # Dummy value so we can treat scalar results the same as many results

    $studio = $null

    foreach ($studio in $studios) {
        $items[$studio] = (Get-Item -ErrorAction Ignore @(
            "$($studio.Path)\$(Get-MSBuildPath Current $Bitness)",
            "$($studio.Path)\$(Get-MSBuildPath $Version $Bitness)"
        )) | Sort-Object -Unique
    }

    if ($items.Count -ge 2) {
        $byVersion = {[System.Version]$_.Key.Version}
        $byProduct = {
            switch -Wildcard ($_.Key.Name) {
                *\Enterprise\* {4}
                *\Professional\* {3}
                *\Community\* {2}
                *\BuildTools\* {1}
                default {0}
            }
        }
        $items = $items.GetEnumerator() | Where-Object { '' -ne $_.Key } | Sort-Object $byVersion, $byProduct | Select-Object -Last 1
    }

    if ($null -eq $items -or -not $items.PSObject.Properties.Name.Contains('Value')) {
        return $null
    }

    return $items.Value.DirectoryName
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
    $ErrorActionPreference = 'Stop'

    $v16 = [Version]'16.0'
    $v15 = [Version]'15.0'
    $vMax = [Version]'9999.0'
    if ([string]::IsNullOrEmpty($Version)) {
        $Version = '*'
    }

    $vRequired = if ($Version -eq '*') {
        $vMax
    } else {
        [Version]$Version
    }

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