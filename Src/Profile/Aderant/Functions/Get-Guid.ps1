function Get-Guid {
    Add-Type -AssemblyName 'System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
    [string]$guid = ([System.Guid]::NewGuid()).Guid
    [System.Windows.Forms.Clipboard]::SetText($guid)

    return $guid
}

Set-Alias -Name 'gg' -Value 'Get-Guid'
Export-ModuleMember -Function 'Get-Guid' -Alias 'gg'