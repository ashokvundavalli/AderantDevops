<# Slightly intelligent deletion of build directory output files using an algorithm to determine
# how many hours back to search for folders to delete. Can be run repeatedly and will be more aggressive
# about how far to go back, based on how much space is free
#
# Rick
# v 1.0.2
# Initial first release
# v 1.0.3
# Changed remove-item to use rmdir because remove-item doesn't deal with sym link directories well.
# v 1.0.4 
# Added support for deleting .nuget folder
# v 1.0.5
# Changed to work with the new vnext build directories
# v 1.0.6
# Fixed up nuget cleaning to remove from both nuget cache folders
# v 1.0.7 
# Updated the build agent cleaner to be less cody and use Get-child recursion with remove-item.
#>
param(
    [Parameter(Mandatory=$false)][string]$drive,
    [Parameter(Mandatory=$false)][ValidateSet("agent", "nuget")][string]$strategy,
    [Parameter(Mandatory=$false)][Int]$olderThan = 7
)

begin {
	Set-StrictMode -Version 2.0
	Start-Transcript -Path "$env:SystemDrive\Scripts\MakeFreeSpaceVnextLog.txt" -Force

	if ([string]::IsNullOrWhiteSpace($drive)) {
		$drive = "C"
	}

	if ([string]::IsNullOrWhiteSpace($strategy)) {
		$strategy = "agent"
	}

	# If disk space is less than $percentageAtWhichToPanic, no folders younger than this will be deleted.
	[int]$failsafeHoursBack = -1
	# At this percentage the $panicHoursBack hours is used to find old folders
	[int]$percentageAtWhichToPanic = 85
	# The number of hours to go back to find things to delete when disk free reaches $percentageAtWhichToPanic
	[int]$panicHoursBack = 0
	# Test only, runs removes as -whatif scenarios
	[bool]$testing = $false
}

process {
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

		if ($percentUsed -lt 51) {$hours = -48}
		if (($percentUsed -lt 71) -and ($percentUsed -gt 50)) {$hours = -24}
		if (($percentUsed -lt 81) -and ($percentUsed -gt 70)) {$hours = -12}
		if (($percentUsed -lt 91) -and ($percentUsed -gt 80)) {$hours = -6}
		if (($percentUsed -lt 95) -and ($percentUsed -gt 90)) {$hours = -3}
		if (($percentUsed -lt 100) -and ($percentUsed -gt 94)) {$hours = $failsafeHoursBack}
    
		# failsafe to prevent us deleting everything
		if ($hours -gt $failsafeHoursBack) {
			$hours = $failsafeHoursBack
		}

		# however, if its over a certain percentage, go crazy and delete oldest directory, irrespective of how old
		if ($percentUsed -gt $percentageAtWhichToPanic) {
			$hours = $panicHoursBack
		}

		Write-Debug "Will attempt to delete oldest folder older than $hours hours"
		return $hours
	}
	
	######################
	# Build Agent Cleanup
	######################
	Function Run-AgentStrategy($drive) {
		$hours = Get-HoursAgoToDelete $drive
		$limit = (Get-Date).AddHours($hours)
		$folder = $drive + ":\b\"
		if ($testing) {
			Get-ChildItem $folder -Recurse -Depth 0 -ErrorAction SilentlyContinue | Where{$_.Name -Match "^\d{1,4}$"} | ? {$_.LastWriteTime -lt $limit } | Remove-Item -Recurse -WhatIf -Force
		} else {
			 Get-ChildItem $folder -Recurse -Depth 0 -ErrorAction SilentlyContinue | Where{$_.Name -Match "^\d{1,4}$"} | ? {$_.LastWriteTime -lt $limit } | % { cmd.exe /c rmdir /q /s $_.FullName }
		}
	}

	################
	# NUGET Cleanup
	################
	Function Run-NugetStrategy() {
		$limit = (Get-Date).AddDays($olderThan*-1)

		if ($testing) {
			$root  = "~\.nuget\packages\"
			Get-ChildItem $root | ? { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force -WhatIf

			$root  = "~\AppData\Local\NuGet\Cache"    
			Get-ChildItem $root | ? { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force -WhatIf
		} else {
			$root  = "~\.nuget\packages\"
        
			if (Test-Path $root) {
				Get-ChildItem $root | ? { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force
			}

			$root  = "~\AppData\Local\NuGet\Cache"

			if (Test-Path $root) {
				Get-ChildItem $root | ? { $_.CreationTime -lt $limit } | Remove-Item -Recurse -Force
			}
		}
	}

	switch ($strategy) {
		"agent" {
			Write-Output "Choosing agent strategy, cleans up build agent scratch folders."
			if ($drive -eq "") {
				Write-Output "Must supply a drive letter to check."
			} else {
				$DebugPreference = "Continue"
				Run-AgentStrategy $drive
			}
		}
		"nuget" {
			Write-Output "Choosing nuget strategy, cleans up nuget cache folders."
			Run-NugetStrategy
		}
	}
}