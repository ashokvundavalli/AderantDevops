[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)][int]$agentsToProvision = ($Env:NUMBER_OF_PROCESSORS / 2),
    [Switch]$removeAllAgents = $true,
    [Parameter(Mandatory=$false)][string]$agentArchive = "$env:SystemDrive\Scripts\vsts.agent.zip",
    [Parameter(Mandatory=$false)][string]$tfsHost = "http://tfs:8080/tfs",
    [Parameter(Mandatory=$false)][string]$agentPool,
    # The scratch drive for the agent, this is where the intermediate objects will be placed
    [Parameter(Mandatory=$false)]$workDirectory
)

begin {
    Set-StrictMode -Version Latest

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
        $agentArchive = "$PSScriptRoot\vsts.agent.zip"
    }
    
    if (-not (Test-Path $workDirectory)) {
        $workDirectory = $Env:SystemDrive + "\"
    }

    # Based on https://github.com/docker/docker/blob/master/pkg/namesgenerator/names-generator.go

    $left = @(
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
        "boring",
        "bothersome",
        "clever",
        "cocky",
        "compassionate",
        "condescending",
        "cranky",
        "daijoubu"
        "derelict"
        "desperate",
        "determined",
        "disappointed",
        "distracted",
        "dreamy",
        "drunk",
        "eager",
        "ecstatic",
        "elastic",
        "elated",
        "elegant",
        "evil",
        "fervent",
        "flaming",
        "focused",
        "furious",
        "gigantic",
        "gloomy",
        "goofy",
        "grave",
        "happy",
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
        "loving",
        "mad",
        "majestic",
        "maniacal",
        "modest",
        "naughty",
        "nauseous",
        "nostalgic",
        "oscillating",
        "peaceful",
        "pedantic",
        "pensive",
        "prickly",
        "psychopathic",
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
        "suspicious",
        "swish",
        "tender",
        "thirsty",
        "tiny",
        "trusting",
        "unsuspecting",
        "vulnerable",
        "zen"
    )

    $right = @(
        "albatross",
        "alligator",
        "badger",
        "bee",
        "camel",
        "cat",
        "cheetah",
        "chicken",
        "chimpanzee",
        "chupacabra",
        "crayfish",
        "crocodile",
        "cthulhu",
        "deer",
        "dog",
        "dolphin",
        "donkey",
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
        "goldfish",
        "goose",
        "hamster",
        "hippopotamus",
        "horse",
        "ibex",
        "jello",
        "kangaroo",
        "kiev",
        "kitten",
        "lion",
        "lobster",
        "monkey",
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
        "turtle",
        "wolf",
        "wonton",
        "zebra"
    )

    function GetRandomName {
        $leftRnd = Get-Random -Minimum 0 -Maximum $left.Length
        $rightRnd = Get-Random -Minimum 0 -Maximum $right.Length

        return ("$($env:COMPUTERNAME)_{0}_{1}" -f ($left[$leftRnd], $right[$rightRnd]))
    }

    function RemoveAllAgents() {
        Write-Host "Removing existing build agents"

        $agentServices = Get-Service -Name "VSTS Agent (tfs.*)"

        if ($agentServices -ne $null) {
            Stop-Service -InputObject $agentServices

            foreach ($agent in $agentServices) {
                & cmd /c "SC DELETE `"$($agent.Name)`""
            }
        }

        if (Test-Path $AgentRootDirectory) {
            $directories = gci $AgentRootDirectory

            foreach ($directory in $directories) {        
                cmd /c "$($directory.FullName)\config.cmd remove --auth Integrated"

                Remove-Item -Path $directory.FullName -Force -Recurse -ErrorAction SilentlyContinue
            }    
        }        

        # Stop IIS while removing build agent directories to prevent file locks
        Import-Module WebAdministration
        iisreset.exe /STOP
        
        # Clear build agent working directory
        [string]$workingDirectory = [System.IO.Path]::Combine($workDirectory, "B")
        Remove-Item -Path "$workingDirectory\*" -Force -Recurse -ErrorAction SilentlyContinue
        
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

    function ProvisionAgent() {
        SetHighPower 
        ConfigureGit

        $agentName = GetRandomName

        $scratchDirectoryName = Get-Random -Maximum 1024

        $workingDirectory = [System.IO.Path]::Combine($workDirectory, "B", $scratchDirectoryName)

        Write-Host "Agent: $agentName Working directory $workingDirectory"   

        $agentInstallationPath = "$AgentRootDirectory\$agentName"
    
        New-Item -ItemType Directory -Path $AgentRootDirectory -ErrorAction SilentlyContinue

        Expand-Archive $agentArchive -DestinationPath $agentInstallationPath -Force

        try {
            Push-Location -Path $agentInstallationPath

            Write-Host "Installing agent $agentName"

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

    for ($i = 0; $i -lt $agentsToProvision; $i++) {
        ProvisionAgent
    }
}