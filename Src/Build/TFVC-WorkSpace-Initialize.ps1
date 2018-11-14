[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$workfolder,
    [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$branch,
    [Parameter(Mandatory=$false)][string]$excludedModules
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

[string]$tf = "$($env:VSINSTALLDIR)Common7\IDE\TF.exe"

if (-not (Test-Path $tf)) {
    Write-Error "Unable to locate TF.exe at path: $tf"
    exit 1
}

[string]$tfsUrl = $Env:SYSTEM_TEAMFOUNDATIONSERVERURI
[string]$workspaceName = [System.Guid]::NewGuid().Guid
[string]$serverPath = "$/ExpertSuite/$($branch.Replace('\', '/'))/Modules"

if (-not (Test-Path -Path $workfolder)) {
    New-Item -Path $workfolder -ItemType Directory -Force
}

[string[]]$excludedModules = $excludedModules.Split(';')

Push-Location -Path $workfolder

try {
    Write-Output "Mapping workspace: $workspaceName;$env:USERNAME"
    & $tf vc workspace /new "$workspaceName;$env:USERNAME" /collection:$tfsUrl /noprompt
    Write-Output "Using workfolder: $workfolder"
    & $tf vc workfold /map $serverPath $workfolder /collection:$tfsUrl /workspace:$workspaceName

    [string[]]$modules = & $tf vc dir $serverPath

    for ([int]$i = 1;$i -lt $modules.Length - 2;$i++) {
        [string]$module = $modules[$i].Replace('$', '')
        if ($null -eq $excludedModules -or -not $excludedModules.Contains($module)) {
            & $tf vc get "$serverPath/$module" /recursive /force /noprompt
        } else {
            Write-Output "Excluding module: $module"
        }
    }
} finally {
    Write-Output "Removing workspace mapping: $workspaceName;$env:USERNAME"
    & $tf vc workspace /delete "$workspaceName;$env:USERNAME" /noprompt
}

Pop-Location