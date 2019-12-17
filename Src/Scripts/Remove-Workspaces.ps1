<#
.SYNOPSIS
Lists and removes TFS workspace mappings.
.DESCRIPTION
Lists and optionally removes TFS workspace mappings for the specified computer name. Requires Visual Studio Team Explorer to function.
.EXAMPLE
& '<Path to script>' -computerName $env:COMPUTERNAME -remove
.EXAMPLE
& '<Path to script>' -computerName $env:COMPUTERNAME -remove -force
.PARAMETER computerName
The computer name to remove associated workspaces from.
.PARAMETER remove
Remove workspaces with prompt, unless force is specified.
.PARAMETER force
Skip remove confirmation.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$computerName,
    [switch]$remove,
    [switch]$force
)

begin {
    Set-StrictMode -Version 'Latest'
    $ErrorActionPreference = 'Stop'

    class Workspace {
        [string]$Workspace
        [string]$UserName

        Workspace($workspace, $userName) {
            $this.Workspace = $workspace
            $this.UserName = $userName
        }
    }
}

process {
    try {
        Get-Command -Name 'TF.exe' -ErrorAction 'SilentlyContinue' | Out-Null
    } catch {
        Write-Error "Requires Visual Studio Team Explorer TF.exe to be mapped in the PATH environment variable."
        exit 1
    }

    [string[]]$workspaces = $null

    try {
        $workspaces = tf workspaces /owner:* /computer:$computerName
    } catch {
        $Error[0] | Format-List -Force
        exit 1
    }

    [System.Collections.Generic.List[Workspace]]$workspaceMappings = @()

    if ($null -ne $workspaces -and $workspaces.Count -gt 3) {
        for ([int]$i = 3; $i -lt $workspaces.Count; $i++) {
            [string[]]$values = $workspaces[$i].Split(' ')
            [void]$workspaceMappings.Add([Workspace]::new($values[0], "$($values[1]).$($values[2])"))
        }
    }

    if ($workspaceMappings.Count -eq 0) {
        Write-Output "No workspace mappings associated with computer name: $computerName"
        exit 0
    }

    Write-Output "Workspace mappings associated with computer name: $computerName"
    Write-Output (Format-Table -InputObject $workspaceMappings)

    if (-not $remove.IsPresent) {
        exit 0
    }

    [bool]$error = $false

    for ([int]$i = 0; $i -lt $workspaceMappings.Count; $i++) {
        if (-not $force.IsPresent) {
            tf workspace /delete "$($workspaceMappings[$i].Workspace);$($workspaceMappings[$i].UserName)"
        } else {
            Write-Output "Removing workspace: $($workspaceMappings[$i].Workspace) assoicated with username: $($workspaceMappings[$i].UserName)"

            try {
                Write-Output '~' | tf workspace /delete "$($workspaceMappings[$i].Workspace);$($workspaceMappings[$i].UserName)" | Out-Null
            } catch {
                $Error[0] | Format-List -Force
                $error = $true
            }
        }
    }
}

end {
    if ($error -eq $false) {
        exit 0
    } else {
        exit 1
    }
}