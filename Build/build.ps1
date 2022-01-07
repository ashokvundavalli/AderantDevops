Set-StrictMode -Version Latest

[System.Environment]::SetEnvironmentVariable('PAKET_SKIP_RESTORE_TARGETS', 'true', [System.EnvironmentVariableTarget]::Process)

# This script is used to bootstrap the compile process for Build.Infrastructure on the server.
$workingDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..\"))
Set-Location -Path $workingDirectory

[string]$buildToolsDirectory = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\Src\Build.Tools\")
[string]$paketBootstrapper = [System.IO.Path]::Combine($buildToolsDirectory, 'paket.bootstrapper.exe')

# Update Paket bootstrapper.
[void](Start-Process -FilePath $paketBootstrapper -ArgumentList @('--self') -NoNewWindow -PassThru -Wait)

# Load the version of Paket we want to use. Released versions can be found here: https://github.com/fsprojects/Paket/releases
[string]$paketVersion = Get-Content -Path "$PSScriptRoot\paket.version"
# Download the version of Paket we're using.
[void](Start-Process -FilePath  $paketBootstrapper -ArgumentList @($paketVersion) -NoNewWindow -PassThru -Wait)

# Run Paket restore.
$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.CreateNoWindow = $true
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.FileName = "$buildToolsDirectory\paket.exe"
$psi.Arguments = @("restore", "--verbose")
$psi.WorkingDirectory = $workingDirectory
$process = [System.Diagnostics.Process]::new()

# Adding event handers for stdout and stderr.
$scripBlock = {
    if (![String]::IsNullOrEmpty($EventArgs.Data))
    {
        Write-Host $EventArgs.Data
    }
}

$stdOutEvent = Register-ObjectEvent `
    -InputObject $process `
    -Action $scripBlock `
    -EventName 'OutputDataReceived'
$stdErrEvent = Register-ObjectEvent `
    -InputObject $process `
    -Action $scripBlock `
    -EventName 'ErrorDataReceived'

$process.StartInfo =  $psi

try {
    [void]$process.Start()

    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    while (-not $process.WaitForExit(100)) {
        # Allow interrupts like CTRL + C by doing a non-blocking wait
    }

    $process.WaitForExit()

} finally {
    Unregister-Event -SourceIdentifier $stdOutEvent.Name
    Unregister-Event -SourceIdentifier $stdErrEvent.Name
    $process.Dispose()
}