[string]$user = 'Auto_'

Get-WmiObject -Class 'Win32_userprofile' | Where-Object { ($_.LocalPath -like "*$user*" -and $_.Loaded -eq $false) } | ForEach-Object { Write-Host $_.LocalPath; Write-Host $_.Loaded; $_.Delete() }

$localUserList = Get-LocalUser
foreach ($localUser in $localUserList) {
    if ($localUser.Description -eq "*Auto Account*") {        
        Write-Host "User: $($localUser.Name)"
        Remove-LocalUser $localUser
    }
}