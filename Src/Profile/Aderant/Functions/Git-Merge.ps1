<#
.Synopsis
    Starts a merge workflow for all Pull Requests linked to a specific work item.
.Description   
    Takes the bug work item ID that needs to be merged, the merge work item ID that the merge PR should be attached with and the target Git branch name as input.
    It then tries to cherry pick each linked Pull Request of the bug work item ID to a new feature branch off the given target branch, commit it and create a PR.
    If a merge conflict occurs, Visual Studio (Experimental Instance, for performance reasons) opens up automatically for manual conflict resolving.
    After successfully resolving the merge conflict, just close Visual Studio and run this command again. It will remember your last inputs for convenience.
    Once a Pull Request for the merge operation is created, Internet Explorer will open up automatically and show the created PR which is set to auto-complete. 
    Additionally, it will automatically do a squash merge and delete the feature branch.
    The CRTDev group will be associated automatically as an optional reviewer which can be changed manually.
#>
function Git-Merge {
    # setup logging
    $ErrorActionPreference = "SilentlyContinue"
    Stop-Transcript | out-null
    $ErrorActionPreference = "Stop"

    # create C:\temp folder if it does not exist
    $tempFolderPath = "C:\temp"
    if (!(Test-Path $tempFolderPath)) {
        New-Item -ItemType Directory -Path $tempFolderPath
    }

    $logFilePath = Join-Path $tempFolderPath git-merge_log.txt
    Start-Transcript -path $logFilePath -append

    # variables
    $tfsUrl = "http://tfs:8080/tfs/aderant"
    $tfsUrlWithProject = "http://tfs:8080/tfs/aderant/ExpertSuite"
    $bugId = $null
    $mergeBugId = $null
    $targetBranch = $null
    $tempFolderPath = "C:\temp\gitMerge"
    $gitError = ""
    $needInput = $true

    # close all open Internet Explorer instances that might have been opened as a COM object
    # we close all instances, even those that were opened manually - assuming nobody uses that browser any more :-)
    (New-Object -COM 'Shell.Application').Windows() | Where-Object {
        $_.Name -like '*Internet Explorer*'
    } | ForEach-Object {
        $_.Quit()
    }

    # create temporary working folder if it does not exist
    if (!(Test-Path $tempFolderPath)) {
        Write-Host "`nCreating directory $tempFolderPath" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $tempFolderPath | Out-Null
    }

    # create application settings to save application state (like the previous input so you don't have to provide it again after a conflict or other failure)
    $appInfoFilePath = Join-Path $tempFolderPath ".app-info"
    if (!(Test-Path $appInfoFilePath)) {
        Write-Host "Creating application info file" -ForegroundColor Gray
        New-Item $appInfoFilePath -ItemType File | Out-Null
    }

    $autoCreateMergeBug = $false

    # grab previous input automatically on request
    $appInfo = Get-Content $appInfoFilePath
    if ($appInfo) {
        $inputs = $appInfo.Split(',')
        Write-Host "The previous inputs were"
        Write-Host " * Bug ID:        $($inputs[0])"
        Write-Host " * Merge Bug ID:  $($inputs[1])"
        Write-Host " * Target branch: $($inputs[2])"

        Write-Host "`nDo you want to use these inputs (y/n)?" -ForegroundColor Magenta
        $useInputsAnswer = Read-Host

        if ($useInputsAnswer -eq 'y') {
            $bugId = $inputs[0]
            $mergeBugId = $inputs[1]
            $targetBranch = $inputs[2]
            $needInput = $false

            if ($mergeBugId -eq 'c' -or $mergeBugId -eq "'c'") {
                $autoCreateMergeBug = $true
            }
        }
    }

    # grab input manually
    if ($needInput) {
        while (!$bugId -or $bugId.Length -le 5) {
            if ($bugId -and $bugId.Length -le 5) {
                Write-Host "$bugId is not a valid work item ID" -ForegroundColor Red
            }
            $bugId = Read-Host -Prompt "`nWhich bug ID to you want to merge"
        }
    
        while (!$mergeBugId -or $mergeBugId.Length -le 5 -or $mergeBugId -eq $bugId) {
            $mergeBugId = Read-Host -Prompt "`nWhich merge bug ID to you want the merge operation to be associated with (enter 'c' to automatically create one)"
            if ($mergeBugId -eq 'c' -or $mergeBugId -eq "'c'") {
                $autoCreateMergeBug = $true
                break
            }
            if ($mergeBugId -and $mergeBugId.Length -le 5) {
                Write-Host "$mergeBugId is not a valid work item ID" -ForegroundColor Red
            }
            if ($mergeBugId -eq $bugId) {
                Write-Host "You cannot use the original work item to be associated with the merge operation" -ForegroundColor Red
            }
        }

        while (!$targetBranch -or $targetBranch.Length -le 1) {
            $targetBranch = Read-Host -Prompt "`nWhich Git branch to you want to merge to (e.g. master, patch/81SP1 etc. - CASE SENSTITIVE)"
        }

        Set-Content $appInfoFilePath "$([System.String]::Join(",", @($bugId, $mergeBugId, $targetBranch)))" -Force
    }

    # get the bug work item from TFS
    $getWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($bugId)?`$expand=all&api-version=1.0"
    Write-Host "Invoke-RestMethod -Uri $getWorkItemUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
    $workItem = Invoke-RestMethod -Uri $getWorkItemUri -ContentType "application/json" -UseDefaultCredentials

    # create new IE browser object to show merge PRs (optionally the newly created merge bug)
    $browser = New-Object -ComObject internetexplorer.application

    #retrieve or auto-create the merge work item
    if ($autoCreateMergeBug -eq $true) {

        $assumedIterationPath = "ExpertSuite"
        switch ($targetBranch) {
            'master' {
                $assumedIterationPath += "\\8.2.0.0"
            }
            'patch/81SP1' {
                $assumedIterationPath += "\\8.1.0.2 (HF)"
            }
            'releases/10.8102' {
                $assumedIterationPath += "\\8.1.1 (SP)"
            }    
        }

        # automatically create the merge work item in TFS
        $createWorkItemUri = "$($tfsUrlWithProject)/_apis/wit/workItems/`$Bug?api-version=1.0"
        $createWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.Title",
    "value": "MERGE: $($workItem.fields.'System.Title'.Replace('"', '\"'))"
  },
  {
    "op": "add",
    "path": "/fields/Microsoft.VSTS.TCM.ReproSteps",
    "value": "Merge work item $($workItem.id) into $targetBranch (and the respective TFS branch, if applicable)."
  },
  {
    "op": "add",
    "path": "/fields/System.History",
    "value": "Automatically created via Git-Merge."
  },
  {
    "op": "add",
    "path": "/fields/System.AreaPath",
    "value": "$($workitem.fields.'System.AreaPath'.Replace('\', '\\'))"
  },
  {
    "op": "add",
    "path": "/fields/System.IterationPath",
    "value": "$assumedIterationPath"
  },
  {
    "op": "add",
    "path": "/relations/-",
    "value": {
      "rel": "System.LinkTypes.Hierarchy-Reverse",
      "url": "$($tfsUrl)/_apis/wit/workItems/$($bugId)",
      "attributes": {
        "comment": "Original bug"
      }
    }
  }
]
"@
        Write-Host "Invoke-RestMethod -Uri $createWorkItemUri -Body $createWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
        $createdMergeWorkItem = Invoke-RestMethod -Uri $createWorkItemUri -Body $createWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch
        $mergeBugId = $createdMergeWorkItem.id

        # assign the merge work item to the creator and set it to Active
        $updateWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($createdMergeWorkItem.id)?api-version=1.0"
        $updateWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.AssignedTo",
    "value": "$($createdMergeWorkItem.fields.'System.CreatedBy'.Replace('\', '\\'))"
  },
  {
    "op": "replace",
    "path": "/fields/System.State",
    "value": "Active"
  }
]
"@
        Write-Host "Invoke-RestMethod -Uri $updateWorkItemUri -Body $updateWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
        $updatedMergeWorkItem = Invoke-RestMethod -Uri $updateWorkItemUri -Body $updateWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

        Write-Host "`nAutomatically created merge work item $mergeBugId. Please verify assignee, area & iteration path.`n" -ForegroundColor Yellow
        Read-Host -Prompt "A new IE browser window will now open to load the work item for editing. Press any key to continue"
        $workItemUrl = "$($tfsUrlWithProject)/_workitems?id=$mergeBugId"
        $browser.navigate($workItemUrl)
        $browser.visible = $true
    }

    # get existing merge work item from TFS
    $getMergeWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($mergeBugId)?`$expand=all&api-version=1.0"
    Write-Host "Invoke-RestMethod -Uri $getMergeWorkItemUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
    $mergeWorkItem = Invoke-RestMethod -Uri $getMergeWorkItemUri -ContentType "application/json" -UseDefaultCredentials

    # gather all PRs from the bug work item that need to be merged
    $repositoriesToProcess = @{}
    foreach ($relation in $workItem.relations | Where-Object { $_.rel -eq "ArtifactLink" -and $_.attributes.name -eq "Pull Request" }) {
        $pullRequestUri = $relation.url
        $pullRequestPath = @($pullRequestUri.Split('/'))[5].Replace("%2f", "/").Replace("%2F", "/")
        $pullRequestPathParts = @($pullRequestPath.Split('/'))
    
        $repositoryId = $pullRequestPathParts[1]
        $pullRequestId = $pullRequestPathParts[2]
        $getPullRequestUri = "$($tfsUrl)/_apis/git/repositories/$repositoryId/pullrequests/$pullRequestId"

        # get the pull request object
        Write-Host "Invoke-RestMethod -Uri $getPullRequestUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
        $pullRequest = Invoke-RestMethod -Uri $getPullRequestUri -ContentType "application/json" -UseDefaultCredentials
        if ($pullRequest.status -ne 'completed') {
            continue
        }
        $repositoryFromPR = $pullRequest.repository

        # add the containing repository to the dictionary if it hasn't already been added
        if (-not $repositoriesToProcess.Contains($repositoryFromPR.name)) {
            $pullRequests = New-Object System.Collections.ArrayList
            $pullRequests.Add($pullRequest) | Out-Null
            $repositoriesToProcess.Add($repositoryFromPR.name, $pullRequests)
        } else {
            $repositoriesToProcess[$repositoryFromPR.name].Add($pullRequest)
        }
    }

    # write a summary of what is gonna happen
    Write-Host "`nMerging bug $($workItem.id) into branch $($targetBranch)" -ForegroundColor Green
    Write-Host "`-> $($workItem.fields.'System.Title')`n" -ForegroundColor Green
    Write-Host "Merge bug: $($mergeWorkItem.id) - $($mergeWorkItem.fields.'System.Title')"
    foreach ($currentRepositoryName in $repositoriesToProcess.Keys) {
        $repository = $repositoriesToProcess[$currentRepositoryName]
        Write-Host "`n$currentRepositoryName PRs:"
        foreach ($pullRequest in $repository) {
            Write-Host " * $($pullRequest.targetRefName.Substring(11)) - PR $($pullRequest.pullRequestId) - $($pullRequest.title)"
        }
    }
    $hasChangesets = $false
    $changeSetInfos = ""
    $allTfvcBranchPaths = [System.Collections.ArrayList]@()
    foreach ($relation in $workItem.relations | Where-Object { $_.rel -eq "ArtifactLink" -and $_.attributes.name -eq "Fixed in Changeset" }) {
        if (-not $hasChangesets) {
            $hasChangesets = $true
            Write-Host "`nTFVC commits (need to be merged manually):" -ForegroundColor Yellow
        }
        $splitUrl = $relation.url.Split('/')
        $changeSetId = $splitUrl[$splitUrl.Count - 1]
        $getChangesetUri = "$($tfsUrl)/_apis/tfvc/changesets/$($changeSetId)?includeDetails=true&api-version=1.0"

        #get the work items from TFS
        $changeSet = Invoke-RestMethod -Uri $getChangesetUri -ContentType "application/json" -UseDefaultCredentials
        $getChangesUri = $changeSet._links.changes.href
        $changes = Invoke-RestMethod -Uri $getChangesUri -ContentType "application/json" -UseDefaultCredentials
        $tfvcBranchPaths = [System.Collections.ArrayList]@()
        foreach ($change in $changes.value) {
            $path = ""
            $pathParts = $change.item.path.Split('/')
            foreach ($part in $pathParts) {
                if ($part -eq "$" -or $part -eq "ExpertSuite") {
                    continue
                }
                if ($part -eq "Modules") {
                    break
                }
                $path += "$part/"
            }
            $path = $path.Substring(0, $path.Length - 1)
            if (-not $tfvcBranchPaths.Contains($path)) {
                $tfvcBranchPaths.Add($path) | Out-Null
            }
            if (-not $allTfvcBranchPaths.Contains($path)) {
                $allTfvcBranchPaths.Add($path) | Out-Null
            }
        }
        $changeSetInfo = " * $tfvcBranchPaths - CS $changeSetId - $($relation.attributes.comment)"
        $changeSetInfos += "$changeSetInfo`n"
        Write-Host $changeSetInfo
    }

    foreach ($branch in $allTfvcBranchPaths) {
        Write-Host "`nCreate an additional merge work item for merging the TFVC changesets from $branch to its parent branch (y/n)?" -ForegroundColor Magenta
        $additionalMergeBugAnswer = Read-Host
        if ($additionalMergeBugAnswer -eq 'y') {

            $getBranchUri = "$($tfsUrl)/_apis/tfvc/branches/`$/ExpertSuite/$branch/Modules?includeParent=true&api-version=1.0-preview.1"
            Write-Host "Invoke-RestMethod -Uri $getBranchUri -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
            $branchInfo = Invoke-RestMethod -Uri $getBranchUri -ContentType "application/json" -UseDefaultCredentials
            $parentBranch = $branchInfo.parent[0].path.Replace("$/ExpertSuite/", "").Replace("/Modules", "")

            $assumedIterationPath = "ExpertSuite"
            switch ($parentBranch) {
                'Releases/811x' {
                    $assumedIterationPath += "\\8.1.1 (SP)"
                }
                'Releases/81x' {
                    $assumedIterationPath += "\\8.1.0"
                }
                'Releases/803x' {
                    $assumedIterationPath += "\\8.0.3 (SP)"
                }
                'Releases/802x' {
                    $assumedIterationPath += "\\8.0.2 (SP)"
                }
                'Releases/801x' {
                    $assumedIterationPath += "\\8.0.1(SP)" # this is not a typo, this iteration path is indeed missing a space...
                }
            }

            # automatically create the merge work item in TFS
            $createAdditionalWorkItemUri = "$($tfsUrlWithProject)/_apis/wit/workItems/`$Bug?api-version=1.0"
            $createAdditionalWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.Title",
    "value": "MERGE: $($workItem.fields.'System.Title'.Replace('"', '\"'))"
  },
  {
    "op": "add",
    "path": "/fields/Microsoft.VSTS.TCM.ReproSteps",
    "value": "Merge work item $($workItem.id) into TFVC branch $parentBranch."
  },
  {
    "op": "add",
    "path": "/fields/System.History",
    "value": "Automatically created via Git-Merge."
  },
  {
    "op": "add",
    "path": "/fields/System.AreaPath",
    "value": "$($workitem.fields.'System.AreaPath'.Replace('\', '\\'))"
  },
  {
    "op": "add",
    "path": "/fields/System.IterationPath",
    "value": "$assumedIterationPath"
  },
  {
    "op": "add",
    "path": "/relations/-",
    "value": {
      "rel": "System.LinkTypes.Hierarchy-Reverse",
      "url": "$($tfsUrl)/_apis/wit/workItems/$($bugId)",
      "attributes": {
        "comment": "Original bug"
      }
    }
  }
]
"@
            Write-Host "Invoke-RestMethod -Uri $createAdditionalWorkItemUri -Body $createAdditionalWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
            $additionallyCreatedMergeWorkItem = Invoke-RestMethod -Uri $createAdditionalWorkItemUri -Body $createAdditionalWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

            # assign the additional merge work item to the creator
            $updateAdditionalWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($additionallyCreatedMergeWorkItem.id)?api-version=1.0"
            $updateAdditionalWorkItemBody = @"
[
  {
    "op": "add",
    "path": "/fields/System.AssignedTo",
    "value": "$($additionallyCreatedMergeWorkItem.fields.'System.CreatedBy'.Replace('\', '\\'))"
  }
]
"@
            Write-Host "Invoke-RestMethod -Uri $updateAdditionalWorkItemUri -Body $updateAdditionalWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials -Method Patch" -ForegroundColor Blue
            $updatedAdditionalMergeWorkItem = Invoke-RestMethod -Uri $updateAdditionalWorkItemUri -Body $updateAdditionalWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

            Write-Host "`nAutomatically created additional merge work item $($additionallyCreatedMergeWorkItem.id). Please verify assignee, area & iteration path.`n" -ForegroundColor Yellow
            Read-Host -Prompt "A new IE browser window will now open to load the work item for editing. Press any key to continue"
            $workItemUrl = "$($tfsUrlWithProject)/_workitems?id=$($additionallyCreatedMergeWorkItem.id)"
            $browser.navigate($workItemUrl)
            $browser.visible = $true
        }
    }

    Write-Host "`nProceed with merging (y/n)?" -ForegroundColor Magenta
    $proceedAnswer = Read-Host

    if ($proceedAnswer -ne 'y') {
        Write-Host "Aborting."
        return
    }


    # create folder for git repo to clone if it does not exist
    $gitReposPath = Join-Path $tempFolderPath $bugId
    if (!(Test-Path $gitReposPath)) {
        Write-Host "`nCreating directory $gitReposPath" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $gitReposPath | Out-Null
    }

    try {

        foreach ($currentRepositoryName in $repositoriesToProcess.Keys) {

            $repository = $repositoriesToProcess[$currentRepositoryName]
            $repositoryId = $repository[0].repository.id

            Write-Host "`nProcessing $currentRepositoryName" -ForegroundColor Green

            $featureBranch = $targetBranch + "-for-merging-$bugId"

            cd $gitReposPath

            $currentRepositoryPath = Join-Path $gitReposPath $currentRepositoryName

            $isInitialRun = $false
            if (!(Test-Path $currentRepositoryPath)) {
                # clone the repository and checkout a new feature branch
                Write-Host "Cloning repository" -ForegroundColor Gray
                Write-Host "git clone $($repository[0].repository.remoteUrl)" -ForegroundColor Blue
                Invoke-Git clone $repository[0].repository.remoteUrl
                $isInitialRun = $true
                Write-Host "Done" -ForegroundColor Gray
            }

            cd $currentRepositoryPath

            if ($isInitialRun) {
                Write-Host "Creating feature branch" -ForegroundColor Gray
                Write-Host "git checkout -b $featureBranch origin/$targetBranch" -ForegroundColor Blue
                Invoke-Git checkout -b $featureBranch origin/$targetBranch
            }

            $pullRequestDescription = ""

            # cherry-pick the commits of every associated PR in this repository
            foreach ($pullRequest in $repository) {

                $pullRequestDescription += "Merging $($pullRequest.title)`n"

                $conflictInfoFilePath = Join-Path (Join-Path $currentRepositoryPath "..\") "$currentRepositoryName.conflicts"
                $processedInfoFilePath = Join-Path (Join-Path $currentRepositoryPath "..\") "$currentRepositoryName.processed"
                if (!(Test-Path $processedInfoFilePath)) {
                    Write-Host "Creating processed info file" -ForegroundColor Gray
                    New-Item $processedInfoFilePath -ItemType File | Out-Null
                }

                $lastMergeCommitId = $pullRequest.lastMergeCommit.commitId

                $processedInfo = Get-Content $processedInfoFilePath
                if (!$processedInfo -or -not (Get-Content $processedInfoFilePath).Contains($pullRequest.pullRequestId)) {

                    if (!(Test-Path $conflictInfoFilePath) -or -not (Get-Content $conflictInfoFilePath).Contains($pullRequest.pullRequestId)) {
                        Write-Host "Cherry-picking $($lastMergeCommitId.Substring(0,7))" -ForegroundColor Gray
                        Write-Host "git cherry-pick $lastMergeCommitId" -ForegroundColor Blue
                        $gitError = Invoke-Git cherry-pick $lastMergeCommitId
                    }

                    if ($gitError.Contains('is a merge but no -m option was given')) {
                        Write-Host "Cherry-picking $($lastMergeCommitId.Substring(0,7)) with merge option" -ForegroundColor Gray
                        Write-Host "git cherry-pick -m 1 $lastMergeCommitId" -ForegroundColor Blue
                        $gitError = Invoke-Git cherry-pick -m 1 $lastMergeCommitId
                    }

                    if ($gitError.StartsWith('error: could not apply')) {
                        # merge conflict occurred
                        if (!(Test-Path $conflictInfoFilePath)) {
                            Write-Host "Creating conflict info file" -ForegroundColor Gray
                            New-Item $conflictInfoFilePath -ItemType File | Out-Null
                        }
                        Write-Host "Updating conflict info file" -ForegroundColor Gray
                        $conflictInfo = Get-Content $conflictInfoFilePath
                        if (!$conflictInfo -or -not $conflictInfo.Contains($pullRequest.pullRequestId)) {
                            Add-Content $conflictInfoFilePath "$($pullRequest.pullRequestId)" | Out-Null
                        }
                        $solutionFilePath = Join-Path $currentRepositoryPath "$currentRepositoryName.sln"
                        Read-Host -Prompt "A new instance of Visual Studio will now open for manual conflict resolving. Press any key to continue"
                        Write-Host "Opening $solutionFilePath for manual conflict resolving" -ForegroundColor Gray
                        Start-Process devenv -ArgumentList "$solutionFilePath /RootSuffix Exp"
                        throw "Please resolve the merge conflict and run this command again."
                    }

                    Write-Host "git commit -m ""Cherry picked commit of PR $($pullRequest.pullRequestId)"" --allow-empty" -ForegroundColor Blue
                    $gitError = Invoke-Git commit -m """Cherry picked commit of PR $($pullRequest.pullRequestId)""" --allow-empty
        
                    if ($gitError) {
                        Write-Host $gitError
                        $solutionFilePath = Join-Path $currentRepositoryPath "$currentRepositoryName.sln"
                        Read-Host -Prompt "A new instance of Visual Studio will now open for manual conflict resolving. Press any key to continue"
                        Write-Host "Opening $solutionFilePath for manual conflict resolving" -ForegroundColor Gray
                        Start-Process devenv -ArgumentList "$solutionFilePath /RootSuffix Exp"
                        throw "Please resolve the merge conflict and run this command again."
                    }

                    Add-Content $processedInfoFilePath "$($pullRequest.pullRequestId)" | Out-Null
                }
            }

            #publish feature branch
            Write-Host "Pushing changes to feature branch" -ForegroundColor Gray
            Write-Host "git push origin $featureBranch" -ForegroundColor Blue
            $gitError = Invoke-Git push origin $featureBranch   

            if ($gitError.Contains("Everything up-to-date")) {
                Write-Host "No more changes to push to origin for repository $currentRepositoryName, skipping PR creation"
            } else {
                Invoke-Git notes add -m """Merged: $bugId"""
                Invoke-Git push origin refs/notes/commits        
              
                $createPullRequestUri = "$($tfsUrl)/_apis/git/repositories/$repositoryId/pullrequests?api-version=3.0"
                $createPullRequestBody = @"
{
    "sourceRefName": "refs/heads/$featureBranch",
    "targetRefName": "refs/heads/$targetBranch",
    "title": "Merge of $bugId into $targetBranch - $($workItem.fields.'System.Title'.Replace('"', '\"'))",
    "description": "$pullRequestDescription",
    "reviewers": [
    {
        "id": "f9c35c2b-e4c7-4940-a045-04e49a8381cc"
    }
    ]
}
"@

                #create the pull request
                Write-Host "Creating a pull request from $featureBranch to $targetBranch" -ForegroundColor Gray
                Write-Host "Invoke-RestMethod -Uri $createPullRequestUri -Body $createPullRequestBody -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
                $createdPullRequest = Invoke-RestMethod -Uri $createPullRequestUri -Body $createPullRequestBody -ContentType "application/json" -UseDefaultCredentials -Method Post


                $modifyPullRequestUri = "$($tfsUrl)/_apis/git/repositories/$repositoryId/pullrequests/$($createdPullRequest.pullRequestId)?api-version=3.0"
                $modifyPullRequestBody = @"
{
    "completionOptions": {
        "deleteSourceBranch": "true",
        "mergeCommitMessage": "Merge of $bugId into $targetBranch",
        "squashMerge": "true"
    }
}
"@

                #update the pull request (setting auto-complete etc.)
                Write-Host "Setting auto-complete for pull request $($createdPullRequest.pullRequestId)" -ForegroundColor Gray
                Write-Host "Invoke-RestMethod -Uri $modifyPullRequestUri -Body $modifyPullRequestBody -ContentType ""application/json"" -UseDefaultCredentials" -ForegroundColor Blue
                $updatedPullRequest = Invoke-RestMethod -Uri $modifyPullRequestUri -Body $modifyPullRequestBody -ContentType "application/json" -UseDefaultCredentials -Method Patch

                $linkWorkItemUri = "$($tfsUrl)/_apis/wit/workItems/$($mergeBugId)?api-version=3.0"
                $linkWorkItemBody = @"
[
    {
    "op": 0,
    "path": "/relations/-",
    "value": {
        "attributes": {
        "name": "Pull Request"
        },
        "rel": "ArtifactLink",
        "url": "$($updatedPullRequest.artifactId)"
    }
    }
]
"@

                #update the merge bug id by linking the newly created PR to it
                Write-Host "Linking for pull request $($createdPullRequest.pullRequestId) to work item $mergeBugId" -ForegroundColor Gray
                Write-Host "Invoke-RestMethod -Uri $linkWorkItemUri -Body $linkWorkItemBody -ContentType ""application/json-patch+json"" -UseDefaultCredentials" -ForegroundColor Blue
                $updatedWorkItem = Invoke-RestMethod -Uri $linkWorkItemUri -Body $linkWorkItemBody -ContentType "application/json-patch+json" -UseDefaultCredentials -Method Patch

                Read-Host -Prompt "Successfully created pull request. A new IE browser window will now open to review the PR and edit optional reviewers. Press any key to continue"
                if ($browser.visible -eq $false) {
                    $browser.navigate($updatedPullRequest.repository.remoteUrl + "/pullrequest/" + $createdPullRequest.pullRequestId)
                    $browser.visible = $true
                } else {
                    $browser.navigate2($updatedPullRequest.repository.remoteUrl + "/pullrequest/" + $createdPullRequest.pullRequestId, "", "_blank")
                }
            }
        }
    } finally {
    }

    Write-Host "`nSuccessfully merged all Pull Requests linked to work item $bugId."
    if ($changeSetInfos.Length -gt 0) {
        Write-Host "`nPlease remember to manually merge the following linked TFVC changesets:" -ForegroundColor Yellow
        Write-Host $changeSetInfos
    }

    try {
        # clean up (take into account symlinks hence don't use Remove-Item as it would delete everything it finds in the symlink folders as well)
        cd C:\Temp\gitMerge
        (cmd /c del /f /s /q $gitReposPath) | Out-Null
        (cmd /c rmdir /s /q $gitReposPath) | Out-Null
        while (Test-Path $gitReposPath) {
            (cmd /c rmdir /s /q $gitReposPath) | Out-Null # do it again as rmdir deletes the directories one by one
        }
    } catch {
        [System.Exception]           
        Write-Host $_.Exception.ToString()
        Write-Host "Could not automatically delete $gitReposPath. You need to clean it up manually." -ForegroundColor Red
    }

    Read-Host -Prompt "Press any key to end this script"

    Write-Host "`n`nDONE" -ForegroundColor Yellow
    Stop-Transcript
}