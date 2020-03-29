# This script is used to bootstrap the compile process for Build.Infrastructure on the server.
Set-Location -Path ($PSScriptRoot + "\..\")
[string]$buildScriptsDirectory = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\Src\Build\")

# Load the version of Paket we want to use. Released versions can be found here: https://github.com/fsprojects/Paket/releases
[string]$paketVersion = Get-Content -Path "$PSScriptRoot\paket.version"

# Update Paket bootstrapper.
Start-Process -FilePath  "$buildScriptsDirectory\paket.bootstrapper.exe" -ArgumentList '--self' -NoNewWindow -PassThru -Wait | Out-Null
# Download the version of Paket we're using.
Start-Process -FilePath  "$buildScriptsDirectory\paket.bootstrapper.exe" -ArgumentList $paketVersion -NoNewWindow -PassThru -Wait | Out-Null
# Run Paket restore.
Start-Process -FilePath  "$buildScriptsDirectory\paket.exe" -ArgumentList 'restore' -NoNewWindow -PassThru -Wait | Out-Null