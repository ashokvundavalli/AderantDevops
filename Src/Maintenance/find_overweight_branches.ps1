$collectionUri = New-Object Uri("http://tfs:8080/tfs/aderant")
 
#Load assemblies
[Void][Reflection.Assembly]::Load("Microsoft.TeamFoundation.Client, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
[Void][Reflection.Assembly]::Load("Microsoft.TeamFoundation.Build.Client, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
 
# Connect to the team collection
$tpc = New-Object Microsoft.TeamFoundation.Client.TfsTeamProjectCollection($collectionUri)
$tpc.EnsureAuthenticated()
 
# Get the build service
$bs = $tpc.GetService([Microsoft.TeamFoundation.Build.Client.IBuildServer])
#$qbSpec = $bs.CreateBuildDetailSpec("ExpertSuite", "*Main*")
#$builds = $bs.QueryBuilds($qbSpec).Builds 


# array to hold all know build numbers
#[string[]]$buildNumbers = @()

# Get all of the build numbers, this may have junk like Main.RunTests.Integration_20150924.1
#$numbers = ($builds | Select-Object -Property "BuildNumber") | % { $_.BuildNumber.Split(" ")[0] }

#foreach ($number in $numbers) {
#    try {
#        [int32]$number.Replace(".", "") | Out-Null
#
#        $buildNumbers += $number.Trim().ToUpperInvariant()       
#    } catch [InvalidCastException] {
#    }
#}
#
#$uniqueBuilds = $buildNumbers | select -Unique

$modules = gci -Path \\na.aderant.com\ExpertSuite\Main\ | ?{ $_.PSIsContainer }

foreach ($module in $modules) {
    $build = gci -Path $module.FullName -Filter 1.8.0.0

    if ($build -ne $null) {
        $assemblyFolder = $build[0]

        Write-Host **** LOOKING IN $module.Name ****
        $moduleBuildFolders = (gci -Path $assemblyFolder.FullName) | Sort-Object -Property Name -Descending

        # Iterate each build for a module and see if the build number is known to TFS
        # If it isn't, then the build is orphaned
        foreach ($buildFolder in $moduleBuildFolders) {

            # Ineffecient but accurate - query TFS for each build folder 

            $qbSpec = $bs.CreateBuildDetailSpec("ExpertSuite", "*")
            $qbSpec.BuildNumber = "$buildFolder ($module)"
            $builds = $bs.QueryBuilds($qbSpec).Builds 

            if ($builds -ne $null -and $builds.Count -gt 0) {
                Write-Host "Managed build found for $buildFolder"
            } else {
                Write-Host "Unmanaged build found! $($buildFolder.FullName)"                
            }
        }   
    }
}



