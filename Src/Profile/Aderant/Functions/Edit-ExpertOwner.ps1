function global:Edit-ExpertOwner {
    <#
    .Synopsis
        Changes the system owner in FWM_ENVIRONMENT.
    .Description
        Changes the system owner in FWM_ENVIRONMENT
    .PARAMETER owner
        The owner to set ISSYSTEM = 'Y' for.
    .PARAMETER ServerInstance
        The SQL server\instance the database is on.
    .PARAMETER database
        The name of the Expert database.
    .EXAMPLE
        Edit-ExpertOwner -owner Aderant
        This will change the system owner to Aderant in the Expert database.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$ServerInstance = $Env:COMPUTERNAME,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Database,
        [switch]$Force
    )

    dynamicparam {
        [string]$parameterName = "Owner"
        $parameterAttribute = New-Object System.Management.Automation.ParameterAttribute
        $parameterAttribute.Position = 0
        $parameterAttribute.Mandatory = $true
        $attributeCollection = New-Object System.Collections.ObjectModel.Collection[System.Attribute]
        $attributeCollection.Add($parameterAttribute)
        $Owners = "Aderant", "Clifford, Maximillian & Scott"
        $validateSetAttribute = New-Object System.Management.Automation.ValidateSetAttribute($Owners)
        $attributeCollection.Add($validateSetAttribute)
        $runtimeParameter = New-Object System.Management.Automation.RuntimeDefinedParameter($parameterName, [string], $attributeCollection)
        $runtimeParameterDictionary = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        $runtimeParameterDictionary.Add($parameterName, $runtimeParameter)
        return $runtimeParameterDictionary
    }

    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Stop'
        $InformationPreference = 'Continue'

        if (-Not (Get-Module -ListAvailable -Name 'SqlServer')) {
            Install-Module -Name 'SqlServer' -Force -AllowClobber
        } elseif (-not (Get-Module -Name 'SqlServer')) {
            Import-Module -Name 'SqlServer'
        }
    }

    process {
        $Owner = $PsBoundParameters[$parameterName]

        Write-Information -MessageData "Server instance set to: $ServerInstance"
        Write-Information -MessageData "Database set to: $Database"
        Write-Information -MessageData "Updating Expert owner to: $Owner"

        if ($Owner -contains "Aderant") {
            [string]$OwnerID = "00000000-0000-0000-0000-00000000000A"
        } else {
            [string]$OwnerID = "402A1B6F-AAB2-4B32-BEFD-D4C9BB556029"
        }
        
        [string]$sql = "DECLARE @OWNER NVARCHAR(100) = '" + $Owner + "';
DECLARE @OWNERID NVARCHAR(40) = '" + $OwnerID + "';

IF NOT EXISTS (SELECT TOP 1 * FROM FWM_OWNER WHERE OWNERID = @OWNERID) BEGIN
INSERT INTO FWM_OWNER (OWNERID, NAME, ISSYSTEM) VALUES (@OWNERID, @OWNER, 'Y');
END;

UPDATE FWM_OWNER SET ISSYSTEM = 'Y' WHERE OWNERID = @OWNERID;
UPDATE FWM_OWNER SET ISSYSTEM = 'N' WHERE OWNERID != @OWNERID;
UPDATE HBM_PARMS SET FIRM_NAME = @OWNER;"
    
        if (-not $Force.IsPresent) {
            Write-Information -MessageData "Continue?"
            $answer = Read-Host "Y/N"

            while ("Y", "N" -notcontains $answer) {
                $answer = Read-Host "Y/N"
            }

            if ($answer -eq "N") {
                return
            }
        }
    
        try {
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $Database -Query $sql
        } catch {
            Write-Error "Failed to change Expert owner to: $Owner for database: $Database"
            return
        }
        
        Write-Information -MessageData "Expert owner set to: $Owner"
    }
}