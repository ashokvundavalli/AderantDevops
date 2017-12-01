﻿[CmdletBinding()]
param(
    [Parameter()]
    $agentsToProvision = ($Env:NUMBER_OF_PROCESSORS / 2),

    [Switch]
    $removeAllAgents = $true,

    [Parameter()]
    $agentArchive = "$env:SystemDrive\Scripts\vsts.agent.zip",

    [Parameter()]
    $tfsHost = "http://tfs:8080/tfs",
    
    [Parameter()]
    $agentPool = "default",

    # The scratch drive for the agent, this is where the intermediate objects will be placed
    [Parameter()]
    $workDirectory = "D:\",

    [Parameter()]
    $credentials
)

$ErrorActionPreference = "Stop"

$AgentRootDirectory = "C:\Agents"

# Converted from https://github.com/docker/docker/blob/master/pkg/namesgenerator/names-generator.go

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
	"clever",
	"cocky",
	"compassionate",
	"condescending",
	"cranky",
	"desperate",
	"determined",
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
	"jolly",
	"jovial",
	"kickass",
	"lonely",
	"loving",
	"mad",
	"modest",
	"naughty",
	"nauseous",
	"nostalgic",
	"peaceful",
	"pedantic",
	"pensive",
	"prickly",
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
	"suspicious",
	"tender",
	"thirsty",
	"tiny",
	"trusting",
	"zen"
)

$right = @(
    "alligator",
    "ant",
    "bear",
    "bee",
    "bird",
    "camel",
    "cat",
    "cheetah",
    "chicken",
    "chimpanzee",
    "cow",
    "crocodile",
    "deer",
    "dog",
    "dolphin",
    "duck",
    "eagle",
    "elephant",
    "fish",
    "fly",
    "fox",
    "frog",
    "giraffe",
    "goat",
    "goldfish",
    "hamster",
    "hippopotamus",
    "horse",
    "kangaroo",
    "kitten",
    "lion",
    "lobster",
    "monkey",
    "octopus",
    "owl",
    "panda",
    "pig",
    "puppy",
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
    "tiger",
    "turtle",
    "wolf",
    "zebra"
)

function GetCredentialsOrPrompt() {
    $credentialFile = "$PSScriptRoot\credentials.xml"

    if (Test-Path $credentialFile) {
        return Import-Clixml -Path $credentialFile        
    }

    if ([System.Environment]::UserInteractive) {
        ($credentials = Get-Credential) | Export-Clixml -Path $credentialFile
        return $credentials
    } else {
        throw "Process is not interactive so cannot prompt for credentials."
    }
}


function GetRandomName {
    $leftRnd = Get-Random -Minimum 0 -Maximum $left.Length
    $rightRnd = Get-Random -Minimum 0 -Maximum $right.Length

	return ("{0}_{1}" -f ($left[$leftRnd], $right[$rightRnd]))
}

function RemoveAllAgents() {
    if (Test-Path $AgentRootDirectory) {
        $directories = gci $AgentRootDirectory

        foreach ($directory in $directories) {        
            cmd /c "$($directory.FullName)\config.cmd remove --auth Integrated"

            Remove-Item -Path $directory.FullName -Force -Recurse -ErrorAction SilentlyContinue
        }    
    }
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

    if (-not (Test-Path $workDirectory)) {
	    $workDirectory = $Env:SystemDrive + "\"
    }

    $scratchDirectoryName = Get-Random -Maximum 1024

    $workingDirectory = [System.IO.Path]::Combine($workDirectory, "B", $scratchDirectoryName)

    Write-Host "Agent: $agentName Working directory $workingDirectory"

    if (Test-Path $workingDirectory) {
        return
    }    

    $agentInstallationPath = "$AgentRootDirectory\$agentName"
    
    New-Item -ItemType Directory -Path $AgentRootDirectory -ErrorAction SilentlyContinue

    Expand-Archive $agentArchive -DestinationPath $agentInstallationPath -Force

    try {
        Push-Location -Path $agentInstallationPath

        $credentials = GetCredentialsOrPrompt
        $serviceAccountName = $credentials.UserName
        $serviceAccountPassword = $credentials.GetNetworkCredential().Password

        Write-Host "Installing agent $agentName"

        .\config.cmd --unattended --url $tfsHost --auth Integrated --pool $agentPool --agent $agentName --runasservice --windowslogonaccount $serviceAccountName --windowslogonpassword $serviceAccountPassword --work "$workingDirectory" --replace
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