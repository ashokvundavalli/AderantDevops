param ( [string] $environmentManifestPath, 
		[string] $dbBackupFile, 
        [string] $directory,
		[string] $dbowner='cmsdbo')
        
       
begin {
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
	
		
	$restoreCmd = "USE MASTER;"
	$restoreCmd += "`nIF DB_ID('$dbName') > 0 `n`tALTER DATABASE $dbName SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" 
	$restoreCmd += "`nRESTORE DATABASE [$dbName] FROM DISK = N'$dbBackupFile'" 
    $restoreCmd += "`nwith replace," + "`nstats=10,"  #???
    $restoreCmd += "`n`tMOVE 'data01' TO '$directory\$dbName"+"_data01.mdf',"
    $restoreCmd += "`n`tMOVE 'log01' TO '$directory\$dbName"+"_log01.ldf',"
    $restoreCmd += "`n`tMOVE 'sysft_CMSFTICatalog' TO '$directory\$dbName"+"_sysft_CMSFTICatalog',"
    $restoreCmd += "`n`tMOVE 'sysft_ExpertTimeFTICatalog' TO '$directory\$dbName"+"_sysft_ExpertTimeFTICatalog'"
	$restoreCmd += ";"
	Invoke-SqlCmd -ServerInstance $dbServerInstance -Query $restoreCmd -ErrorAction Stop

	[string] $setRW =   "ALTER DATABASE $dbName SET READ_WRITE"
	Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setRW -ErrorAction Stop
    
	[string] $setOwner =   "ALTER AUTHORIZATION ON DATABASE::"+"$dbName TO $dbOwner"
    Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setOwner -ErrorAction Stop

		
	[string] $enableBroker = "ALTER DATABASE $dbName SET ENABLE_BROKER"
    
		try
		{
			Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $enableBroker -ErrorAction Stop
		}
		catch
		{

		    $enableBroker = "ALTER DATABASE $dbName SET NEW_BROKER"
  
			try
			{
				Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $enableBroker -ErrorAction Stop
			}
			catch
			{
            Write-Host "Cannot enable broker: $($error[0])"
            break
			}
		}
    
    [string] $setTrustworthy = "ALTER DATABASE $dbName SET TRUSTWORTHY ON" 
    Invoke-Sqlcmd -ServerInstance $dbServerInstance -Query $setTrustworthy -ErrorAction Stop
}

end {}