[CmdletBinding()]
param(
	$Configuration = 'Release',
    $Repository
)

$MSBuildLocation = ${Env:ProgramFiles(x86)} + "\MSBuild\14.0\Bin\"
use -Path $MSBuildLocation -Name MSBuild

$dropLocation = "\\dfs.aderant.com\ExpertSuite\Dev\FrameworkNext"

function Write-Info {
    param ([string] $Message)

    Write-Host "## $Message ##" -ForegroundColor Magenta
}

task Package -Jobs Init, Clean, GetDependencies, Build, Test, CopyToDrop, {
    
}

task GetDependencies {    
    & $Env:EXPERT_BUILD_FOLDER\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath "\\na.aderant.com\ExpertSuite\Main"
}

task Build {

    exec {        
        if ($Env:AGENT_HOMEDIRECTORY) {
            $loggerAssembly = "$Env:AGENT_HOMEDIRECTORY\Agent\Worker\Microsoft.TeamFoundation.DistributedTask.MSBuild.Logger.dll"
            $logger = "/dl:CentralLogger,`"$loggerAssembly`"*ForwardingLogger,`"$loggerAssembly`""
        }        
        
        MSBuild $Env:EXPERT_BUILD_FOLDER\Build\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository $logger
    }

}

task Clean {    
}

task Test {
}

task CopyToDrop {    
    $text = Get-Content $Repository\Build\CommonAssemblyInfo.cs -Raw
    $text -match '(?m)(AssemblyFileVersion\(\")(?<version>[0-9]*.[0-9]*.[0-9]*.[0-9]*)' | Out-Null    
    $version = $Matches.version

    $text = Get-Content $Repository\Build\TFSBuild.rsp -Raw
    $text -match 'ModuleName=(?<name>[^"]+)' | Out-Null    
    $name = $Matches.name    
    
    & $Env:EXPERT_BUILD_FOLDER\Build\CopyToDrop.ps1 -moduleRootPath $Repository -dropRootUNCPath $dropLocation\$name\1.8.0.0 -assemblyFileVersion $version

    $fullDropPath = "$dropLocation\$moduleName\1.8.0.0\$version"

    # Associate the drop back to the build
    Write-Host "##vso[artifact.associate type=filepath;artifactname=drop]$fullDropPath"
}

task Init {
    Write-Info 'Establishing build properties'    

    . $Env:EXPERT_BUILD_FOLDER\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\
}

task Default Package