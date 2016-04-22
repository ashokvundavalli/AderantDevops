##
# Sets environment vaiables used with a Visual Studio build 
##
##
function LoadEnvVariables([string]$environmentVariableName, [string]$vsYear) {
	$vsPath = [Environment]::GetEnvironmentVariable($environmentVariableName)
	if (-not [string]::IsNullOrEmpty($vsPath)) {  
		pushd $vsPath 
		cmd /c "VsDevCmd.bat&set" | foreach {
			if ($_ -match "=") {
				$v = $_.split("="); set-item -force -path "ENV:\$($v[0])"  -value "$($v[1])"
			}
		}
		popd
		Write-Host "`nVisual Studio $vsYear Command Prompt variables set." -ForegroundColor Yellow
		return $true
	}
	return $false
}

# VS 2015
if (LoadEnvVariables "VS140COMNTOOLS" "2015" ) {
	return
}

# VS 2013
if (LoadEnvVariables "VS120COMNTOOLS" "2013" ) {
	return
}

#VS2012
if (LoadEnvVariables "VS110COMNTOOLS" "2012" ) {
	return
}

# VS2010 (This uses a different bat file so it does not use the function)
$vsPath = [Environment]::GetEnvironmentVariable("VS100COMNTOOLS") 
if (-not [string]::IsNullOrEmpty($vsPath)) {    
    pushd $vsPath     
    cmd /c "vsvars32.bat&set" | foreach {
        if ($_ -match "=") {
            $v = $_.split("="); set-item -force -path "ENV:\$($v[0])"  -value "$($v[1])"
        }
    }
    popd
    Write-Host "`nVisual Studio 2010 Command Prompt variables set." -ForegroundColor Yellow
}
