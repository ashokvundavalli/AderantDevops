# This script is used to bootstrap the compile process for Build.Infrastructure on the server.
Set-Location -Path $Env:BUILD_SOURCESDIRECTORY

[string]$buildScriptsDirectory = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\Src\Build\")

# Load the version of Paket we want to use. Released versions can be found here: https://github.com/fsprojects/Paket/releases
[string]$paketVersion = Get-Content -Path "$PSScriptRoot\paket.version"

# Update Paket bootstrapper.
Start-Process -FilePath  "$buildScriptsDirectory\paket.bootstrapper.exe" -ArgumentList '--self' -NoNewWindow -PassThru -Wait
# Download the version of Paket we're using.
Start-Process -FilePath  "$buildScriptsDirectory\paket.bootstrapper.exe" -ArgumentList $paketVersion -NoNewWindow -PassThru -Wait
# Run Paket restore.
Start-Process -FilePath  "$buildScriptsDirectory\paket.exe" -ArgumentList 'restore' -NoNewWindow -PassThru -Wait