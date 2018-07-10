function Get-BuildContext {
    [CmdletBinding()]
    [OutputType([Aderant.Build.Context])]
    param()

    begin {
        Set-StrictMode -Version Latest
    }
    
    process {
        Write-Debug "Retrieving current context"

        if ($MyInvocation.MyCommand.Module -ne $null) {
            try {                
                $data = $MyInvocation.MyCommand.Module.PrivateData

                Write-Debug ($data | Out-String)
                Write-Debug ($data.Context | Out-String)

                return $data.Context
            } catch {
                # Can be invoked from a non-moduule context such when invoked from CI
                return $null
            }
        }
    }
}