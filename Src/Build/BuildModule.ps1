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
    $thisDirectory = (Split-Path -Parent $MyInvocation.MyCommand.Path)

    # Set the environment variables and make sure this hasn't already been done.
    if (!$env:DevEnvDir) {
        & $thisDirectory\vsvars.ps1
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
    
    function BuildArguments() {
        if ($Env:AGENT_HOMEDIRECTORY) {
            $loggerAssembly = "$Env:AGENT_HOMEDIRECTORY\Agent\Worker\Microsoft.TeamFoundation.DistributedTask.MSBuild.Logger.dll"
            return $arguments = "$arguments /dl:CentralLogger,`"$loggerAssembly`"*ForwardingLogger,`"$loggerAssembly`""
        }        
    }


    function BuildModule($moduleToBuildPath) {
        $cleanCommand = $null
        if (-not [string]::IsNullOrEmpty($cleanBin) -and $cleanBin -eq "True") {
            $cleanCommand = "/p:CleanBin=True"
        }

        $debugCommand = $null
        if ($debug) {
            $debugCommand = "/p:BuildFlavor=Debug"
        }

        $arguments = BuildArguments
                
        if (Test-Path $moduleToBuildPath) {            
            if ($getDependencies) {
                &$MSBuild "$moduleToBuildPath\Build\TFSBuild.proj" "@$moduleToBuildPath\Build\TFSBuild.rsp" "/p:DropLocation=$dropRoot" $cleanCommand $debugCommand $ToolsVersion $VsVersion $arguments
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
