function global:Get-BuildConfigFilePaths {
    <#
    .Synopsis
        Given a starting directory look for configuration files needed to control the build.
        Searches in this order:
        - .
        - .\Build
        - .\..\Build
        - ..\..\..\Build
    #>
    [CmdletBinding()]
    param(
        [string]$startingDirectory,
        [string[]]$ceilingDirectories,
        [bool]$setPathAsGlobalVariable
    )

    begin {
        Set-StrictMode -Version Latest

        [System.Collections.ArrayList]$names = @('BranchConfig.xml', 'ExpertManifest.xml')
        $pathStack = [System.Collections.Stack]::new()
        $resultHashtable = @{}

        function TestFilePath($resultHashtable, $currentDirectory, $name) {
             if (-not ($resultHashtable.ContainsKey($name))) {
                $file = Join-Path -Path $currentDirectory -ChildPath $name

                $file = [System.IO.Path]::GetFullPath($file)

                if (Test-Path -Path $file) {
                    $resultHashtable[$name] = $file
                    return $file
                }
                return $null
            }
        }

        function FindFile($resultHashtable, $directory, $name) {
            while ($true) {
                $pathStack.Clear()
                $pathStack.Push($directory)
                $pathStack.Push([System.IO.Path]::Combine($directory, "Build"))

                $file = $null
                while ($pathStack.Count -gt 0) {
                    $currentDirectory = $pathStack.Pop()
                    $file = TestFilePath $resultHashtable $currentDirectory $name

                    if ($null -ne $file) {
                        return
                    }

                    if ($null -eq $ceilingDirectories) {
                        if (Test-Path (Join-Path -Path $currentDirectory -ChildPath ".git")) {
                            break
                        }
                    }
                }

                # Did not find the file, look up
                $directory = [System.IO.Directory]::GetParent($directory)

                if ($null -eq $directory) {
                    break
                }

                $directory = [System.IO.Path]::GetFullPath($directory)

                if ($null -ne $ceilingDirectories) {
                    foreach ($ceilingDirectory in $ceilingDirectories) {
                        if ($ceilingDirectory -eq $directory) {
                            break
                        }
                    }
                }
            }
        }
    }

    process {
        if ([string]::IsNullOrWhitespace($startingDirectory)) {
            $startingDirectory = (Get-Location).Path
        }

        if (Test-Path -Path 'variable:global:BranchConfigPath') {
            if (-not [string]::IsNullOrWhiteSpace($global:BranchConfigPath)) {
                $startingDirectory = $global:BranchConfigPath
            }
        }

        foreach ($name in $names) {
            FindFile $resultHashtable $startingDirectory $name
        }

        $info = [PSCustomObject]@{
            BranchConfigFile    = $null
            ProductManifestFile = $null
        }

        if ($resultHashtable.ContainsKey('BranchConfig.xml')) {
            $info.BranchConfigFile = $resultHashtable['BranchConfig.xml']
            if ($setPathAsGlobalVariable) {
                $global:BranchConfigPath = [System.IO.Path]::GetDirectoryName($info.BranchConfigFile)
            }
        }

        if ($resultHashtable.ContainsKey('ExpertManifest.xml')) {
            $info.ProductManifestFile = $resultHashtable['ExpertManifest.xml']
        }

        return $info
    }
}