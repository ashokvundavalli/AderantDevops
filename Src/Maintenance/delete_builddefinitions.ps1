$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")
 
# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
#tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])
$qbSpec = $bs.CreateBuildDefinitionSpec("ExpertSuite", "*dev.casev1*")
$qbResults = $bs.QueryBuildDefinitions($qbSpec)

foreach ($definition in $qbResults.Definitions) {   

    Write-Host "Deleting build definition: $($definition.Name)"

    # If you are happy with the list, uncomment me to slaughter them like a wolf amongst sheep
    $definition.Delete()
}
