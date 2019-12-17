# Build agent provisioning 

Most of the setup required for a modern build agent is automated. 
This guide does not cover legacy build requirements such as those required by ClearView/Spotlight/Analytics which are using ancient build technology.

The process is something like this.

* Ask IT to provision a Server 2016 Expert Developer image
* Install TFS build agent
* Profit

The OS image comes with most of the requirements needed to build Expert software. Such as MS Build, Visual Studio and the Modeling SDK.

## Install the agent

From `TFS > Admin > Agent Queues click "Download Agent"`

![](Images\agent-queue.png)

Copy the agent zip onto the build machine. Place the zip into `%userprofile%\Downloads`

## Import the Agent Setup Scheduled Task

* Copy `setup-build-agent.ps1``` into `C:\Scripts` on the build machine.
* Import `Setup Build Agents.xml` into the Windows task scheduler. This will setup the provision script that will run each time the machine restarts.

## Run the Setup Script

Run `setup-build-agent.ps1`

```
powershell.exe -NoLogo -Sta -NoProfile -ExecutionPolicy Unrestricted -File C:\Scripts\setup-build-agent.ps1
```

This will remove all existing agents (if any) and provision the new agents. 
The first time you run this script it will prompt for credentials to run the agent(s) as. These credentials are then cached so the script can be run by the scheduled task without prompting.

After running the script a "credentials.xml" is created which contains the encrypted and cached credentials.

![](Images\provision-build-agent.png)