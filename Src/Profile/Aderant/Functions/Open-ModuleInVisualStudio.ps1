#Requires -RunAsAdministrator

function global:Open-ModuleInVisualStudio {
    <#
    .Synopsis
        Opens the solution for a module in the current branch.
    .Description
        Opens a module's main solution in Visual Studio.
    .PARAMETER ModuleName
        The name of the module (Optional).
    .PARAMETER Code
        Changes the behavior to open the module directory in Visual Studio Code.
    .EXAMPLE
        vs Libraries.Presentation
        Will open the Libraries.Presentation solution in Visual Studio.
    .EXAMPLE
        vs Framework -Code
        Will open the Framework directory in Visual Studio Code.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [Alias('vs')]
    param (
        [Parameter(Mandatory=$false, Position=0)][ValidateNotNullOrEmpty()][string]$ModuleName,
        [switch]$Code
    )

    begin {
        $ErrorActionPreference = 'Stop'
        [string]$devenv = 'devenv.exe'
        [string]$vsCode = 'code'
    }

    process {
        [string]$searchDirectory = $null
      
        # Get Directory
        if (-not [string]::IsNullOrWhiteSpace($ModuleName)) {
            $searchDirectory = (Resolve-Path $ModuleName).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
        } else {
            $searchDirectory = (Get-Location).Path
        }
        
        [string]$directoryName = [System.IO.Path]::GetFileName($searchDirectory)

        if ($Code.IsPresent) {
            if (Get-Command $vsCode -errorAction 'SilentlyContinue') {
                Write-Host "Opening directory: '$searchDirectory' in Visual Studio Code."

                if ($PSCmdlet.ShouldProcess($searchDirectory, 'Open Visual Studio Code')) {
                    Invoke-Expression -Command "$vsCode '$searchDirectory'"   
                }

                return
            } else {
                Write-Error -Message "Visual Studio Code ($vsCode) could not be found."
            }
        }

        [System.IO.FileInfo[]]$solutions = (Get-ChildItem -Path $searchDirectory -Filter '*.sln' -File | Where-Object { $_.Name -notmatch '.Custom.sln' })

        [string]$solutionPath = $null

        if ($null -ne $solutions -and $solutions.Length -ne 0) {
            if ($solutions.Length -eq 1) {
                # There is only one solution present.
                $solutionPath = $solutions[0].FullName
            } else {
                # Attempt to discover a solution file matching the name of the module.
                [System.IO.FileInfo]$targetSolution = [System.Linq.Enumerable]::FirstOrDefault(
                        $solutions,
                        [Func[System.IO.FileInfo, bool]] {
                            param($solution)
                            
                            $solution.BaseName.Replace('.', [string]::Empty) -eq $directoryName.Replace('.', [string]::Empty)
                        })

                if ($null -ne $targetSolution) {
                    # Found solution file which matches the module name.
                    $solutionPath = $targetSolution.FullName
                }
            }
        }
        
        if ([string]::IsNullOrWhiteSpace($solutionPath)) {
            Write-Error -Message "Unable to locate Visual Studio solution file associated with module: '$directoryName'."
        }

        Write-Host "Opening Solution: '$solutionPath' in Visual Studio."

        if ($PSCmdlet.ShouldProcess($solutionPath, 'Open Visual Studio')) {
            Invoke-Expression -Command "& '$devenv' '$solutionPath'"
        }
    }
}