<#
	This script enables write caching and disables write cache buffer flushing to improve disk performance
	at the expensive of reliability.

    The multiple device processing is to handle cases where the computer has been cloned
    or had another disk as the OS drive in a past life
#>

# CacheIsPowerProtected - we want performance over all else so lets go with the dangerous mode
$CacheIsPowerProtected = 1
# "1" turns on write-caching policy, "0" turns off write-caching policy
$UserWriteCacheSetting = 1

# Get system drive ID
$Index = (Get-Partition | Where-Object -FilterScript {$_.DriveLetter -eq $Env:SystemDrive[0]}).DiskNumber
$SystemDriveID = (Get-CimInstance -ClassName CIM_DiskDrive | Where-Object -FilterScript {$_.Index -eq $Index}).PNPDeviceID


# Get system drive instance(s)
$devices = (Get-ChildItem -Path "HKLM:\SYSTEM\CurrentControlSet\Enum\SCSI" | Where-Object -FilterScript {$SystemDriveID -match $_.PSChildName})

foreach ($device in $devices) {
    $deviceEntries = (Get-ChildItem -Path $device.PSPath | Where-Object -FilterScript {$SystemDriveID -match $_.PSChildName})

    foreach ($entry in $deviceEntries) {
        if (-not (Test-Path -Path "$($entry.PSPath)\Device Parameters\Disk")) {
            # Create "Disk" folder
            New-Item -Path "$($entry.PSPath)\Device Parameters\Disk" -Force
        }

        # Enable disk write caching
        New-ItemProperty -Path "$($entry.PSPath)\Device Parameters\Disk" -Name CacheIsPowerProtected -PropertyType DWord -Value $CacheIsPowerProtected -Force
        New-ItemProperty -Path "$($entry.PSPath)\Device Parameters\Disk" -Name UserWriteCacheSetting -PropertyType DWord -Value $UserWriteCacheSetting -Force
    }
}