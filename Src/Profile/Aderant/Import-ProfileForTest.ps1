$VerbosePreference = 'SilentlyContinue'

if( (Get-Module -Name 'Aderant') )
{
    Remove-Module -Name 'Aderant' -Force
}

Import-Module (Join-Path -Path $PSScriptRoot -ChildPath 'Aderant.psd1' -Resolve)