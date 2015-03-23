param ( [string] $environmentManifestPath, 
		[string] $directory, 
		[string] $action,
		[string] $buildNumber = $null)
        
begin {

    write "$action DB snapshot params:"
    write "environmentManifestPath = $environmentManifestPath"
    write "directory = $directory"
    write "action = $action"

    $script:ErrorActionPreference = 'Stop'
	$sqlSnapin = Get-PSSnapin | where {$_.Name -eq "SqlServerCmdletSnapin100"}
	if($sqlSnapin -eq $null)
	{
		 Add-PSSnapin SqlServerCmdletSnapin100
	}
}

process {	
	[xml]$environmentManifest =  (Get-Content $environmentManifestPath)      
    $Database = $environmentManifest.environment.expertDatabaseServer.databaseConnection.databaseName
    $dbServerInstance = "{0}\{1}" -f $environmentManifest.environment.expertDatabaseServer.serverName, $environmentManifest.environment.expertDatabaseServer.serverInstance
	$Snapshot = "ExpertSS_$Database_$buildNumber"
	$Folder = $directory
#	$Action = "RESTORE"
#	$dbServerInstance = "svsql303\mssql10"
#	$Database = "VMExpDevB306"
	switch ($action)
	{
		"CREATE"
		{
			try 
			{
			    $cmd = "EXEC master.dbo.spCreateSnapshot @Database = '$Database', @SnapshotName = '$Snapshot', @Folder = '$Folder';"
			    write "Running command to Create DB Snapshot: $cmd"
			    Invoke-SqlCmd -ServerInstance $dbServerInstance -Query $cmd -ErrorAction Stop -Verbose -QueryTimeout 600    
			}
			catch [System.Exception]
			{
			    write "Excpetion thrown while attempting to run $cmd"
			    write $error
				exit 1
			}
		}
		"RESTORE"
		{
			try 
			{
			    $cmd = "EXEC master.dbo.spRestoreSnapShot @Database = '$Database', @SnapshotName = '$Snapshot';"
			    write "Running command to Restore DB from Snapshot: $cmd"
			    Invoke-SqlCmd -ServerInstance $dbServerInstance -Query $cmd -ErrorAction Stop -Verbose -QueryTimeout 600     
			}
			catch [System.Exception]
			{
			    write "Excpetion thrown while attempting to run $cmd"
			    write $error
				exit 1
			}
		}
		default
		{ 
			"No action was specified." 
			exit 1
		}
	}
}

end {
    write "$action DB snapshot finished."
}