Set-Location $Env:BUILD_SOURCESDIRECTORY

& "$PSScriptRoot\..\src\Build\paket.bootstrapper.exe" "5.198.0"
& "$PSScriptRoot\..\src\Build\paket.exe" "restore" "--verbose"