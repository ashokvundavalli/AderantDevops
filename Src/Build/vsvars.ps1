##
# Sets environment vaiables used with a Visual Studio build 
##
##
# VS2013
$vsPath = [Environment]::GetEnvironmentVariable("VS120COMNTOOLS")
if (-not [string]::IsNullOrEmpty($vsPath)) {  
    pushd $vsPath 
    cmd /c "VsDevCmd.bat&set" | foreach {
        if ($_ -match "=") {
            $v = $_.split("="); set-item -force -path "ENV:\$($v[0])"  -value "$($v[1])"
        }
    }
    popd
    Write-Host "`nVisual Studio 2013 Command Prompt variables set." -ForegroundColor Yellow
    return
} 

# VS2012
$vsPath = [Environment]::GetEnvironmentVariable("VS110COMNTOOLS")   
if (-not [string]::IsNullOrEmpty($vsPath)) {  
    pushd $vsPath 
    cmd /c "VsDevCmd.bat&set" | foreach {
        if ($_ -match "=") {
            $v = $_.split("="); set-item -force -path "ENV:\$($v[0])"  -value "$($v[1])"
        }
    }
    popd
    Write-Host "`nVisual Studio 2012 Command Prompt variables set." -ForegroundColor Yellow
    return
} 

# VS2010
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
