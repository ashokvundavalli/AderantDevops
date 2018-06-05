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
        [Parameter(Mandatory=$true)]
        [Aderant.Build.EnvironmentType]$Environment,

        [Parameter(Mandatory=$false)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$DownloadRoot
    )

    begin {
        Set-StrictMode -Version Latest
    }

    process {
        [Aderant.Build.BuildMetadata]$buildMetadata = Get-AmbientBuildMetadata

        [Aderant.Build.Context]$context = [Aderant.Build.Context]::new()
        $context.BuildMetadata = $buildMetadata
        $context.Environment = $Environment

        if (-not [string]::IsNullOrWhiteSpace($DownloadRoot)) {
            $context.DownloadRoot = $DownloadRoot
        }

        return $context
    }
}

Export-ModuleMember -Function New-BuildContext