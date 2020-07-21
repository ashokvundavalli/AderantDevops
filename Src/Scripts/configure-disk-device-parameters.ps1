<#
	This script enables write caching and disables write cache buffer flushing to improve disk performance
	at the expensive of reliability
#>

$DiskNumbersToModify = (0)

# CacheIsPowerProtected parameter in most cases would be "0" - we want performance over all else so lets go with the dangerous mode
$CacheIsPowerProtected = 1
# "1" turns on write-caching policy, "0" turns off write-caching policy
$UserWriteCacheSetting = 1

$cimSession = New-CimSession

foreach ($DiskN in $DiskNumbersToModify) {
    $disk = (Get-Disk -Number $DiskN -CimSession $cimSession)

    $DiskPath = $disk.Path
    $RegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Enum"
    $DiskType = $DiskPath.split([char]0x003F, [char]0x0023)
    $RegistryPath += ($DiskType[1] + "\" + $Disktype[2] + "\" + $Disktype[3] + "\Device Parameters\Disk")

    if (!(Test-Path ($registryPath))) {
        New-Item -Path $registryPath -Force
    }
    New-ItemProperty -Path $registryPath -Name "CacheIsPowerProtected" -Value $CacheIsPowerProtected -PropertyType DWORD -Force -Confirm:$false
    New-ItemProperty -Path $registryPath -Name "UserWriteCacheSetting" -Value $UserWriteCacheSetting -PropertyType DWORD -Force -Confirm:$false
}