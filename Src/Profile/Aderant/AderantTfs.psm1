if (-not $ShellContext.IsTfvcModuleEnabled) {
    return
}

# Called from SwitchBranchTo
function Set-ChangedBranchPaths([string]$name) {
    #initialise from default setting
    Write-Host "Change branch to $name"

    # container as in dev or release
    $newBranchContainer = ""
    $previousBranchContainer = ""

    # name of branch or MAIN
    $newBranchName = ""
    $previousBranchName = ""

    #was the pervious branch MAIN?
    [bool]$changeToContainerFromMAIN = $false

    # get the new and previous name a container parts
    if ((IsDevBanch $ShellContext.BranchName) -or (IsReleaseBanch $ShellContext.BranchName)) {
        $previousBranchContainer = $ShellContext.BranchName.Substring(0, $ShellContext.BranchName.LastIndexOf("\"))
        $previousBranchName = $ShellContext.BranchName.Substring($ShellContext.BranchName.LastIndexOf("\") + 1)
    } elseif ((IsMainBanch $ShellContext.BranchName)) {
        $previousBranchName = "MAIN"
        $changeToContainerFromMAIN = $true
    }

    if ((IsDevBanch $name) -or (IsReleaseBanch $name)) {
        $newBranchContainer = $name.Substring(0, $name.LastIndexOf("\"))
        $newBranchName = $name.Substring($name.LastIndexOf("\") + 1)
    } elseif ((IsMainBanch $name)) {
        $newBranchName = "MAIN"
        $newBranchContainer = "\"
    }

    $success = $false
    if ($changeToContainerFromMAIN) {
        $success = Switch-BranchFromMAINToContainer $newBranchContainer $newBranchName $previousBranchName
    } else {
        $success = Switch-BranchFromContainer $newBranchContainer $previousBranchContainer $newBranchName $previousBranchName
    }

    if ($success -eq $false) {
        Write-Host -ForegroundColor Yellow "'$name' branch was not found on this machine."
        return $false
    }

    #Set common paths
    $ShellContext.BranchModulesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Modules")

    $ShellContext.BranchBinariesDirectory = (Join-Path -Path $global:BranchLocalDirectory -ChildPath "Binaries")
    if ((Test-Path $ShellContext.BranchBinariesDirectory) -eq $false) {
        New-Item -Path $ShellContext.BranchBinariesDirectory -ItemType Directory
    }

    return $true
}

<#
 we need to cater for the fact MAIN is the only branch and not a container like dev or release
#>
function Switch-BranchFromMAINToContainer($newBranchContainer, $newBranchName, $previousBranchName) {
    #change name and then container and remove extra backslash's
    $globalBranchName = ($ShellContext.BranchName -replace $previousBranchName, $newBranchName)
    $globalBranchName = $newBranchContainer + "\" + $globalBranchName

    if ($globalBranchName -eq "\") {
        return $false
    }

    # The strip logic assumes the last slash is the container separator, if the local dir ends with a slash it will break that assumption
    $global:BranchLocalDirectory = $global:BranchLocalDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar)

    #strip MAIN then add container and name
    $globalBranchLocalDirectory = $global:BranchLocalDirectory.Substring(0, $global:BranchLocalDirectory.LastIndexOf("\") + 1)
    $globalBranchLocalDirectory = (Join-Path -Path $globalBranchLocalDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $globalBranchLocalDirectory) -eq $false) {
        return $false
    }

    $ShellContext.BranchName = $globalBranchName
    $global:BranchLocalDirectory = $globalBranchLocalDirectory

    #strip MAIN then add container and name
    $ShellContext.BranchServerDirectory = $ShellContext.BranchServerDirectory.Substring(0, $ShellContext.BranchServerDirectory.LastIndexOf("\") + 1)
    $ShellContext.BranchServerDirectory = (Join-Path -Path $ShellContext.BranchServerDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    $ShellContext.BranchServerDirectory = [System.IO.Path]::GetFullPath($ShellContext.BranchServerDirectory)

    return $true
}

<#
 we dont have to do anything special if we change from a container to other branch type
#>
function Switch-BranchFromContainer($newBranchContainer, $previousBranchContainer, $newBranchName, $previousBranchName) {
    #change name and then container and remove extra backslash's
    $branchName = $ShellContext.BranchName.Replace($previousBranchName, $newBranchName)
    $branchName = $ShellContext.BranchName.Replace($previousBranchContainer, $newBranchContainer)
    if (IsMainBanch $branchName) {
        $branchName = [System.Text.RegularExpressions.Regex]::Replace($branchName, "[^1-9a-zA-Z_\+]", "");
    }

    if ($branchName -eq "\") {
        return $false
    }

    $branchLocalDirectory = $global:BranchLocalDirectory.Substring(0, $global:BranchLocalDirectory.LastIndexOf($previousBranchContainer));
    $branchLocalDirectory = (Join-Path -Path $branchLocalDirectory -ChildPath( Join-Path -Path $newBranchContainer -ChildPath $newBranchName))

    if ((Test-Path $branchLocalDirectory) -eq $false -or $branchLocalDirectory.EndsWith("ExpertSuite")) {
        return $false
    }

    $ShellContext.BranchName = $branchName
    $global:BranchLocalDirectory = $branchLocalDirectory

    $ShellContext.BranchServerDirectory = $ShellContext.BranchServerDirectory.Substring(0, $ShellContext.BranchServerDirectory.LastIndexOf($previousBranchContainer));
    $ShellContext.BranchServerDirectory = (Resolve-Path -Path ($ShellContext.BranchServerDirectory + $newBranchContainer + "\" + $newBranchName)).ProviderPath
    $ShellContext.BranchServerDirectory = [System.IO.Path]::GetFullPath($ShellContext.BranchServerDirectory)

    return $true
}

<#
 Re-set the local working branch
 e.g. Dev\Product or MAIN
#>
function SwitchBranchTo {
    param(
        [Parameter(Mandatory=$true)][string]$newBranch,
        [switch]$setAsDefault
    )

    begin {
        if ($ShellContext.BranchName -Contains $newBranch) {
            Write-Host "The magic unicorn has refused your request." -ForegroundColor Yellow
            return
        }

        $success = Set-ChangedBranchPaths $newBranch

        if ($success -eq $false) {
            return
        }

        Set-Environment

        Set-CurrentModule $ShellContext.CurrentModuleName

        Set-Location -Path $global:BranchLocalDirectory

        if ($setAsDefault.IsPresent) {
            SetDefaultValue dropRootUNCPath $ShellContext.BranchServerDirectory
            SetDefaultValue devBranchFolder $global:BranchLocalDirectory
        }
    }
}


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
    Migrate TFS shelveset from one ExpertSuite branch to another.
.Description
    This is a shortcut for tftp unshelve /migrate
    It wraps your branch names with "$/ExpertSuite/" and "/modules".
    The branch parameters also feature auto-complete.
    You need to quote the shelveset name if it has spaces in it.
.PARAMETER name
    The shelveset name. Please quote it if the name has spaces in it.
.PARAMETER SourceBranch
    The source branch name eg. dev\product
.PARAMETER TargetBranch
    The target branch name eg. dev\anotherProduct
#>
function global:Move-Shelveset($Name, $SourceBranch, $TargetBranch) {
    $sourceBranch = $sourceBranch -replace "\\", "/"
    $targetBranch = $targetBranch -replace "\\", "/"
    $sourceLoc = "$/ExpertSuite/" + $sourceBranch + "/modules"
    $targetLoc = "$/ExpertSuite/" + $targetBranch + "/modules"
    tfpt unshelve $Name /migrate /source:$sourceLoc /target:$targetLoc
}

<# 
.Synopsis 
    Merge of a single changeset from a source branch to a target branch. This will perform a baseless merge.
.Description   
    Performs a baseless merge of a single changeset from a source branch to a target branch.
    
    The user must manually check in the branched changes
.PARAMETER sourceBranch
    The name of the source branch e.g. Main
.PARAMETER targetBranch
    The name of the target branch e.g. Dev\OnTheGo
.PARAMETER changeSet
    The changeset number to merge. e.g.: 273255
.EXAMPLE
        Merge-Baseless -sourceBranch Dev\Validation -targetBranch Dev\BillingBase -changeSet 273255
    
#>
function global:Merge-Baseless([string] $sourceBranch, [string] $targetBranch, [int] $changeSet) {
    # Validate branch names and paths

    if (!($sourceBranch)) {
        write "No source branch specified."
        return
    }

    if (!($targetBranch)) {
        write "No target branch specified."
        return
    }

    if (!($changeSet)) {
    
        $changeSet = Read-Host "Enter the changeset number"

        if (!($changeSet)) {
            Write-Host ""
            Write-Error "No changeset specified"
            return
        }
        
    }
    
    # calculate paths and output to the user

    $sourceTfsPath = "`$/ExpertSuite/" + $sourceBranch.Replace("\", "/").Trim('/') + "/Modules/" + $moduleName
    $targetTfsPath = "`$/ExpertSuite/" + $targetBranch.Replace("\", "/").Trim('/') + "/Modules/" + $moduleName
    $rootPath = $global:BranchLocalDirectory.ToLower().Replace($global:BranchName.ToLower(), "")
    if($global:BranchName.Trim("/").ToUpper() -eq "MAIN"){
        $rootPath = $global:BranchLocalDirectory.Trim("/").Substring(0, $global:BranchLocalDirectory.Length - 4)
    }

    Write-Host "TFS Local Root:           $rootPath"
    Write-Host "Source TFS:               $sourceTfsPath"
    Write-Host "Target TFS:               $targetTfsPath"
    Write-Host "ChangeSet:                $changeSet"
    Write-Host "Baseless:                 Yes"
        

    Write-Host "Performing baseless merge..."

    $resultOfBranch = Invoke-Expression "tf merge /baseless $sourceTfsPath $targetTfsPath /version:C$changeSet~C$changeSet /recursive"

    if($LASTEXITCODE -ne 0){
        Write-Host ""
        Write-Error "An error occurred branching $moduleToBranch"
    }

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
function global:Merge-Branch([string] $sourceBranch, [string] $targetBranch, [switch] $reverseIntegration){

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

<#
.Synopsis
    Builds the current module on server.
.Description
    Builds the current module on server.
.Example
     bm -getDependencies -clean ; Build-ExpertModulesOnServer -downstream
    If a local build succeeded, a server build will then be kicked off for current module.
#>
function Build-ExpertModulesOnServer([string[]] $workflowModuleNames, [switch] $downstream = $false) {
    $moduleBeforeBuild = $null;
    $currentWorkingDirectory = Get-Location;

    if (!$workflowModuleNames) {
        if (($ShellContext.CurrentModulePath) -and (Test-Path $ShellContext.CurrentModulePath)) {
            $moduleBeforeBuild = (New-Object System.IO.DirectoryInfo $ShellContext.CurrentModulePath | foreach {$_.Name});
            $workflowModuleNames = @($moduleBeforeBuild);
        }
    }

    if (-not ($workflowModuleNames)) {
        write "No modules specified.";
        return;
    }

    [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$workflowModuleNames = $global:Workspace.GetModules($workflowModuleNames)

    if ((Test-Path $ShellContext.BranchLocalDirectory) -ne $true) {
        write "Branch Root path does not exist: '$ShellContext.BranchLocalDirectory'"
    }

    [Aderant.Build.DependencyAnalyzer.ExpertModule[]] $modules = Sort-ExpertModulesByBuildOrder -BranchPath $ShellContext.BranchModulesDirectory -Modules $workflowModuleNames -ProductManifestPath $ShellContext.ProductManifestPath

    if (!$modules -or (($modules.Length -ne $workflowModuleNames.Length) -and $workflowModuleNames.Length -gt 0)) {
        Write-Warning "After sorting builds by order the following modules were excluded.";
        Write-Warning "These modules probably have no dependency manifest or do not exist in the Expert Manifest"

        (Compare-Object -ReferenceObject $workflowModuleNames -DifferenceObject $modules -Property Name -PassThru) | Select-Object -Property Name

        $message = "Do you want to continue anyway?";
        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes"
        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
        $result = $host.UI.PromptForChoice($null, $message, $options, 0)

        if ($result -ne 0) {
            write "Module(s) not found."
            return
        }
    }

    if ($downstream -eq $true) {
        write ""
        write "Retrieving downstream modules"

        [Aderant.Build.DependencyAnalyzer.ExpertModule[]]$modules = $global:Workspace.DependencyAnalyzer.GetDownstreamModules($modules)

        $modules = Sort-ExpertModulesByBuildOrder -BranchPath $ShellContext.BranchModulesDirectory -Modules $modules -ProductManifestPath $ShellContext.ProductManifestPath
        $modules = $modules | Where { $_.ModuleType -ne [Aderant.Build.DependencyAnalyzer.ModuleType]::Test }
        write "Done."
    }

    $modules = $modules | Where {$exclude -notcontains $_}

    write ""
    write "********** Build Overview *************"
    $count = 0
    $weHaveSkipped = $false
    foreach ($module in $modules) {
        $count++;
        write "$count. $module";
    }
    write "";
    write "";
    write "Press Ctrl+C now to abort.";
    Start-Sleep -m 2000;

    foreach ($module in $modules) {
        $sourcePath = $BranchName.replace('\', '.') + "." + $module;

        Write-Warning "Build(s) started attempting on server for $module, if you do not wish to watch this build log you can now press 'CTRL+C' to exit.";
        Write-Warning "Exiting will not cancel your current build on server. But it will cancel the subsequent builds if you have multiple modules specified, i.e. -downstream build.";
        Write-Warning "...";
        Write-Warning "You can use 'popd' to get back to your previous directory.";
        Write-Warning "You can use 'New-ExpertBuildDefinition' to create a new build definition for current module if non-exist."

        Invoke-Expression "tfsbuild start http://tfs:8080/tfs ExpertSuite $sourcePath";
    }
    if ($moduleBeforeBuild) {
        cm $moduleBeforeBuild;
    }
    pushd $currentWorkingDirectory;
}

Export-ModuleMember Build-ExpertModulesOnServer

function IsDevBanch([string]$name) {
    return $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsReleaseBanch([string]$name) {
    return $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase)
}

function IsMainBanch([string]$name) {
    return $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -and $name.LastIndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -gt $name.LastIndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase)
}

function ResolveBranchName([string]$branchPath) {
    $name = ""
    if (IsMainBanch $branchPath) {
        $name = "MAIN"
    } elseif (IsDevBanch $branchPath) {
        $name = $branchPath.Substring($branchPath.LastIndexOf("dev\", [System.StringComparison]::OrdinalIgnoreCase))
    } elseif (IsReleaseBanch $branchPath) {
        $name = $branchPath.Substring($branchPath.LastIndexOf("releases\", [System.StringComparison]::OrdinalIgnoreCase))
    }
    return $name
}

<#
.Synopsis
    Sets the default branch information.
.Description
    Sets the default branch information. This was formerly held in the defaults.xml file. After initially setting this information
    you should use the Switch-Branch command with the -SetAsDefault parameter to update it.
.PARAMETER devBranchFolder
    The full path to the development branch
.PARAMETER dropUncPath
    The full unc path to the network drop folder for the branch
.EXAMPLE
        Set-ExpertBranchInfo -devBranchFolder c:\ExpertSuite\Dev\Msg2 -dropUncPath C:\expertsuite\Dev\Msg2
     Will set the default branch information to the Dev\Msg2 branch

#>
function Set-ExpertBranchInfo([string] $devBranchFolder, [string] $dropUncPath) {
    if ((Test-Path $devBranchFolder) -ne $true) {
        Write-Error "The path $devBranchFolder does not exist"
    }

    if ((Test-Path $dropUncPath) -ne $true) {
        Write-Error "The path $dropUncPath does not exist"
    }

    SetDefaultValue DevBranchFolder $devBranchFolder
    SetDefaultValue DropRootUNCPath $dropUncPath
    Set-Environment
    Write-Host ""
    Write-Host "The environment has been configured"
    Write-Host "You should not have to run this command again on this machine"
    Write-Host "In future when changing branches you should use the Switch-Branch command with the -SetAsDefault parameter to make it permanent."
}

Set-Alias merge Merge-Branch -Scope Global
Set-Alias bmerge Merge-Baseless -Scope Global
Export-ModuleMember -function Merge-Branch -alias merge
Export-ModuleMember -function Merge-Baseless -alias bmerge
Export-ModuleMember -function Move-Shelveset

#These commands are in AderantTfs.psm1
Add-BranchExpansionParameter -CommandName "Merge-Branch" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Merge-Branch" -ParameterName "targetBranch"
Add-BranchExpansionParameter -CommandName "Merge-Baseless" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Merge-Baseless" -ParameterName "targetBranch"

# Add branch auto completion scenarios
Add-BranchExpansionParameter -CommandName "SwitchBranchTo" -ParameterName "newBranch" -IsDefault
Add-BranchExpansionParameter -CommandName "Branch-Module" -ParameterName "sourceBranch"
Add-BranchExpansionParameter -CommandName "Branch-Module" -ParameterName "targetBranch"

Add-BranchExpansionParameter –CommandName "New-ExpertManifestForBranch" –ParameterName "SourceBranch" -IsDefault
Add-BranchExpansionParameter –CommandName "New-ExpertManifestForBranch" –ParameterName "TargetBranch"
Add-BranchExpansionParameter -CommandName "Move-Shelveset" -ParameterName "TargetBranch"
Add-BranchExpansionParameter -CommandName "Move-Shelveset" -ParameterName "SourceBranch"

Set-LocalDirectory