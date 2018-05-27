function Publish-BuildContext
{
    <#
    .SYNOPSIS
        Makes the the given build context available to other processes.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory=$true)]
        [Aderant.Build.Context]
        $Context,

        [Parameter(Mandatory=$false)]
        [string]
        $Environment
    )

    Set-StrictMode -Version 'Latest'

    $fileName = [Aderant.Build.Ipc.MemoryMappedBufferReaderWriter]::WriteData($Context)

    [System.Environment]::SetEnvironmentVariable("BuildContextChannelId", $fileName, [System.EnvironmentVariableTarget]::Process)

    return $fileName
}

Export-ModuleMember -Function Publish-BuildContext