<# 
.Synopsis 
    A function for downloading our Build Infrastructure binaries.
.Example     
    GetBuildInfrastructure.ps1 -dropRoot \\na.aderant.com\ExpertSuite\Main\ -targetLocation "C:\B\78\Build.Infrastructure\Build.Tools"
.Remarks
    This is useful because of the chicken and egg problem our build system poses. If we grab BI as an independant module, then we can grab it all at once; Without the need to build it.
#>    

param([string]$dropRoot, [string]$targetLocation)
begin {
    write "GetBuildTools.ps1"
    if ($dropRoot.EndsWith("Build.Infrastructure") -or $dropRoot.EndsWith("Build.Infrastructure\")) {
        $dropRoot = Join-Path $dropRoot "Src";
    } elseif (Test-Path "$dropRoot\Build.Infrastructure") {
        $dropRoot = Join-Path $dropRoot "Build.Infrastructure\Src"
    }
}
process {
    robocopy.exe /E /NJH /NJS /NS /NC /NP /NFL /NDL /R:5 /W:1 /MT:5 $dropRoot\Build.Tools $targetLocation
}
end {
    write "Finished copying $dropRoot to $targetLocation"
}