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
.Parameter buildNumber
    The build id to retrieve build artifacts from.
.Parameter components
    The list of components to retrieve from the drop location. Defaults to 'Product'.
#>
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dropRoot,
    [Parameter(Mandatory=$false, ParameterSetName = "Branch")][ValidateNotNullOrEmpty()][string]$branch,
    [Parameter(Mandatory=$false, ParameterSetName = "PullRequest")][Alias("pull")][ValidateNotNullOrEmpty()][int]$pullRequestId,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][int]$buildNumber,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][ValidateSet("Product", "Test")][string[]]$components
)

begin {
    Set-StrictMode -Version 'Latest'
    $ErrorActionPreference = 'Stop'
    $InformationPreference = 'Continue'

    Write-Information "Running '$($MyInvocation.MyCommand.Name.Replace(`".ps1`", `"`"))' with the following parameters:"

    foreach ($parameter in $MyInvocation.MyCommand.Parameters) {
       Write-Information (Get-Variable -Name $Parameter.Values.Name -ErrorAction SilentlyContinue | Out-String)
    }

    function Clear-Environment {
        <#
        .Synopsis
            Clears the specified directory.
        #>
        [CmdletBinding()]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
            [Parameter(Mandatory=$false)][string[]]$exclusions = @('environment.xml', 'cms.ini')
        )

        process {
            if (Test-Path -Path $binariesDirectory) {
				if ($binariesDirectory -eq [System.IO.path]::GetPathRoot($binariesDirectory)) {
					throw 'Refusing to clean a drive root.'
				}

				if ($binariesDirectory -match 'Windows') {
					throw 'Refusing to clean a path containing Windows.'
				}

				if ($binariesDirectory -match 'Program') {
					throw 'Refusing to clean a path containing Program Files/Data.'
				}

                if (Test-Path -Path ([System.IO.Path]::Combine($binariesDirectory, '.git'))) {
                    throw 'Refusing a clean a source controlled directory.'
                }

                Write-Information "Clearing directory: $($binariesDirectory)"
                Remove-Item $binariesDirectory\* -Recurse -Force -Exclude $exclusions
            }
        }
    }

    function Confirm-Files {
        <#
        .Synopsis
            Check the SHA1 file hashes match the expected values in the specified directory.
        #>
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][System.IO.FileInfo[]]$filesToValidate,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$component
        )

        begin {
            Write-Information "Validating SHA1 file hashes for component: $component."
            Write-Debug 'SHA1 file validation list:'
            Write-Debug ($filesToValidate.FullName | Format-List | Out-String)
            [bool]$errors = $false
        }

        process {
            foreach ($file in $filesToValidate) {
                [string[]]$fileContent = (Get-Content -Path $file.FullName).Split(" ")
                [string]$expectedSha1 = $fileContent[0]
                [string]$fileToValidate = Join-Path -Path $file.DirectoryName -ChildPath $fileContent[2]

                if (-not (Test-Path -Path $fileToValidate)) {
                    Write-Error "Failed to locate file: '$fileToValidate' referenced in SHA1 file: '$($file.FullName)'." -ErrorAction Continue
                    $errors = $true
                    continue
                }

                [string]$actualSha1 = (Get-FileHash -Path $fileToValidate -Algorithm SHA1).Hash.ToUpper()

                if (-not $expectedSha1 -eq $actualSha1) {
                    Write-Error "Integrity check failed for file: '$fileToValidate'.`r`nExpected SHA1 hash: '$expectedSha1'`r`nActual SHA1 hash: '$actualSha1'" -ErrorAction Continue
                    $errors = $true
                } else {
                    Write-Debug "`r`nSHA1 validation for file: $fileToValidate with hash: '$actualSha1' succeeded."
                }
            }

            if ($errors) {
                exit 1
            }
        }
    }

    function Copy-Binaries {
        <#
        .Synopsis
            Acquires binaries from the specified drop location.
        #>
        [CmdletBinding()]
        [OutputType([double])]
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory,
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$dropRoot,
            [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string[]]$components,
            [switch]$clearBinariesDirectory
        )

        $components = $components | Sort-Object -Unique

        if (-not (Test-Path -Path $dropRoot)) {
            Write-Error "Drop path: '$droproot' does not exist."
            exit 1
        }

        if ($clearBinariesDirectory.IsPresent) {
            Clear-Environment -binariesDirectory $binariesDirectory
        }

        if (-not (Test-Path -Path $binariesDirectory)) {
            New-Item -Path $binariesDirectory -ItemType 'Directory' -Force | Out-Null
        }

        [double]$totalTime = 0

        foreach ($component in $components) {
            [string]$componentPath = Join-Path -Path $dropRoot -ChildPath $component

            if (-not (Test-Path $componentPath)) {
                Write-Error "Directory: '$componentPath' does not exist."
                exit 1
            }

            Start-Process -FilePath "robocopy.exe" -ArgumentList @($componentPath, $binariesDirectory, "*.*", "/NJH", "/MT", "R:10", "/W:1") -Wait -NoNewWindow

            [System.IO.FileInfo]$filesToValidate = Get-ChildItem -Path $binariesDirectory -File -Filter "*.sha1"

            if (-not $null -eq $filesToValidate) {
                $action = {
                    Confirm-Files -filesToValidate $filesToValidate -component $component
                }

                $executionTime = [System.Math]::Round((Measure-Command $action).TotalSeconds, 2)
                Write-Host "`r`nValidation for $component completed in: $executionTime seconds." -ForegroundColor Cyan
                $totalTime = $totalTime + $executionTime
            }
        }

        if ($null -ne $components -and $components.Length -gt 1) {
            Write-Host "Total binary acquisition time: $totalTime seconds." -ForegroundColor Cyan
        }

        return $totalTime
    }

    function ExtractArchives {
        <#
        .Synopsis
            Extracts all archives in the given directory.
        #>
        param (
            [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$binariesDirectory
        )

        process {
            [System.IO.FileInfo[]]$archives = Get-ChildItem -Path $binariesDirectory -File -Include "*.zip", "*.7z" -Depth 1

            [double]$totalTime = 0

            if ($null -eq $archives -or $archives.Length -eq 0) {
               Write-Information "No archives discovered at path: '$binariesDirectory'."
                return
            }

            if ($archives.Name.Contains('Binaries.')) {
                [string]$expertSourceDirectory = Join-Path -Path $binariesDirectory -ChildPath 'ExpertSource'
                [string]$logDirectory = Join-Path -Path $binariesDirectory -ChildPath 'Logs'

                New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
                New-Item -Path $expertSourceDirectory -ItemType Directory -Force | Out-Null
            }

            Add-Type -AssemblyName 'System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'

            Write-Information "`r`nExtracting archives:"
            Write-Information ($archives.FullName | Format-List | Out-String)
            Write-Information 'To directory:'
            Write-Information "$binariesDirectory`r`n"

            [string]$zipExe = [System.IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '\..\Build.Tools\7z.exe'))

            foreach ($archive in $archives) {
                [double]$executionTime = [System.Math]::Round((Measure-Command { Start-Process -FilePath $zipExe -ArgumentList "x -o$binariesDirectory $($archive.FullName) -r" -Wait -PassThru -NoNewWindow }).TotalSeconds, 2)
                Write-Host "Extracted archive: '$($archive.Name)' in: $executionTime seconds." -ForegroundColor Cyan
                $totalTime = $totalTime + $executionTime
            }
        }

        end {
            if ($null -ne $archives -and $archives.Length -gt 1) {
                Write-Host "`r`nTotal archive extraction time: $totalTime seconds." -ForegroundColor Cyan
            }

            return $totalTime
        }
    }
}

process {
    [double]$totalTime = 0

	$binariesDirectory = [System.IO.Path]::GetFullPath($binariesDirectory)

    switch ($PSCmdlet.ParameterSetName) {
        'Branch' {
            [string]$branchDropRoot = Join-Path -Path $dropRoot -ChildPath "product\refs\heads\$branch"

            if (-not (Test-Path -Path $branchDropRoot)) {
                Write-Error "Failed to retrieve binaries for branch: '$branch'`r`n at directory: '$branchDropRoot'."
                exit 1
            }

            if (!$buildNumber) {
                # Get the most recent build number if build number has not been specified by the user
                [int[]]$buildNumbers = Get-ChildItem -Path $branchDropRoot -Directory | Select-Object -ExpandProperty Name | Where-Object { $_ -match "^[\d\.]+$" }
                $buildNumber = $buildNumbers | Sort-Object -Descending | Select-Object -First 1
            }

            if (-not $PSCmdlet.ShouldProcess('Build number')) {
                Write-Information "The latest successful Build Number for branch: $branch is: $buildNumber."
                return $buildNumber
                exit 0
            }

            [string]$build = Join-Path $branchDropRoot -ChildPath $buildNumber

            Write-Information "Selected build: $build"

            $totalTime = Copy-Binaries -dropRoot $build -binariesDirectory $binariesDirectory -components $components -clearBinariesDirectory
        }
        'PullRequest' {
            [string]$pullRequestDropRoot = Join-Path -Path $dropRoot -ChildPath "pulls\$pullRequestId"

            if (-not (Test-Path -Path $pullRequestDropRoot)) {
                Write-Error "Failed to retrieve binaries for pull request: '$pullRequestId'`r`n at directory: '$pullRequestDropRoot'."
                exit 1
            }

            $totalTime = (Copy-Binaries -dropRoot $pullRequestDropRoot -binariesDirectory $binariesDirectory -components $components -clearBinariesDirectory)
        }
    }

    $totalTime = $totalTime + (ExtractArchives -binariesDirectory $binariesDirectory)
    Write-Host "`r`nProduct retrieved in $totalTime seconds.`r`n" -ForegroundColor Cyan
    exit 0
}