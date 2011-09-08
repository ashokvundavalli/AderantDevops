<# 
.Synopsis 
    Runs a load test on remote machine.
.Example     
    RunRemoteLoadTest.ps1 "lrsrv307.lr.aderant.com" ".\TestBinaries" "C:\TestBinaries" "remotetest.testsettings" "loadtest50users.loadtest"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $testBinariesDirectory is the path to the test binaries on the build machine
.Parameter $remoteTestBinariesDirectory is the path of the test binaries folder on the remote machine
.Parameter $loadTestSettings is the testsettings file for the load test - passed to the mstest /testsettings parameter
.Parameter $loadTestContainer is the test container file for the load test - passed to the mstest /testcontainer parameter
#>

param ( [string]$remoteMachineName, 
        [string]$testBinariesDirectory, 
        [string]$remoteTestBinariesDirectory,
        [string]$loadTestSettings,
        [string]$loadTestContainer)

begin{
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath
}

process{
	$ErrorActionPreference = "Stop"
	$session = Get-RemoteSession $remoteMachineName
    $pathToTestSettings = "$remoteTestBinariesDirectory\$loadTestSettings"
    $pathToLoadTestContainer = "$remoteTestBinariesDirectory\$loadTestContainer"

    # Copy the test binaries to the remote machine
	if (Test-Path $pathToTestSettings)
	{
        # Test Settings file may have the readonly attribute set
		Remove-Item -Path $pathToTestSettings -Force
	}
    CopyBinariesToRemoteMachine $testBinariesDirectory $remoteTestBinariesDirectory

    # Invoke mstest on the remote machine
    Invoke-Command -Session $session `
     -ScriptBlock { 
            param($pathToTestSettings, $pathToLoadTestContainer, $remoteTestBinariesDirectory)
            #$vsmstest = "`'C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\mstest.exe`'"
			$vsmstest = "mstest.exe"
			$i = Get-Item -Path "$remoteTestBinariesDirectory"
			$p = (Get-WmiObject -Class Win32_Share -Filter "Name='$($i.Name)'").Path
            $params = "/testsettings:`"$pathToTestSettings`" /testcontainer:`"$pathToLoadTestContainer`""
            $cmd = "$vsmstest $params"
            Write-Host "Command: $cmd"
            Push-Location -Path $p
			CMD /C $cmd
			Pop-Location
     } `
     -ArgumentList $pathToTestSettings, $pathToLoadTestContainer, $remoteTestBinariesDirectory    
    
	# Remove the remote session
	Remove-PSSession -Session $session
}