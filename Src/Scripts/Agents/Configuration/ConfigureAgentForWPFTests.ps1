#Requires -RunAsAdministrator 

[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$DefaultUserName,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$DefaultDomainName,
    [Parameter(Mandatory=$true)][ValidateNotNull()][SecureString]$DefaultPassword
)

begin {
    [string]$winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
}

process {
    Write-Information -MessageData "Configuring registry to enable auto logon."

    Set-ItemProperty -Path $winlogon -Name 'DefaultUserName' -Value $DefaultUserName -Type 'String' -Verbose
    Set-ItemProperty -Path $winlogon -Name 'DefaultDomainName' -Value $DefaultDomainName -Type 'String' -Verbose
    Set-ItemProperty -Path $winlogon -Name 'DefaultPassword' -Value ([System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($DefaultPassword))) -Type 'String' -Verbose
    Set-ItemProperty -Path $winlogon -Name 'AutoAdminLogon' -Value '1' -Type 'String' -Verbose

    # Disable lock screen.
    powercfg.exe /CHANGE monitor-timeout-ac 0

    # Disable Windows Error Reporting UI. This prevents Windows error dialog pops-up in the middle of UI test execution which causes tests to hang.
    Set-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows\Windows Error Reporting' -Name 'DontShowUI' -Value 1 -Type 'DWord'
}