<#
.Synopsis 
	Helper module for performing deployment actions on a remote machine.
#>

Function TryKillProcess([string] $processName, [string] $computerName){
    Invoke-Command -ComputerName $computerName -ArgumentList $processName -ScriptBlock {
        param([string] $processName)
        begin{
            $process =  Get-Process -ErrorAction "SilentlyContinue" $processName 
            if ($process) {
                write "Stopping $processName"
                $process | Stop-Process -force
            }
        }
    }
}

Function Get-SourceDirectory($environmentManifestPath){    
	[xml]$environmentManifest =  Get-Content $environmentManifestPath      
	$sourcePath = $environmentManifest.environment.deploymentBinariesPath    
	return $sourcePath
}

Function Get-DbProjectTargetDatabaseServer($environmentManifestPath){    
	[xml]$environmentManifest =  Get-Content $environmentManifestPath      
    return $environmentManifest.environment.expertDatabaseServer.serverName + "\" + $environmentManifest.environment.expertDatabaseServer.serverInstance
}

Function Get-DbProjectTargetDatabaseName($environmentManifestPath){    
	[xml]$environmentManifest =  Get-Content $environmentManifestPath
    return $environmentManifest.environment.expertDatabaseServer.databaseConnection.databaseName
}

Function Get-StoredCredential {
	$k = [Byte[]]'71 43 121 203 174 13 87 21 153 38 212 235 190 140 224 147'.Split(" ")
	$user = "aderant_ap\service.expert.ap"
	$pass = "76492d1116743f0423413b16050a5345MgB8AHQAOABDADAAagBQAEYANgBXAGIAbABKAEsAOQBPAFEAOABIAFMAOAA2AGcAPQA9AHwAMAA4AGUAYwBhAGYAZgBmADgAYwA2AGQANAAwAGYAMwAyAGYAZgAxADkAZgA3ADYANwAxADgANAAzADcAMwA4ADkANQBjADkAZQBhADcAZQA1AGIAOQAzAGUAMQA5AGMAZQA2AGQAMAA4AGMANQBmADgAZAA4AGIANAA3AGIANgA=" | ConvertTo-SecureString -Key $k
	$credential = New-Object System.Management.Automation.PSCredential($user, $pass)
	return $credential
}

Function Get-RemoteSession($remoteMachineName) {
	$credential = Get-StoredCredential
	$session = New-PSSession $remoteMachineName -Auth CredSSP -Cred $credential
	return $session
}

Function CopyBinariesToRemoteMachine($localBinaries, $remoteBinaries) {
    Write-Host "Copying Binaries [$localBinaries\*] to remote machine [$remoteBinaries]."
	Remove-Item -Recurse $remoteBinaries\*
	Copy-Item -Path "$localBinaries\*" -Destination "$remoteBinaries" -Recurse -Force
}

Export-ModuleMember -Function CopyBinariesToRemoteMachine
Export-ModuleMember -Function Get-RemoteSession
Export-ModuleMember -Function Get-SourceDirectory
Export-ModuleMember -Function Get-DbProjectTargetDatabaseServer
Export-ModuleMember -Function Get-DbProjectTargetDatabaseName
Export-ModuleMember -Function TryKillProcess