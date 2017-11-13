param ( 
    [Parameter(Mandatory=$true)][string]$server,
    [Parameter(Mandatory=$false)]$credentials
)
process {

    if (-not ($server.EndsWith(".ap.aderant.com","CurrentCultureIgnoreCase"))){
        $server = $server + ".ap.aderant.com"
    }

    $serviceUsername = "ADERANT_AP\service.tfsbuild.ap"
    
    if (-not $credentials) {
        $credentials = Get-Credential $serviceUsername
    }

    $session = New-PSSession -ComputerName $server -Credential $credentials -Authentication Credssp
    
    $setupScriptBlock = {
        param (
            $credentials
        )

        $powerPlan = Get-WmiObject -Namespace root\cimv2\power -Class Win32_PowerPlan -Filter "ElementName = 'High Performance'"
        $powerPlan.Activate()

        $scriptsDirectory = "$env:SystemDrive\Scripts"
        
        mkdir $scriptsDirectory -ErrorAction SilentlyContinue        

        cd $scriptsDirectory

        $credentials | Export-Clixml -Path $scriptsDirectory\credentials.xml

        Write-Host "Downloading Agent Zip"

        try {
            $currentProgressPreference = $ProgressPreference
            $ProgressPreference = "SilentlyContinue"
            #wget http://go.microsoft.com/fwlink/?LinkID=851123 -OutFile vsts.agent.zip -UseBasicParsing
        } finally {
            $ProgressPreference = $currentProgressPreference 
        }

        # Return the machine specific script home
        return $scriptsDirectory
    }
        
    $scriptsDirectory = Invoke-Command -Session $session -ScriptBlock $setupScriptBlock -ArgumentList $credentials    
        
    Write-Host "Generating Scheduled Tasks"    

    <# 
    ============================================================
    Setup Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $STTrigger = New-ScheduledTaskTrigger -AtStartup
        $STName = "Setup Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\setup-agent-host.ps1" -WorkingDirectory $scriptsDirectory
        #Configure when to stop the task and how long it can run for. In this example it does not stop on idle and uses the maximum possible duration by setting a timelimit of 0
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password

        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force    
    } -ArgumentList $scriptsDirectory


    <# 
    ============================================================
    Reboot Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $STTrigger = New-ScheduledTaskTrigger -Daily -At 11pm
        $STName = "Restart Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\restart-agent-host.ps1" -WorkingDirectory $scriptsDirectory
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password
        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force          
     } -ArgumentList $scriptsDirectory


    <# 
    ============================================================
    Refresh Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $interval = New-TimeSpan -Minutes 15
        $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
        $STName = "Refresh Agent Host Scripts"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\refresh-agent-host.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password
        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force          
     } -ArgumentList $scriptsDirectory


    Invoke-Command -Session $session -ScriptBlock {
        Remove-Item "$scriptsDirectory\Build.Infrastructure" -Force -Recurse -ErrorAction SilentlyContinue
        & git clone "http://tfs.ap.aderant.com:8080/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure" "$scriptsDirectory\Build.Infrastructure" -q
    } -ArgumentList $scriptsDirectory
}


