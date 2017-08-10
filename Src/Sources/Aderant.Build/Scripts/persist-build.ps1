param([switch]$undo) # this switch can be used to reset the build retain flags and quality as well as remove the label and tags from the repositories


#setup logging
$ErrorActionPreference="SilentlyContinue"
Stop-Transcript | out-null
$ErrorActionPreference = 'Stop'

# create C:\temp folder if it does not exist
$tempFolderPath = "C:\temp"
if (!(Test-Path $tempFolderPath)) {
    New-Item -ItemType Directory -Path $tempFolderPath
}

$logFilePath = Join-Path $tempFolderPath persist-build_log.txt
Start-Transcript -path $logFilePath -append


# read build info file to set input variables
$commitInfoFilePath = Join-Path $PSScriptRoot .\CommitInfo.json
$commitInfoJson = Get-Content $commitInfoFilePath -Raw
$commitInfo = ConvertFrom-Json -InputObject $commitInfoJson

$tfvcChangeSet = $commitInfo.Tfvc.ChangeSet
$tfvcBranchName = $commitInfo.Tfvc.Branch.Replace('\','/')
$teamProject = $commitInfo.Tfvc.TeamProject
$buildDefinition = $commitInfo.Tfvc.BuildId.Split('_')[0]
$buildId = $commitInfo.Tfvc.BuildId

$tfsUrl = "http://tfs:8080/tfs/aderant"

$retainIndefinitely = -not $undo
$keepForeverValue = $retainIndefinitely.ToString().ToLowerInvariant()


# get tag/label name and comments from user input
if ($undo) {
    Write-Host "`nPlease provide the label name for this undo action, i.e. revert retaining of builds & delete labels/tags: " -ForegroundColor Green
} else {
    Write-Host "`nPlease provide a name that will be used for the TFS label and the Git tags, e.g. 81SP1 or 81SP1Patch1234: " -ForegroundColor Green
}
$labelName = Read-Host

# git tags cannot have spaces
if (!$labelName -or $labelName.Contains(' ')) {
    throw "Invalid label name. It cannot be empty or contain spaces."
}

# provide a comment for the label/tags
if (-not $undo) {
    Write-Host "`nProvide a comment that will be used for the TFS label and the Git tags: " -ForegroundColor Green
    $labelComment = Read-Host
}

# we always want a comment
if (!$labelComment -and -not $undo) {
    throw "You need to provide a comment."
}

if ($undo) {
	$labelSummary = "'$labelName'"
} else {
	$labelSummary = "'$labelName' (with comment '$labelComment')"
}


# print summary of what is about to happen next
if ($undo) {
    Write-Host "`nActions (PLEASE READ CAREFULLY!):`n * TFS`n   - REVERT retaining of build $buildId`n   - Set this build quality BACK TO unassigned`n   - DELETE label $labelSummary for TFVC changeset number $tfvcChangeSet" -ForegroundColor Yellow
} else {
    Write-Host "`nActions (PLEASE READ CAREFULLY!):`n * TFS`n   - Retain build $buildId indefinitely`n   - Set this build quality to 'Released'`n   - Apply label $labelSummary to TFVC changeset number $tfvcChangeSet" -ForegroundColor Yellow
}

if ($commitInfo.Git.Length -gt 0) {
    Write-Host " * GIT" -ForegroundColor Yellow
}
ForEach ($gitInfo in $commitInfo.Git) {

    $shortHash = $gitInfo.CommitHash.Substring(0,7)
    $gitRepoName = $gitInfo.Repository.ToUpperInvariant()
    $gitBranch = $gitInfo.Branch
    $gitBuildId = $gitInfo.BuildId

    Write-Host "   - $gitRepoName (branch: $gitBranch)" -ForegroundColor Yellow

    if ($undo) {
        Write-Host "     + REMOVE tag $labelSummary for commit $shortHash" -ForegroundColor Yellow
		Write-Host "     + REMOVE tags 'Released' and '$labelName' for build $gitBuildId" -ForegroundColor Yellow
		Write-Host "     + REVERT retaining of build $gitBuildId" -ForegroundColor Yellow
    } else {
        Write-Host "     + Create tag $labelSummary for commit $shortHash" -ForegroundColor Yellow
		Write-Host "     + Create tags 'Released' and '$labelName' for build $gitBuildId" -ForegroundColor Yellow
		Write-Host "     + Retain build $gitBuildId" -ForegroundColor Yellow
    }
}

Write-Host "`nProceed with dry run (y/n)?" -ForegroundColor Magenta
$dryRunAnswer = Read-Host

if ($dryRunAnswer -ne 'y') {
    Write-Host "Aborting."
    return
}


# go (first a test round, then for real)
$testRoundPassed = $false
Write-Host "`n++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++" -ForegroundColor Yellow
Write-Host "Starting dry run to make sure everything is supposed to pass before actually updating TFS." -ForegroundColor Yellow
Write-Host "++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++" -ForegroundColor Yellow

$liveRun = $false

while ($liveRun -eq $false) {

    if ($testRoundPassed -eq $true) {
        $liveRun = $true
        Write-Host "`n+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++" -ForegroundColor Yellow
        Write-Host "Dry run succeeded. Updating TFS now during live run." -ForegroundColor Yellow
        Write-Host "+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++" -ForegroundColor Yellow

        Write-Host "`nProceed with live run (y/n)?" -ForegroundColor Magenta
        $liveRunAnswer = Read-Host

        if ($liveRunAnswer -ne 'y') {
            Write-Host "Aborting."
            return
        }
    }



    #############################################
    # Retain the BuildAll and mark it as released
    #############################################

    if ($undo) {
        Write-Host "`nRevert retaining of build $buildId and marking it as unassigned ..."
    } else {
        Write-Host "`nRetaining build $buildId indefinitely and marking it as 'Released' ..."
    }
    
    # set the build quality to 'Released' (or reset it if in undo-mode)
    if ($undo) {
        $buildQuality = ""
    } else {
        $buildQuality = "Released"
    }

    #retain this this build indefinitely (or not if in undo-mode)
    $getV1TfvcBuildBody = "{ ""retainIndefinitely"": ""$keepForeverValue"", ""quality"": ""$buildQuality"" }"

    $getV1TfvcBuildUri = "$($tfsUrl)/$($teamProject)/_apis/build/builds/$($buildId)?api-version=1.0"

    #verify the build exists
    $existingV1TfvcBuild = Invoke-RestMethod -Uri $getV1TfvcBuildUri -ContentType "application/json" -UseDefaultCredentials

    # check if we need to do anything
    if ($existingV1TfvcBuild.retainIndefinitely -eq $retainIndefinitely) {
        if ($retainIndefinitely) {
            Write-Host "Build $buildId is already retained. Nothing to do."
        } else {
            Write-Host "Build $buildId is not retained. Nothing to do."
        } 
    }

    # **************************
    # update the V1 build in TFS
    # **************************
    if ($liveRun) {
        try {
            Invoke-RestMethod -Uri $getV1TfvcBuildUri -Body $getV1TfvcBuildBody -ContentType "application/json" -UseDefaultCredentials -Method PATCH
        } catch {
            Write-Host "An error occurred trying to modify the build quality and retain flag of build $buildId. Trying again, otherwise aborting." -ForegroundColor Red
            Invoke-RestMethod -Uri $getV1TfvcBuildUri -Body $getV1TfvcBuildBody -ContentType "application/json" -UseDefaultCredentials -Method PATCH
        }
    } else {
        # DRY RUN ONLY
        Write-Host "Simulating call:" -ForegroundColor Green
        Write-Host "Invoke-RestMethod -Uri $getV1TfvcBuildUri -Body $getV1TfvcBuildBody -ContentType ""application/json"" -UseDefaultCredentials -Method PATCH" -ForegroundColor DarkGreen
    }
    Write-Host "Retain Indefinitely flag set to $retainIndefinitely"



    ###########################################################
    # Apply label to changeset the BuildAll was associated with
    ###########################################################

    Write-Host "`nProcessing label '$labelName' for TFVC changeset $tfvcChangeSet ..."

    # set the label scope
	$labelScope = "$/$teamProject/$tfvcBranchName"

    # check for an existing TFS label to determine if there is something to do
    $getLabelsUri = "$($tfsUrl)/$($teamProject)/_apis/tfvc/labels?api-version=1.0&name=$($labelName)&itemLabelFilter=$($labelScope)"
    $existingLabels = Invoke-RestMethod -Uri $getLabelsUri -ContentType "application/json" -UseDefaultCredentials

    # The machine you use to execute this PowerShell must have Team Explorer installed so that we can load the needed assemblies.
    # The [void] stops the GAC messages from being output to screen.
    Write-Verbose "Loading assemblies"
    [void][System.Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
    [void][System.Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")
    [void][System.Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.VersionControl.Client.dll")

    # Connect to TFS
    $teamProjectCollection = [Microsoft.TeamFoundation.Client.TfsTeamProjectCollectionFactory]::GetTeamProjectCollection($tfsUrl)

    # Get the version control service
    $versionControlServer = $teamProjectCollection.GetService([Microsoft.TeamFoundation.VersionControl.Client.VersionControlServer])

    if ($undo) {

        if ($existingLabels.count -eq 0) {
            Write-Host "Label $labelName did not exist. Nothing to do."
        } else {

            if ($existingLabels.count -gt 1) {
                $numberOfIdenticalLabels = $existingLabels.count

                Write-Host "There are $numberOfIdenticalLabels labels named $labelName under scope $labelScope. Only the first found label will be deleted." -ForegroundColor Magenta
                Write-Host "`nDo you want to proceed (y/n)?" -ForegroundColor Magenta
                $deleteOnlyFirstLabelAnswer = Read-Host

                if ($deleteOnlyFirstLabelAnswer -ne 'y') {
                    Write-Host "Aborting."
                    return
                }
            }

            # ***********************
            # delete the label in TFS
            # ***********************
            if ($liveRun) {
                try {
                    $versionControlServer.DeleteLabel($labelName, $labelScope)
                } catch {
                    Write-Host "An error occurred trying to remove label '$labelName' from TFVC changeset $tfvcChangeSet. Trying again, otherwise aborting." -ForegroundColor Red
                    $versionControlServer.DeleteLabel($labelName, $labelScope)
                }
            } else {
                # DRY RUN ONLY
                Write-Host "Simulating call:" -ForegroundColor Green
                Write-Host "`$versionControlServer.DeleteLabel($labelName, $labelScope)`n" -ForegroundColor DarkGreen
            }
        }

    } else {
    
        if ($existingLabels.count -gt 0) {
            Write-Host "Label $labelName already exists. Nothing to do."
        } else {

            # !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            # unable to create a TFS label with the current REST API version
            # !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            # create a new TFS label object
            $labelVersion = [Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]::ParseSingleSpec($tfvcChangeSet, $null)
            $labelRecursionType = [Microsoft.TeamFoundation.VersionControl.Client.RecursionType]::Full
            $versionControlLabel = New-Object Microsoft.TeamFoundation.VersionControl.Client.VersionControlLabel -ArgumentList $versionControlServer, "$labelName", $versionControlServer.AuthorizedUser, "$labelScope", "$labelComment"
            $itemSpec = New-Object Microsoft.TeamFoundation.VersionControl.Client.ItemSpec -ArgumentList "$labelScope", $labelRecursionType
            $labelItemSpec = @(New-Object Microsoft.TeamFoundation.VersionControl.Client.LabelItemSpec -ArgumentList $itemSpec, $labelVersion, $false)

            # **********************
            # apply the label in TFS
            # **********************
            if ($liveRun) {
                try {
                    $versionControlServer.CreateLabel($versionControlLabel, $labelItemSpec, [Microsoft.TeamFoundation.VersionControl.Client.LabelChildOption]::Merge)
                } catch {
                    Write-Host "An error occurred trying to apply label '$labelName' to TFVC changeset $tfvcChangeSet. Trying again, otherwise aborting." -ForegroundColor Red
                    $versionControlServer.CreateLabel($versionControlLabel, $labelItemSpec, [Microsoft.TeamFoundation.VersionControl.Client.LabelChildOption]::Merge)
                }
            } else {
                # DRY RUN ONLY
                Write-Host "Simulating call:" -ForegroundColor Green
                Write-Host "`$versionControlServer.CreateLabel(`$versionControlLabel, `$labelItemSpec, [Microsoft.TeamFoundation.VersionControl.Client.LabelChildOption]::Merge)" -ForegroundColor DarkGreen
            }
        }

    }



    # now to the git repos
    Write-Host "`nRetrieving git repositories`n"

    # make sure we never tag the following git repos
    $exclude = 'Training', 'TrainingTest', 'SoftwareFactory-DONT_USE', 'Database2-DONT_USE', 'Environments'

    # get them all
    $availableGitRepositories = ((Invoke-WebRequest "$($tfsUrl)/$($teamProject)/_apis/git/repositories" -UseDefaultCredentials).Content | ConvertFrom-Json)[0].value

    # create temp folder for the git action
    $tempPath = "C:\temp\gitTag"
    if (!(Test-Path $tempPath)) {
        New-Item -ItemType Directory -Path $tempPath
    }



    ##################################################################################################################
    # Create/delete tag for Git commits that were used to create the packages and retain/dismiss the associated builds
    ##################################################################################################################
    ForEach ($gitInfo in $commitInfo.Git) {
        $fullHash = $gitInfo.CommitHash
        $shortHash = $gitInfo.CommitHash.Substring(0,7)
        $gitRepoName = $gitInfo.Repository
        $gitBranch = $gitInfo.Branch
        $gitBuildId = $gitInfo.BuildId

        # don't worry about the black-listed repos
        if ($exclude -contains $gitRepoName) {
            Write-Host "Ignoring Git repository $gitRepoName`n" -ForegroundColor Gray
            continue
        }

        Write-Host "$gitRepoName ($gitBranch):" -ForegroundColor Yellow
        if ($undo) {
            Write-Host "Removing tag '$labelName' for commit $shortHash"
        } else {
            Write-Host "Creating tag '$labelName' for commit $shortHash"
        }

        # get current Git repository object
        $gitRepo = $availableGitRepositories | Where-Object { $_.name -eq $gitRepoName }

        # find out if the tag exists already
        $getGitTagsUri = "$($tfsUrl)/$($teamProject)/_apis/git/repositories/$($gitRepo.id)/refs/tags?api-version=2.0"
        $existingGitTags = Invoke-RestMethod -Uri $getGitTagsUri -ContentType "application/json" -UseDefaultCredentials
        $existingGitTagToModify = $existingGitTags.value | Where { $_.name -eq "refs/tags/$labelName" }

        if ($undo) {

            if (!$existingGitTagToModify) {
                Write-Host "Git label $labelName does not exist. Nothing to do."
            } else {
                
                # delete the tag
                $deleteGitTagUri = "$($tfsUrl)/$($teamProject)/_apis/git/repositories/$($gitRepo.id)/refs?api-version=2.0"
                $deleteGitTagUriBody = "[ { ""name"": ""refs/tags/$labelName"", ""oldObjectId"": ""$fullHash"" , ""newObjectId"": ""0000000000000000000000000000000000000000"" } ]"

                # *************************************
                # delete the tag in the remote Git repo
                # *************************************
                if ($liveRun) {
                    try {
                        Invoke-RestMethod -Uri $deleteGitTagUri -Body $deleteGitTagUriBody -ContentType "application/json" -UseDefaultCredentials -Method POST
                    } catch {
                        Write-Host "An error occurred trying to remove the Git tag '$labelName' for commit $shortHash. Trying again, otherwise aborting." -ForegroundColor Red
                        Invoke-RestMethod -Uri $deleteGitTagUri -Body $deleteGitTagUriBody -ContentType "application/json" -UseDefaultCredentials -Method POST
                    }
                } else {
                    # DRY RUN ONLY
                    Write-Host "Simulating call:" -ForegroundColor Green
                    Write-Host "Invoke-RestMethod -Uri $deleteGitTagUri -Body $deleteGitTagUriBody -ContentType ""application/json"" -UseDefaultCredentials -Method POST" -ForegroundColor DarkGreen
                }

            }

        } else {

            if ($existingGitTagToModify) {
                Write-Host "Git label $labelName already exists. Nothing to do."
            } else {

                # !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                # this does not work yet with the current REST API version
                # !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                if (!$existingGitTagToModify -and $isApiVersion4Available) {

                    # create the tag
                    $createGitTagUri = "$($tfsUrl)/$($teamProject)/_apis/git/repositories/$($gitRepo.id)/annotatedTags?api-version=4.0"
                    $createGitTagUriBody = "{ ""name"": ""$labelName"", ""taggedObject"": { ""objectId"": ""$fullHash"" }, ""message"": ""$labelComment"" }"

                    # *************************************
                    # create the tag in the remote Git repo
                    # *************************************
                    if ($liveRun) {
                        try {
                            Invoke-RestMethod -Uri $createGitTagUri -Body $createGitTagUriBody -ContentType "application/json" -UseDefaultCredentials -Method POST
                        } catch {
                            Write-Host "An error occurred trying to create the Git tag '$labelName' for commit $shortHash. Trying again, otherwise aborting." -ForegroundColor Red
                            Invoke-RestMethod -Uri $createGitTagUri -Body $createGitTagUriBody -ContentType "application/json" -UseDefaultCredentials -Method POST
                        }
                    } else {
                        # DRY RUN ONLY
                        Write-Host "Simulating call:" -ForegroundColor Green
                        Write-Host "Invoke-RestMethod -Uri $createGitTagUri -Body $createGitTagUriBody -ContentType ""application/json"" -UseDefaultCredentials -Method POST" -ForegroundColor DarkGreen
                    }
                }


                $repoPath = Join-Path $tempPath $gitRepoName

                $isFreshRepository = $false

                if (!(Test-Path $repoPath)) {
            
                    # initially clone the git repo into a temporary folder (this has to be done only once)

                    Write-Host "Cloning Git repo $gitRepoName into $repoPath ..."
                    New-Item -ItemType Directory -Path $repoPath | Out-Null
            
                    $ErrorActionPreference = 'SilentlyContinue'
                    git clone $gitRepo.remoteUrl $repoPath
                    $ErrorActionPreference = 'Stop'

                    $isFreshRepository = $true
                }

                cd $repoPath

                # make sure the local repo looks the same as the remote repo
                if (!$isFreshRepository) {
                    git fetch origin
                    git reset --hard origin/master
                    git tag -d $(git tag)

                    $ErrorActionPreference = 'SilentlyContinue'
                    git pull | Out-Null
                    $ErrorActionPreference = 'Stop'
                }

                # create the tag locally
                git tag -a $labelName $shortHash -m "$labelComment" -f

                # *************************************
                # create the tag in the remote Git repo
                # *************************************
                if ($liveRun) {
                    $ErrorActionPreference = 'SilentlyContinue'
                    git push --tag
                    $ErrorActionPreference = 'Stop'
                } else {
                    # DRY RUN ONLY
                    Write-Host "Simulating call:" -ForegroundColor Green
                    Write-Host "git push --tag" -ForegroundColor DarkGreen
                }

            }

        }

        if ($undo) {
            Write-Host "`nRevert retaining of Git build $gitBuildId"
        } else {
            Write-Host "`nRetaining Git build $gitBuildId"
        }

        $getV2GitBuildUri = "$($tfsUrl)/$($teamProject)/_apis/build/builds/$($gitBuildId)?api-version=2.0"

        #verify the build exists
        $existingV2GitBuild = Invoke-RestMethod -Uri $getV2GitBuildUri -ContentType "application/json" -UseDefaultCredentials

        # check if we need to do anything
        if ($existingV2GitBuild.keepForever -eq $retainIndefinitely) {
            if ($retainIndefinitely) {
                Write-Host "Build $gitBuildId is already retained. Nothing to do."
            } else {
                Write-Host "Build $gitBuildId is not retained. Nothing to do."
            } 
        }

        #retain this this build indefinitely (or not if in undo-mode)
        $getV2GitBuildBody = "{ ""keepForever"": $keepForeverValue }"

        # ******************************************
        # update the Git build (retain indefinitely)
        # ******************************************
        if ($liveRun) {
            try {
                Invoke-RestMethod -Uri $getV2GitBuildUri -Body $getV2GitBuildBody -ContentType "application/json" -UseDefaultCredentials -Method PATCH
            } catch {
                Write-Host "An error occurred trying to retain Git build $gitBuildId. Trying again, otherwise aborting." -ForegroundColor Red
                Invoke-RestMethod -Uri $getV2GitBuildUri -Body $getV2GitBuildBody -ContentType "application/json" -UseDefaultCredentials -Method PATCH
            }
        } else {
            # DRY RUN ONLY
            Write-Host "Simulating call:" -ForegroundColor Green
            Write-Host "Invoke-RestMethod -Uri $getV2GitBuildUri -Body $getV2GitBuildBody -ContentType ""application/json"" -UseDefaultCredentials -Method PATCH" -ForegroundColor DarkGreen
        }
        Write-Host "Retain Indefinitely flag set to $retainIndefinitely"


        $applyReleasedTagToV2GitBuildUri = "$($tfsUrl)/$($teamProject)/_apis/build/builds/$($gitBuildId)/tags/Released?api-version=2.0"
        $applyVersionTagToV2GitBuildUri = "$($tfsUrl)/$($teamProject)/_apis/build/builds/$($gitBuildId)/tags/$($labelName)?api-version=2.0"

		if ($undo) {
			$updateTagMethod = "DELETE"
		} else {
			$updateTagMethod = "PUT"
		}

        # *********************************************
        # update the Git build in TFS (apply some tags)
        # *********************************************
        if ($liveRun) {
            try {
                Invoke-RestMethod -Uri $applyReleasedTagToV2GitBuildUri -UseDefaultCredentials -Method $updateTagMethod
                Invoke-RestMethod -Uri $applyVersionTagToV2GitBuildUri -UseDefaultCredentials -Method $updateTagMethod
            } catch {
                Write-Host "An error occurred trying to apply tags to Git build $gitBuildId. Trying again, otherwise aborting." -ForegroundColor Red
                Invoke-RestMethod -Uri $applyReleasedTagToV2GitBuildUri -UseDefaultCredentials -Method $updateTagMethod
                Invoke-RestMethod -Uri $applyVersionTagToV2GitBuildUri -UseDefaultCredentials -Method $updateTagMethod
            }
        } else {
            # DRY RUN ONLY
            Write-Host "Simulating call:" -ForegroundColor Green
            Write-Host "Invoke-RestMethod -Uri $applyReleasedTagToV2GitBuildUri -UseDefaultCredentials -Method $updateTagMethod" -ForegroundColor DarkGreen
            Write-Host "Invoke-RestMethod -Uri $applyVersionTagToV2GitBuildUri -UseDefaultCredentials -Method $updateTagMethod" -ForegroundColor DarkGreen
        }
        Write-Host "Successfully updated tags"

    }

    # if nothing blew up so far, there is a very good chance this will work for real - so do it again and this time alter the TFS (fingers crossed!)
    $testRoundPassed = $true
}

Write-Host "`n`nDONE" -ForegroundColor Yellow
Stop-Transcript