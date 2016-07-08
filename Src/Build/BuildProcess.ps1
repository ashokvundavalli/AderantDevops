[CmdletBinding()]
param(
	$Configuration = 'Release',
    $Repository
)

$MSBuildLocation = ${Env:ProgramFiles(x86)} + "\MSBuild\14.0\Bin\"
use -Path $MSBuildLocation -Name MSBuild

$dropLocation = "\\dfs.aderant.com\ExpertSuite\Dev\FrameworkNext"

function Write-Info {
    param ([string] $message)

    Write-Host "## $message ##" -ForegroundColor Magenta
}

function Write-Vso() {
    param ([string] $message)
    
    #if ($Env:BUILD_URI) {
        Write-Output $message
    #}
}

function Start-BuildStep {
    param ([string] $message)   
    [string]$g = [Guid]::NewGuid()

    Write-Vso "##vso[task.logdetail id=$g;name=project1;type=build;order=1]$message"

    return $g
}

function End-BuildStep {
    param ([string] $message,
    [ValidateSet('Succeeded','SucceededWithIssues','Failed','Cancelled','Skipped')] $state)

    Write-Vso "##vso[task.complete result=$state;]$message"    
}

task Package -Jobs Init, Clean, GetDependencies, Build, Test, CopyToDrop, {
    
}

task GetDependencies {    
    #& $Env:EXPERT_BUILD_FOLDER\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath "\\na.aderant.com\ExpertSuite\Main"
}

task Build {

    exec {        
        if ($Env:AGENT_HOMEDIRECTORY) {
            $loggerAssembly = "$Env:AGENT_HOMEDIRECTORY\Agent\Worker\Microsoft.TeamFoundation.DistributedTask.MSBuild.Logger.dll"
            $logger = "/dl:CentralLogger,`"$loggerAssembly`"*ForwardingLogger,`"$loggerAssembly`""
        }        
        
        #MSBuild $Env:EXPERT_BUILD_FOLDER\Build\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository $logger
    }

}

task Clean {    
}

task Test {
}

task CopyToDrop {
    Start-BuildStep "CopyToDrop"

    $text = Get-Content $Repository\Build\CommonAssemblyInfo.cs -Raw
    $text -match '(?m)(AssemblyFileVersion\(\")(?<version>[0-9]*.[0-9]*.[0-9]*.[0-9]*)' | Out-Null    
    $version = $Matches.version

    $text = Get-Content $Repository\Build\TFSBuild.rsp -Raw
    $text -match 'ModuleName=(?<name>[^"]+)' | Out-Null    
    $name = $Matches.name    
    
    #& $Env:EXPERT_BUILD_FOLDER\Build\CopyToDrop.ps1 -moduleRootPath $Repository -dropRootUNCPath $dropLocation\$name\1.8.0.0 -assemblyFileVersion $version

    $fullDropPath = "$dropLocation\$moduleName\1.8.0.0\$version"

    # Associate the drop back to the build
    Write-Vso "##vso[artifact.associate type=filepath;artifactname=drop]$fullDropPath"

    End-BuildStep -message "CopyToDrop completed" -state Succeeded    
}

task Init {
    Write-Info 'Establishing build properties'    

    . $Env:EXPERT_BUILD_FOLDER\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\
}

task Default Package