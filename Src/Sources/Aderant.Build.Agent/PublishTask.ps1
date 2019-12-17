# This script takes care of bundling and uploading a task to the TFS server for use with the build system.
[Cmdletbinding()]
param(
   [Parameter(Mandatory=$true)][string]$TaskPath,
   [Parameter(Mandatory=$false)][string]$TfsUrl = 'https://tfs.aderant.com/tfs/Aderant',
   [switch]$Overwrite,
   [switch]$Publish
)

function RemovePathQuiet([string]$path) {
    Remove-Item $path -Force -Recurse -ErrorAction "SilentlyContinue"
}

function DownloadLatestSdk([string]$TaskPath) {
    # Refer to https://github.com/microsoft/azure-pipelines-task-lib/blob/master/powershell/Docs/Consuming.md for package structure
    $vstsTaskSdk = "VstsTaskSdk"
    $psModulePath = [System.IO.Path]::Combine($TaskPath, "ps_modules")
    
    [System.IO.Directory]::CreateDirectory($psModulePath)

    $vstsTaskSdkHome = [System.IO.Path]::Combine($psModulePath, $vstsTaskSdk)
    RemovePathQuiet $vstsTaskSdkHome

    $temporarySdkHome = [System.IO.Path]::Combine($Env:TEMP, [Guid]::NewGuid)
    RemovePathQuiet $temporarySdkHome

    Save-Module -Name $vstsTaskSdk -Path $temporarySdkHome -Force

    $taskLib = Get-ChildItem -Path "$temporarySdkHome\VstsTaskSdk" -Depth 1 -Directory | Select-Object -First 1
    $taskLib.MoveTo($vstsTaskSdkHome)
}

$originalErrorActionPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Stop'

    $TaskPath = Resolve-Path $TaskPath

    DownloadLatestSdk $TaskPath

    # Load task definition from the JSON file
    $taskDefinition = (Get-Content $taskPath\task.json) -join "`n" | ConvertFrom-Json
    $taskFolder = Get-Item $TaskPath

    if (-not $Overwrite) {
    # Bump the patch version. This is so our changes are automatically deployed to the build agents
        $taskDefinition.version.patch = $taskDefinition.version.patch + 1
        ConvertTo-Json -InputObject $taskDefinition -Depth 100 | Out-File $taskPath\task.json -Encoding utf8
    }

    # Zip the task content
    Write-Output "Zipping task content"
    $taskZip = ("{0}\..\{1}.zip" -f $taskFolder, $taskDefinition.id)
    if (Test-Path $taskZip) {
        Remove-Item $taskZip
    }

    Add-Type -AssemblyName "System.IO.Compression.FileSystem"

    # Clean up before publish
    Get-ChildItem -Path $taskFolder -Filter "Thumbs.db" -Hidden -Recurse | Remove-Item -Force

    [IO.Compression.ZipFile]::CreateFromDirectory($taskFolder, $taskZip)

    # Prepare to upload the task    
    $headers = @{ "Accept" = "application/json; api-version=2.0-preview"; "X-TFS-FedAuthRedirect" = "Suppress" }
    $taskZipItem = Get-Item $taskZip
    $headers.Add("Content-Range", "bytes 0-$($taskZipItem.Length - 1)/$($taskZipItem.Length)")
    $url = ("{0}/_apis/distributedtask/tasks/{1}" -f $TfsUrl, $taskDefinition.id)
    if ($Overwrite) {
       $url += "?overwrite=true"
    }

    if ($Publish) {
      Write-Output "Uploading task content"
      # Actually upload it
      Invoke-RestMethod -Uri $url -Headers $headers -ContentType application/octet-stream -Method Put -InFile $taskZipItem -UseDefaultCredentials
    }
} finally {
    $ErrorActionPreference = $originalErrorActionPreference
}