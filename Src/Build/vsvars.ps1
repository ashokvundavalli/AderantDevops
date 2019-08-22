<#
.SYNOPSIS
    Allows PowerShell to work like the Visual Studio developer console by placing interesting
    executables onto the current PATH and setting key environment varibles.
.PARAMETER IsBuildAgent
    Specifies that this script is running in the context of the build agent.
#>
param(
    [switch]$IsBuildAgent
)

##
## Sets environment variables used to interop with the Visual Studio Developer Command Prompt
##
function LoadEnvVariables([string]$environmentVariableName, [string]$vsYear, [string]$vsPath) {
    if ([string]::IsNullOrEmpty($vsPath) -and ![string]::IsNullOrEmpty($environmentVariableName)) {
        $vsPath = [Environment]::GetEnvironmentVariable($environmentVariableName)
    } else {
        $vsPath = FindVisualStudioPath $vsYear
    }

    if (-not [string]::IsNullOrEmpty($vsPath)) {
        $globalEnvironmentVariables = [Environment]::GetEnvironmentVariables()
        $vars = @{}

        $variablesFromScript = cmd /c "`"$vsPath\VsDevCmd.bat`"&set"

        $variablesFromScript.ForEach({
            $v = $_.Split("=")
            if ($v.Count -gt 1) {
                $vars.Add($v[0], $v[1])
            }
        })

        $globalEnvironmentVariables.GetEnumerator().ForEach({
            if ($vars.ContainsKey($_.Key) -and ($_.Key -ne "Path")) {
                $vars.Remove($_.Key)
            }
        })

        # If this is an agent then we need to set the variables here
        if ($isBuildAgent) {
            foreach ($item in $vars.GetEnumerator()) {
                [System.Environment]::SetEnvironmentVariable($item.Key, $item.Value, [System.EnvironmentVariableTarget]::Process)
            }
        }

        # When executing in a job return the values so the caller can apply them to that context
        $script:vars = $vars
        return $true
    }
    return $false
}

function FindVisualStudioPath {
    [CmdletBinding()]
    [OutputType([String])]
    param(
        [string]$Version
    )
    $pathsToTry = @($Version)

    $programFiles = ${env:ProgramFiles(x86)}

    $items = @(
        foreach ($folder in $pathsToTry) {
            Get-Item -ErrorAction SilentlyContinue @(
                "$programFiles\Microsoft Visual Studio\$folder\*\Common7\Tools\VsDevCmd.bat"
            )
        }
    )

    if ($items.Count -ge 1) {
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

        return [System.IO.Path]::GetDirectoryName($items[-1].FullName)
    }
}

if (LoadEnvVariables $null "2019") {
    return $script:vars
}

if (LoadEnvVariables $null "2017") {
    return $script:vars
}

if (LoadEnvVariables "VS140COMNTOOLS" "2015") {
    return $script:vars
}

if (LoadEnvVariables "VS120COMNTOOLS" "2013") {
    return $script:vars
}

if (LoadEnvVariables "VS110COMNTOOLS" "2012") {
    return $script:vars
}