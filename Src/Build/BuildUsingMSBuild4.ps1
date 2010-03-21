##
# Used by server build to invoke MSBuild version 4.
# A server build is done using a 2008 build agent which uses MSBuild 3.5.  We changed the
# build script to call this script which invokes MSBuild 4 and calls a new target.
# We pass SolutionDirectoryPathForMSBuild4 to the build script so it can find the solution.
##
param(         
    [string]$moduleToBuildPath = $(if(!$moduleToBuildPath){Throw "Parameter missing -moduleToBuildPath moduleToBuildPath" }),
    [string]$buildInfrastructurePath = $(if(!$buildInfrastructurePath){Throw "Parameter missing -buildInfrastructurePath buildInfrastructurePath" })
)

begin{        
    $MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30128\")  
	
    if(Test-Path $MSNetFrameworkLocation){    
        $msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
    }else{
        throw "Build directory for .NET 4 Beta 2 does not exist [$MSNetFrameworkLocation]"
    }

    Function BuildModule($moduleToBuildPath){                                                              
        &$MSBuild "$buildInfrastructurePath\ModuleBuild.proj" "/t:BuildUsingMSBuild4" "@$moduleToBuildPath\Build\TFSBuild.rsp" "/p:SolutionDirectoryPathForMSBuild4=$moduleToBuildPath"        
        write "Built $moduleToBuildPath"
    }
}
   
process{
    BuildModule($moduleToBuildPath)
}    
