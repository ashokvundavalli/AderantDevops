param(
    [string]$Repository,
    [string]$Configuration = "Release",
    [string]$Platform = "AnyCPU",
    [bool]$Clean,
    [bool]$LimitBuildWarnings,
    [string]$Flavor,
    [switch]$DatabaseBuildPipeline,
    [bool]$CodeCoverage,
    [switch]$Integration,
    [switch]$Automation
)

[string]$inputRepository = $Repository

if (-not [string]::IsNullOrWhiteSpace($env:directoryToBuild)) {
    $Repository = "$env:BUILD_SOURCESDIRECTORY\$env:directoryToBuild"
    Write-Debug "Directory to build: $Repository"
}

$EntryPoint = Get-Variable "BuildTask"
$global:BuildFlavor = ""

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
$global:ToolsDirectory = "$PSScriptRoot\..\Build.Tools"
[System.Environment]::SetEnvironmentVariable("IsDesktopBuild", $global:IsDesktopBuild, [System.EnvironmentVariableTarget]::Process)

function GetVssConnection() {
    try {
        Write-Host "Creating VSS connection to: $($Env:SYSTEM_TEAMFOUNDATIONSERVERURI)"
        return [Microsoft.TeamFoundation.DistributedTask.Task.DevTestLabs.VssHelper]::GetVssConnection()        
    } catch {
        Write-Error "Failed to create VSS connection to: $($Env:SYSTEM_TEAMFOUNDATIONSERVERURI)"
        throw $_
    }
}

function WarningRatchet() {
    $destinationBranch = $Env:SYSTEM_PULLREQUEST_TARGETBRANCH
    $isPullRequest = ![string]::IsNullOrEmpty($destinationBranch)
    if ($true) {
        Write-Host "Running warning ratchet for $destinationBranch"
        
        $assembly = [System.Reflection.Assembly]::LoadFrom("$global:ToolsDirectory\WarningRatchet.dll")
        Import-Module -Assembly $assembly

        $destinationBranch = "master"

        $result = Invoke-WarningRatchet -TeamFoundationServer $Env:SYSTEM_TEAMFOUNDATIONSERVERURI -TeamProject $Env:SYSTEM_TEAMPROJECT -BuildId $Env:BUILD_BUILDID -DestinationBranchName $destinationBranch

        $lastGoodBuildWarningCount = $result.LastGoodBuildCount
        $currentBuildCount = $result.CurrentBuildCount
        $ratchet = $result.Ratchet
        $ratchetRequest = $result.Request

        if (-not $lastGoodBuildWarningCount) {
            Write-Host "No last good build found for DefinitionId: $buildDefinitionId"
        }

        [int]$lastGoodBuildCount = $lastGoodBuildWarningCount

        if ($lastGoodBuildWarningCount -ne $null) {
            Write-Host "The last good build id was: $($ratchetRequest.LastGoodBuild.Id) with $($lastGoodBuildCount) warnings"

            if ($currentBuildCount -gt $lastGoodBuildCount) {
                $reporter = $ratchet.GetWarningReporter($ratchetRequest)

                GenerateAndUploadReport $reporter

                [int]$adjustedWarningCount = $reporter.GetAdjustedWarningCount()
                Write-Output "Adjusted build warnings: $adjustedWarningCount"

                RenderWarningShields $true $adjustedWarningCount $lastGoodBuildCount

                $permittedWarningsThreshold = 5

                # Only fail if the adjusted count exceeds the last build
                if ($adjustedWarningCount -gt $lastGoodBuildCount -and $adjustedWarningCount -gt $permittedWarningsThreshold) {

                    $sourceBranchName = $Env:BUILD_SOURCEBRANCHNAME

                    # We always want the master branch to build
                    if ($sourceBranchName -ne "master") {
                        throw "Warning count has increased since the last good build"
                    }
                }
                return
            }
            RenderWarningShields $false $currentBuildCount $lastGoodBuildCount
        }
    } else {
        Write-Host "Build is not for a pull request, skipping warning ratchet"
        RenderWarningShields $false $currentBuildCount $lastGoodBuildCount
        return
    }
}

function GenerateAndUploadReport($reporter) {
    $report = $reporter.CreateWarningReport()

    $stream = [System.IO.StreamWriter] "$env:SYSTEM_DEFAULTWORKINGDIRECTORY\WarningReport.md"
    $stream.WriteLine($report)
    $stream.Close()
    $stream.Dispose()

    if (-not [string]::IsNullOrWhiteSpace($report)) {
        Write-Output "##vso[task.uploadsummary]$env:SYSTEM_DEFAULTWORKINGDIRECTORY\WarningReport.md"
    }
}

function RenderWarningShields([bool]$inError, [int]$this, [int]$last) {
    . $PSScriptRoot\PostWarningShields.ps1 -inError $inError -thisBuild $this -lastBuild $last
}

function BuildAssociation($vssConnection, $teamProject, $buildId) {
    $logger = New-Object Aderant.Build.Logging.PowerShellLogger -ArgumentList $Host

    $association = New-Object Aderant.Build.Tasks.BuildAssociation -ArgumentList $logger, $vssConnection

    Write-Output "Associating work items to build: $teamProject/$buildId"
    $association.AssociateWorkItemsToBuild($teamProject, [int]$buildId)
}

# Auto detect build target in debug or release mode by code branch name. If it contains "release" then get into release mode, otherwise debug
# To force build into release or debug mode, set the variable "build.flavor" at TFS build definition, Edit, Variables
# In release mode, the splash screen will remove "(Development)" text and display the current version
function GetBuildFlavor() {
    $buildFlavor = ""
    if ($Env:Build_Flavor) {
        $buildFlavor = $Env:Build_Flavor
        Write-Host "....................................................................................................." -foregroundcolor Green
        Write-Host ".......            Using Build.Flavor from build definition to $buildFlavor          ................" -foregroundcolor Green
        Write-Host "....................................................................................................." -foregroundcolor Green
    } elseif ( $Env:Build_SourceBranch -like "*release*" ) {
        $buildFlavor = "release"
        Write-Host "....................................................................................................." -foregroundcolor Green
        Write-Host ".......                           Build in release mode                      ........................" -foregroundcolor Green
        Write-Host "....................................................................................................." -foregroundcolor Green
    } else {
        $buildFlavor = "debug"
        Write-Host "....... Build in debug mode ................" -foregroundcolor Green
    }
    return [string]$buildFlavor;
}

function LoadAgentSdk() {
    $assembly = [System.Reflection.Assembly]::LoadFrom("$($env:AGENT_HOMEDIRECTORY)\externals\vstshost\Microsoft.TeamFoundation.DistributedTask.Task.LegacySDK.dll")
    Import-Module -Assembly $assembly

    # Ensure the VSS connection can be created as this thing is a nightmare of dependency hell.
    # All of the libraries involved depend on different version of System.Net.Http.Formatting and Newtonsoft.Json
    GetVssConnection
    WarningRatchet
}

#=================================================================================================
# Synopsis: Performs a incremental build of the Visual Studio Solution if possible.
# Applies a common build number, executes unit tests and packages the assemblies as a NuGet
# package
#=================================================================================================
task EndToEnd -Jobs Init, Clean, GetDependencies, BuildCore, Test, Package, {
    # End of all tasks. Print out current build flavor: Debug or Release.
    Write-Host "Finished build in $global:buildFlavor. Use the -debug or -release to switch." -foregroundcolor Green
}

task PostBuild -Jobs Init, Package, CopyToDrop, {
}

task GetDependencies {
    if (-not $IsDesktopBuild) {
        # VSTS/TFS agent doesn't always pull the entire repository as it recycles the build directories.
        # This means the local cached repository might be missing information, so want to always ensure we have full local state
        # for proper version generation, see bugs:
        # https://github.com/GitTools/GitVersion/issues/285
        # https://github.com/GitTools/GitVersion/issues/878
        # https://github.com/GitTools/GitVersion/issues/993
        # https://github.com/GitTools/GitVersion/issues/912
        # Basically VSTS is annoying, like all build systems.
        & git -C $inputRepository fetch --tags --prune --progress origin

        . $Env:EXPERT_BUILD_DIRECTORY\Build\LoadDependencies.ps1 -modulesRootPath $Repository
    }
}

task Build {
    # Get submodules
    & git submodule update --init --recursive

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
    $buildFlavor = $Flavor
    if ($buildFlavor -eq "") {
        $buildFlavor = GetBuildFlavor   # to build in debug or release
    }

    $global:BuildFlavor = $buildFlavor # to remember and display at the end

    $commonArgs = "$commonArgs /p:BuildFlavor=$buildFlavor"

    if ($Clean) {
        $commonArgs = "$commonArgs /p:CleanBin=true"
    }

    if ($DatabaseBuildPipeline.IsPresent) {
        $commonArgs = "$commonArgs /p:RunDatabaseDeployPipeline=true /p:DropOnFailure=false"
    }

    if ($Automation.IsPresent) {
        $commonArgs = "$commonArgs /p:RunDesktopAutomationTests=true"
    }

    # /p:RunWixToolsOutOfProc=true is required due to this bug with stdout processing
    # https://connect.microsoft.com/VisualStudio/feedback/details/1286424/
    $commonArgs = "$commonArgs /p:RunWixToolsOutOfProc=true"

    try {
        Push-Location $Repository

        if ($IsDesktopBuild) {
            $commonArgs = "$commonArgs /p:CodeCoverage=$CodeCoverage"

            $commonArgs = "$commonArgs /p:RunDesktopIntegrationTests=$($Integration.IsPresent)"

            Invoke-Tool -FileName $MSBuildLocation\MSBuild.exe -Arguments $commonArgs -RequireExitCodeZero
        } else {
            $commonArgs = "$commonArgs /clp:PerformanceSummary"
            #. $Env:EXPERT_BUILD_DIRECTORY\Build\InvokeServerBuild.ps1 -Repository $Repository -MSBuildLocation $MSBuildLocation -CommonArgs $commonArgs
        }
    } finally {
        Pop-Location
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
            #$logger = [Microsoft.TeamFoundation.DistributedTask.Task.TestResults.Logger]
            #$job = Register-ObjectEvent -inputObject $logger -eventName LogVerbose -Action { Write-Verbose $_ }

            $buildId = $Env:BUILD_BUILDID
            $buildUri = $Env:BUILD_BUILDURI
            $owner = $Env:BUILD_REQUESTEDFOR
            $project = $Env:SYSTEM_TEAMPROJECT

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
    if ($LimitBuildWarnings) {
        WarningRatchet
    }
}

task CopyToDrop -If (-not $IsDesktopBuild) {
    if (Test-Path "$($Repository)\CopyToDrop.ps1") {
        . $Repository\CopyToDrop.ps1
    }
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
        . $Env:EXPERT_BUILD_DIRECTORY\Build\Package.ps1 -repository $Repository -replicate:($Env:ReplicateDependenciesToPaketTemplate -ne $null -and $Env:ReplicateDependenciesToPaketTemplate -eq $true)
    }
}

task Init {
    . $Env:EXPERT_BUILD_DIRECTORY\Build\Build-Libraries.ps1
    CompileBuildLibraryAssembly "$Env:EXPERT_BUILD_DIRECTORY\Build\"
    LoadLibraryAssembly "$Env:EXPERT_BUILD_DIRECTORY\Build\"

    Write-Info "Build tree"
    .\Show-BuildTree.ps1 -File $PSCommandPath

    Write-Info ("Build Uri:".PadRight(20) + $Env:BUILD_BUILDURI)
    Write-Info ("Is Desktop Build:".PadRight(20) + $IsDesktopBuild)

    if (-not $IsDesktopBuild) {        
        cmd /c "`"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools\x64\sn.exe`" -Vr *,b03f5f7f11d50a3a"

        $global:probeDirectories = @(
            $global:ToolsDirectory, 
            "$Env:AGENT_HOMEDIRECTORY\externals\vstshost", 
            "$Env:AGENT_HOMEDIRECTORY\externals\vstsom", 
            "$Env:AGENT_HOMEDIRECTORY\bin", 
            "$Env:VS140COMNTOOLS..\IDE\PrivateAssemblies")        

        $global:rebindMap = @{
                "System.Net.Http.Primitives, Version=1.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"="System.Net.Http.Primitives, Version=4.2.22.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
                #"System.Net.Http.Formatting, Version=5.2.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"="System.Net.Http.Formatting, Version=5.2.3.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
                # Legacy SDK depends on version 9 but System.Net.Http.Formatting depends on version 6, version 10 and 11 cannot be substituted
                "Newtonsoft.Json, Version=6.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed"="Newtonsoft.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed"                
                #"Newtonsoft.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed"="Newtonsoft.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed"
            }     

        $OnAssemblyResolve = [System.ResolveEventHandler] {
            param($sender, $e)
            if ($e.Name -like "*resources*") {
                return $null
            }

            Write-Host "Resolving $($e.Name)"

            $assemblyName = [System.Reflection.AssemblyName]::new($e.Name)        
             Lookup if we are allowed to rebind this assembly
            $newName = $global:rebindMap[$assemblyName.FullName]
            if ($null -ne $newName) {
                Write-Information "Rebinding $($assemblyName.FullName) -> $newName"
                $assemblyName = [System.Reflection.AssemblyName]::new($newName)
            }

            $fileName = $e.Name.Split(",")[0]
            $fileName = $fileName + ".dll"
            
            foreach ($dir in $global:probeDirectories) {
                $fullFilePath = "$dir\$fileName"

                Write-Debug "Probing: $fullFilePath"

                if (Test-Path ($fullFilePath)) {
                    Write-Debug "File exists: $fullFilePath"
                    try {
                        $name = [System.Reflection.AssemblyName]::GetAssemblyName($fullFilePath)

                        if ($name.FullName -eq $assemblyName.FullName) {
                            $asm = [System.Reflection.Assembly]::LoadFrom($fullFilePath)
                            Write-Debug "Loaded dependency: $fullFilePath"
                            return $asm
                        } else {
                            Write-Debug "Not loading assembly. Name mismatch $($name.FullName) != $($assemblyName.FullName)"
                        }
                    } catch {
                        Write-Error "Failed to load $fullFilePath. $_.Exception"
                    }
                }
            }

            Write-Host "Cannot locate $($e.Name). The build will probably fail now."
            return $null
        }        

        [System.AppDomain]::CurrentDomain.add_AssemblyResolve($OnAssemblyResolve)
        
        LoadAgentSdk        
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
        Write-Verbose "Task `"$($Task.Name)`" has errored!"
        $script:step.Finish("Done", [Result]::Failed)
    } else {
        $script:step.Finish("Done", [Result]::Succeeded)
    }
}

task . EndToEnd
