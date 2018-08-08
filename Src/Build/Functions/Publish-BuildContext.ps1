function Publish-BuildContext
{
    <#
    .SYNOPSIS
        Makes the given build context available to other processes.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory=$true)]
        [Aderant.Build.BuildOperationContext]
        $Context
    )

    Set-StrictMode -Version 'Latest'

    [string]$name = [System.Diagnostics.Process]::GetCurrentProcess().Id
    $fileName = [Aderant.Build.Ipc.MemoryMappedFileReaderWriter]::WriteData($name, $Context)

    [System.Environment]::SetEnvironmentVariable([Aderant.Build.WellKnownProperties]::ContextFileName, $name, [System.EnvironmentVariableTarget]::Process)

    return $fileName
}