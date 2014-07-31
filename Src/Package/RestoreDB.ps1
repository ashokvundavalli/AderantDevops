param ( [string] $environmentManifestPath, 
		[string] $databaseBackupLocation, 
		[string] $dbowner='cmsdbo')
        
       
begin {
	$databaseBackupPath=Split-Path -parent $databaseBackupLocation
	$dbBackupFile=Split-Path -leaf $databaseBackupLocation

    write "RestoreDB.ps1 params:"
    write "environmentManifestPath = $environmentManifestPath"
    write "databaseBackupFile = $dbBackupFile"
    write "databaseBackupPath = $databaseBackupPath"
    write "dbowner = $dbowner"

    $script:ErrorActionPreference = 'Stop'
	$sqlSnapin = Get-PSSnapin | where {$_.Name -eq "SqlServerCmdletSnapin100"}
	if($sqlSnapin -eq $null)
	{
		 Add-PSSnapin SqlServerCmdletSnapin100
	}
}

process {
	
	[xml]$environmentManifest =  (Get-Content $environmentManifestPath)      
    $ServerName = $environmentManifest.environment.expertDatabaseServer.serverName
    $ServerInstance = $environmentManifest.environment.expertDatabaseServer.serverInstance
    $dbName = $environmentManifest.environment.expertDatabaseServer.databaseConnection.databaseName
    $dbServerInstance = "$ServerName\" + "$ServerInstance"
	
	try {
	    $singleUserCmd = "`nIF DB_ID('$dbName') > 0 `n`tALTER DATABASE $dbName SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" 
        write "Running command: $singleUserCmd"
        Invoke-SqlCmd -ServerInstance $dbServerInstance -Query $singleUserCmd -ErrorAction Stop
        write "Set single user on database $dbName"
        
    }
    catch [System.Exception]{
        write "Excpetion thrown while attempting to set $dbName to single user."
        write $error
    }

	$restoreCmd = "`nRESTORE FILELISTONLY FROM DISK = N'$databaseBackupLocation'"
    write "Running command: $restoreCmd"
	$fileList = Invoke-SqlCmd -ServerInstance $dbServerInstance -Query $restoreCmd -ErrorAction Stop

	$restoreCmd = "`nRESTORE DATABASE [$dbName] FROM DISK = N'$databaseBackupLocation'" 
    $restoreCmd += "`nWITH REPLACE"
    foreach ($file in $fileList) {
        $restoreCmd += ",`n`tMOVE '$($file.LogicalName)' TO '$databaseBackupPath\$($dbName)_$($file.LogicalName)'"
    }
	$restoreCmd += ";"

    write "Running command: $restoreCmd"
	Invoke-SqlCmd -ServerInstance $dbServerInstance -Query $restoreCmd -ErrorAction Stop -QueryTimeout 1000
    write "Restored database $dbName"
    
	[string] $setSimpleRecovery =   "ALTER DATABASE $dbName SET RECOVERY SIMPLE"
    write "Running command: $setSimpleRecovery"
	Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setSimpleRecovery -ErrorAction Stop
    write "Set database $dbName to simple recovery."

	[string] $setRW =   "ALTER DATABASE $dbName SET READ_WRITE"
    write "Running command: $setRW"
	Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setRW -ErrorAction Stop
    write "Set database $dbName to read-write"
    
	[string] $setOwner =   "ALTER AUTHORIZATION ON DATABASE::"+"$dbName TO $dbOwner"
    write "Running command: $setOwner"    
    Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setOwner -ErrorAction Stop
    write "Set database $dbName owner to $dbOwner"
		
    try	{
        [string] $enableBroker = "ALTER DATABASE $dbName SET ENABLE_BROKER"
		write "Running command: $enableBroker"
        Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $enableBroker -ErrorAction Stop
        write "Enabled broker on database $dbName"
	}
    catch{
		try {
            $enableBroker = "ALTER DATABASE $dbName SET NEW_BROKER"
            write "Running command: $enableBroker"
			Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $enableBroker -ErrorAction Stop
            write "Set new broker on database $dbName"
		}
		catch{
        Write-Host "Cannot enable broker: $($error[0])"
        break
		}
	}
    
    [string] $setTrustworthy = "ALTER DATABASE $dbName SET TRUSTWORTHY ON" 
    write "Running command: $setTrustworthy"
    Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setTrustworthy -ErrorAction Stop
    write "Set database $dbName trustworthy on."
}

end {
    write "RestoreDB.ps1 finished."
}