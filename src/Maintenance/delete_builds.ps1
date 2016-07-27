$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")

 
# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
$tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])
$qbSpec = $bs.CreateBuildDetailSpec("ExpertSuite", "*releases.803time.*")

$qbSpec.InformationTypes = $null

$builds = $bs.QueryBuilds($qbSpec).Builds 

foreach ($build in $builds) {
    if ($build.KeepForever) {
        continue
    }

    if ([string]::IsNullOrEmpty($build.Quality)) {
        Write-Host "Will delete build: $($build.BuildNumber) [Label: $($build.LabelName)] on $($build.DropLocation)"        

        try {
            # If you are happy with the list, uncomment me to slaughter them like a wolf amongst sheep
            $build.Delete()

        } catch {
            Write-Error $_

            if ($_.Exception.Message.Contains("TF215034")) {
                Write "Build in progress... stopping. Please run the script again"

                $build.Stop();                
            }
            
        }
    }
}

