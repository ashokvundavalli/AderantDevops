[CmdletBinding()]
param(
	$Configuration = 'Release',
    $Repository
)

$MSBuildLocation = ${Env:ProgramFiles(x86)} + "\MSBuild\14.0\Bin\"
use -Path $MSBuildLocation -Name MSBuild

function Write-Info {
    param ([string] $Message)

    Write-Host "## $Message ##" -ForegroundColor Magenta
}

task Package Init, Clean, GetDependencies, Build, Test, {

}

task GetDependencies {    
    . $Env:EXPERT_BUILD_FOLDER\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath "\\na.aderant.com\ExpertSuite\Main"
}

task Build {

    exec {
        MSBuild $Env:EXPERT_BUILD_FOLDER\Build\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository
    }

}

task Clean {    
}

task Test {
}

task Init {
    Write-Info 'Establishing build properties'    

    . $Env:EXPERT_BUILD_FOLDER\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\
}

task Default Package