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

if (-not (Test-Path -Path $workfolder)) {
    New-Item -Path $workfolder -ItemType Directory -Force
}

Push-Location -Path $workfolder

try {
    TF.exe vc workspace /new "$workspaceName;$env:USERNAME" /collection:$tfsUrl /noprompt
    TF.exe vc workfold /map "$/ExpertSuite/$($branch.Replace('\', '/'))/Modules" $workfolder /collection:$tfsUrl /workspace:$workspaceName

    foreach ($path in $paths) {
        TF.exe vc get $paths /recurse /force /noprompt
    }
} finally {
    TF.exe vc workspace /delete "$workspaceName;$env:USERNAME" /noprompt
}

Pop-Location