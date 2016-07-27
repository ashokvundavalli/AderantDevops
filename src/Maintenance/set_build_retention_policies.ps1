$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::Load("Microsoft.TeamFoundation.Client, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
[Void][Reflection.Assembly]::Load("Microsoft.TeamFoundation.Build.Client, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
[Void][Reflection.Assembly]::LoadFrom("$Env:VS120COMNTOOLS\..\IDE\ReferenceAssemblies\v2.0\Microsoft.TeamFoundation.Build.Workflow.dll")
 
# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
$tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])
$qbSpec = $bs.CreateBuildDefinitionSpec("ExpertSuite", "*803Time*")

$buildDefinitions = $bs.QueryBuildDefinitions($qbSpec).Definitions 


$setToRelease = $false

foreach ($build in $buildDefinitions) {
    Write-Host "Processing build: $($build.Name)"
    
    $retentionPolicyList = $build.RetentionPolicyList
    foreach ($policy in $retentionPolicyList) {
                
        if ($policy.BuildReason -eq [Microsoft.TeamFoundation.Build.Client.BuildReason]::Triggered) {

            if ($policy.BuildStatus -eq [Microsoft.TeamFoundation.Build.Client.BuildStatus]::Stopped) {
                $policy.NumberToKeep = 1
                $policy.DeleteOptions = [Microsoft.TeamFoundation.Build.Client.DeleteOptions]::All
                continue                  
            }

            if ($policy.BuildStatus -eq [Microsoft.TeamFoundation.Build.Client.BuildStatus]::Failed) {
                $policy.NumberToKeep = 5
                $policy.DeleteOptions = [Microsoft.TeamFoundation.Build.Client.DeleteOptions]::All
                continue
            }

            if ($policy.BuildStatus -eq [Microsoft.TeamFoundation.Build.Client.BuildStatus]::PartiallySucceeded) {
                $policy.NumberToKeep = 2
                $policy.DeleteOptions = [Microsoft.TeamFoundation.Build.Client.DeleteOptions]::All
                continue
            }

            if ($policy.BuildStatus -eq [Microsoft.TeamFoundation.Build.Client.BuildStatus]::Succeeded) {
                $policy.NumberToKeep = 5
                $policy.DeleteOptions = [Microsoft.TeamFoundation.Build.Client.DeleteOptions]::All
            }
        }
    }

    if ($build.BuildController -ne $null) {        
        
        if ($setToRelease) {
            if (-not ($build.Name.Contains("Third"))) {
                $p = [Microsoft.TeamFoundation.Build.Workflow.WorkflowHelpers]::DeserializeProcessParameters($build.ProcessParameters)
                $arg = $p["MSBuildArguments"]

                if ([string]::IsNullOrEmpty($arg)) {
                    $p["MSBuildArguments"] = "/p:BuildFlavor=Release"
                    $build.ProcessParameters = [Microsoft.TeamFoundation.Build.Workflow.WorkflowHelpers]::serializeProcessParameters($p)
                    $build.Save()
                }
            }          
        }

        $build.Save()

    } else {
        Write-Warning "Invalid build controller for build: $($build.Name)"
    }    
}