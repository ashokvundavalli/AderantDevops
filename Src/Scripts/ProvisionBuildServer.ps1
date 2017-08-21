param ( 
    [Parameter(Mandatory=$true)][string]$server
)
process {

    if (-not ($server.EndsWith(".ap.aderant.com","CurrentCultureIgnoreCase"))){
        $server = $server + ".ap.aderant.com"
    }

    $serviceUsername = "ADERANT_AP\service.tfsbuild.ap"
    
    $credentials = Get-Credential $serviceUsername

    $session = New-PSSession -ComputerName $server -Credential $credentials -Authentication Credssp

    $setupScriptBlock = {
        param (
            $credentials
        )
        
        if( -not (Test-Path "C:\Scripts")){
            mkdir "C:\Scripts"
        }

        cd C:\Scripts

        $credentials | Export-Clixml -Path C:\Scripts\credentials.xml

        Write-Host "Downloading Agent Zip"

        wget http://go.microsoft.com/fwlink/?LinkID=851123 -OutFile vsts.agent.zip
    }

    $setupScripts = Invoke-Command -Session $session -ScriptBlock $setupScriptBlock -ArgumentList $credentials
    
    Copy-Item -ToSession $session -Path $PSScriptRoot\setup-build-agent.ps1 -Destination "C:\Scripts\"

    Write-Host "Generating Scheduled Task"

    $scriptBlock = {
        param (
            $Credentials
        )

        $STTrigger = New-ScheduledTaskTrigger -AtStartup
        $STName = "Setup Build Agents"
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute 'Powershell.exe' -Argument 'NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File C:\Scripts\setup-build-agent-test.ps1'
        #Configure when to stop the task and how long it can run for. In this example it does not stop on idle and uses the maximum possible duration by setting a timelimit of 0
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) 
        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User ADERANT_AP\service.tfsbuild.ap -Password $Credentials.GetNetworkCredential().Password -Settings $STSettings 
    
    }

    $provisionServer = Invoke-Command -Session $session -ScriptBlock $scriptBlock -ArgumentList $credentials

}

