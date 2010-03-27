##
# Used by server build to invoke MSBuild version 4 to publish a click once project
##
param(         
    [string]$pathForProjectToPublish = $(if(!$pathForProjectToPublish){Throw "Parameter missing -pathForProjectToPublish pathForProjectToPublish" }),
    [string]$assemblyVersion = $(if(!$assemblyVersion){Throw "Parameter missing -assemblyVersion assemblyVersion" })
)

begin{        
    $MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30128\")  
	
    if(Test-Path $MSNetFrameworkLocation){    
        $msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
    }else{
        throw "Build directory for .NET 4 Beta 2 does not exist [$MSNetFrameworkLocation]"
    }

    Function Publish($assemblyVersion, $pathForProjectToPublish){                                                              
        &$MSBuild "/target:publish /property:ApplicationVersion=$assemblyVersion $pathForProjectToPublish"
    }
}
   
process{
    Publish $assemblyVersion $pathForProjectToPublish
} 