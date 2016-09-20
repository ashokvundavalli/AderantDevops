[CmdletBinding()]
param(
    [string]$teamProject = "ExpertSuite",
    [string]$pattern = "*releases.81x*",
    [switch]$setToRelease = $false
)

$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Workflow.dll")

# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
$tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])
$qbSpec = $bs.CreateBuildDefinitionSpec($teamProject, $pattern)

$buildDefinitions = $bs.QueryBuildDefinitions($qbSpec).Definitions

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

                    Write-Host "Added flavor to $($build.Name)" 
                } else {
                    Write-Host "$($build.Name) has arguments.. not updating" 
                }
            }          
        }

        $build.Save()

    } else {
        Write-Warning "Invalid build controller for build: $($build.Name)"
    }    
}