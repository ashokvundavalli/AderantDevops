[CmdletBinding()]
param(
	$Configuration = 'Release',
    $Repository
)

function Write-Info {
    param ([string] $Message)

    Write-Host "## $Message ##" -ForegroundColor Magenta
}

task Package Init, Clean, GetDependencies, Build, Test, {

}

task GetDependencies {
    . $Env:EXPERT_BUILD_FOLDER\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath "\\na.aderant.com\ExpertSuite\Main"
}

task Build {

    exec {
        MSBuild $Env:EXPERT_BUILD_FOLDER\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository
    }

}

task Clean {    
}

task Test {
}

task Init {
    Write-Info 'Establishing build properties'

    $MSBuildLocation = ${Env:ProgramFiles(x86)} + "\MSBuild\14.0\Bin\"

    use -Path $MSBuildLocation -Name MSBuild    
}

task Default Package