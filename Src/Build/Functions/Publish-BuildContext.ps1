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

    $fileName = [Aderant.Build.Ipc.MemoryMappedFileReaderWriter]::WriteData($Context)

    [System.Environment]::SetEnvironmentVariable([Aderant.Build.WellKnownProperties]::ContextFileName, $fileName, [System.EnvironmentVariableTarget]::Process)

    return $fileName
}

Export-ModuleMember -Function Publish-BuildContext