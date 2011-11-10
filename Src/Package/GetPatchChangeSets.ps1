<#  
.Synopsis 
    Gets a filtered set of change sets for building a patch.
.Parameter teamFoundationServer
    The hostname of the Team Foundation Server.
.Parameter teamProject
    The team project to target.
.Parameter solution
    The server Uri or solution to use as the root directory.
    If a drop location is used, the last part of the name is assumed to be the branch name.
.Example
    Build-Patch -teamFoundationServer "tfs" -teamProject "ExpertSuite" -solution "/$/ExpertSuite/Releases/MatterPlanning"
.Notes
    See "PatchBuilder" under Modules/Build.Tools/Src/PatchBuilder for a definition of ChangeSetInfo
#>
param(
    [string]$teamFoundationServer = "http://tfs:8080",
    [string]$teamProject = "ExpertSuite",
    [string]$branchPathOrName = ""
)

function Get-TeamFoundationServer([string]$serverName = $(Throw 'serverName is required')) {
    Write-Host "Connecting to Team Foundation Server $($serverName)."
    
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.Client")
    
    $propertiesToAdd = (
        ('VersionControl', 'Microsoft.TeamFoundation.VersionControl.Client', 'Microsoft.TeamFoundation.VersionControl.Client.VersionControlServer'),
        ('WorkItems', 'Microsoft.TeamFoundation.WorkItemTracking.Client', 'Microsoft.TeamFoundation.WorkItemTracking.Client.WorkItemStore')
        #('CommonStructures', 'Microsoft.TeamFoundation', 'Microsoft.TeamFoundation.Server.ICommonStructureService'),
        #('GroupSecurity', 'Microsoft.TeamFoundation', 'Microsoft.TeamFoundation.Server.IGroupSecurityService')
    )    
    
    $teamFoundation = $null
    try {
        # fetch the TFS instance, but add some useful properties to make life easier
        # Make sure to "promote" it to a psobject now to make later modification easier
        [psobject]$teamFoundation = [Microsoft.TeamFoundation.Client.TeamFoundationServerFactory]::GetServer($serverName)       
    } catch [System.Exception] {
        Write-Error $_.Exception
        throw $_.Exception
    }
    
    if ($teamFoundation -ne $null) {            
        foreach ($entry in $propertiesToAdd) {
            $scriptBlock = '
                [System.Reflection.Assembly]::LoadWithPartialName("{0}") > $null
                $this.GetService([{1}])            
            ' -f $entry[1],$entry[2]
            $teamFoundation | Add-Member scriptproperty $entry[0] $ExecutionContext.InvokeCommand.NewScriptBlock($scriptBlock) -Force
        }
    }
    
    Write-Host "Connection handle created for $($teamFoundation.Uri)."    
    return $teamFoundation
}


function Get-AllTeamProjects() {
    $projects = $tfs.WorkItems.Projects
    return $projects
}


function Get-GetProjectDetailsByName([string]$name) {
    Write-Host "Getting project details for $name."
    
    $project = $tfs.WorkItems.Projects[$name]
    return $project
}


function Get-AllQueries($project) {
    return $project.StoredQueries
}

<#  
.Synopsis 
    Gets the change sets for the specified work items.    
.Parameter workitems
    The workitems to process.
.Parameter $branch
    A full or partial branch path. This is used to make sure only change sets for the specified branch are returned.
#>
function Get-ChangeSets($workItems, $branch) {
    $branch = (Get-BranchPath).ToLower()
    $changeSets = @()

    foreach ($workItem in $workItems) {
        $w = $tfs.WorkItems.GetWorkItem($workItem.Id)
        
        # Try and find any change sets that are attached to this work item 
        foreach ($link in $w.Links) {
            if ($link -ne $null -and $link.LinkedArtifactUri -ne $null) {                
                $artifact = [Microsoft.TeamFoundation.LinkingUtilities]::DecodeUri($link.LinkedArtifactUri)
                
                if ([String]::Equals($artifact.ArtifactType, "Changeset", [StringComparison]::Ordinal)) {
                    # Convert the artifact URI to Changeset object.
                    [Microsoft.TeamFoundation.VersionControl.Client.Changeset]$changeSet = $tfs.VersionControl.ArtifactProvider.GetChangeset([Uri]($link.LinkedArtifactUri))
					
					# ensure the change set ServerItem matches our target branch or things will go badly wrong
                    $isChangeSetValid = $true
                    foreach ($change in $changeSet.Changes) {                        
                        if (!($change.Item.ServerItem.ToLowerInvariant().Contains($branch.ToLowerInvariant()))) {                            
                            Write-Host "Work item $($workItem.Id) has a change set $($changeSet.ChangesetId) with an invalid change location." -ForegroundColor Yellow
                            Write-Host "Expected change for $branch but found $($change.Item.ServerItem)" -ForegroundColor Yellow
                            Write-Host ""
                            $isChangeSetValid = $false
                        }                            
                    }
                    
                    if ($isChangeSetValid) {
                        $changeSets += $changeSet
                    }                    
                }
            }
        }        
    }
    Write-Host "Found $($changeSets.Count) change sets."
    
    WriteChangeSetInfo $changeSets   
    
    return $changeSets
}

function WriteChangeSetInfo($changes) {
    foreach ($change in $changes) {  
        $id = $change.WorkItems[0].Id
        $title = $change.WorkItems[0].Title
        Write-Host "Work item: $id - $title"        
        Write-Host "Changeset: $($change.ChangesetId) - $($change.Comment)"
        Write-Host ""
    }
}

function Get-BranchPath() {
    $path = $null
    if ([string]::IsNullOrEmpty($BranchName)) {
        if ([string]::IsNullOrEmpty($branchPathOrName)) {
            throw "`$BranchName or -branchPathOrName must be set. Cannot continue."
        }
        
        # this is far from ideal
        # TODO: Ask Andy how we can work out what branch we are on reliably
        if ($branchPathOrName.ToLower().Contains("na.aderant.com")) {
            $parts = $branchPathOrName.Split("\")
            $path = $parts[$parts.Count-1]
        } else {
            $path = $branchPathOrName
        }        
    } else {
        # BranchName comes from the Expert development PowerShell environment
        $path = $serverPath = "$/" + $teamProject + "/" + $BranchName
    }
    $serverPath = $path.Replace("\", "/")
    
    if ($serverPath.StartsWith("/")) {
        $serverPath = $serverPath.Remove(0, 1)
    }
    return $serverPath
}

<# 
.Synopsis 
    Returns a list of ChangeSetInfo for the specified branch history.
.Description
#>
function Get-ChangesByHistory([Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]$start, [Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]$end) {
    $serverPath = Get-BranchPath
    
    Write-Host "Querying history on $serverPath from $($start.DisplayString) to $($end.DisplayString)"
    
    $query = $tfs.VersionControl.QueryHistory(
        $serverPath,
        [Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]::Latest, 
        0, 
        [Microsoft.TeamFoundation.VersionControl.Client.RecursionType]::Full, 
        $null, 
        $start, 
        $end, 
        [System.Int32]::MaxValue, 
        $true, 
        $false)
    
    return $query
}


<# 
.Synopsis 
    Returns a list of ChangeSetInfo for the specified Team Query.
.Description
#>
function Get-ChangesByQuery([string]$queryName) {
    $project = Get-GetProjectDetailsByName $teamProject
    $queries = Get-AllQueries -project $project
    
    foreach ($query in $queries) {
        if ($query.Name.Contains($queryName)) {            
            $queryProxy = New-Object Microsoft.TeamFoundation.WorkItemTracking.Client.Query($tfs.WorkItems, $query.QueryText)
            
            Write-Host "Running query $($query.Name)."
            # run the matching query
            $items = $queryProxy.RunQuery()
            
            return $changes = Get-ChangeSets -workitems $items
        }    
    }
}


<# 
.Synopsis 
    Loads the patching manifest and executes the appropriate action.
.Description
#>
function Read-ManifestAndExecute() {
    [xml]$manifest = Get-Content .\PatchingManifest.xml
    
    $teamQuery = $manifest.Settings.TeamQuery
    $start = $manifest.Settings.StartVersionSpec    
    
    [void][System.Reflection.Assembly]::LoadWithPartialName("Microsoft.TeamFoundation.VersionControl.Client")
    
    if ($teamQuery -ne $null -and ![string]::IsNullOrEmpty($teamQuery)) {
        Write-Host "Team Query is set: $teamQuery"
        
        $results = Get-ChangesByQuery $teamQuery
        Write-Output $results        
        return
    }
    
    if ($start -ne $null -and ![string]::IsNullOrEmpty($start)) {
        Write-Host "Start VersionSpec is set: $start"
        
        $end = $manifest.Settings.EndVersionSpec
        
        $startSpec = [Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]::ParseSingleSpec($start, $null)
        $endSpec = $null
        if (![string]::IsNullOrEmpty($end)) {
            $endSpec = [Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]::ParseSingleSpec($end, $null)
        }
        
        $changes = Get-ChangesByHistory $startSpec $endSpec
        #Write-Output $changes
        return
    }
}

# handle to the Team Foundation Server
[psobject]$tfs = Get-TeamFoundationServer $teamFoundationServer

Read-ManifestAndExecute