$stem = "http://tfs:8080/tfs/ADERANT/ExpertSuite/_apis/"
$done = [System.Collections.Generic.HashSet[int]]::new()

function DeleteBuild($data) {
    if ($data.count -eq 0) {
        return
    }
    foreach ($pr in $data.value) {
        $prId = $pr.pullRequestId
         
        if (($done.Add($prId))) {             
            $branchName = "refs/pull/$prId/merge"
            $branchName = [System.Web.HTTPUtility]::UrlEncode($branchName)
            $buildsForPullRequest = Invoke-RestMethod -Method GET "$stem/build/builds?repositoryId=ExpertSuite&repositoryType=TfsGit&branchName=$branchName&api-version=2.0" -UseDefaultCredentials -ErrorAction Continue
            $builds = $buildsForPullRequest.value
            foreach ($build in $builds) {        
                if (-not $build.keepForever -and -not $build.retainedByRelease) {
                    Invoke-RestMethod -Method Delete "$($build.url)?api-version=2.0" -UseDefaultCredentials -ErrorAction Continue    
                }
            }
        }
    }
}

Add-Type -AssemblyName System.Web

$skip = 0
$top = 100
$branchName = [System.Web.HTTPUtility]::UrlEncode("refs/heads/master")
while ($true) {
    $data = Invoke-RestMethod -Method GET "$stem/git/repositories/ExpertSuite/pullRequests?searchCriteria.status=completed&searchCriteria.targetRefName=$branchName&top=$top&skip=$skip&api-version=2.0" -UseDefaultCredentials
    if ($data.count -eq 0) {
        break
    }
    DeleteBuild $data
    $skip = $skip + $top
    if ($skip -ge 50000) {
        break
    }
}