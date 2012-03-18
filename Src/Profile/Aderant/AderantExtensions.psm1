[string]$global:NetworkShare
[string]$global:ExpertPath
[xml]$global:EnvironmentManifest

function Load-ExpertEnvironmentManifest($manifestShortName) {
    if ([string]::IsNullOrEmpty($manifestShortName)) {
        $manifestShortName = "local"
    }

    $manifest = $null
    if ($global:BranchExpertVersion -eq "8") {
        $manifestShortName = ""
        
        $testPath = "$global:BranchEnvironmentDirectory\$manifestShortName.environment.xml"
        
        if ((Test-Path $testPath) -eq $false) {
            Write-Warning "Could not find $testPath. Looking in $BranchEnvironmentDirectory"
            
            $testPath = "$global:BranchEnvironmentDirectory\..\Binaries\environment.xml"
            
            if ((Test-Path $global:BranchEnvironmentDirectory\..\Binaries\environment.xml) -eq $false) {
                Write-Warning "Could not find $testPath - no manifest will be loaded!"
                return
            } else {
                Write-Host "Found manifest in $global:BranchEnvironmentDirectory"
                $manifest = $testPath
            }
        }
    } else {
        $manifest = "$global:BranchEnvironmentDirectory\$manifestShortName.environment.xml"
    }

    if ($manifest -eq $null) {
        return
    }
        
    [xml]$global:EnvironmentManifest = Get-Content $manifest
    [string]$global:NetworkShare = $global:EnvironmentManifest.environment.networkSharePath
    [string]$global:ExpertPath = $global:EnvironmentManifest.environment.servers.server.expertPath
    
    $manifestInfo = "Manifest      : $manifest"
    $shareInfo =    "Network Share : $global:NetworkShare"
    
    $len = 0
    if ($manifestInfo.Length -gt $shareInfo.Length) {
        $len = $manifestInfo.Length
    } else {
        $len = $shareInfo.Length
    }
    
    for ($i = 0; $i -ne $len + 10; $i++) {
        $header += "-"
    }
    Write-Host ""   
    Write-Host $header    
    Write-Host $manifestinfo
    Write-Host $shareInfo    
    Write-Host $header
    Write-Host ""
}


<# 
.Synopsis 
    Stops all Expert processes traumatically.
.Description
#>
function Stop-ExpertEnvironment {       
    $apps = Get-ChildItem $global:NetworkShare
    
    Invoke-Expression "taskkill /IM Expert* /F"    
    
    foreach($app in $apps) {
        if ($app.Name.EndsWith('.exe')) {
            Invoke-Expression "taskkill /IM $app /F"
        }
    }
    
    Invoke-Expression "iisreset /STOP"
    Invoke-Expression "de stop local"    
    
    Write-Host "The halls are eerily quiet after vanquishing your enemies."    
}


<# 
.Synopsis 
    Sets all services containing "ADERANT" to Manual Start 
.Description
#>
function Set-ExpertServicesToManualStart {
    Write-Host "Setting services to manual start..."
    $key = "HKLM:\SYSTEM\CurrentControlSet\Services\"
    $Services = ls -path $key

    foreach ($s in $services) {
        $serviceName = ($s.name.split("\"))[($s.name.split("\").count-1)]
        $ss = get-ItemProperty -path registry::$s -erroraction silentlycontinue

        if ($serviceName.Contains("Aderant.")) {
            Write-Host "Setting start up type to manual for service: $serviceName"
            $service = get-service $serviceName
            set-Service $serviceName -startuptype manual
        }
    }
}  


<# 
.Synopsis 
    Clears the local cache.
.Description
#>
function Clear-ExpertCache([switch]$killApps) {
    if ($killApps -eq $true) {
        Invoke-Expression "cmd /c taskkill /IM ExpertAssistant* /F"
        Invoke-Expression "cmd /c taskkill /IM ExpertTimer* /F"
    }
    
    $env = $global:EnvironmentManifest.environment.name
    Remove-Item -Force "${env:APPDATA}\ADERANT\$env" -recurse
}


<# 
.Synopsis 
    Creates a symlink for log4net.xml from AderantExpert\log4net.xml to the Application Services.
.Description
#>
function Setup-EnvironmentLogging {
    $location = (Get-ChildItem Env:userprofile).Value
    
    $log4net = [System.IO.Path]::Combine($location, "AderantExpert", "log4net.xml")
    
    if (![System.IO.File]::Exists($log4net)) {
        Write-Warning "No log4net.xml found at $log4net. Logging not setup."
        return
    }    
    
    Invoke-Expression "cmd /c del $global:ExpertPath\LegacyServices\log4net.xml"
    Invoke-Expression "cmd /c del $global:ExpertPath\SharedBin\log4net.xml"       
    
    Invoke-Expression "cmd /c mklink $global:ExpertPath\LegacyServices\log4net.xml $log4net"
    Invoke-Expression "cmd /c mklink $global:ExpertPath\SharedBin\log4net.xml $log4net"
}


<# 
.Synopsis 
    Prepares the a database after restoring from backup.
.Description
    Grants privilege level 1.
    Creates the service accounts.
    Sets up the SQL Server broker.
    Removes the read only flag.
#>
function Setup-ExpertDatabase {
    $expertConnection = Get-AderantExpertDatabaseSettings $global:EnvironmentManifest
    $database = $expertConnection.Database    
       
    $sql = "
    use [$database]
    go    
    alter database [$database] set read_write with no_wait
    go    
    exec dbo.sp_changedbowner @loginame = N'cmsdbo', @map = false 
    go
    alter database [$database] set recovery simple with no_wait
    go    
    alter database [$database] set new_broker with rollback immediate
    go
    alter database [$database] set enable_broker with rollback immediate
    go    
    alter database [$database] set trustworthy on with rollback immediate
    go   
    update hbm_persnl set privilege_level = 1 where login = (select substring(suser_sname(),(charindex('\',suser_sname()) + 1),len(suser_sname())))
    go"
    
    ExecuteNonQuery $sql
}


<# 
.Synopsis 
    Synchronizes the network share and services with the local binaries.
.Description
    Use -share to copy current module binaries to the network share only.
.Parameter share
    Copies modified files to the network share.
.Parameter services
    Copies modified files to services.
.Parameter sharedBin
    Copies modified files to the IIS shared bin.
#>
function Update-ExpertDeployment([switch]$share = $true, [switch]$services = $false, [switch]$sharedBin = $false, [switch]$legacy = $false) {
    $src = $null
    
    # Handle V8 or older
    if ($BranchExpertVersion -eq "8") {        
        $src =  $global:BranchExpertSourceDirectory
    } else {
        Write-Debug "Version 8 not detected. Using legacy branch."
        $src = $BranchBinariesDirectory
    }
    
    if ($src -eq $null) {
        Write-Error "Unable to determine current branch version."
        return
    }   
     
    Write-Host "Source [$src]" -ForegroundColor yellow
    
    # just incase the user forgot :)
    Copy-BinariesFromCurrentModule
    
    if ($share -eq $true) {                        
        UpdateTarget $src $global:NetworkShare
    }               
    
    if ($services -eq $false) {
        return 
    }
            
    #Invoke-Expression "iisreset /stop"
    
    $applicationServices = [System.IO.Path]::Combine($global:ExpertPath, "Services", "ApplicationServices")
    $frameworkServices = [System.IO.Path]::Combine($global:ExpertPath, "Services", "FrameworkServices")        
    $sharedBinaries = [System.IO.Path]::Combine($global:ExpertPath, "SharedBin")
    $legacyServices = [System.IO.Path]::Combine($global:ExpertPath, "LegacyServices")
                   
    UpdateServices $src $applicationServices
    UpdateServices $src $frameworkServices

    if ($sharedBin) {        
        UpdateServices $src $sharedBinaries            
    }
    
    #Invoke-Expression "iisreset /start"
    
    if ($legacy) {
        UpdateServices $src $legacyServices
    }    
}


<# 
.Synopsis 
    Sets the branch build flavor (release or debug)
.Parameter release 
    Changes the build flavor to Release.
.Parameter debug
    Changes the build flavor to Debug.
.Parameter nocheckout
    Does not check the file out when changing flavors.
#>
function Set-BuildFlavor([switch]$debug, [switch]$release, [switch]$nocheckout) {
    if ($release) {
        $shell = ".\Set-BuildFlavor.ps1 -release"    
    } else {
        $shell = ".\Set-BuildFlavor.ps1 -debug"    
    }
    
    if ($nocheckout) {
        $shell += " -nocheckout"
    }
    
    pushd $global:BuildScriptsDirectory
    invoke-expression $shell
    popd
}

<# 
.Synopsis 
    Deletes all workflows and tasks
.Description
    During development we frequently get a w3wp.exe crash. This is caused when you change the development workflow definition while there are still
    running instances. The existing instances cannot be deserialized correctly as the activity or process data hierarchy gets messed up and so a crash occurs.
    Solution... delete all running workflow instances.
.Notes
    See: http://ttwiki/wiki/index.php?title=Visual_Studio_Just-In-Time_Debugger_error_-_unhandled_exceptions_in_w3wp.exe    
#>
function Remove-ExpertWorkflowsAndTasks {
    $sql = "
delete from workflow.WorkflowSummary
delete from Workflow.ActivityEventTrack
delete from Workflow.Task

-- Delete the Security Rows for Workflows that Don’t Exist
delete Security.FWA_PolicyResourceInstance
from Security.FWA_PolicyResourceInstance pri
inner join Security.FWM_Policy p ON pri.PolicyId = p.PolicyId AND p.PolicyType = 'V'
inner join Security.FWM_ResourceHierarchy rh ON pri.HierarchyNodeID = rh.HierarchyNodeID
AND rh.NodePath = 'Framework.Workflow.Process' OR rh.NodePath = 'Framework.Workflow.Task'

-- Delete Permissions in policies that are transient, and not actually being used to secure something
delete Security.FWA_Permission
from Security.FWA_Permission pp
inner join Security.FWM_Policy po on pp.PolicyID = po.PolicyID and po.PolicyType = 'V'
left outer join Security.FWA_PolicyResourceInstance pri on po.PolicyID = pri.PolicyID
where pri.PolicyID is null

-- Delete Grantees in policies that are transient, and not actually being used to secure something
delete Security.FWA_PolicyGrantee
from Security.FWA_PolicyGrantee pg
inner join Security.FWM_Policy po on pg.PolicyID = po.PolicyID and po.PolicyType = 'V'
left outer join Security.FWA_PolicyResourceInstance pri on po.PolicyID = pri.PolicyID
where pri.PolicyID is null

-- Delete policies that are transient, and not actually being used to secure something
delete Security.FWM_Policy
from Security.FWM_Policy po
left outer join Security.FWA_PolicyResourceInstance pri on po.PolicyID = pri.PolicyID
where po.PolicyType = 'V'
and pri.PolicyID is null
go"
    
    Write-Host "Attempting to delete all tasks and workflows..."
    ExecuteNonQuery $sql    
   
    # Workflow store 
    $workflowConnection = Get-AderantWorkflowDatabaseSettings $global:EnvironmentManifest
    
    $server = $workflowConnection.Server
    $database = $workflowConnection.Database
    
    Write-Host "Attempting to delete workflows on $server.$database"
    $sql = "delete from [System.Activities.DurableInstancing].InstancesTable; delete from [System.Activities.DurableInstancing].RunnableInstancesTable; exec sp_updatestats"
    Invoke-Expression "sqlcmd -S $server -d $database -E -Q `"$sql`" "
    
    # Monitoring
    $monitoringConnection = Get-AderantMonitoringDatabaseSettings $global:EnvironmentManifest
    $server = $monitoringConnection.Server
    $database = $monitoringConnection.Database
    
    Import-Module ApplicationServer  
    Clear-ASMonitoringSqlDatabase -Database "$database" -Server "$server"    
}


function UpdateServices($source, $servicesRoot) {    
    Write-Host "Updating services under [$servicesRoot]" -ForegroundColor green    
    
    UpdateTarget $source $servicesRoot    
    
    $targetPaths = Get-PathsRecursive $servicesRoot
    
    foreach ($targetPath in $targetPaths) {    
        UpdateTarget $source $targetPath.FullName
    }
}


function Get-PathsRecursive($root) {
    # gets all paths recursively and excludes reparse points
    
    $paths = @()    
    $directories = Get-ChildItem $root | Where { $_.PSIsContainer }
    
    if ($directories -ne $null) {
        foreach ($path in $directories) {
            if ((Test-ReparsePoint $path.FullName) -eq $false) {
                $paths += $path                
                Get-PathsRecursive $path.FullName
            }
        }
    }
    return $paths
}


function Test-ReparsePoint([string]$path) {
    $file = Get-Item $path -Force -ea 0  
    return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
}


function UpdateTarget($source, $target) {  
    Write-Host "Updating files in [$target]" -ForegroundColor yellow
    
    # /XO :: eXclude Older files.
    # /XL :: eXclude Lonely files and directories.
    # /XX :: eXclude eXtra files and directories.
    # /W:n :: Wait time between retries: default is 30 seconds.
    # /NP :: No Progress - don't display percentage copied.
    # /NJH :: No Job Header.
    # /NJS :: No Job Summary.
    # /NDL :: No Directory List - don't log directory names.
    # /XF file [file]... :: eXclude Files matching given names/paths/wildcards.
    robocopy "$source" "$target" /MIR /XO /XL /XX /W:5 /NP /NJH /XJD /NDL /XF *.instance *.config log4net.xml
}


# Executes a SQL command against the current environment database
function ExecuteNonQuery([string]$sql) {
    $expertConnection = Get-AderantExpertDatabaseSettings $global:EnvironmentManifest
    
    $server = $expertConnection.Server
    $database = $expertConnection.Database    
    
    Invoke-Expression "sqlcmd -S $server -d $database -E -Q `"$sql`" "      
}


function BuildConnectionSettings($databaseServer) {
    if ($databaseServer -ne $null) {
    
        $server = $databaseServer.serverName
        $instance = $databaseServer.serverInstance
        
        if (![string]::IsNullOrEmpty($instance)) {
            $server += "\$instance"
        }
        
        $database = $databaseServer.databaseConnection.databaseName        
        
        $connection = New-Object Object |   
            Add-Member NoteProperty -Name Server -Value $server.ToString() -PassThru |   
            Add-Member NoteProperty -Name Database -Value $database.ToString() -PassThru
            #Add-Member NoteProperty -Name Username -Value $username.ToString() -PassThru |   
            #Add-Member NoteProperty -Name Password -Value $password.ToString() -PassThru
        
        return $connection            
    } else {
        Write-Error "No environment manifest could be parsed"
    }
}


function Get-AderantExpertDatabaseSettings([xml]$environment) {
    return BuildConnectionSettings $environment.environment.expertDatabaseServer
}    


function Get-AderantMonitoringDatabaseSettings([xml]$environment) {
    return BuildConnectionSettings $environment.environment.monitoringDatabaseServer
}


function Get-AderantWorkflowDatabaseSettings([xml]$environment) {
    return BuildConnectionSettings $environment.environment.workflowDatabaseServer
}

Set-Alias cc Clear-ExpertCache 
Set-Alias ct Remove-ExpertWorkflowsAndTasks 
Set-Alias ud Update-ExpertDeployment 
Set-Alias killall Stop-ExpertEnvironment

Export-ModuleMember -Function Load-ExpertEnvironmentManifest
Export-ModuleMember -Function Set-BuildFlavor
Export-ModuleMember -Function Update-ExpertDeployment
Export-ModuleMember -Function Set-ExpertServicesToManualStart
Export-ModuleMember -Function Setup-EnvironmentLogging
Export-ModuleMember -Function Setup-ExpertDatabase
Export-ModuleMember -Function Stop-ExpertEnvironment
Export-ModuleMember -Function Clear-ExpertCache
Export-ModuleMember -Function Remove-ExpertWorkflowsAndTasks

Export-ModuleMember -Alias cc
Export-ModuleMember -Alias ct
Export-ModuleMember -Alias ud
Export-ModuleMember -Alias killall