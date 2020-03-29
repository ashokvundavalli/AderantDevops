Set-StrictMode -Version "Latest"
$InformationPreference = 'Continue'

#refs/heads/update/82SP1EX0002
$branch = $Env:BUILD_SOURCEBRANCH
if ([string]::IsNullOrWhiteSpace($branch)) {
    return
}

$branch = $branch.Replace("refs/heads/", "")
$segments = $branch.Split('/')

$prefix = $segments[0]

$tags = @()

$wellKnownBranches = @("master", "update", "patch", "release")
foreach ($wellKnownBranch in $wellKnownBranches) {
    if ($prefix -eq $wellKnownBranch) {
        $tags += $prefix
    }
}

$lastPart = $segments[$segments.Count-1]
if ($lastPart -ne $prefix) {
    $tags += $lastPart
}

foreach ($tag in $tags) {
    Write-Information "##vso[build.addbuildtag]$tag"
}