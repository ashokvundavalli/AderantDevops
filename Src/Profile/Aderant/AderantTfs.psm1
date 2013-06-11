
function PromptUserToShelveBranch($branchName, $tfsPath, $shelvesetName){

    $message = "What do you want to do?";
    $shelve = new-Object System.Management.Automation.Host.ChoiceDescription "&Shelve (Recommended)","Shelve your current progress and revert workspace (Recommended)";
    $continue = new-Object System.Management.Automation.Host.ChoiceDescription "&Continue","Continue anyway (Risky)";
    $cancel = new-Object System.Management.Automation.Host.ChoiceDescription "Ca&ncel","Cancel the merge";
    $view = new-Object System.Management.Automation.Host.ChoiceDescription "&View","View the list of local changes";
    $choices = [System.Management.Automation.Host.ChoiceDescription[]]($shelve, $continue, $cancel, $view);
    $answer = $host.ui.PromptForChoice($caption,$message,$choices,0)
    switch ($answer){
        0 {
            Write-Host "Shelving changes..."; 
            $dateString = Get-Date
            $dateString = $dateString.ToString().Replace(":", ".")
            $shelvesetName = $shelvesetName.Replace("\", "_")
            $result = Invoke-Expression "tf shelve ""$shelvesetName"" $tfsPath /noprompt /move /recursive"
            if($LASTEXITCODE -ne 0){
                throw "Could not shelve changes in $branchName"
            }
            Write-Host "Shelveset saved as $shelvesetName"; 
            Write-Host "";
            return $true;
            break;
        }
        1 {
            Write-Host "Continuing regardless (Don't say I didn't....KABOOM!!! Just kidding)";
            return $true;
            break;
        }
        2 {
            Write-Host "Cancelling action...";  
            return $false;
            break;
        }
        3 {
            Write-Host "These are your local changes"; 
            Write-Host ""
            $expression = "tf status /recursive $tfsPath"
            $changesList = Invoke-Expression $expression
            foreach($line in $changesList){
                Write-Host $line
            }
            Write-Host ""

            return $false;
            break;
        }
    }
}

function PrintBig([string] $message){
    $asterisks = $message.Length + 4

    Write-Host ""

    for ($i=1; $i -le $asterisks; $i++){
        Write-Host "*" -NoNewline
    }
    Write-Host ""

    Write-Host "* $message *"
    
    for ($i=1; $i -le $asterisks; $i++){
        Write-Host "*" -NoNewline
    }
    Write-Host ""
    Write-Host ""
}

function GetListOfTfsChildFolders([string] $parentTfsPath){
    $tfDirResult = Invoke-Expression "tf dir $parentTfsPath /folders"
    $childItems = $tfDirResult | Where {$_.StartsWith("$/") -ne $true -and $_.Trim() -ne "" -and $_.StartsWith("$")} | Select @{Name="Name";Expression={$_.TrimStart("$")}} | foreach {$_.Name};
    return $childItems;
}

<# 
.Synopsis 
    Merges an Expert source branch to a target branch
.Description   
    Merges any changes from a source branch to a target branch by first checking for existing changes,
    offering to shelve those changes, getting latest of source and target and merging the necessary 
    modules. Use the -reverseIntegration flag to only merge modules that exist in the target branch
.PARAMETER sourceBranch
    The name of the source branch e.g. Main
.PARAMETER targetBranch
    The name of the target branch e.g. Dev\OnTheGo
.PARAMETER reverseIntegration
    Use this flag to signify that this is an RI. This means that only the modules that exist in the target branch will be merged
.EXAMPLE
        Merge-Branch Services.Query -sourceBranch Dev\Validation -targetBranch Main
    Will merge changes in the Validation development branch down to the Main branch
.EXAMPLE
        Merge-Branch Services.Query -sourceBranch Main -targetBranch Dev\Validation -reverseIntegration
    Will merge changes in the Main branch up to the Validation development branch but will ignore any modules
    that do not exist in the target branch. This is commonly referred to as a reverse integration merge (RI)

    The user must manually check in the branched changes
    
#>
function Merge-Branch([string] $sourceBranch, [string] $targetBranch, [switch] $reverseIntegration){

    # Command-Definition
    # Merge-Branch -sourceBranch $sourceBranch -targetBranch $targetBranch

    # Validate branch names and paths

        if (!($sourceBranch)) {
            write "No source branch specified."
            return
        }

        if (!($targetBranch)) {
            write "No target branch specified."
            return
        }

        $sourceTfsPath = "`$/ExpertSuite/" + $sourceBranch.Replace("\", "/").Trim('/') + "/Modules/" + $moduleName
        $targetTfsPath = "`$/ExpertSuite/" + $targetBranch.Replace("\", "/").Trim('/') + "/Modules/" + $moduleName
        $rootPath = $global:BranchLocalDirectory.ToLower().Replace($global:BranchName.ToLower(), "")
        if($global:BranchName.Trim("/").ToUpper() -eq "MAIN"){
            $rootPath = $global:BranchLocalDirectory.Trim("/").Substring(0, $global:BranchLocalDirectory.Length - 4)
        }

        Write-Host "TFS Local Root:           $rootPath"
        Write-Host "Source TFS:               $sourceTfsPath"
        Write-Host "Target TFS:               $targetTfsPath"

        PrintBig "Looking for existing changes"
    

    # Need to look for local changes in the source branch and prompt the user

    $changesetStrings = Invoke-Expression "tf status"
    $changesetString = [System.String]::Join("\r\n", $changesetStrings)
    $changesetString = $changesetString.ToUpperInvariant()

    if($changesetString.Contains($sourceTfsPath.ToUpperInvariant())) {
        Write-Warning "You have local changes in the source branch $sourceTfsPath"
    
        # "You have pending changes in the source branch $sourceBranch. Are you sure you want to continue?"
        # "Press Y to continue merging, N to cancel, V to view the pending changes or S to shelve the changes and remove them from the local disk"

        $shouldContinue = PromptUserToShelveBranch $sourceBranch $sourceTfsPath "Changes in $sourceBranch before merge to $targetBranch"
        if($shouldContinue -eq $false){
            Write-Warning "Merge Cancelled"
            return;
        }
    }

    # Need to look for local changes in the target branch and prompt the user

    if($changesetString.Contains($targetTfsPath.ToUpperInvariant())) {
        Write-Warning "You have local changes in the target branch $targetTfsPath"
        # "You have pending changes in the target branch $targetBranch. Are you sure you want to continue?"
        # "Press Y to continue merging, N to cancel, V to view the pending changes or S to shelve the changes and remove them from the local disk"    
        $shouldContinue = PromptUserToShelveBranch $targetBranch $targetTfsPath "Changes in $targetBranch before merge from $sourceBranch"
        if($shouldContinue -eq $false){
            Write-Warning "Merge Cancelled"
            return;
        }
    }

    PrintBig "Fetching latest from source control"

    Write-Host ""
    Write-Host "Getting latest of $sourceTfsPath"
    # Fetch the latest from the source branch
    Invoke-Expression "tf get $sourceTfsPath /recursive"

    Write-Host ""
    Write-Host "Getting latest of $targetTfsPath"
    # Fetch the latest from the target branch
    Invoke-Expression "tf get $targetTfsPath /recursive"
    Write-Host ""

    PrintBig "Merging Modules"

    Write-Host "Getting list of modules to branch"

    # Get the list of modules in the source branch
    $sourceBranchModules = GetListOfTfsChildFolders $sourceTfsPath

    $targetBranchModules = GetListOfTfsChildFolders $targetTfsPath

    # Merging only happens to modules in both branches
    $modulesToMerge = $sourceBranchModules | ?{$targetBranchModules -contains $_}

    # If we are not doing an RI, branch the new modules
    if($reverseIntegration -eq $false){

        # Now look for modules to branch that are in the source branch but not in the target branch
        $modulesToBranch = $sourceBranchModules | ?{($targetBranchModules -contains $_) -eq $false}

        $moduleIndex = 1;
        $moduleCount = $modulesToBranch.Count;

        foreach($moduleToBranch in $modulesToBranch){
            Write-Host ""
            Write-Host "Branching Module $moduleToBranch ($moduleIndex of $moduleCount)"
            Write-Host ""

        
            $resultOfBranch = Invoke-Expression "tf branch $sourceTfsPath$moduleToBranch $targetTfsPath$moduleToBranch /noprompt"

            if($LASTEXITCODE -ne 0){
                Write-Host ""
                Write-Error "An error occurred branching $moduleToBranch"
            }
         
            $moduleIndex++;
        } 
    }

    $moduleIndex = 1;
    $moduleCount = $modulesToMerge.Count;
    $hasConflicts = $false;

    # For each module directory that exists in both the source and target branch:
    #    tf merge $sourceModulePath $targetModulePath /recursive
    foreach($moduleToMerge in $modulesToMerge){
        Write-Host ""
        Write-Host "Merging Module $moduleToMerge ($moduleIndex of $moduleCount)"
        Write-Host ""

        
        $resultOfMerge = Invoke-Expression "tf merge $sourceTfsPath$moduleToMerge $targetTfsPath$moduleToMerge /recursive /noprompt"

        if($LASTEXITCODE -ne 0){
            $hasConflicts = $true;
            Write-Host ""
            Write-Warning "Conflicts or Access Denied errors were found in $moduleToMerge"
        }
         
        $moduleIndex++;
    } 



    if($hasConflicts){

        PrintBig "Resolving Conflicts"

        # Now we are going to enter a loop checking for outstanding conflicts and presenting options

        $loopCheckForConflicts = $true;
        while($loopCheckForConflicts){
            Write-Host ""

            # Present the conflicts options to the user

            $message = "There were conflicts or ""Access Denied"" errors. What do you want to do?";
            $resolve = new-Object System.Management.Automation.Host.ChoiceDescription "&Resolve","Resolve using the default merge tools";
            $checkAgain = new-Object System.Management.Automation.Host.ChoiceDescription "&Check Again","I have just resolved the conflicts or permissions, check again for conflicts.";
            $quit = new-Object System.Management.Automation.Host.ChoiceDescription "&Quit","Quit the merge process";
            $choices = [System.Management.Automation.Host.ChoiceDescription[]]($resolve, $checkAgain, $quit);
            $answer = $host.ui.PromptForChoice($caption,$message,$choices,0)
            switch ($answer){
                0 {
                    Write-Host ""
                    Write-Host "Launching Merge Tools...";
                    Invoke-Expression "tf resolve $targetTfsPath /recursive"
                    break;
                }
                1 { 
                    Write-Host ""
                    Write-Host "Checking for conflicts..."; 
                    break;
                }
                2 {
                    Write-Host ""
                    Write-Host "Cancelling merge..."; 
                    Write-Host ""
                    Write-Warning "You must manually resolve the conflicts in the workspace, build and test the branch"
                    $loopCheckForConflicts = $false;
                    break;
                }
            }
            if($loopCheckForConflicts){
                # Perform the conflict check again
                $targetBranchStatus = Invoke-Expression "tf resolve $targetTfsPath /preview /recursive"
                if($LASTEXITCODE -eq 0){
                    $loopCheckForConflicts = $false 
                }
            }
        }
    }

    Switch-Branch $targetBranch | Out-Null

    # Now summarise the changes in the target branch.
    PrintBig "Summary"

    $changesStatusText = Invoke-Expression "tf status $targetTfsPath /recursive"

    if($changesStatusText.Length -eq 1 -and $changesStatusText.StartsWith("There are no")){
        Write-Warning "There was nothing to merge!!!!!"
    } else {
        $changesSummaryText = $changesStatusText[$changesStatusText.Length-1].Replace(" item(s)", "")
        Write-Host ""
        Write-Host "There are $changesSummaryText files changed in the merge"
        Write-Host ""
        Write-Host "The modules now with changes in $targetBranch are: "

        Get-ExpertModulesInChangeset
        Write-Host ""
        Write-Host ""
        Write-Warning "Please build all modules in this branch, deploy and smoke test before checking in" 
    }

    Write-Host "Merge Completed!!!"
}

Set-Alias merge Merge-Branch
Export-ModuleMember -function Merge-Branch -alias merge

