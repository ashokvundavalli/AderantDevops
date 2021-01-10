# This script is used to bootstrap the compile process for Build.Infrastructure on the server.
Set-Location -Path ([System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\")))
[string]$buildScriptsDirectory = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\Src\Build\")
[string]$paketBootstrapper = [System.IO.Path]::Combine($buildScriptsDirectory, 'paket.bootstrapper.exe')

[System.Environment]::SetEnvironmentVariable('PAKET_SKIP_RESTORE_TARGETS', 'true', [System.EnvironmentVariableTarget]::Process)

# Update Paket bootstrapper.
[void](Start-Process -FilePath $paketBootstrapper -ArgumentList @('--self') -NoNewWindow -PassThru -Wait)

# Load the version of Paket we want to use. Released versions can be found here: https://github.com/fsprojects/Paket/releases
[string]$paketVersion = Get-Content -Path "$PSScriptRoot\paket.version"
# Download the version of Paket we're using.
[void](Start-Process -FilePath  $paketBootstrapper -ArgumentList @($paketVersion) -NoNewWindow -PassThru -Wait)

# Run Paket restore.
[void](Start-Process -FilePath  "$buildScriptsDirectory\paket.exe" -ArgumentList @('restore') -NoNewWindow -PassThru -Wait)