& (Join-Path -Path $PSScriptRoot -ChildPath 'Initialize-ProfileTest.ps1' -Resolve)

Describe 'Import-ProfilePs1 when Profile is not loaded' {
    if ((Get-Module -Name 'Aderant'))
    {
        Remove-Module -Name 'Aderant'        
    }

    & Import-Module (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Src\Profile\Aderant\Aderant.psd1')

    It 'should load the module' {
        Get-Module -Name 'Aderant' | Should Not BeNullOrEmpty
    }
}

Describe 'Import-ProfilePs1 when Profile is loaded' {

    $Global:Error.Clear()

    & Import-Module (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Src\Profile\Aderant\Aderant.psd1')

    It 'should load the module' {
        Get-Module -Name 'Aderant' | Should Not BeNullOrEmpty
    }

    & Import-Module (Join-Path -Path $PSScriptRoot -ChildPath '..\..\Src\Profile\Aderant\Aderant.psd1')

    It 'should not write an error' {
        $Global:Error.Count | Should Be 0
    }
}