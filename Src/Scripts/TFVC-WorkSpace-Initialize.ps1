[CmdletBinding()]
param (
    [Paramter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$workfolder,
    [Parameter(Mandatory=$true)][string[]]$modules
)

begin {
    Set-StrictMode -Version 'Latest'
    $ErrorActionPreference = 'Stop'

    [string]$tfsUrl = "http://tfs.$($env:USERDNSDOMAIN.ToLowerInvariant()):8080/tfs/"
    [string]$workspaceName = [System.Guid]::NewGuid().Guid
}

process {
    if (-not (Test-Path -Path $workfolder)) {
        New-Item -Path $workfolder -ItemType Directory -Force
    }

    Push-Location -Path $workfolder

    try {
        TF.exe vc workspace /new "$workspaceName;$env:USERNAME" /collection:$tfsUrl /noprompt
        TF.exe vc workfold /map '$/ExpertSuite/Dev/vnext/Modules' $workfolder /collection:$tfsUrl /workspace:$workspaceName

        foreach ($path in $paths) {
            TF.exe vc get $paths /recurse /force /noprompt
        }
    } finally {
        TF.exe vc workspace /delete "$workspaceName;$env:USERNAME" /noprompt
    }
}

end {
    Pop-Location
}