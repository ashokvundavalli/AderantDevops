param(
    [string]$Repository,
    [string]$Configuration = 'Release',    
    [string]$Platform = "AnyCPU"     
)

# THe VsTsTaskSdk specifies a prefix of Vsts. Thus commands are renamed from what appears in the source under ps_modules.
# eg Invoke-Tool becomes Invoke-VstsTool

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

function GetVssConnection() {
   $endpoint = Get-VstsEndpoint -Name "SystemVssConnection" -Require
   $serviceEndpoint = new-object Microsoft.TeamFoundation.DistributedTask.WebApi.ServiceEndpoint
   $serviceEndpoint.Url = [System.Uri]$endpoint.Url

   $vssConnection = [Microsoft.TeamFoundation.DistributedTask.Agent.Common.CredentialsExtensions]::GetVssConnection($serviceEndpoint)
   return $vssConnection
}

task EndToEnd -Jobs Init, Clean, GetDependencies, Build, Test, {
}

task PostBuild -Jobs Init, Quality, Package, CopyToDrop, {   
}

task Package -Jobs Init,  {
    #. $Env:EXPERT_BUILD_FOLDER\Build\Package.ps1 -Repository $Repository
}

task GetDependencies {
    . $Env:EXPERT_BUILD_FOLDER\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath $dropLocation
}

task Build {
    exec { 
        #try {
        #    $detailId = [guid]::NewGuid()
        #    #$detailName = Get-VstsLocString -Key MSB_Build0 -ArgumentList ([System.IO.Path]::GetFileName($ProjectFile))
        #    $detailStartTime = [datetime]::UtcNow.ToString('O')
        #    Write-VstsLogDetail -Id $detailId -Type Process -Name "Foo" -Progress 0 -StartTime $detailStartTime -State Initialized -AsOutput

        #    # /p:RunWixToolsOutOfProc=true is required due to bug 
        #    # https://connect.microsoft.com/VisualStudio/feedback/details/1286424/
        #    MSBuild $Env:EXPERT_BUILD_FOLDER\Build\ModuleBuild2.targets @$Repository\Build\TFSBuild.rsp /p:BuildRoot=$Repository $logger /nologo /p:RunWixToolsOutOfProc=true
        #    #Invoke-Tool 
        #} finally {
        #    # TODO: Failed handling
        #    $detailFinishTime = [datetime]::UtcNow.ToString('O')
        #    Write-VstsLogDetail -Id $detailId -FinishTime $detailFinishTime -Progress 100 -State Completed -Result $detailResult -AsOutput
        #}

        . $Env:EXPERT_BUILD_FOLDER\Build\InvokeServerBuild.ps1 -Repository $Repository -MSBuildLocation $MSBuildLocation
    }
}

task Clean {    
}

task Test -Jobs Init, {
   # http://tfs:8080/tfs/Aderant/ExpertSuite/_apis/test/codeCoverage?buildId=630576&flags=1&api-version=2.0-preview
   
   $vssConnection = GetVssConnection

   # Fucking PowerShell. On a desktop OS the implicit conversion to string[] picks FullName, on the build box it picks Name which
   # fucks everything up as the data that gets passed to the ResultPublisher doesn't have the directory info attached, so we have to wrangle it ourselves
   $testResults = gci -Path "$Repository\TestResults" -Filter "*.trx" -Recurse | Select-Object -ExpandProperty FullName 

   if ($testResults) {
        # Bug in Invoke-ResultPublisher, no one subscribes to LogVerbose which throws a NullReferenceException since there is no null check 
        # before raising the event
        $logger = [Microsoft.TeamFoundation.DistributedTask.Task.TestResults.Logger]
        $job = Register-ObjectEvent -inputObject $logger -eventName LogVerbose -Action { Write-Verbose $_ }

        $buildId = (Get-VstsTaskVariable -Name 'Build.BuildId' -Require)
        $buildUri = (Get-VstsTaskVariable -Name 'Build.BuildUri' -Require)
        $owner = (Get-VstsTaskVariable -Name 'Build.RequestedFor' -Require)
        $project = (Get-VstsTaskVariable -Name 'System.TeamProject' -Require)

        Write-Output "Build Number: $buildId"
        Write-Output "Build Uri: $buildUri"        

        Invoke-ResultPublisher -BuildNumber $buildId -BuildUri $buildUri -Connection $vssConnection -ProjectName $project -resultFiles $testResults -ResultType "Trx" -Owner $owner  #-Configuration -Platform 
   }
}

task Quality -If (-not $IsDesktopBuild) {
    $buildId = (Get-VstsTaskVariable -Name 'Build.BuildId' -Require)
    $buildDefinitionId = (Get-VstsTaskVariable -Name 'System.DefinitionId' -Require) 
    $teamProject = (Get-VstsTaskVariable -Name 'System.TeamProject' -Require)
    
    $vssConnection = GetVssConnection
        
    $ratchet = New-Object Aderant.Build.Tasks.WarningRatchet -ArgumentList $vssConnection
    $currentBuildCount = $ratchet.GetBuildWarningCount($teamProject, [int]$buildId)
    $lastGoodBuild = $ratchet.GetLastGoodBuildWarningCount($teamProject, [int]$buildDefinitionId)

    Write-Output "Last good build warnings: $lastGoodBuild"
    Write-Output "Current warnings: $currentBuildCount"

    if ($currentBuildCount -gt $lastGoodBuild) {
        throw "Warning count has increased since the last good build"
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
    
    Write-Info ("Build Uri:".PadRight(20) + $Env:BUILD_BUILDURI)
    Write-Info ("Is Desktop Build:".PadRight(20) + $IsDesktopBuild)

    if (-not $IsDesktopBuild) {        
        $agentWorkerModulesPath = "$($env:AGENT_HOMEDIRECTORY)\agent\worker\Modules"

        $modules = @("$agentWorkerModulesPath\Microsoft.TeamFoundation.DistributedTask.Task.Common\Microsoft.TeamFoundation.DistributedTask.Task.Common.dll",
                     "$agentWorkerModulesPath\Microsoft.TeamFoundation.DistributedTask.Task.Internal\Microsoft.TeamFoundation.DistributedTask.Task.Internal.dll",                      
                     "$agentWorkerModulesPath\Microsoft.TeamFoundation.DistributedTask.Task.TestResults\Microsoft.TeamFoundation.DistributedTask.Task.TestResults.dll")      
                  
        $files = gci -Path "$Env:AGENT_HOMEDIRECTORY\agent\worker\" -Filter "*.dll"
        foreach ($file in $files) {
            try {
                [System.Reflection.Assembly]::LoadFrom($file.FullName)| Out-Null
            } catch {                
            }
        }

        foreach ($module in $modules) {
            Import-Module $module
        }       
    }

    Write-Info "Established build environment"
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