# This scripts takes care of purging nuget packages from builds that have been deleted

param (
    [string]$packageUrl = "https://expertpackages-test.azurewebsites.net/",
    [string]$packageKey = "lHYdGnnp4RlE9oHNRs9VPaqbDiyjRL9kfT3RiaclqmiMYd0y0AYGvtH44UJn5w9gvQ11wX5QrtjDvoLEJUZprw==",
    [bool]$debug = 1
)

Set-StrictMode -Version 'Latest'
$InformationPreference = 'Continue'

if ($debug) {
    $DebugPreference = 'Continue'
}

$script:packageTag = 'PublishedPackages'

# https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.1
function GetAllBuilds {
    [string]$buildsUrl  = 'https://tfs.aderant.com/tfs/ADERANT/ExpertSuite/_apis/build/builds'

    $top = 100

    $currentProgressPreference = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'

    $allResults = @()

    $continuationToken = ""

    $minDate = (Get-Date).AddDays(-365).ToUniversalTime().ToString('O')
    $i = 0
    while ($true) {
        $url = "$($buildsUrl)?deletedFilter=onlyDeleted&queryOrder=finishTimeAscending&`$top=$top&continuationToken=$continuationToken&minTime=$($minDate)"

        $request = [System.Net.WebRequest]::Create($url)
        $request.Method = "Get"
        $request.UseDefaultCredentials = $true
        $request.PreAuthenticate = $true
        $request.UnsafeAuthenticatedConnectionSharing = $true
        $request.ContentType =  "application/json"

        $response = $request.GetResponse()
        $responseStream = $response.GetResponseStream()
        $readStream = [System.IO.StreamReader]::new($responseStream)

        $data = $readStream.ReadToEnd()

        $buildData = ($data | ConvertFrom-Json).value

        $allResults += $buildData

        #Get the continuation token
        $continuationToken = $response.Headers['x-ms-continuationtoken']

        $response.Dispose()
        $readStream.Dispose()
        $responseStream.Dispose()

        if ([string]::IsNullOrWhiteSpace($continuationToken)) {
            break
        }

        $i = $i + 1
        Write-Information ("Iteration: " + $i)
    }

    $ProgressPreference = $currentProgressPreference

    return $allResults
}

function GetPackageTags($build) {
    # Unicode snowman ☃︎.
    $tagDelimiter = [char]0x00002603
    $tags = @()

    foreach ($tag in $build.tags) {
        if (-not $tag.Contains($tagDelimiter)) {
            Write-Debug 'No tag delimiter found.'
            continue
        }

        $properties = $tag.Split($tagDelimiter)
        if ($properties.Count -ne 2) {
            Write-Debug "Property count: $($properties.Count)"
            continue
        }

        $dict = @{
            package = $properties[0]
            version = $properties[1].SubString(1).Trim() # Trim invalid character from the start of the version string.
        }

        $tags += New-Object -TypeName psobject -Property $dict
    }

    return $tags
}

function DeleteNugetPackages {
    # https://tfs.aderant.com/tfs/ADERANT/ExpertSuite/ExpertSuite%20Team/_wiki/wikis/ExpertSuite.wiki?wikiVersion=GBwikiMaster&pagePath=%2FDevOps%2FAzure%20Package%20Repository
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$packageUrl,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$packageKey,
        $buildsToDelete
    )

    begin {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    }

    process {
        $headers = @{
            "X-NuGet-ApiKey" = $packageKey
        }

        foreach ($buildToDelete in $buildsToDelete.GetEnumerator()) {
            foreach ($packageToDelete in $buildToDelete.Value) {
                $package = $packageToDelete.package
                $version = $packageToDelete.version
                $url = "$($packageUrl)/api/v2/package/$($package)/$($version)"

                try {
                    Write-Information "Deleting package: $package$version"
                    Invoke-WebRequest -Method Delete $url -Headers $headers
                } catch [System.Net.WebException] {
                    if ($PSItem.Exception.Response.StatusCode -eq 404) {
                    # We expect this to fail when it tries to delete a package that has already been deleted.
                        Write-Warning "Failed to delete package: $($packageToDelete.package)$($packageToDelete.version) => $($_.ToString())"
                    } else {
                        Write-Error $PSItem.Exception.ToString()
                    }
                }
            }
        }
    }
}

Write-Information "Querying builds..."
$builds = GetAllBuilds

$buildsToDelete = @{}

foreach ($build in $builds) {
    Write-Information "Processing build: $($build.id)"

    $buildId = $build.id

    if ($null -eq $build.PSObject.Properties.Item("deleted") -or ($build.deleted -eq $false)) {
        continue
    }

    if ($build.keepForever -or $build.retainedByRelease) {
        continue
    }

    if (-not $build.tags.Contains($script:packageTag)) {
        foreach ($tag in $build.tags) {
            Write-Debug $tag
        }

        continue
    }

    $tags = GetPackageTags $build

    if (($null -eq $tags.PSObject.Properties.Item('count')) -or ($tags.count -le 0)) {
        Write-Debug 'No associated packages.'
        continue
    }

    $buildsToDelete[$buildId] = $tags
    Write-Information "Packages associated with build: $($buildId) queued for deletion."
}

DeleteNugetPackages -packageUrl $packageUrl -packageKey $packageKey -buildsToDelete $buildsToDelete