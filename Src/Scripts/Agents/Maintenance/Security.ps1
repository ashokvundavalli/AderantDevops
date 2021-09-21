#Requires -RunAsAdministrator

function Set-SecurityPermissions {
    begin {
        Set-StrictMode -Version 'Latest'
        $InformationPreference = 'Continue'

        [string[]]$groups = @(
            'ADERANT_AP\SG_AP_Dev_Operations'
            'ADERANT_NA\SG_GB_BuildServices'
        )
    }

    process {
        foreach ($group in $groups) {
            try {
                [void](Get-LocalGroupMember -Name 'Administrators' -Member $group)

                Write-Information -MessageData "$group is already a member of the Administrators group."
            } catch {
                Write-Information -MessageData "Adding $group to the Administrators group."
                Add-LocalGroupMember -Group 'Administrators' -Member $group
            }
        }
    }
}

function Remove-GroupMembers {
    begin {
        Set-StrictMode -Version 'Latest'
        $InformationPreference = 'Continue'
        $ErrorActionPreference = 'Stop'
    }

    process {
        $users = Get-LocalGroupMember -Name 'Administrators' | Where-Object { $_.ObjectClass -eq 'User' -and $_.PrincipalSource -eq 'ActiveDirectory' }

        foreach ($user in $users) {
            if ($user.Name -eq "$Env:USERDOMAIN\$Env:USERNAME") {
                # Don't remove the user running the command.
                continue
            }

            Write-Information -MessageData "Removing user: '$($user.Name)' from group: 'Administrators'."
            Remove-LocalGroupMember -Group 'Administrators' -Member $user.Name
        }
    }
}