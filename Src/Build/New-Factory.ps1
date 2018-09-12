<#
.Synopsis
    Generate the customization sitemap.
.Description    
    Generate the customization sitemap using FactoryResourceGenerator.exe.
.Example
    New-Factory -factoryDirectory 'C:\TFS\ExpertSuite\Dev\vnext\Binaries\ExpertSource' -searchDirectory 'C:\TFS\ExpertSuite\Dev\vnext\Binaries\ExpertSource' -logDirectory 'C:\TFS\ExpertSuite\Dev\vnext\Binaries\Logs'
.Parameter factoryDirectory
    The directory containing the FactoryResourceGenerator.exe.
.Parameter searchDirectory
    The directory to operate on.
.Parameter searchPath
    The search pattern for the Factory Resource Generator to use.
.Parameter logDirectory
    The directory to output the FactoryResourceGenerator.log file.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$factoryDirectory,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$searchDirectory,
    [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$searchPath = "Aderant*.dll`,*.exe",
    [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$logDirectory
)

begin {
    $ErrorActionPreference = "Stop"
    Set-StrictMode -Version Latest
    
    Write-Output "Running '$($MyInvocation.MyCommand.Name.Replace(`".ps1`", `"`"))' with the following parameters:"

    foreach ($parameter in $MyInvocation.MyCommand.Parameters) {
        Write-Output (Get-Variable -Name $Parameter.Values.Name -ErrorAction SilentlyContinue | Out-String)
    }

    [string]$factoryResourceGenerator = Join-Path -Path $factoryDirectory -ChildPath "FactoryResourceGenerator.exe"
}

process {
    if (-not (Test-Path -Path $factoryResourceGenerator)) {
        Write-Error "FactoryResourceGenerator does not exist at path: '$factoryResourceGenerator'."
        exit 1
    }

    (& "$factoryDirectory\FactoryResourceGenerator.exe" /v:+ /f:"$searchDirectory" /of:"$searchDirectory\Factory.bin" /sp:"$searchPath") | Tee-Object -FilePath (Join-Path -Path $logDirectory -ChildPath "FactoryResourceGenerator.log")
}

end {
    exit $LASTEXITCODE
}