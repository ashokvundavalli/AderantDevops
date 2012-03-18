<# 
.Synopsis 
    Sets the build flavor in the project RSP for all modules in the current branch
.Description
.Parameter release 
    Changes the build flavor to Release.
.Parameter debug
    Changes the build flavor to Debug.
.Parameter nocheckout
    Does not check the file out when changing flavors
#>
param([switch]$release, [switch]$debug, [switch]$nocheckout)

begin {    
}

process {
    if ($release -eq $true -and $debug -eq $true) {
        Write-Error "Only one switch may be used"
        return
    }
    
    if ($release) {    
        $find = "BuildFlavor=Debug"
        $replace = "BuildFlavor=Release"
    } else {
       $find = "BuildFlavor=Release"
       $replace = "BuildFlavor=Debug"
    }
    
    $currentDirectory = Get-Location
    Set-Location $BranchModulesDirectory    
    
    if ($nocheckout) {
        Write-Host "Updating reponse files with no check out."
        
        dir -Include *.rsp -Recurse | % { 
                                            Write-Host $_.FullName; 
                                            Set-ItemProperty $_.FullName -name IsReadOnly -value $false; 
                                            $rsp = gc $_; $rsp | % {$_ -replace $find, $replace} | sc $_;
                                            Set-ItemProperty $_.FullName -name IsReadOnly -value $true;
                                        }
    } else { 
        Write-Host "Updating reponse files with check out."
        
        dir -Include *.rsp -Recurse | % { 
                                            Write-Host $_.FullName; 
                                            tf checkout $_.FullName; 
                                            $rsp = gc $_; $rsp | % {$_ -replace $find, $replace} | sc $_
                                        }
    }
    
    Set-Location $currentDirectory
}

end {
    Write-Host "Updated build response files."
}