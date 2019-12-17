# This scripts takes care of purging outputs from builds that have been deleted
# because apparently TFS is incapable of doing this reliably
# https://github.com/Microsoft/azure-pipelines-agent/issues/1302
# https://developercommunity.visualstudio.com/content/problem/19481/old-builds-cant-be-deleted-even-if-the-retention-p.html
# https://developercommunity.visualstudio.com/content/problem/376720/retention-policy-not-removing-all-builds-artifacts.html
# https://developercommunity.visualstudio.com/content/problem/308559/tfs-2018-build-artifacts-not-being-removed.html

Set-StrictMode -Version Latest
$InformationPreference = 'Continue'

$version = "2017-04-17"
$storageAccount = "expertbuildrete8bb0"
$storageTable = "builds"
$table_url = "https://$storageAccount.table.core.windows.net"
$accesskey = "Te2XHPUov+UvG09q94Kh5Cbl8hHAMlfBQwfGPi9flevbnGBCNaf/c0tJmDwrqJwxCY4HKgGYFfElIlHGCwwTRw=="

# A lookup cache so we don't query the same build more than once
$queryCache = @{}
$pathsToDelete = @{}

function SignContent($resource) {
    $GMTTime = (Get-Date).ToUniversalTime().ToString('R')
    $stringToSign = "$GMTTime`n/$storageAccount/$resource"
    $hmacsha = [System.Security.Cryptography.HMACSHA256]::new()
    $hmacsha.key = [Convert]::FromBase64String($accesskey)
    $signature = $hmacsha.ComputeHash([Text.Encoding]::UTF8.GetBytes($stringToSign))

    return [Convert]::ToBase64String($signature),$GMTTime
}


# Based on samples from https://gcits.com/knowledge-base/use-azure-table-storage-via-powershell-rest-api/
function GetTableEntityAll([string]$tableName) {
    $resource = $tableName
    $table_url = "$table_url/$resource"
    $signature,$GMTTime = SignContent $resource
    $headers = @{
        'x-ms-date'    = $GMTTime
        Authorization  = "SharedKeyLite " + $storageAccount + ":" + $signature
        "x-ms-version" = $version
        Accept         = "application/json;odata=fullmetadata"
    }
    $item = Invoke-RestMethod -Method Get -Uri $table_url -Headers $headers -ContentType application/json
    return $item.value
}


function DeleteTableEntity($tableName, $partitionKey, $rowKey) {
    $resource = "$tableName(PartitionKey='$partitionKey',RowKey='$rowKey')"
    $table_url = "$table_url/$resource"
    $signature,$GMTTime = SignContent $resource
    $headers = @{
        'x-ms-date'    = $GMTTime
        Authorization  = "SharedKeyLite " + $storageAccount + ":" + $signature
        "x-ms-version" = $version
        Accept         = "application/json;odata=minimalmetadata"
        'If-Match'     = "*"
    }
    $item = Invoke-RestMethod -Method DELETE -Uri $table_url -Headers $headers -ContentType application/http -ErrorAction Stop
}


function DeleteBuildOutputs($pathsToDelete) {
    $rowKeysToDelete = @()
    $pathsWithErrors = @()

    foreach ($kvp in $pathsToDelete.GetEnumerator()) {
        $hadError = $false

        if (Test-Path -LiteralPath $kvp.Key) {
            try {
                Remove-Item -LiteralPath $kvp.Key -Force -Recurse -Verbose -ErrorAction Continue
            } catch {
                $hadError = $true
                $pathsWithErrors += $kvp.Key
            }
        } else {
            Write-Information "Path $($kvp.Key) does not exist"
        }

        if (-not $hadError) {
            foreach ($rowKey in $kvp.Value) {
                $rowKeysToDelete += $rowKey
            }
        }
    }

    return $rowKeysToDelete
}

Write-Information "Querying builds..."
[Array]$builds = GetTableEntityAll $storageTable

foreach ($tableEntity in $builds) {
    $buildUrl = $tableEntity.Url

    Write-Information "'$($tableEntity.RowKey): Processing: $buildUrl"

    $result = $null
    $seenBefore = $false

    if ($queryCache.ContainsKey($buildUrl)) {
        $result = $queryCache[$buildUrl]
        $seenBefore = $true
    } else {
        try {
            $result = Invoke-RestMethod -Method Get -Uri $buildUrl -UseDefaultCredentials
        } catch {
            Write-Warning -Message $_
            continue
        }
        $queryCache.Add($buildUrl, $result)
    }

    Write-Debug $result

    if ($null -ne $result.PSObject.Properties.Item("deleted")) {
        if ($result.deleted) {
            if ($result.keepForever) {
                continue
            }

            if ($result.retainedByRelease) {
                continue
            }

            $artifactPath = $tableEntity.ArtifactPath
            if (-not $pathsToDelete.ContainsKey($artifactPath)) {
                Write-Information "Adding '$artifactPath' to delete queue"
                $pathsToDelete[$artifactPath] = @()
            }

            $pathsToDelete[$artifactPath] += $tableEntity.RowKey
        }
    } else {
        if (-not $seenBefore) {
            Write-Information "'$($tableEntity.RowKey): Build $buildUrl is not deleted yet"
        }
    }
}

$rowKeysToDelete = DeleteBuildOutputs $pathsToDelete

foreach ($key in $rowKeysToDelete) {
    Write-Information "Deleting artifact data with row key: $key"
    DeleteTableEntity $storageTable "" $key
}