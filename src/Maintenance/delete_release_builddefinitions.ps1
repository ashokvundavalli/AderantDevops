$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")
 
# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
#tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])
$qbSpec = $bs.CreateBuildDefinitionSpec("ExpertSuite", "*releases.*803time*")

$qbResults = $bs.QueryBuildDefinitions($qbSpec)

foreach ($definition in $qbResults.Definitions) {
    if ($definition.Name -ilike "*BuildAll") {
        continue
    }

    if ($definition.Name -ilike "*ThirdParty*") {
        continue
    }

    if ($definition.Name -ilike "*Build.Infrastructure*") {
        continue
    }

    Write-Host "Deleting build definition: $($definition.Name)"
       
    $builds = $definition.QueryBuilds();

    $canDeleteDefinition = $true

    foreach ($build in $builds) {
        if ($build.KeepForever) {
            $canDeleteDefinition = $false
            continue
        }
        
        Write-Host "Will delete build: $($build.BuildNumber) [Label: $($build.LabelName)] on $($build.DropLocation)"        
        $build.Delete()
    }

    if ($canDeleteDefinition) {
        $definition.Delete()
    }
}
