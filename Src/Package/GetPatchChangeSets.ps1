<#  
.Synopsis 
    Constructs a collection of wrapped change sets (ChangeSetInfo) which contains the project information for building a patch.
.Parameter teamFoundationServer
    The hostname of the Team Foundation Server.
.Parameter teamProject
    The team project to target.
.Parameter solution
    The server Uri or solution to use as the root directory.
.Example
    Build-Patch -teamFoundationServer "tfs" -teamProject "ExpertSuite" -solution "/$/ExpertSuite/Releases/MatterPlanning"
.Notes
    See "PatchBuilder" under Modules/Build.Tools/Src/PatchBuilder for a definition of ChangeSetInfo
#>
param(
    [string]$teamFoundationServer = "http://tfs:8080",
    [string]$teamProject = "ExpertSuite",
    [string]$solution = ""
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


function Get-ChangeSets($workitems) {
    $changeSets = @()

    foreach ($workitem in $workitems) {
        $w = $tfs.WorkItems.GetWorkItem($workitem.Id)
        
        # Try and find any change sets that are attached to this work item 
        foreach ($link in $w.Links) {
            if ($link -ne $null -and $link.LinkedArtifactUri -ne $null) {                
                $artifact = [Microsoft.TeamFoundation.LinkingUtilities]::DecodeUri($link.LinkedArtifactUri)
                
                if ([String]::Equals($artifact.ArtifactType, "Changeset", [StringComparison]::Ordinal)) {
                    # Convert the artifact URI to Changeset object.
                    $changeSets += $tfs.VersionControl.ArtifactProvider.GetChangeset([Uri]($link.LinkedArtifactUri))
                }
            }
        }        
    }
    Write-Host "Found $($changeSets.Count) change sets."
    return $changeSets
}


<# 
.Synopsis 
    Returns a list of ChangeSetInfo for the specified branch history.
.Description
#>
function Get-ChangesByHistory([Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]$start, [Microsoft.TeamFoundation.VersionControl.Client.VersionSpec]$end) {
    $path = $null
    if ([string]::IsNullOrEmpty($BranchName)) {
        if ([string]::IsNullOrEmpty($solution)) {
            throw "`$BranchName or -solution must be set. Cannot continue."
        }
        $path = $solution
    } else {
        # BranchName comes from the Expert development PowerShell environment
        $path = $serverPath = "$/" + $teamProject + "/" + $BranchName
    }
    $serverPath = $path.Replace("\", "/")
    
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
        Write-Output $changes
        return
    }
}

# handle to the Team Foundation Server
[psobject]$tfs = Get-TeamFoundationServer $teamFoundationServer

Read-ManifestAndExecute