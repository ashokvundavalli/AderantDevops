if ((Get-Module -Name 'Aderant'))
{
    Remove-Module -Name 'Aderant' -Force        
}

if ((Get-Module -Name 'dynamic_code_module_Aderant.Build, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'))
{
    Remove-Module -Name 'dynamic_code_module_Aderant.Build, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' -Force 
}


& (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Src\Profile\Aderant\Import-Profile.ps1' -Resolve)