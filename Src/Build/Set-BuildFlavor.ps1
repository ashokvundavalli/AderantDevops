<# 
.Synopsis 
    Sets the build flavor in the project RSP for all modules in the current branch
.Description
.Parameter $buildFlavor The build flavor - "Release" or "Debug"
#>
param([string]$buildFlavor = "Release")

begin {    
}

process {        
    if ($buildFlavor.ToLower().Contains("debug") -or $buildFlavor.ToLower().Contains("release")) {
        $currentDirectory = Get-Location
        
        $comingFrom = $null
        $goingTo = $null
        
        if ($buildFlavor.ToLower().Equals("debug")) {
            $comingFrom = "Release"
            $goingTo = "Debug"
        } else {
            $comingFrom = "Debug"
            $goingTo = "Release"
        }
        
        Write-Host "Switching build to type $goingTo"
    
        Set-Location $BranchModulesDirectory
        dir -Include *.rsp -Recurse | % { Write-Host $_.FullName; tf checkout $_.FullName; $rsp = gc $_; $rsp | % {$_ -replace "BuildFlavor=$comingFrom", "BuildFlavor=$goingTo"} | sc $_}
        Set-Location $currentDirectory  
    } else {
        Write-Warning "Debug or Release build type not specified"
    }
}

end {
    Write-Host "Updated RSPs"
}