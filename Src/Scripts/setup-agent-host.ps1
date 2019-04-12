[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)][int]$agentsToProvision = 0,
    [bool]$removeAllAgents = $true,
    [Parameter(Mandatory=$false)][string]$agentArchive,
    [Parameter(Mandatory=$false)][string]$tfsHost = "https://tfs.aderant.com/tfs",
    [Parameter(Mandatory=$false)][string]$agentPool,
    # The scratch drive for the agent, this is where the intermediate objects will be placed
    [Parameter(Mandatory=$false)]$workDirectory
)

begin {
    Set-StrictMode -Version Latest

    if ($agentsToProvision -eq 0) {
        $agentsToProvision = ((Get-CimInstance -Class win32_Processor).NumberOfCores / 2)

        if ((Get-CimInstance win32_computersystem).Model -eq "Virtual Machine") {
            $agentsToProvision = 1
        }

        if ($agentsToProvision -eq 0) {
            $agentsToProvision = 1
        }
    }

    if ([string]::IsNullOrWhiteSpace($agentPool)) {
        if (-not [string]::IsNullOrWhiteSpace($Env:AgentPool)) {
            $agentPool = $Env:AgentPool
        } else {
            $agentPool = "Default"
        }
    }

    if ([string]::IsNullOrWhiteSpace($workDirectory)) {
        $workDirectory = "$Env:SystemDrive\"
    }
}

process {
    Start-Transcript -Path "$env:SystemDrive\Scripts\SetupAgentHostLog.txt" -Force

    $ErrorActionPreference = "Stop"
    [string]$AgentRootDirectory = "C:\Agents"

    if ([string]::IsNullOrWhiteSpace($agentArchive)) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        $agentArchive = "$env:SystemDrive\Scripts\vsts-agent.zip"
        #Invoke-WebRequest "https://github.com/Microsoft/azure-pipelines-agent/releases/download/v2.119.1/vsts-agent-win7-x64-2.119.1.zip" -OutFile $agentArchive
        Invoke-WebRequest "https://vstsagentpackage.azureedge.net/agent/2.141.2/vsts-agent-win-x64-2.141.2.zip" -OutFile $agentArchive
    }

    if (-not (Test-Path $workDirectory)) {
        $workDirectory = $Env:SystemDrive + "\"
    }

    # Based on https://github.com/docker/docker/blob/master/pkg/namesgenerator/names-generator.go

    [string]$agentPrefixes = '\\SVNAS301.ap.aderant.com\internal\team\DevOps\Agents\Prefixes.txt'
    [string]$agentSuffixes = '\\SVNAS301.ap.aderant.com\internal\team\DevOps\Agents\Suffixes.txt'

    if ((Test-Path -Path $agentPrefixes) -and (Test-Path -Path $agentSuffixes)) {
        [string[]]$prefixes = [System.IO.File]::ReadAllLines($agentPrefixes)
        [string[]]$suffixes = [System.IO.File]::ReadAllLines($agentSuffixes)
    } else {
        [string[]]$prefixes = @(
            "admiring",
            "adoring",
            "affectionate",
            "agitated",
            "amazing",
            "angry",
            "awesome",
            "backstabbing",
            "berserk",
            "big",
            "blessed",
            "boring",
            "bothersome",
            "cached",
            "clever",
            "cocky",
            "compassionate",
            "condescending",
            "cranky",
            "dank",
            "daijoubu",
            "derelict",
            "desperate",
            "determined",
            "disappointed",
            "distracted",
            "disguised",
            "dreamy",
            "drunk",
            "eager",
            "ecstatic",
            "elastic",
            "elated",
            "elegant",
            "elusive",
            "evil",
            "fervent",
            "flaming",
            "focused",
            "furious",
            "gangsta",
            "gigantic",
            "gloomy",
            "goofy",
            "grave",
            "happy",
            "hardcore",
            "high",
            "hopeful",
            "hungry",
            "infallible",
            "intoxicated",
            "jolly",
            "jovial",
            "kawaii",
            "kickass",
            "lonely",
            "lost",
            "loving",
            "mad",
            "majestic",
            "maniacal",
            "modest",
            "naughty",
            "nauseous",
            "nostalgic",
            "numberwang",
            "oscillating",
            "peaceful",
            "pedantic",
            "pensive",
            "prickly",
            "psychopathic",
            "psychedelic",
            "quality",
            "reluctant",
            "reverent",
            "romantic",
            "sad",
            "serene",
            "sharp",
            "sick",
            "silly",
            "sleepy",
            "small",
            "stoic",
            "stupefied",
            "stylish",
            "surprised",
            "suspicious",
            "swagalicious",
            "swish",
            "tender",
            "thirsty",
            "tiny",
            "trusting",
            "unsuspecting",
            "vulnerable",
            "zen"
        )

        [string[]]$suffixes = @(
            "aardvark",
            "ai-chan",
            "ant",
            "albatross",
            "alligator",
            "badger",
            "bee",
            "bread",
            "bug",
            "camel",
            "cake",
            "cat",
            "cheetah",
            "chicken",
            "chimpanzee",
            "chancellor",
            "chupacabra",
            "crayfish",
            "crocodile",
            "cthulhu",
            "deer",
            "dog",
            "dolphin",
            "doge",
            "donkey",
            "dragon",
            "duck",
            "dumpling",
            "eagle",
            "elephant",
            "fish",
            "fly",
            "fox",
            "frog",
            "giraffe",
            "goat",
            "godzilla",
            "goldfish",
            "goose",
            "hamster",
            "hippopotamus",
            "homura",
            "horse",
            "ibex",
            "jello",
            "kangaroo",
            "kiev",
            "kitten",
            "kyubey"
            "lion",
            "lobster",
            "madoka",
            "memelord",
            "monkey",
            "nekomimi",
            "octopus",
            "owl",
            "ocelot",
            "ox",
            "pancake",
            "panda",
            "phoenix",
            "pig",
            "pikachu",
            "puffin",
            "puppy",
            "qilin",
            "rabbit",
            "rat",
            "roper",
            "scorpion",
            "seal",
            "shark",
            "sheep",
            "snail",
            "snake",
            "spider",
            "squirrel",
            "stroganoff",
            "taniwha",
            "tiger",
            "trogdor",
            "turtle",
            "unicorn",
            "userstory",
            "wizard",
            "wolf",
            "wonton",
            "workflow",
            "zebra"
        )
    }

    function Get-RandomName {
        $prefixesRnd = Get-Random -Minimum 0 -Maximum $prefixes.Length
        $suffixesRnd = Get-Random -Minimum 0 -Maximum $suffixes.Length

        return ("$($env:COMPUTERNAME)_{0}_{1}" -f ($prefixes[$prefixesRnd], $suffixes[$suffixesRnd]))
    }

    function RemoveAllAgents() {
        Write-Output "Removing existing build agents"

        $agentServices = Get-Service -Name "VSTS Agent (tfs.*)"

        if ($null -ne $agentServices) {
            Stop-Service -InputObject $agentServices

            foreach ($agent in $agentServices) {
                & cmd /c "SC DELETE `"$($agent.Name)`""
            }
        }

        if (Test-Path $AgentRootDirectory) {
            $directories = Get-ChildItem -LiteralPath $AgentRootDirectory

            foreach ($directory in $directories) {
                cmd /c "$($directory.FullName)\config.cmd remove --auth Integrated"

                Remove-Item -Path $directory.FullName -Force -Recurse -Verbose -ErrorAction SilentlyContinue
            }
        }

        # Stop IIS while removing build agent directories to prevent file locks
        Import-Module WebAdministration
        iisreset.exe /STOP

        # Clear build agent working directory
        [string]$workingDirectory = [System.IO.Path]::Combine($workDirectory, "B")

        # If the path doesn't exist Get-ChildItem will happily pick the working directory instead which could delete C:\Windows\ ...
        # https://github.com/PowerShell/PowerShell/issues/5699
        if (Test-Path $workingDirectory) {
            # Work around PowerShell bugs: https://github.com/powershell/powershell/issues/621
            Get-ChildItem -LiteralPath $workingDirectory -Recurse -Attributes ReparsePoint | % { $_.Delete() }

            Remove-Item -Path $workingDirectory -Force -Recurse -Verbose -ErrorAction SilentlyContinue
        }

        & $PSScriptRoot\iis-cleanup.ps1

        # Start IIS after removing files
        iisreset.exe /START
    }

    function SetHighPower() {
        $powerPlan = Get-WmiObject -Namespace root\cimv2\power -Class Win32_PowerPlan -Filter "ElementName = 'High Performance'"
        $powerPlan.Activate()
    }

    function ConfigureGit() {
        . $PSScriptRoot\configure-git-for-agent-host.ps1
    }

    function OptimizeBuildEnvironment {
        # TODO: merge with "Optimize-Environment"
        try {
            Import-Module Defender

            $processes = @(
            "7z.exe",
            "7zip.exe",
            "csc.exe",
            "csi.exe",
            "devenv.exe",
            "git.exe",
            "lc.exe",
            "JetBrains.Profiler.Windows.PdbServer.exe",
            "JetBrains.ReSharper.TaskRunner.CLR45.x64.exe",
            "JetBrains.ETW.Collector.Host.exe",
            "Microsoft.Alm.Shared.Remoting.RemoteContainer.dll",
            "Microsoft.VsHub.Server.HttpHost.exe",
            "Microsoft.Alm.Shared.RemoteContainer.dll",
            "MSBuild.exe",
            "PowerShell.exe",
            "ServiceHub.Host.CLR.x86.exe",
            "ServiceHub.Host.Node.x86.exe",
            "ServiceHub.RoslynCodeAnalysisService32.exe",
            "ServiceHub.VSDetouredHost.exe",
            "TE.ProcessHost.Managed.exe",
            "testhost.x86.exe",
            "testhostw.exe",
            "VBCSCCompiler.exe",
            "aspnet_compiler.exe",
            "vstest.console.exe",
            "vstest.discoveryengine.exe",
            "vstest.discoveryengine.x86.exe",
            "vstest.executionengine.exe",
            "vstest.executionengine.x86.exe",

            "node.exe",
            "tsc.exe",

            "FxCopCmd.exe",
            "dbprepare.exe",
            "DeploymentEngine.exe",
            "DeploymentManager.exe",
            "Expert.Help.sfx"
            "PackageManagerConsole.exe",

            "ffmpeg.exe",
            "Agent.Listener.exe",
            "AgentService.exe",
            "robocopy.exe"
            )

            foreach ($proc in $processes) {
                Add-MpPreference -ExclusionProcess $proc
            }
        } catch {
            Write-Verbose $Error[0].Exception
        }
    }

    function ProvisionAgent() {
        SetHighPower
        ConfigureGit

        $agentName = Get-RandomName

        $scratchDirectoryName = Get-Random -Maximum 1024

        $workingDirectory = [System.IO.Path]::Combine($workDirectory, "B", $scratchDirectoryName)

        Write-Output "Agent: $agentName Working directory $workingDirectory"

        $agentInstallationPath = "$AgentRootDirectory\$agentName"

        New-Item -ItemType Directory -Path $AgentRootDirectory -ErrorAction SilentlyContinue

        Expand-Archive $agentArchive -DestinationPath $agentInstallationPath -Force

        try {
            Push-Location -Path $agentInstallationPath

            Write-Output "Installing agent $agentName"

            .\config.cmd --unattended --url $tfsHost --auth Integrated --pool $agentPool --agent $agentName --windowslogonaccount "ADERANT_AP\tfsbuildservice$" --work "$workingDirectory" --replace

            $command = "sc create `"VSTS Agent (tfs.$agentName)`" binpath=$agentInstallationPath\bin\AgentService.exe obj= `"ADERANT_AP\tfsbuildservice$`" start= delayed-auto"
            & cmd /c $command
            net start "VSTS Agent (tfs.$agentName)"
        } finally {
            Pop-Location
        }
    }

    ##
    ## Agent Setup ##
    ##
    if ($removeAllAgents) {
        RemoveAllAgents
    }

    OptimizeBuildEnvironment

    for ($i = 0; $i -lt $agentsToProvision; $i++) {
        ProvisionAgent
    }
}