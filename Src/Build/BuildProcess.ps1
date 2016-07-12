#[CmdletBinding()]
param(
    $Configuration = 'Release',
    $Repository
)

Enum Result {
 Succeeded
 SucceededWithIssues
 Failed
 Cancelled
 Skipped
}

Enum State {
 Unknown
 Initialized
 InProgress
 Completed
}

class LogDetail {
    [Guid]$id = [Guid]::NewGuid()
    [datetime]$startTime
    [datetime]$finishTime
    [State]$state
    [Result]$result

    LogDetail([string]$message){ 
        $this.start($message)
    }

    [void] Start([string]$message) {
        $this.startTime = [DateTime]::UtcNow
        $this.state = [State]::InProgress
        $this.Log($message)
    }

    [void] Finish([string]$message, [Result]$result) {   
                
        $this.finishTime = [DateTime]::UtcNow
        $this.state = [State]::Completed
        $this.result = $result
        $this.Log($message)
    }

    [void] Log([string]$message) {
        $stateText = $this.state.ToString()

        if ($this.state -eq [State]::InProgress) {
            Write-Host ("##vso[task.logdetail id=$($this.id);type=build;name=$message;order=1;starttime=$($this.startTime);state=$stateText;]")
            return
        }

        if ($this.state -eq [State]::Completed) {
            $resultText = $this.result.ToString()

            Write-Host ("##vso[task.logdetail id=$($this.id);finishtime=$($this.finishTime);result=$resultText;state=$stateText]$message")        
        }        
    }  
}

$MSBuildLocation = ${Env:ProgramFiles(x86)} + "\MSBuild\14.0\Bin\"
use -Path $MSBuildLocation -Name MSBuild

$global:IsDesktopBuild = $Env:BUILD_BUILDURI -eq $null

$dropLocation = "\\dfs.aderant.com\ExpertSuite\Dev\FrameworkNext"

task Package -Jobs Init, Clean, GetDependencies, Build, Test, CopyToDrop, {
    $step = New-Object LogDetail "Get dependencies" 

    . $Env:EXPERT_BUILD_FOLDER\Build\Package.ps1 -Repository $Repository

    $step.Finish("Done", [Result]::Succeeded)
}

task GetDependencies {    
    $step = New-Object LogDetail "Get dependencies" 

    . $Env:EXPERT_BUILD_FOLDER\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath $dropLocation

    $step.Finish("Done", [Result]::Succeeded)
}

task Build {
    $step = New-Object LogDetail "Build" 

    exec {        
        if ($Env:AGENT_HOMEDIRECTORY) {
            $loggerAssembly = "$Env:AGENT_HOMEDIRECTORY\Agent\Worker\Microsoft.TeamFoundation.DistributedTask.MSBuild.Logger.dll"
            $logger = "/dl:CentralLogger,`"$loggerAssembly`"*ForwardingLogger,`"$loggerAssembly`""
        }        
        
        MSBuild $Env:EXPERT_BUILD_FOLDER\Build\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository $logger
    }

     $step.Finish("Done", [Result]::Succeeded)
}

task Clean {    
}

task Test {
}

task CopyToDrop -If (-not $IsDesktopBuild) {
    $step = New-Object LogDetail "CopyToDrop" 

    $text = Get-Content $Repository\Build\CommonAssemblyInfo.cs -Raw
    $text -match '(?m)(AssemblyFileVersion\(\")(?<version>[0-9]*.[0-9]*.[0-9]*.[0-9]*)' | Out-Null    
    $version = $Matches.version

    $text = Get-Content $Repository\Build\TFSBuild.rsp -Raw
    $text -match 'ModuleName=(?<name>[^"]+)' | Out-Null    
    $name = $Matches.name    
    
    . $Env:EXPERT_BUILD_FOLDER\Build\CopyToDrop.ps1 -moduleRootPath $Repository -dropRootUNCPath $dropLocation\$name\1.8.0.0 -assemblyFileVersion $version

    $fullDropPath = "$dropLocation\$moduleName\1.8.0.0\$version"

    # Associate the drop back to the build
    Write-Host "##vso[artifact.associate type=filepath;artifactname=drop]$fullDropPath"

    $step.Finish("Done", [Result]::Succeeded)
}

task Init {
    . $Env:EXPERT_BUILD_FOLDER\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\    
    LoadLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\

    Write-Info "Build tree"
    .\Show-BuildTree.ps1 -File $PSCommandPath

    Write-Info "Established build environment"

    Write-Info ("Build URI:".PadRight(20) + $Env:BUILD_URI)
    Write-Info ("Is Desktop Build:".PadRight(20) + $IsDesktopBuild)
}

task Default Package