<#
.SYNOPSIS
    Slightly intelligent deletion of build directory output files using an algorithm to determine
    how many hours back to search for folders to delete. Can be run repeatedly and will be more aggressive
    about how far to go back, based on how much space is free.
#>
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$drive = 'C',
    [Parameter(Mandatory=$false)][ValidateSet('agent', 'nuget')][string]$strategy = 'agent',
    [Parameter(Mandatory=$false)][ValidateNotNull()][int]$olderThan = 3
)

begin {
    Set-StrictMode -Version 'Latest'
    $InformationPreference = 'Continue'

    # If disk space is less than $percentageAtWhichToPanic, no folders younger than this will be deleted.
    [int]$failsafeHoursBack = -3
    # At this percentage the $panicHoursBack hours is used to find old folders
    [int]$percentageAtWhichToPanic = 95
    # The number of hours to go back to find things to delete when disk free reaches $percentageAtWhichToPanic
    [int]$panicHoursBack = -1

    # Get the percentage space used for a drive
    Function Get-PercentUsed($drive) {
        $s = "DeviceID='" + $drive + ":'"
        $disk = Get-WmiObject Win32_LogicalDisk -Filter $s | Select-Object Size,FreeSpace
        $diskUsed = $disk.Size - $disk.FreeSpace
        $percent = [math]::Round($diskUsed*100 / $disk.Size)
        Write-Information "$percent% of $drive is used"
        return $percent
    }

    Function Get-HoursAgoToDelete($drive) {
        [int]$hours = $failsafeHoursBack
        $percentUsed = Get-PercentUsed $drive
        $percentUsed = [math]::Round($percentUsed)

        if ($percentUsed -gt $percentageAtWhichToPanic) {
            $hours = $panicHoursBack
        }

        Write-Information "Will attempt to delete folders older than $hours hours"
        return $hours
    }

    Function CleanBuildAgent {
        <#
        .SYNOPSIS
            Build Agent cleanup.
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

            if (Test-Path -Path $folder)  {
                if (-not $PSCmdlet.ShouldProcess("Result")) {
                    Write-Information "Would delete files under $folder"
                    Get-ChildItem $folder -Recurse -Depth 0 -ErrorAction SilentlyContinue | Where-Object { $_.Name -Match "^\d{1,4}$"} | Where-Object {$_.LastWriteTime -lt $limit } | Remove-Item -Recurse -WhatIf -Force -Verbose
                } else {
                    $agentDirectories = Get-ChildItem -Path $folder -Directory | Where-Object { $_.Name -Match "^\d{1,4}$" }

                    foreach ($directory in $agentDirectories) {
                        Write-Information "Deleting files under $($directory.FullName)"
                        Get-ChildItem $directory.FullName -Directory | Where-Object { $_.Name -Match "^\d{1,4}$" } | Where-Object {$_.LastWriteTime -lt $limit } | ForEach-Object { cmd.exe /c rmdir /q /s $_.FullName }
                    }
                }
            }


            if ($PSCmdlet.ShouldProcess("Clean IIS")) {
                # Clean up IIS after removing files
                . "$PSScriptRoot\iis-cleanup.ps1"
            } else {
                Write-Information "Not cleaning IIS due to lack of -WhatIf support"
            }
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

        [string]$root = "$Env:USERPROFILE\.nuget\packages\"
        Write-Information "Deleting files under $root"
        Get-ChildItem $root | Where-Object { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force -Verbose -ErrorAction SilentlyContinue

        $root = "$Env:LOCALAPPDATA\NuGet\Cache"
        Write-Information "Deleting files under $root"
        Get-ChildItem $root | Where-Object { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force -Verbose -ErrorAction SilentlyContinue
    }
}

process {
    switch ($strategy) {
        'agent' {
            Write-Information -MessageData 'Choosing agent strategy to clean up build agent scratch folders.'
            CleanBuildAgent -drive $drive
        }
        'nuget' {
            Write-Information -MessageData 'Choosing NuGet strategy to clean up nuget cache folders.'
            RemoveNuGetPackages -days $olderThan
        }
    }
}