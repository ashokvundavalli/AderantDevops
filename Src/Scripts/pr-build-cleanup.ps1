$InformationPreference = 'Continue'
Add-Type -AssemblyName System.Web

$stem = "http://tfs:8080/tfs/ADERANT/ExpertSuite/_apis/"

$repositories =  @(    
    "Database",
    "ExpertSuite",
    "Deployment"
)

$done = [System.Collections.Generic.HashSet[int]]::new()

function DeleteBuild() {
    param(
        $data,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $RepositoryId
    )

    if ($data.count -eq 0) {
        return
    }

    foreach ($pr in $data.value) {
        $prId = $pr.pullRequestId
         
        if (($done.Add($prId))) {             
            $branchName = "refs/pull/$prId/merge"
            $branchName = [System.Web.HTTPUtility]::UrlEncode($branchName)
            $buildsForPullRequest = Invoke-RestMethod -Method GET "$stem/build/builds?repositoryId=$repositoryId&repositoryType=TfsGit&branchName=$branchName&api-version=2.0" -UseDefaultCredentials -ErrorAction Continue
            $builds = $buildsForPullRequest.value
            foreach ($build in $builds) {        
                if (-not $build.keepForever -and -not $build.retainedByRelease) {
                    Write-Information $build.url 
                    Invoke-RestMethod -Method Delete "$($build.url)?api-version=2.0" -UseDefaultCredentials -ErrorAction Continue    
                }            
            }
        }
    }
}


function FindAndDeleteBuilds() {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $RepositoryId
    )

    $skip = 0
    $top = 100
    $branchName = [System.Web.HTTPUtility]::UrlEncode("refs/heads/master")
    
    $currentPage = [System.Collections.Generic.HashSet[int]]::new()
    $previousPage = $null

    while ($true) {
        $data = Invoke-RestMethod -Method GET "$stem/git/repositories/$RepositoryId/pullRequests?searchCriteria.status=completed&searchCriteria.targetRefName=$branchName&top=$top&skip=$skip&api-version=2.0" -UseDefaultCredentials
        if ($data.count -eq 0) {
            break
        }

        foreach ($id in $data.value) {
            [void]$currentPage.Add($id.pullRequestId)
        }

        DeleteBuild $data $repositoryId
        $skip = $skip + $top

        if ($skip -ge 50000) {
            break
        }

        if ($null -ne $previousPage -and $previousPage.Count -gt 0) {
            # No more results - bail out
            if ($currentPage.SetEquals($previousPage)) {
                Write-Information "No more results for $RepositoryId"
                break
            }
        }

        $previousPage = [System.Collections.Generic.HashSet[int]]::new($currentPage)
        $currentPage.Clear()
    }
}

foreach ($repositoryId in $repositories.GetEnumerator()) {    
    Write-Information "Deleting pull requests for $repositoryId"
    FindAndDeleteBuilds $repositoryId
}

