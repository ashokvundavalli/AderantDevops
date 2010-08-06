##
# Used by server build to invoke MSBuild version 4 to publish a click once project
##
param(
    [string]$pathForModuleToPublish = $(if(!$pathForModuleToPublish){Throw "Parameter missing -pathForModuleToPublish pathForModuleToPublish" }),
    [string]$applicationVersion = $(if(!$applicationVersion){Throw "Parameter missing -applicationVersion applicationVersion" })
)

begin{        
    $MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30319\")
	
    if(Test-Path $MSNetFrameworkLocation){    
        $msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
    }else{
        throw "Build directory for .NET 4 RTM does not exist [$MSNetFrameworkLocation]"
    }

    Function Publish($applicationVersion, $pathForModuleToPublish){       
        [string]$publishCommandLine = "$MSBuild $pathForModuleToPublish /target:publish /property:ApplicationVersion=$applicationVersion"     
        Write-Debug "The MSBuild publishing command line is [$publishCommandLine]"   
        Invoke-Expression $publishCommandLine
    }
}
   
process{
    Publish $applicationVersion $pathForModuleToPublish
} 