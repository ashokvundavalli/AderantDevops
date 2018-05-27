function New-BuildContext
{
    <#
    .SYNOPSIS
        Creates a context object to use when running builds.   
    #>
    [CmdletBinding()]
    [OutputType([Aderant.Build.Context])]
    param(
        [Parameter(Mandatory=$true)]
        [string]
        # The environment you're building in.
        $Environment,        

        [string]
        # The place where downloaded tools should be cached. The default is the build root.
        $DownloadRoot
    )

    Set-StrictMode -Version 'Latest'

    [Aderant.Build.BuildMetadata]$buildMetadata = Get-AmbientBuildMetadata

    $context = [Aderant.Build.Context]::new()
    $context.BuildMetadata = $buildMetadata  

    return $context
}

Export-ModuleMember -Function New-BuildContext