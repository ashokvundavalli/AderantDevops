param ([string]$environmentManifestPath, [string]$deploymentToolPath, [string]$updateBundlePath, [switch]$interactive)

begin {
	Write-Host "Loading manifest from $environmentManifestPath"	
	
	function GetSourceDirectory($environmentManifestPath) {    
		[xml]$environmentManifest = Get-Content $environmentManifestPath      
		$sourcePath = $environmentManifest.environment.sourcePath  
		return $sourcePath
	}

	function GetDatabaseServer($environmentManifestPath) {    
		[xml]$environmentManifest = Get-Content $environmentManifestPath
		
		if (-not [string]::IsNullOrEmpty($environmentManifest.environment.expertDatabaseServer.serverInstance)) {
	    	return $environmentManifest.environment.expertDatabaseServer.serverName + "\" + $environmentManifest.environment.expertDatabaseServer.serverInstance
		} else {
			return $environmentManifest.environment.expertDatabaseServer.serverName
		}
	}

	function GetDatabaseName($environmentManifestPath) {    
		[xml]$environmentManifest = Get-Content $environmentManifestPath
	    return $environmentManifest.environment.expertDatabaseServer.databaseConnection.databaseName
	}	
	
	function GetLogin($environmentManifestPath) {    
		[xml]$environmentManifest = Get-Content $environmentManifestPath
	    return $environmentManifest.environment.expertDatabaseServer.databaseConnection.username
	}
	
	function GetPassword($environmentManifestPath) {    
		[xml]$environmentManifest = Get-Content $environmentManifestPath
	    return $environmentManifest.environment.expertDatabaseServer.databaseConnection.password
	}
}

process {
	$path = GetSourceDirectory $environmentManifestPath
	[System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($path, "Aderant.Framework.dll")) | Out-Null
	
	$encryption = New-Object Aderant.Framework.Encryption		
	$password = GetPassword $environmentManifestPath
	
	Write-Debug "Decrypting manifest password"	
	$password = $encryption.Decrypt($password)	

	$server = GetDatabaseServer $environmentManifestPath
	$database = GetDatabaseName $environmentManifestPath
	$login = GetLogin $environmentManifestPath	
	
	Write-Debug "Using connection $server.$database ($login [$password])"
	
	Write-Host "Loading database module"	
	$module = [System.IO.Path]::Combine($path, "Aderant.Database.Build.dll")
	
	if (Test-Path $module) {
		Import-Module $module		
		Update-ExpertDatabase -Server $server -Database $database -Login $login -Password $password -DeploymentToolPath $deploymentToolPath -UpdateBundlePath $updateBundlePath -interactive
	} else {
		Write-Error "Module assembly was not found at path: $path"
	}
}