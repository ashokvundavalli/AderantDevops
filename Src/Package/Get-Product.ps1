<# 
.Synopsis 
    Acquire build artifacts from the specified drop location.
.Description    
    Copy the build artifacts from the specified drop location to the specified binaries directory, and exctract any archives retrieved.
.Example
    Get-Product -binariesDirectory C:\TFS\ExpertSuite\<branch name>\Binaries -$dropRoot \\dfs.aderant.com\ExpertSuite -branch <branch name>
.Example
    Get-Product -binariesDirectory C:\TFS\ExpertSuite\<branch name>\Binaries -$dropRoot \\dfs.aderant.com\ExpertSuite -pullRequestId 19159
.Parameter binariesDirectory
    The directory you want the binaries to be copied too
.Parameter dropRoot
    The path drop location that the binaries will be fetched from
.Parameter branch
    The branch to retrieve build artifacts from.
.Parameter pullRequestId
    The pull request id to retrieve build artifacts from.
.Parameter components
    The list of components to retrieve from the drop location. Defaults to 'Product'.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dropRoot,
    [Parameter(Mandatory=$false, ParameterSetName = "Branch")][ValidateNotNullOrEmpty()][string]$branch,
    [Parameter(Mandatory=$false, ParameterSetName = "PullRequest")][Alias("pull")][int]$pullRequestId,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][ValidateSet("Product", "Test")][string[]]$components = @("Product")
)

begin {
    $ErrorActionPreference = "Stop"
    Set-StrictMode -Version Latest

    Write-Host "Running '$($MyInvocation.MyCommand.Name.Replace(`".ps1`", `"`"))' with the following parameters:" -ForegroundColor Cyan

    foreach ($parameter in $MyInvocation.MyCommand.Parameters) {
        Write-Host (Get-Variable -Name $Parameter.Values.Name -ErrorAction SilentlyContinue | Out-String)
    }

    function Clear-Environment {
        <#
        .Synopsis
            Clears the specified directory.
        #>
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
            [Parameter(Mandatory=$false)][string[]]$exclusions = @("environment.xml", "cms.ini")
        )

        process {
            if (Test-Path $binariesDirectory) {
                Write-Host "Clearing directory: $($binariesDirectory)"
                Remove-Item $binariesDirectory\* -Recurse -Force -Exclude $exclusions
            }

            [string]$expertSourceDirectory = Join-Path -Path $binariesDirectory -ChildPath "ExpertSource"
            [string]$logDirectory = Join-Path -Path $binariesDirectory -ChildPath "Logs"

            New-Item -Path $logDirectory -ItemType Directory | Out-Null
            New-Item -Path $expertSourceDirectory -ItemType Directory | Out-Null
        }
    }

    function Copy-Binaries {
        <#
        .Synopsis
            Acquires binaries from the specified drop location.
        #>
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dropRoot,
            [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string[]]$components,
            [switch]$clearBinariesDirectory
        )

        begin {
            $components = $components | Sort-Object -Unique
        }

        process {
            if (-not (Test-Path -Path $dropRoot)) {
                Write-Error "Drop path: '$droproot' does not exist."
                exit 1
            }

            if ($clearBinariesDirectory.IsPresent) {
                Clear-Environment -binariesDirectory $binariesDirectory
            }

            [double]$totalTime = 0

            foreach ($component in $components) {
                [string]$componentPath = Join-Path -Path $dropRoot -ChildPath $component

                if (-not (Test-Path $componentPath)) {
                    Write-Error "Directory: '$componentPath' does not exist."
                    exit 1
                }

                [System.IO.FileInfo[]]$binaries = Get-ChildItem -Path $componentPath -File
                
                Write-Host "`r`nCopying files:"
                Write-Host ($binaries.FullName | Format-List | Out-String)
                Write-Host "To directory:"
                Write-Host "$binariesDirectory`r`n"
        
                [TimeSpan]$executionTime = Measure-Command { ForEach-Object -InputObject $binaries { Copy-Item -Path $_.FullName -Destination $binariesDirectory } }
                Write-Host "Acquired $component in: $([System.Math]::Round($executionTime.TotalSeconds, 2)) seconds.`r`n" -ForegroundColor Cyan
                $totalTime = $totalTime + [System.Math]::Round($executionTime.TotalSeconds, 2)
            }
        }

        end {
            if ($components.Count -gt 1) {
                Write-Host "Total binary acquisition time: $totalTime seconds." -ForegroundColor Cyan
            }

            return $totalTime
        }
    }

    function Export-Archives {
        <#
        .Synopsis
            Extracts all archives in the binaries directory.
        #>
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory
        )

        process {
            [System.IO.FileInfo[]]$archives = Get-ChildItem -Path $binariesDirectory -File -Filter "*.zip"

            if ($null -eq $archives) {
                Write-Host "No archives discovered at path: '$binariesDirectory'."
                return
            }

            Add-Type -AssemblyName "System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
            [double]$totalTime = 0

            Write-Host "`r`nExtracting archives:"
            Write-Host ($archives.FullName | Format-List | Out-String)
            Write-Host "To directory:"
            Write-Host "$binariesDirectory`r`n"

            foreach ($archive in $archives) {
                [TimeSpan]$executionTime = Measure-Command { [System.IO.Compression.ZipFile]::ExtractToDirectory($archive.FullName, $binariesDirectory) }
                Write-Host "Extracted archive: '$($archive.Name)' in: $([System.Math]::Round($executionTime.TotalSeconds, 2)) seconds." -ForegroundColor Cyan
                $totalTime = $totalTime + [System.Math]::Round($executionTime.TotalSeconds, 2)
            }
        }

        end {
            if ($archives.Count -gt 1) {
                Write-Host "`r`nTotal archive extraction time: $totalTime seconds." -ForegroundColor Cyan
            }

            return $totalTime
        }
    }


}


process {
    [double]$totalTime = 0

    switch ($PSCmdlet.ParameterSetName) {
        "Branch" {
            [string]$branchDropRoot = Join-Path -Path $dropRoot -ChildPath "product\refs\heads\$branch"

            if (-not (Test-Path -Path $branchDropRoot)) {
                Write-Error "Failed to retrieve binaries for branch: '$branch'`r`n at directory: '$branchDropRoot'."
                exit 1
            }

            [string]$build = Join-Path $branchDropRoot -ChildPath (Get-ChildItem -Path $branchDropRoot -Directory | Sort-Object -Property Name | Select-Object -First 1)

            $totalTime = (Copy-Binaries -dropRoot $build -binariesDirectory $binariesDirectory -components $components -clearBinariesDirectory)
        }
        "PullRequest" {
            [string]$pullRequestDropRoot = Join-Path -Path $dropRoot -ChildPath "pulls\$pullRequestId"

            $totalTime = (Copy-Binaries -dropRoot $pullRequestDropRoot -binariesDirectory $binariesDirectory -components $components -clearBinariesDirectory)
        }
    }

    $totalTime = $totalTime + (Export-Archives -binariesDirectory $binariesDirectory)
}

end {
    Write-Host "`r`nProduct retrieved in $totalTime seconds.`r`n" -ForegroundColor Cyan

    exit $LASTEXITCODE
}