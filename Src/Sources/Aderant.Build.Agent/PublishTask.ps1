[Cmdletbinding()]
param(
   [Parameter(Mandatory=$true)][string]$TaskPath,
   [Parameter(Mandatory=$false)][string]$TfsUrl = "http://tfs:8080/tfs/Aderant",
   [PSCredential]$Credential = (Get-Credential),
   [switch]$Overwrite = $false
)

# Load task definition from the JSON file
$taskDefinition = (Get-Content $taskPath\task.json) -join "`n" | ConvertFrom-Json
$taskFolder = Get-Item $TaskPath

# Bump the patch version. This is so our changes are automatically deployed to the build agents
$taskDefinition.version.patch = $taskDefinition.version.patch + 1
ConvertTo-Json -InputObject $taskDefinition -Depth 100 | Out-File $taskPath\task.json -Encoding utf8

# Zip the task content
Write-Output "Zipping task content"
$taskZip = ("{0}\..\{1}.zip" -f $taskFolder, $taskDefinition.id)
if (Test-Path $taskZip) { Remove-Item $taskZip }

Add-Type -AssemblyName "System.IO.Compression.FileSystem"

# Clean up before publish
gci -Path $taskFolder -Filter "Thumbs.db" -Hidden -Recurse | Remove-Item -Force

[IO.Compression.ZipFile]::CreateFromDirectory($taskFolder, $taskZip)

# Prepare to upload the task
Write-Output "Uploading task content"
$headers = @{ "Accept" = "application/json; api-version=2.0-preview"; "X-TFS-FedAuthRedirect" = "Suppress" }
$taskZipItem = Get-Item $taskZip
$headers.Add("Content-Range", "bytes 0-$($taskZipItem.Length - 1)/$($taskZipItem.Length)")
$url = ("{0}/_apis/distributedtask/tasks/{1}" -f $TfsUrl, $taskDefinition.id)
if ($Overwrite) {
   $url += "?overwrite=true"
}

# Actually upload it
Invoke-RestMethod -Uri $url -Credential $Credential -Headers $headers -ContentType application/octet-stream -Method Put -InFile $taskZipItem