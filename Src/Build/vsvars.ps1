<#
.SYNOPSIS
    Allows PowerShell to work like the Visual Studio developer console by placing interesting
    executables onto the current PATH and setting key environment variables.
.PARAMETER IsBuildAgent
    Specifies that this script is running in the context of the build agent.
#>
[CmdletBinding()]
param(
    [switch]$IsBuildAgent
)

begin {
    . Resolve-MSBuild
    CompileVisualStudioLocationHelper

    ##
    ## Sets environment variables used to interop with the Visual Studio Developer Command Prompt
    ##
    function LoadEnvVariables([string]$environmentVariableName, [string]$vsYear, [string]$vsPath) {
        if ([string]::IsNullOrEmpty($vsPath) -and ![string]::IsNullOrEmpty($environmentVariableName)) {
            $vsPath = [Environment]::GetEnvironmentVariable($environmentVariableName)
        } else {
            $instance = ([VisualStudioConfiguration.VisualStudioLocationHelper]::GetInstances() | Where-Object { $_.Name.Contains($vsYear) }  | Select-Object -First 1)
            if ($null -eq $instance) {
                return $false
            } else {
                $vsPath = $instance.Path
            }
        }

        if (-not [string]::IsNullOrEmpty($vsPath)) {
            $globalEnvironmentVariables = [Environment]::GetEnvironmentVariables()
            $vars = @{}

            # Disable telemetry when running VsDevCmd.bat
            [System.Environment]::SetEnvironmentVariable('VSCMD_SKIP_SENDTELEMETRY', '1', [System.EnvironmentVariableTarget]::Process)

            $variablesFromScript = cmd /c "`"$vsPath\Common7\Tools\VsDevCmd.bat`"&set"

            $variablesFromScript.ForEach({
                Write-Debug $_

                $v = $_.Split("=")
                if ($v.Count -gt 1) {
                    $vars.Add($v[0], $v[1])
                }
            })

            # We remove the version as the build engine uses this when trying to resolve things from the MSBuild extension path
            $vars.Remove("VisualStudioVersion")

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
}

process {
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
}

end {

}