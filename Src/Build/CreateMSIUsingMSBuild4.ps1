<# 
.Synopsis 
    Used to create an MSI via devenv
.Description    
    Create an MSI for a valid module
.Parameter $moduleSolution is the path and solution name of the solution to create the MSI for
.Parameter $moduleDirectory the directory the solution we a referencing, the build log will be here 
.Parameter $buildType type of build in the format of $(BuildFlavour)|$(Platform)
#>
param(         
    [string]$moduleSolution = $(if(!$moduleSolution){Throw "Parameter missing -moduleSolution moduleSolution" }),
    [string]$moduleDirectory = $(if(!$moduleDirectory){Throw "Parameter missing -moduleDirectory moduleDirectory" }),
    [string]$buildType = $(if(!$buildType){Throw "Parameter missing -buildType buildType" })        
)

begin{        
    $visualStudioIDEPath = Join-Path -Path (Get-ChildItem Env:VS100COMNTOOLS).value -ChildPath "..\IDE\"
	
    if(!(Test-Path $visualStudioIDEPath)){            
        throw "devenv.com path not found at [$visualStudioIDEPath]"
    }
        
    Function CreateMSI($solution,$buildType){   
    
        $buildLog = Join-Path -Path $moduleDirectory -ChildPath CreateMSI.log
        if(Test-Path $buildLog){
          Remove-Item -Path $buildLog -Force
        }    
       
        pushd $visualStudioIDEPath
        .\devenv.com $solution /rebuild  | Out-Null
        popd
        write "created MSI $moduleToBuildPath"
    }
}
   
process{
    CreateMSI $moduleSolution $buildType
}    
