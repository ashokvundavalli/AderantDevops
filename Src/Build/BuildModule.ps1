##
# Build a single module, optional get the modules dependencies from the drop location
##
param(         
    [string]$moduleToBuildPath = $(if(!$moduleToBuildPath){Throw "Parameter missing -moduleToBuildPath moduleToBuildPath" }),
    [bool]$getDependencies = $false, 
    $cleanBin,
    [string]$dropRoot = $(if($getDependencies) {Throw "Parameter missing -dropRoot dropRoot" })    
)

begin {
    
    $MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30319\")            
    
    if (Test-Path $MSNetFrameworkLocation) {    
        $msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
    } else {
        throw "Build directory for .NET 4 RTM does not exist [$MSNetFrameworkLocation]"
    }
    
    
    #set the environment variables and make sure this hasn't already been done.
    if (!$env:DevEnvDir) {
        .\vsvars.ps1
    }

    Function BuildModule($moduleToBuildPath ) {

        $cleanCommand = $null
        if (-not [string]::IsNullOrEmpty($cleanBin) -and $cleanBin -eq "True") {
            $cleanCommand = "/p:CleanBin=True"
        }        
                
        if (Test-Path $moduleToBuildPath) {            
            if ($getDependencies) {
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp" "/p:DropLocation=$dropRoot" "/m:8" $cleanCommand
            }
            else {
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp" "/m:8" $cleanCommand
            }     
            write "Built $moduleToBuildPath"
        } else {
            $cannotResolvePath = "cannot resolve path [$moduleToBuildPath]"   
            write $cannotResolvePath
            throw              
        }
    }
}   
   
process {
    BuildModule($moduleToBuildPath)
}    
