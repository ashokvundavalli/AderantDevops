function Get-BuildContext {
    [CmdletBinding()]
    [OutputType([Aderant.Build.BuildOperationContext])]
    param(
        [Parameter()]
        [switch]$CreateIfNeeded
    )

    begin {
        Set-StrictMode -Version Latest
    }

    process {
        Write-Debug "Retrieving current context"

        if ($null -ne $MyInvocation.MyCommand.Module) {
            try {
                $data = $MyInvocation.MyCommand.Module.PrivateData

                if ($null -ne $data.Context) {
                    Write-Debug ($data | Out-String)
                    Write-Debug ($data.Context | Out-String)
                    return $data.Context
                }
            } catch {
                if ($CreateIfNeeded) {
                    return New-BuildContext
                }
                # Can be invoked from a non-module context such when invoked from CI
                return $null
            }
        }

        if ($CreateIfNeeded) {
            return New-BuildContext
        }

        Write-Debug "Current context is null"
        return $null
    }
}