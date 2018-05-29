function Get-BuildContext
{
    [CmdletBinding()]
    [OutputType([Aderant.Build.Context])]
    param()

    Set-StrictMode -Version 'Latest'

    Write-Debug "Retrieving current context"

    $data = $MyInvocation.MyCommand.Module.PrivateData

    Write-Debug ($data | Out-String)
    Write-Debug ($data.Context | Out-String)

    return $data.Context
}

Export-ModuleMember -Function Get-BuildContext