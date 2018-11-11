[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$workfolder,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$workspaceName,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$branch,
    [Parameter(Mandatory=$true)][string[]]$modules
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

[string]$tfsUrl = "http://tfs.$($env:USERDNSDOMAIN.ToLowerInvariant()):8080/tfs/"

if ($null -eq $workspaceName) {
    $workspaceName = [System.Guid]::NewGuid().Guid
}

[string]$serverPath = "$/ExpertSuite/$($branch.Replace('\', '/'))/Modules"

if (-not (Test-Path -Path $workfolder)) {
    New-Item -Path $workfolder -ItemType Directory -Force
}

Push-Location -Path $workfolder

try {
    Write-Output "Mapping workspace: $workspaceName;$env:USERNAME"
    TF.exe vc workspace /new "$workspaceName;$env:USERNAME" /collection:$tfsUrl /noprompt
    Write-Output "Using workfolder: $workfolder"
    TF.exe vc workfold /map $serverPath $workfolder /collection:$tfsUrl /workspace:$workspaceName

    foreach ($module in $modules) {
        TF.exe vc get "$serverPath/$module" /recursive /force /noprompt
    }
} finally {
    Write-Output "Removing workspace mapping: $workspaceName;$env:USERNAME"
    TF.exe vc workspace /delete "$workspaceName;$env:USERNAME" /noprompt
}

Pop-Location