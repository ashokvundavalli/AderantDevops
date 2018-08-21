<#
.Synopsis
    Changes the system owner in FWM_ENVIRONMENT.
.Description
    Changes the system owner in FWM_ENVIRONMENT
.PARAMETER owner
    The owner to set ISSYSTEM = 'Y' for.
.PARAMETER serverInstance
    The SQL server\instance the database is on.
.PARAMETER database
    The name of the Expert database.
.EXAMPLE
        Change-ExpertOwner -owner Aderant
    This will change the system owner to Aderant in the Expert database.
#>
function Change-ExpertOwner {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [string]$serverInstance,
        [Parameter(Mandatory = $false)] [string]$database,
        [Parameter(Mandatory = $false)] [switch]$force
    )

    dynamicparam {
        [string]$parameterName = "owner"
        $parameterAttribute = New-Object System.Management.Automation.ParameterAttribute
        $parameterAttribute.Position = 0
        $parameterAttribute.Mandatory = $true
        $attributeCollection = New-Object System.Collections.ObjectModel.Collection[System.Attribute]
        $attributeCollection.Add($parameterAttribute)
        $owners = "Aderant", "Clifford, Maximillian & Scott"
        $validateSetAttribute = New-Object System.Management.Automation.ValidateSetAttribute($owners)
        $attributeCollection.Add($validateSetAttribute)
        $runtimeParameter = New-Object System.Management.Automation.RuntimeDefinedParameter($parameterName, [string], $attributeCollection)
        $runtimeParameterDictionary = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        $runtimeParameterDictionary.Add($parameterName, $runtimeParameter)
        return $runtimeParameterDictionary
    }

    begin {
        $owner = $PsBoundParameters[$parameterName]
        if ([string]::IsNullOrEmpty($serverInstance)) {
            $serverInstance = Get-DatabaseServer
        } else {
            Write-Host "Server instance set to: $serverInstance"
        }

        if ([string]::IsNullOrEmpty($database)) {
            $database = Get-Database
        } else {
            Write-Host "Database name set to: $database"
        }

        Write-Host "Expert owner: $owner"
    }

    process {
        if (-Not (Get-Module -ListAvailable -Name Sqlps)) {
            Write-Error "The Sqlps module is not available on this system."
            return
        }

        Import-Module Sqlps -DisableNameChecking
    
        if ($owner -contains "Aderant") {
            [string]$ownerID = "00000000-0000-0000-0000-00000000000A"
        } else {
            [string]$ownerID = "402A1B6F-AAB2-4B32-BEFD-D4C9BB556029"
        }
        
        [string]$sql = "DECLARE @OWNER NVARCHAR(100) = '" + $owner + "';
DECLARE @OWNERID NVARCHAR(40) = '" + $ownerID + "';

IF NOT EXISTS (SELECT TOP 1 * FROM FWM_OWNER WHERE OWNERID = @OWNERID) BEGIN
INSERT INTO FWM_OWNER (OWNERID, NAME, ISSYSTEM) VALUES (@OWNERID, @OWNER, 'Y');
END;

UPDATE FWM_OWNER SET ISSYSTEM = 'Y' WHERE OWNERID = @OWNERID;
UPDATE FWM_OWNER SET ISSYSTEM = 'N' WHERE OWNERID != @OWNERID;
UPDATE HBM_PARMS SET FIRM_NAME = @OWNER;"
    
        if (-not $force.IsPresent) {
            Write-Host "Continue?"
            $answer = Read-Host "Y/N"

            while ("Y", "N" -notcontains $answer) {
                $answer = Read-Host "Y/N"
            }

            if ($answer -eq "N") {
                return
            }
        }
    
        try {
            Invoke-Sqlcmd -ServerInstance $serverInstance -Database $database -Query $sql
        } catch {
            Write-Error "Failed to change Expert owner to: $owner for database: $database"
            return
        }
        
        Write-Host "Expert owner set to: $owner" -ForegroundColor Cyan
    }
}