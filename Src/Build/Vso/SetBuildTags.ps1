Set-StrictMode -Version "Latest"
$InformationPreference = 'Continue'

$script:tags = @()

function AddTag([string]$prefix, [string]$tagValue) {
    if (-not ([string]::IsNullOrWhiteSpace($tagValue))) {
        $script:tags += $prefix + $tagValue
    }
}

#refs/heads/update/82SP1EX0002
$branch = $Env:BUILD_SOURCEBRANCH
if ([string]::IsNullOrWhiteSpace($branch)) {
    return
}

$branch = $branch.Replace("refs/heads/", "")
$segments = $branch.Split('/')

$prefix = $segments[0]

$wellKnownBranches = @("master", "update", "patch", "release")
foreach ($wellKnownBranch in $wellKnownBranches) {
    if ($prefix -eq $wellKnownBranch) {
        AddTag "" $prefix
    }
}

$lastPart = $segments[$segments.Count-1]
if ($lastPart -ne $prefix) {
    AddTag "" $lastPart
}

AddTag "pr-" $Env:SYSTEM_PULLREQUEST_PULLREQUESTID
AddTag "" $Env:BUILD_REQUESTEDFOR

foreach ($tag in $script:tags) {
    Write-Information "##vso[build.addbuildtag]$tag"
}