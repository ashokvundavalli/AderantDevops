function New-BuildContext
{
    <#
    .SYNOPSIS
        Creates a context object to use when running builds.
    #>
    [CmdletBinding()]
    [OutputType([Aderant.Build.BuildOperationContext])]
    param(
        [string]
        # The environment you're building in.
        $Environment
    )

    Set-StrictMode -Version 'Latest'

    Write-Debug "Creating new context"

    [Aderant.Build.BuildMetadata]$buildMetadata = Get-AmbientBuildMetadata

    $context = [Aderant.Build.BuildOperationContext]::new()
    $context.BuildMetadata = $buildMetadata
    $context.BuildScriptsDirectory = [System.IO.Path]::Combine($PSScriptRoot, "..\")

    return $context
}