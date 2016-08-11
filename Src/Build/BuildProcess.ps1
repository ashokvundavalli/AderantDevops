param(
    [string]$Repository,
    [string]$Configuration = 'Release',    
    [string]$Platform = "AnyCPU",  
    [bool]$Clean,
    [bool]$LimitBuildWarnings
)

$EntryPoint = Get-Variable "BuildTask"

# The VsTsTaskSdk specifies a prefix of Vsts. Thus commands are renamed from what appears in the source under ps_modules.
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

    LogDetail() {         
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

function WarningRatchet($vssConnection, $teamProject, $buildId, $buildDefinitionId) {
    $ratchet = New-Object Aderant.Build.Tasks.WarningRatchet -ArgumentList $vssConnection
    $currentBuildCount = $ratchet.GetBuildWarningCount($teamProject, [int]$buildId)
    $lastGoodBuild = $ratchet.GetLastGoodBuildWarningCount($teamProject, [int]$buildDefinitionId)

    if ($lastGoodBuild) {
        RenderWarningSummary $currentBuildCount $lastGoodBuild
        
        if ($currentBuildCount -gt $lastGoodBuild) {
            RenderWarningShields $true $currentBuildCount $lastGoodBuild
            $ratchet.DiffWarnings($teamProject, [int]$buildDefinitionId, [int]$buildId)

            throw "Warning count has increased since the last good build"
        }
        RenderWarningShields $false $currentBuildCount $lastGoodBuild
    }
}

function RenderWarningSummary([int]$this, [int]$last) {
    Write-Host (New-Object string -ArgumentList '*', 80)
    Write-Host "=== Warning Summary ===" 
    Write-Host "Last good build warnings: $last"
    Write-Host "Current build warnings: $this"
    Write-Host "=== Warning Summary ===" 
    Write-Host (New-Object string -ArgumentList '*', 80)
}

function RenderWarningShields([bool]$inError, [int]$this, [int]$last) {
    $stream = [System.IO.StreamWriter] "$env:SYSTEM_DEFAULTWORKINGDIRECTORY\Warnings.md"
    
    $lastGoodShield = Get-Content -Raw -Path $PSScriptRoot\Resources\last-good-build.svg
    $lastGoodShield = $lastGoodShield -f $last

    if ($inError) {
        $thisBuildShield = Get-Content -Raw -Path $PSScriptRoot\Resources\this-build-bad.svg
    } else {
        $thisBuildShield = Get-Content -Raw -Path $PSScriptRoot\Resources\this-build-good.svg
    }

    $thisBuildShield = $thisBuildShield -f $this
    
    $stream.WriteLine($lastGoodShield)
    $stream.WriteLine($thisBuildShield)
    $stream.Close()
        
    Write-Host "##vso[task.addattachment type=Distributedtask.Core.Summary;name=Build Warnings;]$env:SYSTEM_DEFAULTWORKINGDIRECTORY\Warnings.md"
}

function BuildAssociation($vssConnection, $teamProject, $buildId) {
    $logger = New-Object Aderant.Build.Logging.PowerShellLogger -ArgumentList $Host    
    
    $association = New-Object Aderant.Build.Tasks.BuildAssociation -ArgumentList $logger,$vssConnection

    Write-Output "Associating work items to build: $teamProject/$buildId"
    $association.AssociateWorkItemsToBuild($teamProject, [int]$buildId)
}

#=================================================================================================
# Synopsis: Performs a incremental build of the Visual Studio Solution if possible.
# Applies a common build number, executes unit tests and packages the assemblies as a NuGet 
# package
#=================================================================================================
task EndToEnd -Jobs Init, Clean, GetDependencies, BuildCore, Test, Package, {    
}

task PostBuild -Jobs Init, Package, CopyToDrop, {   
}

task GetDependencies {
    if (-not $IsDesktopBuild) {
        . $Env:EXPERT_BUILD_DIRECTORY\Build\LoadDependencies.ps1 -modulesRootPath $Repository -dropPath $dropLocation
    }
}

task Build {
    exec {
        # Don't show the logo and do not allow node reuse so all child nodes are shut down once the master
        # node has completed build orchestration.
        $commonArgs = "/nologo /nr:false /m"       
        $commonArgs = "$commonArgs $Repository\Build\TFSBuild.proj @$Repository\Build\TFSBuild.rsp"

        if (-not $Repository.EndsWith("\")) {
            $Repository += "\"
        }

        if (-not $Env:EXPERT_BUILD_DIRECTORY.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
            $commonArgs = "$commonArgs /p:EXPERT_BUILD_DIRECTORY=$Env:EXPERT_BUILD_DIRECTORY\"
        }

        $commonArgs = "$commonArgs /p:SolutionRoot=$Repository"
        $commonArgs = "$commonArgs /p:IsDesktopBuild=$global:IsDesktopBuild"

        if ($Clean) {
            $commonArgs = "$commonArgs /p:CleanBin=true"
        }

        # /p:RunWixToolsOutOfProc=true is required due to this bug with stdout processing 
        # https://connect.microsoft.com/VisualStudio/feedback/details/1286424/
        $commonArgs = "$commonArgs /p:RunWixToolsOutOfProc=true"

        try {
            pushd $Repository
             
            if ($IsDesktopBuild) {
                Start-Process -FilePath $MSBuildLocation\MSBuild.exe -ArgumentList $commonArgs -Wait -NoNewWindow
            } else {
                . $Env:EXPERT_BUILD_DIRECTORY\Build\InvokeServerBuild.ps1 -Repository $Repository -MSBuildLocation $MSBuildLocation -CommonArgs $commonArgs
            }
        } finally {
            popd
        }
    }  
}

task BuildCore (job Build -Safe), {
   # This task always runs after Build

   # TODO:
   # http://tfs:8080/tfs/Aderant/ExpertSuite/_apis/test/codeCoverage?buildId=630576&flags=1&api-version=2.0-preview
      
   if (-not $IsDesktopBuild) {
       # We always want to try publish test results as a test failure might be the cause of the build failure and so 
       # we want to see the test results on the TFS dashboard for future analysis
       $vssConnection = GetVssConnection

       # Fucking PowerShell. On a desktop OS the implicit conversion to string[] picks "FullName", on the build box it picks "Name" which
       # fucks everything up as the data that gets piped to the ResultPublisher doesn't have the directory info...so we have to explicit
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

   # Test for a failure from the Build task and re-throw to fail the build
   $error = Get-BuildError Build
   if ($error) {
       throw $error
   }
}

#=================================================================================================
# Synopsis: Does what msbuild/VS can't do consistently.  
# Aggressively and recursively deletes all /obj and /bin folders from the build path as well as the output folder.
#=================================================================================================
task Clean {
}

task Test {   
}

task Quality -If (-not $IsDesktopBuild) {
    $vssConnection = GetVssConnection

    $buildId = (Get-VstsTaskVariable -Name 'Build.BuildId' -Require -AsInt)
    $buildDefinitionId = (Get-VstsTaskVariable -Name 'System.DefinitionId' -Require -AsInt) 
    $teamProject = (Get-VstsTaskVariable -Name 'System.TeamProject' -Require)   

    if ($LimitBuildWarnings) {
        WarningRatchet $vssConnection $teamProject $buildId $buildDefinitionId
    }
    
    # TODO: Decide on what we want here
    #BuildAssociation $vssConnection $teamProject $buildId       
}

task CopyToDrop -If (-not $IsDesktopBuild) {
    $text = Get-Content $Repository\Build\CommonAssemblyInfo.cs -Raw
    $text -match '(?m)(AssemblyFileVersion\(\")(?<version>[0-9]*.[0-9]*.[0-9]*.[0-9]*)' | Out-Null    
    $version = $Matches.version

    $text = Get-Content $Repository\Build\TFSBuild.rsp -Raw
    $text -match 'ModuleName=(?<name>[^"]+)' | Out-Null    
    $name = $Matches.name    
    
    . $Env:EXPERT_BUILD_DIRECTORY\Build\CopyToDrop.ps1 -moduleRootPath $Repository -dropRootUNCPath $dropLocation\$name\1.8.0.0 -assemblyFileVersion $version

    $fullDropPath = "$dropLocation\$name\1.8.0.0\$version"

    # Associate the drop back to the build
    Write-Host "##vso[artifact.associate type=filepath;artifactname=drop]$fullDropPath"    
}


task PackageDesktop -If ($global:IsDesktopBuild) {
    $script:CreatePackage = $true
}

task PackageServer -If (-not $global:IsDesktopBuild -and $script:EntryPoint.Value -eq "PostBuild") -Jobs Quality, {
    $script:CreatePackage = $true

}

task Package -Jobs Init, PackageDesktop, PackageServer, { 
    if ($script:CreatePackage) {
        Write-Output "Entry point was: $($script:EntryPoint.Value)"
       
        . $Env:EXPERT_BUILD_DIRECTORY\Build\Package.ps1 -Repository $Repository    
    }
}

task Init {
    . $Env:EXPERT_BUILD_DIRECTORY\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly $Env:EXPERT_BUILD_DIRECTORY\Build\    
    LoadLibraryAssembly $Env:EXPERT_BUILD_DIRECTORY\Build\

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
    $script:step = New-Object LogDetail
    $script:step.isDesktopBuild = $IsDesktopBuild
    $script:step.Start($Task.Name)
}


function Exit-BuildTask {
    if ($Task.Error) {
        Write-Output "Task `"$($Task.Name)`" has errored!"
        $script:step.Finish("Done", [Result]::Failed)        
    } else {        
        $script:step.Finish("Done", [Result]::Succeeded)
    }
}

task . EndToEnd
