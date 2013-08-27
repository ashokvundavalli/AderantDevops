param ([string]$environmentManifestPath, [string]$expertSourceDirectory, [switch]$interactive)

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
    $ErrorActionPreference='Stop'
    $fw = [System.IO.Path]::Combine($expertSourceDirectory, "Aderant.Framework.dll")
    
    if (-not (Test-Path $fw)) {
        throw [string]"Aderant.Framework.dll not found at path: $fw"
    }
    
    [System.Reflection.Assembly]::LoadFrom($fw) | Out-Null
    
    $encryption = New-Object Aderant.Framework.Encryption		
    $password = GetPassword $environmentManifestPath
    
    Write-Debug "Decrypting manifest password"	
    $password = $encryption.Decrypt($password)	

    $server = GetDatabaseServer $environmentManifestPath
    $database = GetDatabaseName $environmentManifestPath
    $login = GetLogin $environmentManifestPath	
    
    Write-Debug "Using connection $server.$database ($login [$password])"
    
    Write-Host "Loading database module"	
    $module = [System.IO.Path]::Combine($expertSourceDirectory, "Aderant.Database.Build.dll")
    
    if (Test-Path $module) {
        Import-Module $module
        
        $upd = "$expertSourceDirectory\Database\EXPERT_1.UPD"
        
        if (Test-Path $upd) {            
            Update-ExpertDatabase -Server "$server" -Database "$database" -Login "$login" -Password "$password" -DeploymentToolPath "$expertSourceDirectory" -DeploymentManifestPath "$upd" -interactive:$interactive
        } else {
            throw [string]"No EXPERT_1.UPD exists at path: $upd"
        }
    } else {
        throw [string]"Module assembly was not found at path: $path"
    }
}