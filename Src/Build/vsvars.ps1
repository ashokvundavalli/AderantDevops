##
## Sets environment variables used with a Visual Studio build 
##
function LoadEnvVariables([string]$environmentVariableName, [string]$vsYear) {
    $vsPath = [Environment]::GetEnvironmentVariable($environmentVariableName)
    
    if (-not [string]::IsNullOrEmpty($vsPath)) {
        $vars = GetCacheItem "VsDevCmd.bat"
        if (-not $vars) {       
            $globalEnvironmentVariables = [Environment]::GetEnvironmentVariables()
            $vars = @{}

            $variablesFromScript = cmd /c "`"$vsPath\VsDevCmd.bat`"&set"
            $variablesFromScript.ForEach({
                $v = $_.Split("=")
                $vars.Add($v[0], $v[1])
            })
            
            $globalEnvironmentVariables.GetEnumerator().ForEach({ 
              if ($vars.ContainsKey($_.Key) -and ($_.Key -ne "Path")) { 
                $vars.Remove($_.Key)
              } 
            })

            PutCacheItem "VsDevCmd.bat" $vars "$vsPath\VsDevCmd.bat", $PSCommandPath
        }

        foreach ($item in $vars.GetEnumerator()) {
            Set-Item -Force -Path "ENV:\$($item.Key)" -Value $item.Value
        }

        Write-Host "`nVisual Studio $vsYear Command Prompt variables set." -ForegroundColor Yellow
        return $true
    }
    return $false
}

# VS 2015
if (LoadEnvVariables "VS140COMNTOOLS" "2015") {
    return
}

# VS 2013
if (LoadEnvVariables "VS120COMNTOOLS" "2013") {
    return
}

#VS2012
if (LoadEnvVariables "VS110COMNTOOLS" "2012") {
    return
}