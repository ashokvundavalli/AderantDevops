[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$workfolder,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$branch,
    [Parameter(Mandatory=$false)][string]$excludedModules
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

[string]$tfsUrl = "http://tfs.$($env:USERDNSDOMAIN.ToLowerInvariant()):8080/tfs/"
[string]$workspaceName = [System.Guid]::NewGuid().Guid
[string]$serverPath = "$/ExpertSuite/$($branch.Replace('\', '/'))/Modules"

if (-not (Test-Path -Path $workfolder)) {
    New-Item -Path $workfolder -ItemType Directory -Force
}

[string[]]$excludedModules = $excludedModules.Split(';')

Push-Location -Path $workfolder

try {
    Write-Output "Mapping workspace: $workspaceName;$env:USERNAME"
    TF.exe vc workspace /new "$workspaceName;$env:USERNAME" /collection:$tfsUrl /noprompt
    Write-Output "Using workfolder: $workfolder"
    TF.exe vc workfold /map $serverPath $workfolder /collection:$tfsUrl /workspace:$workspaceName

    [string[]]$modules = TF.exe vc dir $serverPath

    for ([int]$i = 1;$i -lt $modules.Length - 2;$i++) {
        [string]$module = $modules[$i].Replace('$', '')
        if ($null -eq $excludedModules -or -not $excludedModules.Contains($module)) {
            TF.exe vc get "$serverPath/$module" /recursive /force /noprompt
        } else {
            Write-Output "Excluding module: $module"
        }
    }
} finally {
    Write-Output "Removing workspace mapping: $workspaceName;$env:USERNAME"
    TF.exe vc workspace /delete "$workspaceName;$env:USERNAME" /noprompt
}

Pop-Location