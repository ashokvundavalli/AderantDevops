function New-BuildContext {
    <#
    .SYNOPSIS
        Creates a context object to use when running builds.
    .PARAMETER Environment
        The environment you're building in.
    .PARAMETER DownloadRoot
        The place where downloaded tools should be cached. The default is the build root.
    #>
    [CmdletBinding()]
    [OutputType([Aderant.Build.Context])]
    param (
        [Parameter(Mandatory=$false)]
        [ValidateNotNullOrEmpty()]
        [string]$DownloadRoot
    )

    begin {
        Set-StrictMode -Version Latest

        . "$PSScriptRoot\Get-AmbientBuildMetadata.ps1"
    }

    process {
        [Aderant.Build.BuildMetadata]$buildMetadata = Get-AmbientBuildMetadata

        [Aderant.Build.Context]$context = [Aderant.Build.Context]::new($buildMetadata)

        if (-not [string]::IsNullOrWhiteSpace($DownloadRoot)) {
            $context.DownloadRoot = $DownloadRoot
        }

        return $context
    }
}