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
        [Aderant.Build.Context]
        $Context
    )

    Set-StrictMode -Version 'Latest'

    [string]$name = [System.Diagnostics.Process]::GetCurrentProcess().Id
    $fileName = [Aderant.Build.Ipc.MemoryMappedFileReaderWriter]::WriteData($name, $Context)

    return $fileName
}