<# 
.SYNOPSIS 
    This script helps to set up an ADERANT Expert development environment
.DESCRIPTION 
    The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment
.NOTES 
    File Name  : Setup-Development-Environment.ps1     
    Requires   : PowerShell V2 
.EXAMPLE 
    PSH [C:\source\expertsuite]: .Setup-Development-Environment.ps1 -defaults setupDefaults.xml' 
#>
    
    <# 
    .SYNOPSIS 
        This script helps to set up an ADERANT Expert development environment
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment    
    .NOTES 
        http://ttwiki/wiki/index.php?title=How_to_Apply_a_DBGen_Script         
    #>
    Function global:Reset-ExpertCMSNetLocally(){
        write "getting CMSNet"                                
        
        $cmsNetLocalPath = Get-ValueSpecifiedFor cmsNetLocalPath
        $cmsNetDownloadPath = Get-ValueSpecifiedFor cmsNetDownloadPath
        
        if(!(Test-Path $cmsNetLocalPath)){
            New-Item -Path $cmsNetLocalPath -ItemType directory
        }
        if(Test-Path $cmsNetDownloadPath){            
            Copy-Item -Path $cmsNetDownloadPath\* -Destination $cmsNetLocalPath -Force -Recurse -ErrorAction Stop            
            write "CMSNet downloaded to $cmsNetLocalPath"
        }else{
            throw "Path not found for $cmsNetDownloadPath"
        }            
    }
    
    <# 
    .SYNOPSIS 
        This script helps to set up an ADERANT Expert development environment
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment    
    .NOTES 
        http://ttwiki/wiki/index.php?title=How_to_Apply_a_DBGen_Script         
    #>
    Function global:Create-ExpertCMSIniFile(){
        $cmsIniFile = 'CMS.INI' 
        $cmsIniPath = (Join-Path (Get-ChildItem ENV:WINDIR).Value $cmsIniFile)
        
        if(Test-Path $cmsIniPath){
            write "$cmsIniFile already exists at $cmsIniPath"
            return 
        }else{
            $cmsNetLocalPath = Get-ValueSpecifiedFor cmsNetLocalPath
            Copy-Item -Path $cmsNetLocalPath\$cmsIniFile -Destination (Get-ChildItem ENV:WINDIR).Value
            write "created $cmsIniFile at $cmsIniPath"                    
        }    
    }

    <# 
    .SYNOPSIS 
        This script helps to set up an ADERANT Expert development environment
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment    
    .NOTES 
        http://ttwiki/wiki/index.php?title=How_to_Apply_a_DBGen_Script         
    #>
    Function global:Start-ExpertDBGen(){
        write "Executing DBGen"         
        $cmsNetLocalPath = Get-ValueSpecifiedFor cmsNetLocalPath                   
        pushd $cmsNetLocalPath\bin
        .\dbgen.exe expertmode=1 | Out-Null -ErrorAction Stop        
        popd   
    }
    
    <# 
    .SYNOPSIS 
        Create a new database and set the owner
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment    
    .NOTES 
        http://ttwiki/wiki/index.php?title=How_to_Apply_a_DBGen_Script         
    #>
    Function global:Add-ExpertDatabase{
    
        $owner = Get-ValueSpecifiedFor expertDatabaseOwner        
        $dbInstance = Get-ValueSpecifiedFor sqlServerInstance
        $databaseName = Get-ValueSpecifiedFor expertDatabaseName
        
        [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo")        
        
        $dbServer = new-object Microsoft.SqlServer.Management.Smo.Server ($dbInstance)
        $db = new-object Microsoft.SqlServer.Management.Smo.Database 
        $db.Name = $databaseName
        $db.Parent = $dbServer        
        $db.Create()                
        $db.SetOwner($owner, $true)
    }
    
    <# 
    .SYNOPSIS 
        Adds the owner to the logins if they donm't exist already
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment    
    .NOTES 
        http://ttwiki/wiki/index.php?title=How_to_Apply_a_DBGen_Script         
    #>
    Function global:Add-ExpertDatabaseOwner{
    
        $owner = Get-ValueSpecifiedFor expertDatabaseOwner
        $ownerPassword = Get-ValueSpecifiedFor expertDatabaseOwnerPassword
        $dbInstance = Get-ValueSpecifiedFor sqlServerInstance
        $databaseName = Get-ValueSpecifiedFor expertDatabaseName
        
        [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo")
        [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SqlEnum")
        
        $dbServer = new-object Microsoft.SqlServer.Management.Smo.Server ($dbInstance)
        
        if(!$dbServer.Logins.Contains($owner)){        
            $login = New-Object Microsoft.SqlServer.Management.Smo.Login ($dbServer, $owner)
            $login.DefaultDatabase = $databaseName;
            $login.LoginType = New-Object Microsoft.SqlServer.SqlEnum.LoginType.SqlLogin; 
            $login.Enable(); 
            $login.Create($ownerPassword);         
        }
    }
    
    <# 
    .SYNOPSIS 
        Check that database exists on the given instance
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment        
    #>
    Function global:DatabaseExists([string]$expertDatabaseName, [string]$dbInstance){                    
        [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.Smo") | Out-Null
        $dbServer = new-object Microsoft.SqlServer.Management.Smo.Server ($dbInstance) | Out-Null   
                        
        foreach ($_ in $dbServer.Databases){
            if ($_.Name -eq $expertDatabaseName){    
                return $true
            }
        }
        return $false    
    }
    
    <# 
    .SYNOPSIS 
        Create a new Expert Database, or update if one already exists
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment        
    #>
    Function global:Create-ExpertDatabase(){
    
        $expertDatabaseName = Get-ValueSpecifiedFor expertDatabaseName
        $dbInstance = Get-ValueSpecifiedFor sqlServerInstance         
        $exists = DatabaseExists $expertDatabaseName $dbInstance
               
        if($exists){
            write "Databse $expertDatabaseName already exists!"            
        }else{
            write "Databse $expertDatabaseName about to be created!"
            Add-ExpertDatabase | Out-Null -ErrorAction Stop
            Restore-ExpertDatabaseFromBackup | Out-Null -ErrorAction Stop
            write "Databse $expertDatabaseName has been created!"
        }                  
    }
    
    <# 
    .SYNOPSIS 
        Update an existing Expert Database
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment        
    #>
    Function global:Update-ExpertDatabase(){        
        $expertDatabaseName = Get-ValueSpecifiedFor expertDatabaseName
        $dbInstance = Get-ValueSpecifiedFor sqlServerInstance
        write "Databse $expertDatabaseName about to be updated!"
        if(!(DatabaseExists $expertDatabaseName $dbInstance)){
            write "Databse $expertDatabaseName does not exist!"            
        }else{
            Start-ExpertDBGen
        }
        write "Databse $expertDatabaseName has been updated!"
    }

    <#

    #>
    Function global:Restore-ExpertDatabaseFromBackup{                
        $expertDatabaseName = Get-ValueSpecifiedFor expertDatabaseName
        $dbInstance = Get-ValueSpecifiedFor sqlServerInstance
        $dbOwner = Get-ValueSpecifiedFor expertDatabaseOwner                
        $serverExpertDatabaseBackupPath = Get-ValueSpecifiedFor serverExpertDatabaseBackupPath
        $databaseBackupFile = Get-ValueSpecifiedFor databaseBackupFile
        $localExpertDatabaseBackupDirectory = Get-ValueSpecifiedFor localExpertDatabaseBackupDirectory        
                            
        #load assemblies
        [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SMO") | Out-Null
        [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SmoExtended") | Out-Null
        [Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.ConnectionInfo") | Out-Null
        [Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SmoEnum") | Out-Null
            
        #copy backupFile    
        if(!(Test-Path $localExpertDatabaseBackupDirectory)){
            New-Item -Path $localExpertDatabaseBackupDirectory -ItemType directory
        }
                
        $backupFile = (Get-ChildItem -Path $serverExpertDatabaseBackupPath | Where-Object{$_.Name -like $databaseBackupFile+'*'}).Name        
        $pathToLatestBackupFile =  Join-Path $serverExpertDatabaseBackupPath $backupFile
        
        Copy-Item -Path $pathToLatestBackupFile -Destination $localExpertDatabaseBackupDirectory -Force
        
        $pathToCopiedBackupFile = Join-Path $localExpertDatabaseBackupDirectory $backupFile

        #we will query the database name from the backup header later
        $server = New-Object ("Microsoft.SqlServer.Management.Smo.Server") $dbInstance
        <#
        $backupDevice = New-Object("Microsoft.SqlServer.Management.Smo.BackupDeviceItem") ($pathToCopiedBackupFile, "File")
        $smoRestore = new-object("Microsoft.SqlServer.Management.Smo.Restore")
             
        #restore settings
        $smoRestore.NoRecovery = $false;
        $smoRestore.ReplaceDatabase = $true;
        $smoRestore.Action = "Database"
        $smoRestorePercentCompleteNotification = 10;
        $smoRestore.Devices.Add($backupDevice)

        #get database name from backup file
        $smoRestoreDetails = $smoRestore.ReadBackupHeader($server)    
        #give a new database name
        $smoRestore.Database =$expertDatabaseName
        #specify new data and log files (mdf and ldf)
        $smoRestoreFile = New-Object("Microsoft.SqlServer.Management.Smo.RelocateFile")
        $smoRestoreLog = New-Object("Microsoft.SqlServer.Management.Smo.RelocateFile")

        #the logical file names should be the logical filename stored in the backup media
        $smoRestoreFile.LogicalFileName = $expertDatabaseName
        $smoRestoreFile.PhysicalFileName = $server.Information.MasterDBPath + "\" + $expertDatabaseName + "_Data.mdf"
        $smoRestoreLog.LogicalFileName = $expertDatabaseName + "_Log"
        $smoRestoreLog.PhysicalFileName = $server.Information.MasterDBLogPath + "\" + $expertDatabaseName + "_Log.ldf"
        $smoRestore.RelocateFiles.Add($smoRestoreFile)
        $smoRestore.RelocateFiles.Add($smoRestoreLog)
        #restore database
        $smoRestore.SqlRestore($server)
        #>
        
        ###########
        ## Restore the database
        $Restore = new-object "Microsoft.SqlServer.Management.Smo.Restore"
        $Restore.Database = $expertDatabaseName
        $Restore.Action = 'Database'
        $BkFile = new-object "Microsoft.SqlServer.Management.Smo.BackupDeviceItem"
        $BkFile.DeviceType = 'File'
        $BkFile.Name = $pathToCopiedBackupFile
        $Restore.Devices.Add($BkFile)
        $Restore.ReplaceDatabase = $true

        ## Check file list and generate new file names if files already exists
        $DateSerial = Get-Date -Format yyyyMMddHHmmss
        $DataFiles = $Restore.ReadFileList($Server)
        ForEach ($DataRow in $DataFiles) {
                $LogicalName = $DataRow.LogicalName
                $PhysicalName = $DataRow.PhysicalName
                $FileExists = Test-Path $PhysicalName
                if ($FileExists) {
                        $PhysicalName = $PhysicalName -replace(".mdf", "_$DateSerial.mdf")
                        $PhysicalName = $PhysicalName -replace(".ldf", "_$DateSerial.ldf")
                        $PhysicalName = $PhysicalName -replace(".ndf", "_$DateSerial.ndf")
                        $Restore.RelocateFiles.Add((new-object microsoft.sqlserver.management.smo.relocatefile -ArgumentList $LogicalName, $PhysicalName)) | out-null;
                }
        }
        $Restore.NoRecovery = $false
        $Restore.PercentCompleteNotification = 5
        $Restore.SqlRestore($server)
        if (!$error){
                write-host "`tDatabase $Database restored from $Backup" -f green
        } else {
                RaisError "`tRestore of database $Database returned an error."
                Exit
        }
        
        #########
            
    }

    <#

    #>
    Function global:Install-ExpertDotNETVersion{                
        
        $msDotNetFrameworkInstaller = Get-ValueSpecifiedFor msDotNetFrameworkInstaller
        $msDotNetVersion = Get-ValueSpecifiedFor msDotNetVersion
                                        
        [bool]$noUpdate = $false
        $major = $msDotNetVersion.Split(".")[0]
        $minor = $msDotNetVersion.Split(".")[1]
        
        if(Test-Path "hklm:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v$major\Full"){
            $noUpdate=$true
        }
        if(Test-Path "hklm:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v$major.$minor"){
            $noUpdate=$true
        }    
            
        if($noUpdate){
            ".NET version $version is installed"
        }else{
            write "about to install version $version of the .NET framework."                                   
            Invoke-Item $msDotNetFrameworkInstaller | Out-Default -ErrorAction Stop
            Write "finished installing version $version of the .NET framework."
        }
    }

    Function Enable-DistributedTransactionCoordinator(){

    }

    Function Enable-WebDeploymentTool(){
        #Enable IISmanger UI Module
                
        #Add MS deploy into path
        #C:\Program Files\IIS\Microsoft Web Deploy        
    }
    
    <#
        http://blogs.msdn.com/motleyqueue/archive/2007/10/20/unattended-msmq-installation-on-windows-vista.aspx
    #>
    Function global:Enable-MSMQ(){        
        $msmqStatus = (Get-Service MSMQ -ErrorAction SilentlyContinue).Status
        if(!$msmqStatus){
            pkgmgr /iu:'MSMQ-Container;MSMQ-Server'    
        }    
    }
    
    <#
        Import the required SQL Server settings using SAC
        http://msdn.microsoft.com/en-us/library/ms162800(SQL.90).aspx
        SQL Server 2008 we can do the following
        http://blogs.msdn.com/sql_protocols/archive/2008/08/29/configuring-sql-protocols-through-windows-powershell.aspx
    #>
    Function global:Configure-ExpertSQLServerSettings{            
        $sqlServerInstance = Get-ValueSpecifiedFor sqlServerInstance
        $sqlServerSettingsFile = Get-ValueSpecifiedFor sqlServerSettingsFile
        $sqlServerProgramPath = Get-ValueSpecifiedFor sqlServerProgramPath
                
        pushd $sqlServerProgramPath
        .\SAC.exe in $sqlServerSettingsFile –S $sqlServerInstance | Out-Null -ErrorAction Stop
        popd
    }



    Function Enable-ExpertMSDTCSettings(){
        <#
        <registryPrerequisite path="HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSDTC\Security\NetworkDtcAccess"
                              value="1"
                              description="MSDTC configured to allow remote access."/>
        <registryPrerequisite path="HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSDTC\Security\NetworkDtcAccessTransactions"
                              value="1"
                              description="MSDTC configured to allow transaction participation."/>
        <registryPrerequisite path="HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSDTC\Security\NetworkDtcAccessInbound"
                              value="1"
                              description="MSDTC configured to allow participating in an existing remote transaction."/>
        <registryPrerequisite path="HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSDTC\Security\NetworkDtcAccessOutbound"
                              value="1"
                              description="MSDTC configured to allow co-ordination of new remote transactions."/>
        #>
        
        $machineName = Get-ValueSpecifiedFor machineName
        
        Stop-Service *MSDTC* -ErrorAction Stop
        
        $reg = [Microsoft.Win32.RegistryKey]::OpenRemoteBaseKey('LocalMachine', $machineName)  
        $msdtcSecurityKey= $reg.OpenSubKey("SOFTWARE\Microsoft\MSDTC\Security", $true)
        
        $msdtcSecurityKey.SetValue('NetworkDtcAccess',1)
        $msdtcSecurityKey.SetValue('NetworkDtcAccessTransactions',1)
        $msdtcSecurityKey.SetValue('NetworkDtcAccessInbound',1)
        $msdtcSecurityKey.SetValue('NetworkDtcAccessOutbound',1)
        
        Start-Service *MSDTC* -ErrorAction Stop
    }

    Function Add-ExpertUsersAsAdministrators(){
        [Array]$machineAdministrationUsers = Array(Get-ValueSpecifiedFor machineAdministrationUsers)
        $machine = Get-ValueSpecifiedFor machineName
        
        foreach($user in $machineAdministrationUsers){
            ([adsi]”WinNT://$machine/administrators,group”).add(“WinNT://$user”)    
        }        
    }            

    <#
        http://ttwiki/wiki/index.php?title=Deployment_for_non_developers#Network_Share
    #>
    Function Create-ExpertNetworkShare([string]$path){
        if(!(Test-Path $path)){
            New-Item $path -ItemType directory
        }        
        $shareName = (Split-Path $path -Leaf)        
        write "Sharing $path as $shareName"        
        net share $shareName/$path /remark:"Expert network share"                
        write "Finished creating networkshare "
    }


    # Check .NET4 security to see if this is required # Function Set-CASPOL($networkSharePath){}


    <#
        http://blogs.msdn.com/motleyqueue/archive/2007/10/20/unattended-msmq-installation-on-windows-vista.aspx
    #>
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the standard prerequites for an Expert environment
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment                 
    .LINKS
        http://blogs.msdn.com/motleyqueue/archive/2007/10/20/unattended-msmq-installation-on-windows-vista.aspx    
    #>
    Function Enable-ExpertIISSettings(){  
        write "start Enabling of IIS"
        pkgmgr /iu:'IIS-WebServerRole;IIS-WebServer;IIS-CommonHttpFeatures;IIS-StaticContent;IIS-DefaultDocument;IIS-DirectoryBrowsing;IIS-HttpErrors;IIS-HttpRedirect;IIS-ApplicationDevelopment;IIS-ASPNET;IIS-NetFxExtensibility;IIS-ASP;IIS-CGI;IIS-ISAPIExtensions;IIS-ISAPIFilter;IIS-ServerSideIncludes;IIS-HealthAndDiagnostics;IIS-HttpLogging;IIS-LoggingLibraries;IIS-RequestMonitor;IIS-HttpTracing;IIS-CustomLogging;IIS-ODBCLogging;IIS-Security;IIS-BasicAuthentication;IIS-WindowsAuthentication;IIS-DigestAuthentication;IIS-ClientCertificateMappingAuthentication;IIS-IISCertificateMappingAuthentication;IIS-URLAuthorization;IIS-RequestFiltering;IIS-IPSecurity;IIS-Performance;IIS-HttpCompressionStatic;IIS-HttpCompressionDynamic;IIS-WebServerManagementTools;IIS-ManagementConsole;IIS-ManagementScriptingTools;IIS-ManagementService;IIS-IIS6ManagementCompatibility;IIS-Metabase;IIS-WMICompatibility;IIS-LegacyScripts;IIS-LegacySnapIn;WAS-WindowsActivationService' |  
        Out-Null -ErrorAction Stop  
        write "finished Enabling IIS"
    }
    
    <# 
    .SYNOPSIS 
        Will install AppFabric on the machine
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment                 
    #>
    Function Install-AppFabric(){    
        $installDirectory = Get-ValueSpecifiedFor appFabricInstallDirectory
        pushd $installDirectory
        Setup.exe /i WORKER,CACHESERVICE
        popd    
    }
    
    <# 
    .SYNOPSIS 
        Will Enable-PSRemoting as required by the Query Service
    .DESCRIPTION 
        The function will enable PS Remoting that is a requirement for the Query Service
    #>
    Function global:Enable-RemotePowerShell(){
        write "start Enable-PSRemoting"
        Enable-PSRemoting
        write "finished Enable-PSRemoting"
    }

    <#
        http://ttwiki/wiki/index.php?title=Query#Installation
    #>
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the standard prerequites for an Expert environment
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment                 
    #>
    Function global:Register-ASPDotNETWithIIS(){
        write "start ASP.NET Register with IIS"
        aspnet_regiis.exe -i
        write "finished ASP.NET Register with IIS"
    }

    #Sets all environment defaults
    Function global:Get-ValueSpecifiedFor([string]$parameter) {        
        $defaultsFile = "C:\ExpertPrerequisites\DevelopmentEnvironmentDefaults.xml"
                
        # read the load default file    
        if (test-path $defaultsFile) {
            [xml]$defaults = Get-Content $defaultsFile
            $value = $defaults.EnvironmentDefaults.$parameter.Value            
            return $value    
        } else {
            Throw "Unable to read $defaultsFile file"
        }
    }
    
    
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the standard prerequites for an Expert environment
    .DESCRIPTION 
        The script will setup you machine setup by step with the pre-requisits and configuration required before running deployment                 
    #>
    Function global:Configure-ExpertDeploymentEnvironment{
        Install-ExpertDotNETVersion
        Configure-ExpertSQLServerSettings
        Create-ExpertDatabase
        Install-AppFabric
        Create-ExpertNetworkShare
        Add-ExpertUsersAsAdministrators
    }
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the Configuration Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertConfigurationRolePrerequisites{                
        Enable-ExpertMSDTCSettings                                           
    }
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the Identity Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertIdentityRolePrerequisites{
    
    }
    
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the QueryService Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertQueryServiceRolePrerequisites{
        <#
        <!-- ensure that IIS7 is running-->
        <registryPrerequisite path="HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\W3SVC\Parameters\MajorVersion"
                              value="7"
                              description="Version of IIS is 7."/>
        <servicePrerequisite serviceName="W3SVC"
                             description="Ensure World Wide Web Publishing Service is available"/>
        
        <!-- ensure windows remote management is running-->
        <servicePrerequisite serviceName="WinRM"
                             description="Ensure Windows Remote Management (WS-Management) service is available"/>
        
        <!-- ensure that .NET 4.0 is installed, 3.0 is checked as a minimum to support the pre-req service -->
        <dotNetPrerequisite minimumVersion="4.0.0.0"
                            description=".NET Framework version 4.0 or greater is installed."/>
        
        <!-- ensure remote powershell is working-->
        <powerShellPrerequisite description="Ensure that remote powershell is enabled and configured"/>
        #>    
        
        Enable-RemotePowerShell
        Enable-ExpertIISSettings
        Register-ASPDotNETWithIIS
        Enable-WebDeployment    
    }
    
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the Security Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertSecurityRolePrerequisites{
    }
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the Workflow Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertWorkflowRolePrerequisites{
    }
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the Messaging Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertMessagingRolePrerequisites{
        Enable-MSMQ
    }
    
    <# 
    .SYNOPSIS 
        Will setup and/or configure the Monitoring Role Prerequisites
    .DESCRIPTION 
        The script will setup you machine setup by step with the prerequisites and configuration required before running deployment                 
    #>
    Function global:Install-ExpertMonitoringRolePrerequisites{
    
    }


