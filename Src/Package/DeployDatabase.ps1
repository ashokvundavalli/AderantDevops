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
	
	$CurrentUser = [Environment]::UserName
	if ($CurrentUser -like '*TFSBuild.AP') {
		try{
			Write-Host "Adding Service.TFSBuild.AP to the database"
			$AddDBUserCommand = "'n 
				USE $database
				IF NOT EXISTS (SELECT * FROM HBM_PERSNL WHERE LOGIN LIKE '%TFSBUILD.AP')
				BEGIN 
					DECLARE @PERSNLKEY INT
					DECLARE @NAMEKEY INT 
					EXEC SP_CMSNEXTKEY_OUTPUT 'HBM_PERSNL',1, @PERSNLKEY OUTPUT  
					EXEC SP_CMSNEXTKEY_OUTPUT 'HBM_NAME',1, @NAMEKEY OUTPUT   
					INSERT INTO HBM_NAME (ADDRESS_UNO,MIDDLE_NAME,FIRST_NAME,
					LAST_NAME,NAME_SORT,NAME_UNO,NAME_TYPE,NAME,PEOPLE_NAME_UNO,
					INACTIVE,AVAIL_EM,AUTH_EMUSER, LAST_MODIFIED) VALUES (0,'',
					'ExpertAP','Service','Service',@NAMEKEY,'P','Service, TFSBuild.AP, ExpertAP',0,'N','N',0, 
					getdate() )
					INSERT INTO HBM_NAME_PEOPLE (NUM_CHILD,SPOUSE_NAME_UNO,
					TITLE,SUFFIX,SALUTATION,GENDER,NAME_UNO, LAST_MODIFIED) 
					VALUES (0,0,'','','','N',@NAMEKEY, getdate() )
					INSERT INTO HBM_PERSNL (TB_EMPL_UNO,EMAIL,FAX,
					AUTH_CONTEXT_UNO,CURRENCY_CODE,NAME_UNO,DE_EDLIST_REQD,
					HLDY_GROUP_CODE,PREV_TERM_DATE,PREV_HIRE_DATE,
					TERMINATE_DATE,HIRE_DATE,SUPERV_EMPL_UNO,WORK_TYPE_CODE,
					LOGIN,LOCATION,PHONE_NO,SORT_POS,EDIT_DATE,PERSNL_TYP_CODE,
					PROF,DEPT,OFFC,INITIALS,INTERNAL_NUM,EMPLOYEE_NAME,
					EMPLOYEE_CODE,INACTIVE,SECURITY_ID,EMPL_UNO,PRIVILEGE_LEVEL,
					COMP_YEAR,GRAD_YEAR,POSITION,APP_GROUP_UNO,BOOK, 
					LAST_MODIFIED) VALUES (@PERSNLKEY,'','',0,'USD',@NAMEKEY,'N','',NULL,NULL,
					NULL,'2014-07-14',@PERSNLKEY,'','SERVICE.TFSBUILD.AP','','',0,NULL,'','FIRM','ADM',
					'TLH','','','Service, TFSBuild.AP','SSTFS','N',0,@PERSNLKEY,'1',0,0,'',1000000010,1,
					getdate() )
					INSERT INTO TBM_PERSNL (PB_REVIEW_APPROVAL_WF_NAME,
					PB_MARKUP_WF_NAME,ADMIN_ASSIST_EMPL_UNO,ASSIGN_PERIOD,
					ST_ACTIVE_EMPL,SHIFT_ID,WKSTATUS_CODE,FUNCTIONAL_PCNT,
					REIMBURSE_CURR,WEEKLY_HRS,BL_PRINTER_UNO,PB_PRINTER_UNO,
					TIME_OK,NEED_AUTH,RSCLASS_CODE,TEXT_ID,BILL_HR_MIN,
					NBILL_HR_MIN,PARTIME_PCNT,SPEC_RATE_OK,RANK_CODE,
					RANK_CODE_DATE,PREV_RANK_CODE,LOCATION_CODE,ASSIST_EMPL_UNO,
					EMPL_UNO,PB_FORMAT_UNO,BL_FORMAT_UNO,RS_FORMAT_UNO,
					OVERTIME_ELIG,SATELLITE_ONLY,SATELLITE_DB_UNO,
					SEPARATE_CHECK,PB_MARKUP_LAUNCH_METHOD, LAST_MODIFIED) 
					VALUES ('','',0,'','N',0,'',0,'USD',0.0000000000,0,0,'Y',
					'N','',0,0.0000000000,0.0000000000,0,'N',1,'2014-07-14',1,
					'',0,@PERSNLKEY,0,0,0,'N','N',0,'N','SY', getdate() )
				END"

			Invoke-SqlCmd -ServerInstance $server -Query $AddDBUserCommand -ErrorAction Stop
			Write-Host "Service.TFSBuild.AP added to $database"
		}
		catch [System.Exception]{
			Write-Host "Exception thrown while attempting to add Service.TFSBuild.AP to $database"
			Write-Host $error
		}
	}
}