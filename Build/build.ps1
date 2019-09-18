Set-Location $Env:BUILD_SOURCESDIRECTORY

$buildScriptsDirectory = "$PSScriptRoot\..\Src\Build\"

Start-Process -FilePath  "$buildScriptsDirectory\paket.bootstrapper.exe" -ArgumentList  "5.219.0" -NoNewWindow -PassThru -Wait
Start-Process -FilePath  "$buildScriptsDirectory\paket.exe" -ArgumentList "restore" -NoNewWindow -PassThru -Wait