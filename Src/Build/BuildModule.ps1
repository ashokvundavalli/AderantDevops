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
    # Set the environment variables and make sure this hasn't already been done.
    if (!$env:DevEnvDir) {
        .\vsvars.ps1
    }

    $MSNetFrameworkLocation = ((Get-ChildItem Env:windir).value + "\Microsoft.NET\Framework\v4.0.30319\")
    
    $MSBuildLocation = ${env:ProgramFiles(x86)} + "\MSBuild\$Env:VisualStudioVersion\Bin\"

    if (Test-Path $MSBuildLocation) {    
        $msbuild = $MSBuildLocation + "msbuild.exe"    
        $ToolsVersion = "/tv:" + $Env:VisualStudioVersion
        $VsVersion = "/p:VisualStudioVersion=" + $Env:VisualStudioVersion    
    } else {
        if (Test-Path $MSNetFrameworkLocation) {    
            $msbuild = $MSNetFrameworkLocation + "msbuild.exe"    
            $ToolsVersion = "/tv:4.0"
            $VsVersion = "/p:VisualStudioVersion=11.0"    
        } else {
            throw "Build directory for .NET 4 RTM does not exist [$MSNetFrameworkLocation]"
        }
    }   


    Function BuildModule($moduleToBuildPath) {
        [Console]::TreatControlCAsInput = $false

        $arguments = "$moduleToBuildPath\Build\TFSBuild.proj @$moduleToBuildPath\Build\TFSBuild.rsp"

        if (-not [string]::IsNullOrEmpty($cleanBin) -and $cleanBin -eq "True") {
            $arguments = "$arguments /p:CleanBin=True"
        }

        if ($debug) {
            $arguments = "$arguments /p:BuildFlavor=Debug"
        }

        if ($getDependencies) {
            $arguments = "$arguments /p:DropLocation=$dropRoot"
        }

        $arguments = "$arguments $ToolsVersion $VsVersion"

        $arguments = "$arguments /p:SolutionRoot=$moduleToBuildPath"         

        $arguments = "$arguments /nologo /m /nr:false"
                        
        if (Test-Path $moduleToBuildPath) {
            [System.Environment]::SetEnvironmentVariable("BuildScriptsDirectory", "$global:BuildScriptsDirectory\", [System.EnvironmentVariableTarget]::Process)

            Start-Process -FilePath $MSBuild -ArgumentList $arguments -Wait -NoNewWindow
             
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
    BuildModule $moduleToBuildPath
}    
