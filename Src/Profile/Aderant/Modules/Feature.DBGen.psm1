<#
.Synopsis
    Starts dbgen.exe. This is very similar to Update-Database -interactive. You may want to use that instead.
.Description
    Starts dbgen.exe found in your expert source directory. This is very similar (but inferior) to Update-Database -interactive. You might want to use that instead.
#>
function Start-DBGen() {
    #Normally found at: C:\CMS.NET\Bin\dbgen.exe
    $dbgen = [System.IO.Path]::Combine($global:ShellContext.BranchServerDirectory, "dbgen\dbgen.exe")
    Invoke-Expression $dbgen
}

Export-ModuleMember Start-DBGen

<#
.Synopsis
    Runs dbprepare.exe on the specified database. Defaults to version 81.
.Description
    Runs dbprepare.exe on the specified database. Defaults to version 81.
.PARAMETER database
    MANDATORY: The name of the database to use. 
.PARAMETER saPassword
    MANDATORY: The sa user password for the database server.
.PARAMETER server
    The name of the database server. Defaults to the local machine name.
.PARAMETER instance
    The database serverinstance that the database resides on.
.PARAMETER version
    The version to prep the database to. Defaults to 81.
.PARAMETER interactive
    Starts DBPREPARE in interactive mode
#>
function Prepare-Database (
    [Parameter(Mandatory = $true)]
    [string]$database,
    [Parameter(Mandatory = $true)]
    [string]$saPassword,
    [string]$server = $env:COMPUTERNAME,
    [string]$instance,
    [string]$version = "81",
    [switch]$interactive
) {

    [string]$cmsConnection
    if (-Not [string]::IsNullOrWhiteSpace($instance)) {
        $instance = "\$instance"    
        $cmsConnection = "[$server]`r`nVendor=1`r`nLocation=$server$instance`r`nDatabase=$database`r`nTitle=$server.$database`r`nForceManualLogin=0"
        Write-Debug "Running dbprepare against database: $database on server: $server$instance"
    } else {
        $cmsConnection = "[$server]`r`nVendor=1`r`nLocation=$server`r`nDatabase=$database`r`nTitle=$server.$database`r`nForceManualLogin=0"
        Write-Debug "Running dbprepare against database: $database on server: $server"
    }

    # Checks if CMS.INI is present in %APPDATA%\Aderant and create it if not. Appends connection details to CMS.INI if they are not present.
    $cmsPath = "$env:APPDATA\Aderant\CMS.INI"
    if (Test-Path $cmsPath) {
        if ((Select-String -InputObject (Get-Content $cmsPath | Out-String) -SimpleMatch "[$server]") -eq $null) {
            Write-Debug "Adding $server connection to existing CMS.INI file in %APPDATA%\Aderant."
            Add-Content $cmsPath "`r`n$cmsConnection"
        } else {
            # If there is an existing connection with incorrect data, dbprepare will open and wait for user input.
            Write-Debug "$server connection present in existing CMS.INI file in %APPDATA%\Aderant."
        }
    } else {
        Write-Debug "Creating CMS.INI file in %APPDATA%\Aderant with $server connection."
        New-Item $cmsPath -ItemType File -Value $cmsConnection
    }

    $dbPreparePath = "$($global:ShellContext.BranchServerDirectory)\dbgen\dbprepare.exe"
    $dbPrepareArgs = "target=$server$instance.$database autostart=1 autoclose=1 installshield=1 login=SA password=`"$saPassword`" ERRORLOG=`"$($global:ShellContext.BranchBinariesDirectory)\Logs\dbprep.log`" version=$version prep=database,other"

    if (-not $interactive.IsPresent) {
        $dbPrepareArgs = $dbPrepareArgs + " hide=1"
    }

    Write-Host "Starting dbprepare.exe from: $dbPreparePath"
    Write-Host "dbprepare.exe arguments: $dbPrepareArgs"

    Start-Process -FilePath $dbPreparePath -ArgumentList $dbPrepareArgs -Wait -PassThru | Out-Null
}

Export-ModuleMember Prepare-Database

<#
.Synopsis
    Deploys the database project to your database defined in the environment manifest
.Description
    Deploys the database project, thereby updating your database to the correct definition.
.PARAMETER interactive
    Starts DBGEN in interactive mode
#>
function Update-Database([string]$manifestName, [switch]$interactive) {
    [string]$fullManifest = ''

    Write-Warning "The 'upd' command is currently unavailable. Please use DBGen for now to update your database."

    return

    if ($global:ShellContext.BranchExpertVersion.StartsWith("8")) {
        $fullManifest = Join-Path -Path $global:ShellContext.BranchBinariesDirectory -ChildPath 'environment.xml'
    } else {
        if ($null -eq $manifestName) {
            Write-Host "Usage: Update-BranchDatabase <manifestName>"
            return
        } else {
            $fullManifest = Join-Path -Path $global:ShellContext.BranchBinariesDirectory -ChildPath "\$manifestName.environment.xml"
        }
    }


    if (Test-Path $fullManifest) {
        Write-Debug "Using manifest: $fullManifest"

        # Update the DBGEN defaults for development churn
        [Xml]$manifest = Get-Content $fullManifest
        $server = $manifest.environment.expertDatabaseServer.serverName
        $db = $manifest.environment.expertDatabaseServer.databaseConnection.databaseName

        $query = @"
begin tran
update CMS_DB_OPTION set OPTION_VALUE = '{0}', LAST_MODIFIED = getdate()
where OPTION_CODE in ('PERMIT_NULLLOSS', 'PERMIT_DATALOSS')
commit
"@
        # set PERMIT_NULLLOSS and PERMIT_DATALOSS to true
        $command = "sqlcmd -S $server -d $db -E -Q `"" + [string]::Format($query, "Y") + "`""
        Invoke-Expression $command

        $shell = "powershell -NoProfile -NoLogo `"$($global:ShellContext.PackageScriptsDirectory)\DeployDatabase.ps1`" -environmentManifestPath `"$fullManifest`" -expertSourceDirectory `"$global:ShellContext.BranchServerDirectory`" -interactive:$" + $interactive
        # Invoke-Expression falls on its face here due to a bug with [switch] - if used the switch argument cannot be converted to a switch parameter
        # which is very annoying
        # http://connect.microsoft.com/PowerShell/feedback/details/742084/powershell-v2-powershell-cant-convert-false-into-swtich-when-using-file-param
        cmd /c $shell

        # reset PERMIT_NULLLOSS and PERMIT_DATALOSS to false
        $command = "sqlcmd -S $server -d $db -E -Q `"" + [string]::Format($query, "N") + "`""
        Invoke-Expression $command
    } else {
        Write-Error "No manifest specified at path: $fullManifest"
    }
}

Export-ModuleMember Update-Database
