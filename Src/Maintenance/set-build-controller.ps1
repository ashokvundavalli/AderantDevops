$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")
 
# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
$tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])

$templates = $bs.QueryProcessTemplates("ExpertSuite");
$legacyTemplate = ($templates | Where-Object -Property "ServerPath" -eq "$/ExpertSuite/BuildProcessTemplates/UpgradeTemplate.xaml")

$qbSpec = $bs.CreateBuildDefinitionSpec("ExpertSuite", "*")

$newController = $null

$controllers = $bs.QueryBuildControllers()
foreach ($controller in $controllers) {
	
	if ($controller.Name.StartsWith("VMBLD301", [System.StringComparison]::OrdinalIgnoreCase)) {
		$newController = $controller
		break
	}
}

$qbResults = $bs.QueryBuildDefinitions($qbSpec)
foreach ($definition in $qbResults.Definitions) {
    
	#Write-Host ($definition.BuildController | gm)
	if ($definition.BuildController -eq $null) {
		Write-Host "No controller for $($definition.Name)"
		continue
	}
	
    if ($definition.BuildController.Name.StartsWith("vmbld201", [System.StringComparison]::OrdinalIgnoreCase)) {
		Write-Host "Updating $($definition.Name)"
		$definition.BuildController = $newController
		$definition.Save()
	}
}
