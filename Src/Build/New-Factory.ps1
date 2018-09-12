<#
.Synopsis
    Generate the customization sitemap.
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

    exit $LASTEXITCODE
}