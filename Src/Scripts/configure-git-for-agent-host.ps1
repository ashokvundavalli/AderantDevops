<#
Git must be configured to pass our Windows credentials or the tool will prompt for user/password input and so will hang indefinitely
#>

$ErrorActionPreference = "Stop"

$pathToGit = "C:\Program Files\Git\cmd\git.exe"

if (-not (Test-Path $pathToGit)) {
    throw "Git.exe not found at $pathToGit"
}

$hosts = @("tfs", "tfs.ap.aderant.com")
foreach ($entry in $hosts) { # Host is a reserved readonly PowerShell variable and so cannot be assigned to
    & $pathToGit config --global credential.$entry.interactive never
    & $pathToGit config --global credential.$entry.integrated true                
}
    
& $pathToGit config --global http.emptyAuth true
& $pathToGit config --global credential.authority ntlm