<#
    (Maybe) required for WatiN tests to run correctly.
#>
function Disable-InternetExplorerESC {
    $AdminKey = "HKLM:\SOFTWARE\Microsoft\Active Setup\Installed Components\{A509B1A7-37EF-4b3f-8CFC-4F3A74704073}"

    Set-ItemProperty -Path $AdminKey -Name "IsInstalled" -Value 0

    Write-Information "IE Enhanced Security Configuration (ESC) has been disabled."
}

Disable-InternetExplorerESC