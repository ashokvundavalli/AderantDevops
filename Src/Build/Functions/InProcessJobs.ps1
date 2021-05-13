[CmdletBinding()]
param(
    [string]$Version
)

if (!$Version) {
    return
}

if (([System.Management.Automation.PSTypeName]'InProcess.InMemoryJob').Type) {
    # The type is already defined so bail out
    return
}

{
    param (
        [string]$ThisFileFullPath
    )
    $directory = [System.IO.Path]::GetDirectoryName($ThisFileFullPath)
    $file = [System.IO.Path]::GetFileNameWithoutExtension($ThisFileFullPath)

    $file = [System.IO.Path]::Combine($directory, $file + ".dll")

    $options = [System.IO.FileOptions]::DeleteOnClose
    $share = [System.IO.FileShare]::Read -bor [System.IO.FileShare]::Delete

    function Compile {
      [OutputType([bool])]
      param (
        [string] $AssemblyPath,
        [string] $Code
        )

        try {
            if (-not (Test-Path $AssemblyPath)) {
                Add-Type -Path $Code -OutputAssembly "$AssemblyPath"
                return $true
            }
        } catch [System.UnauthorizedAccessException] {
             # We should ignore this exception if we got it,
             # the most important reason is that the file has already been
             # scheduled for deletion and will be deleted when all handles
             # are closed.
        }

        Add-Type -Path $Code
        return $false
    }

    $compiled = $false
    $useMemoryMappedFile = $true
    $mapName = "InProcessJobs.ps1-$($Version)"
    $encoding = [System.Text.Encoding]::UTF8
    [System.Byte[]]$buffer = $null

    [void][System.Reflection.Assembly]::Load("System.IO.MemoryMappedFiles, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
    $memoryMappedFile = $null

    # Performance optimization. To avoid the slow PowerShell C# compiler we
    # store the last seen outputs in a MMF. Here we try to open that MMF and load the assembly data
    try {
        Write-Debug "Attempting to open MMF: $mapName"
        $memoryMappedFile = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting($mapName)
    } catch [System.IO.FileNotFoundException], [System.IO.DirectoryNotFoundException] {
        Write-Debug "Failed to open MMF: $mapName"

        try {
            $compiled = Compile -AssemblyPath $file -Code ([System.IO.Path]::Combine($PSScriptRoot, "InProcessJobs.cs"))
        } catch [System.Exception] {
            $useMemoryMappedFile = $false
        }

        if ($compiled -and $useMemoryMappedFile) {
            [System.IO.File]::SetAttributes($file, [System.IO.File]::GetAttributes($file) -bor [System.IO.FileAttributes]::NotContentIndexed -bor [System.IO.FileAttributes]::Temporary)
            $fs = [System.IO.FileStream]::new($file, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $share, 4096, $options)

            $memoryMappedFile = [System.IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew($mapName, 10000)
            $memoryMappedStream = $memoryMappedFile.CreateViewStream()

            # Write the assembly length to the first 8 bytes of the stream
            $writer = [System.IO.BinaryWriter]::new($memoryMappedStream, $encoding, $true)
            $writer.Write($fs.Length)
            $writer.Dispose()

            $pos = $memoryMappedStream.Position
            $fs.CopyTo($memoryMappedStream)

            $memoryMappedStream.Position = $pos

            $buffer = [System.Byte[]]::new($fs.Length)
            [void]$memoryMappedStream.Read($buffer, 0, $fs.Length)

            [System.AppDomain]::CurrentDomain.SetData("IN_PROCESS_JOB_FILE", $fs)
        }
    }

    if (!$compiled -and $null -ne $memoryMappedFile) {
        $memoryMappedStream = $memoryMappedFile.CreateViewStream()

        # Read the assembly length and then the assembly data
        $reader = [System.IO.BinaryReader]::new($memoryMappedStream, $encoding, $true)
        $length = $reader.ReadInt64()
        $buffer = $reader.ReadBytes($length)
    }

    if ($null -ne $memoryMappedFile) {
        # GC root so it does not get garbage collected prematurely
        [System.AppDomain]::CurrentDomain.SetData("IN_PROCESS_JOB_MMF", $memoryMappedFile)
    }

    if ($null -ne $buffer) {
        [void][System.Reflection.Assembly]::Load($buffer)
    }

}.Invoke($MyInvocation.MyCommand.Path)

function Start-JobInProcess {
    [CmdletBinding()]
    param
    (
        [ScriptBlock] $ScriptBlock,
        $ArgumentList,
        [string] $Name
    )

    function Get-JobRepository {
        [CmdletBinding()]
        param()
        $pscmdlet.JobRepository
    }

    function Add-Job {
        [CmdletBinding()]
        param
        (
            $job
        )
        $pscmdlet.JobRepository.Add($job)
    }

    if ($ArgumentList) {
        $PowerShell = [PowerShell]::Create().AddScript($ScriptBlock)

        if ($ArgumentList -is [Array]) {
            foreach ($argument in $ArgumentList) {
                $PowerShell = $PowerShell.AddArgument($argument)
            }
        } else {
            $PowerShell = $PowerShell.AddArgument($ArgumentList)
        }

        $MemoryJob = [InProcess.InMemoryJob]::new($PowerShell, $Name)
    } else {
        $MemoryJob = [InProcess.InMemoryJob]::new($ScriptBlock, $Name)
    }

    $MemoryJob.Start()
    Add-Job $MemoryJob
    $MemoryJob
}