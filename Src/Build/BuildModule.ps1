##
# Build a single module, optional get the modules dependencies from the drop location
##
param(         
    [string]$moduleToBuildPath = $(if(!$moduleToBuildPath){Throw "Parameter missing -moduleToBuildPath moduleToBuildPath" }),
    [bool]$getDependencies = $false, 
    $cleanBin,
    [switch]$debug,
    [string]$dropRoot = $(if($getDependencies) {Throw "Parameter missing -dropRoot dropRoot" })    
)

begin {
	$MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30319\")
	
	$MSBuildLocation = ${env:ProgramFiles(x86)} + "\MSBuild\12.0\Bin\"

    if (Test-Path $MSBuildLocation) {    
        $msbuild = $MSBuildLocation + "msbuild.exe"    
   		$ToolsVersion = "/tv:12.0"
		$VsVersion = "/p:VisualStudioVersion=12.0"    
    } else {
    	if (Test-Path $MSNetFrameworkLocation) {    
			$msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
			$ToolsVersion = "/tv:4.0"
			$VsVersion = "/p:VisualStudioVersion=11.0"    
		} else {
			throw "Build directory for .NET 4 RTM does not exist [$MSNetFrameworkLocation]"
	    }
	}

    
    #set the environment variables and make sure this hasn't already been done.
    if (!$env:DevEnvDir) {
        .\vsvars.ps1
    }

    Function BuildModule($moduleToBuildPath) {
        $cleanCommand = $null
        if (-not [string]::IsNullOrEmpty($cleanBin) -and $cleanBin -eq "True") {
            $cleanCommand = "/p:CleanBin=True"
        }

        $debugCommand = $null
        if ($debug) {
            $debugCommand = "/p:BuildFlavor=Debug"
        }
                
        if (Test-Path $moduleToBuildPath) {            
            if ($getDependencies) {
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp" "/p:DropLocation=$dropRoot" $cleanCommand $debugCommand $ToolsVersion $VsVersion
            }
            else {
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp" $cleanCommand $debugCommand $ToolsVersion $VsVersion
            } 
            $date = Get-Date -format t    
            write "Built $moduleToBuildPath"
            write "Build completed at: $date" 
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
