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

        if ($CreateIfNeeded) {
            $context = New-BuildContext
            $script:BuildContext = $context

            return $context
        }

        return $script:BuildContext
    }
}