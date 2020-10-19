# This scripts takes care of purging nuget packages from builds that have been deleted

param (
    [string]$packageUrl,
    [string]$packageKey,
    [bool]$debug
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

    $minDate = (Get-Date).AddDays(-31).ToUniversalTime().ToString('O')
    [string]$url = "$($buildsUrl)?deletedFilter=onlyDeleted&tagFilters=$($script:packageTag)&queryOrder=finishTimeAscending&minTime=$($minDate)&`$top=5000"
    $result = @()

    try {
        $result = (Invoke-RestMethod -Method Get -UseDefaultCredentials -Uri $url).value
    } catch {
        Write-Warning "Failed to retrive deleted builds: $($_.ToString())"
    }

    return $result
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

        try {
            $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('My', 'CurrentUser')
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
            $certificates = $store.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByApplicationPolicy, "1.3.6.1.5.5.7.3.2", $true)
    
            if ($certificates.Count -eq 0) {
                Write-Error 'No certificates for client authentication are available.'
                exit 1
            }

            $certificate = $certificates[0]

            foreach ($buildToDelete in $buildsToDelete.GetEnumerator()) {
                foreach ($packageToDelete in $buildToDelete.Value) {
                    $package = $packageToDelete.package
                    $version = $packageToDelete.version
                    $url = "$($packageUrl)/api/v2/package/$($package)/$($version)"
    
                    try {
                        Write-Information "Deleting package: $package$version"
                        Invoke-WebRequest -Method Delete $url -Headers $headers -Certificate $certificate
                    } catch {
                        # We expect this to fail when it tries to delete a package that has already been deleted.
                        Write-Warning "Failed to delete package: $($packageToDelete.package)$($packageToDelete.version) => $($_.ToString())"
                    }
                }
            }
        } finally {
            $store.Dispose()
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