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
        [Parameter(Mandatory=$true, HelpText="The environment you're building in.")]
        [string]$Environment,        

        [Parameter(HelpText="The place where downloaded tools should be cached. The default is the build root.")]
        [string]$DownloadRoot
    )

    begin {
        Set-StrictMode -Version Latest
    }

    process {
        [Aderant.Build.BuildMetadata]$buildMetadata = Get-AmbientBuildMetadata

        [Aderant.Build.Context]$context = [Aderant.Build.Context]::new()
        $context.BuildMetadata = $buildMetadata

        return $context
    }
}

Export-ModuleMember -Function New-BuildContext