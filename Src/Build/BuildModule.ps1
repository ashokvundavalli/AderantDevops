##
# Build a single module, optional get the modules dependancies from the drop location
##
param(         
    [string]$moduleToBuildPath = $(if(!$moduleToBuildPath){Throw "Parameter missing -moduleToBuildPath moduleToBuildPath" }),
    [bool]$getDependencies = $false, 
    [string]$dropRoot = $(if($getDependencies) {Throw "Parameter missing -dropRoot dropRoot" })
)

begin{
    
	$MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30128\")
    
    if(![System.IO.Directory]::Exists($MSNetFrameworkLocation)){
        $MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.21006\")
    }        
	
    if(Test-Path $MSNetFrameworkLocation){    
        $msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
    }else{
        throw "Build directory for .NET 4 Beta 2/RC does not exist [$MSNetFrameworkLocation]"
    }
    
    
    #set the environment variables and make sure this hasn't already been done.
    if(!$env:DevEnvDir){
        .\vsvars2010.ps1
    }

    Function BuildModule($moduleToBuildPath){
                
        if(Test-Path $moduleToBuildPath){            
            if($getDependencies){
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp" "/p:DropLocation=$dropRoot"
            }
            else{
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp"
            }     
            write "Built $moduleToBuildPath"
        }else{
            $cannotResolvePath = "cannot resolve path [$moduleToBuildPath]"   
            write $cannotResolvePath
            throw              
        }
    }
}   
   
process{
    BuildModule($moduleToBuildPath)
}    
