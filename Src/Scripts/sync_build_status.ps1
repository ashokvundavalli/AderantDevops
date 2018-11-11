# This script identifies builds which are in progress and stops them.
# When a build all runs, it creates manual builds for each module. These manual module builds are used so TFS can track the drop location of each module
# so the retention policy of TFS can properly clean up the drop.
# However there if a build all is stopped (not via build failure but manual intervention) then the manual module build(s) started by the build all
# will not be stopped and will show as running forever in TFS.

Function Clean-Builds-For($project, $pattern) {

    $collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
     
    #Load assemblies
    [Void][Reflection.Assembly]::Load("Microsoft.TeamFoundation.Client, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
    [Void][Reflection.Assembly]::Load("Microsoft.TeamFoundation.Build.Client, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
     
    # Connect to the team collection
    $tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
     
    # Get the build service
    $bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])

    $qbSpec = $bs.CreateBuildDetailSpec($project, $pattern)

    $qbSpec.Status = [Microsoft.TeamFoundation.Build.Client.BuildStatus]::InProgress
    $builds = $bs.QueryBuilds($qbSpec).Builds 

    foreach ($build in $builds) {       

#        Write-Host $build

        if ($build.Status -eq [Microsoft.TeamFoundation.Build.Client.BuildStatus]::InProgress) {        

            Write-Host $build.BuildNumber ":" $builds.StartTime

            $nodes = $build.Information.GetNodesByType("CustomSummaryInformation", $true)     


     #       Write-Host $nodes

            if ($nodes.Count -eq 0) {
                # Hm that's strange.. no custom nodes
                # Backup check is to look if it's be running for more than a day
                if ([DateTime]::Now.Subtract($build.StartTime).TotalHours -gt 24) {
                    Write-Host "Build $($build.BuildNumber) has run for more than 24 hours. Started on $($build.StartTime)... stopping."
                    $build.Stop()                                        
                    $build.Save()            

                }
                continue    
            }  

            
            foreach ($node in $nodes) {

                if ($node.Fields.Count -gt 0) {

                   $message = $node.Fields.Message

                    $pos = $message.IndexOf("(")
                    if ($pos -gt 0) {
                        $message = $message.Substring($pos)
                        $parentBuild = $message.Replace("(", "").Replace(")", "")                   

                        $spec = $bs.CreateBuildDetailSpec("ExpertSuite")
                        $spec.BuildNumber = $parentBuild
                        $spec.InformationTypes = $null
                                    
                        $builds = $bs.QueryBuilds($spec).Builds

                        if ($builds.Count -eq 0) {
                            Write-Host "Build $($build.BuildNumber) is orphaned. Started on $($build.StartTime)"

                            $build.Stop()                                        
                            $build.Save()                           
                            
                        } else {
                            if (($builds[0].Status -ne [Microsoft.TeamFoundation.Build.Client.BuildStatus]::InProgress)) {
                                Write-Host "Build all $($builds[0].BuildNumber) is not in progress. Stopping build $($build.BuildNumber)"
                                $build.Stop()                                                            
                                $build.Save()   
                            }
                        }
                    }                      
                }          
            }
        }
        
    }
}


# Clean builds for all Dev.* branches
Clean-Builds-For "ExpertSuite" "Dev.*"

# Clean builds for all Releases.* branches
Clean-Builds-For "ExpertSuite" "Releases.*"
