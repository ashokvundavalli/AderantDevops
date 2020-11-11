<#
.SYNOPSIS
    Slightly intelligent deletion of build directory output files using an algorithm to determine
    how many hours back to search for folders to delete. Can be run repeatedly and will be more aggressive
    about how far to go back, based on how much space is free.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$drive = 'C',
    [Parameter(Mandatory=$false)][ValidateSet('agent', 'nuget')][string]$strategy = 'agent',
    [Parameter(Mandatory=$false)][ValidateNotNull()][int]$olderThan = 3,
    [switch]$testing
)

begin {
    Set-StrictMode -Version 'Latest'
    $ErrorActionPreference = 'Continue'
    $InformationPreference = 'Continue'

    Start-Transcript -Path "$Env:SystemDrive\Scripts\MakeFreeSpaceVnextLog.txt" -Force

    # If disk space is less than $percentageAtWhichToPanic, no folders younger than this will be deleted.
    [int]$failsafeHoursBack = -1
    # At this percentage the $panicHoursBack hours is used to find old folders
    [int]$percentageAtWhichToPanic = 85
    # The number of hours to go back to find things to delete when disk free reaches $percentageAtWhichToPanic
    [int]$panicHoursBack = 0

    # Get the percentage space used for a drive
    Function Get-PercentUsed($drive) {
        $s = "DeviceID='" + $drive + ":'"
        $disk = Get-WmiObject Win32_LogicalDisk -Filter $s | Select-Object Size,FreeSpace
        $diskUsed = $disk.Size - $disk.FreeSpace
        $percent = [math]::Round($diskUsed*100 / $disk.Size)
        Write-Debug "$percent% of $drive is used"
        return $percent
    }

    # Work out the hours to delete ago based on a really complex algorithm!!
    Function Get-HoursAgoToDelete($drive) {
        [int]$hours = -1000
        $percentUsed = Get-PercentUsed $drive
        $percentUsed = [math]::Round($percentUsed)

        if ($percentUsed -lt 51) {
            $hours = -2
        }
        if (($percentUsed -lt 71) -and ($percentUsed -gt 50)) {
            $hours = -2
        }
        if (($percentUsed -lt 81) -and ($percentUsed -gt 70)) {
            $hours = -2
        }
        if (($percentUsed -lt 91) -and ($percentUsed -gt 80)) {
            $hours = -1
        }
        if (($percentUsed -lt 95) -and ($percentUsed -gt 90)) {
            $hours = -1
        }
        if (($percentUsed -lt 100) -and ($percentUsed -gt 94)) {
            $hours = $failsafeHoursBack
        }

        # failsafe to prevent us deleting everything.
        if ($hours -gt $failsafeHoursBack) {
            $hours = $failsafeHoursBack
        }

        # However, if its over a certain percentage, go crazy and delete oldest directory, irrespective of how old.
        if ($percentUsed -gt $percentageAtWhichToPanic) {
            $hours = $panicHoursBack
        }

        Write-Debug "Will attempt to delete oldest folder older than $hours hours"
        return $hours
    }

    Function CleanBuildAgent {
        <#
        .SYNOPSIS
            Build agent cleanup.
        #>
        [CmdletBinding(SupportsShouldProcess=$true)]
        param (
            [string]$drive
        )

        begin {
            $DebugPreference = 'Continue'
        }

        process {
            $hours = Get-HoursAgoToDelete $drive
            $limit = (Get-Date).AddHours($hours)
            $folder = $drive + ":\b\"
            if (-not $PSCmdlet.ShouldProcess("Result")) {
                Get-ChildItem $folder -Recurse -Depth 0 -ErrorAction SilentlyContinue | Where-Object { $_.Name -Match "^\d{1,4}$"} | Where-Object {$_.LastWriteTime -lt $limit } | Remove-Item -Recurse -WhatIf -Force
            } else {
                $agentDirectories = Get-ChildItem -Path $folder -Directory | Where-Object { $_.Name -Match "^\d{1,4}$" }

                foreach ($directory in $agentDirectories) {
                    Get-ChildItem $directory.FullName -Directory | Where-Object { $_.Name -Match "^\d{1,4}$" } | Where-Object {$_.LastWriteTime -lt $limit } | ForEach-Object { cmd.exe /c rmdir /q /s $_.FullName }
                }
            }

            # Clean up IIS after removing files
            & "$PSScriptRoot\iis-cleanup.ps1"
        }
    }

    function RemoveNuGetPackages {
        <#
        .SYNOPSIS
            NuGet cleanup.
        .DESCRIPTION
            Removes NuGet packages based on the specified.
        .PARAMETER days
            Cleans up NuGet packages older than the specified number of days.
        #>
        [CmdletBinding(SupportsShouldProcess=$true)]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNull()][int]$days
        )

        [DateTime]$limit = (Get-Date).AddDays($days* - 1)

        if (-not $PSCmdlet.ShouldProcess("Result")) {
            [string]$root = "$Env:USERPROFILE\.nuget\packages\"
            Get-ChildItem $root | Where-Object { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force -WhatIf

            $root = "$Env:LOCALAPPDATA\NuGet\Cache"
            Get-ChildItem $root | Where-Object { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force -WhatIf
        } else {
            [string]$root = "$Env:USERPROFILE\.nuget\packages\"

            if (Test-Path $root) {
                Get-ChildItem $root | Where-Object { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force
            }

            $root = "$Env:LOCALAPPDATA\NuGet\Cache"

            if (Test-Path $root) {
                Get-ChildItem $root | Where-Object { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force
            }
        }
    }
}

process {
    switch ($strategy) {
        'agent' {
            Write-Information -MessageData 'Choosing agent strategy, cleans up build agent scratch folders.'
            #CleanBuildAgent -drive $drive -WhatIf:$testing.IsPresent
        }
        'nuget' {
            Write-Information -MessageData 'Choosing NuGet strategy, cleans up nuget cache folders.'
            RemoveNuGetPackages -days $olderThan -WhatIf:$testing.IsPresent
        }
    }
}

end {
    Stop-Transcript
}