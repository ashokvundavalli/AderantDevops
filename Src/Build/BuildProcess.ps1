param(
    $Configuration = 'Release',
    [string]$Repository
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
    [bool]$isDesktopBuild

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

        if ($this.isDesktopBuild) {
            return
        }        

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

task EndToEnd -Jobs Init, Clean, GetDependencies, Build, Test, Package, CopyToDrop, {
}

task Package -Jobs Init,  {
    . $Env:EXPERT_BUILD_FOLDER\Build\Package.ps1 -Repository $Repository
}

task GetDependencies {
    . $Env:EXPERT_BUILD_FOLDER\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath $dropLocation
}

task Build {
    exec {        
        if ($Env:AGENT_HOMEDIRECTORY) {
            $loggerAssembly = "$Env:AGENT_HOMEDIRECTORY\agent\Worker\Microsoft.TeamFoundation.DistributedTask.MSBuild.Logger.dll"
            $logger = "/dl:CentralLogger,`"$loggerAssembly`"*ForwardingLogger,`"$loggerAssembly`""
        }        
        
        # /p:RunWixToolsOutOfProc=true is required due to bug 
        # https://connect.microsoft.com/VisualStudio/feedback/details/1286424/
        MSBuild $Env:EXPERT_BUILD_FOLDER\Build\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository $logger /nologo /p:RunWixToolsOutOfProc=true
    }
}

task Clean {    
}

task Test {
    if (-not $IsDesktopBuild) {
        [System.Reflection.Assembly]::LoadFrom("$Env:AGENT_HOMEDIRECTORY\agent\Worker\Microsoft.TeamFoundation.DistributedTask.Agent.Interfaces.dll")        

        . $Env:AGENT_HOMEDIRECTORY\tasks\PublishTestResults\1.0.22\PublishTestResults.ps1 -testRunner "VSTest" -testResultsFiles "**/*.trx" -mergeTestResults $true -publishRunAttachments $true
    }
}

task CopyToDrop -If (-not $IsDesktopBuild) {
    $text = Get-Content $Repository\Build\CommonAssemblyInfo.cs -Raw
    $text -match '(?m)(AssemblyFileVersion\(\")(?<version>[0-9]*.[0-9]*.[0-9]*.[0-9]*)' | Out-Null    
    $version = $Matches.version

    $text = Get-Content $Repository\Build\TFSBuild.rsp -Raw
    $text -match 'ModuleName=(?<name>[^"]+)' | Out-Null    
    $name = $Matches.name    
    
    . $Env:EXPERT_BUILD_FOLDER\Build\CopyToDrop.ps1 -moduleRootPath $Repository -dropRootUNCPath $dropLocation\$name\1.8.0.0 -assemblyFileVersion $version

    $fullDropPath = "$dropLocation\$name\1.8.0.0\$version"

    # Associate the drop back to the build
    Write-Host "##vso[artifact.associate type=filepath;artifactname=drop]$fullDropPath"    
}

task Init {
    . $Env:EXPERT_BUILD_FOLDER\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\    
    LoadLibraryAssembly $Env:EXPERT_BUILD_FOLDER\Build\

    Write-Info "Build tree"
    .\Show-BuildTree.ps1 -File $PSCommandPath

    Write-Info "Established build environment"

    Write-Info ("Build URI:".PadRight(20) + $Env:BUILD_BUILDURI)
    Write-Info ("Is Desktop Build:".PadRight(20) + $IsDesktopBuild)

    if (-not $IsDesktopBuild) {        
        $agentWorkerModulesPath = "$($env:AGENT_HOMEDIRECTORY)\agent\worker\Modules"
        $agentDistributedTaskInternalModulePath = "$agentWorkerModulesPath\Microsoft.TeamFoundation.DistributedTask.Task.Internal\Microsoft.TeamFoundation.DistributedTask.Task.Internal.dll"
        $agentDistributedTaskCommonModulePath = "$agentWorkerModulesPath\Microsoft.TeamFoundation.DistributedTask.Task.Common\Microsoft.TeamFoundation.DistributedTask.Task.Common.dll"
   
        Write-Host "Importing VSTS Module $agentDistributedTaskInternalModulePath"
        Import-Module $agentDistributedTaskInternalModulePath
    
        Write-Host "Importing VSTS Module $agentDistributedTaskCommonModulePath"
        Import-Module $agentDistributedTaskCommonModulePath
    }
}

function Enter-BuildTask {    
    $script:step = New-Object LogDetail $Task.Name
    $script:step.isDesktopBuild = $IsDesktopBuild    
}

function Exit-BuildTask {
    if ($Task.Error) {
        Write-Host "Task `"$($Task.Name)`" has errored!" -ForegroundColor Red
        $script:step.Finish("Done", [Result]::Failed)        
    } else {        
        $script:step.Finish("Done", [Result]::Succeeded)        
    }
}


task Default EndToEnd