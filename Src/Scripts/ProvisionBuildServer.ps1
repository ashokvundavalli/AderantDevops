[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$server,
    [Parameter(Mandatory=$false)][string]$agentPool,
    [switch]$skipAgentDownload,
    [switch]$restart
)

begin {
    Set-StrictMode -Version Latest
}

process {

$lsaCode = @'
using System;

namespace Willys.LsaSecurity
{
   using System.ComponentModel;
   using System.Runtime.InteropServices;
   using System.Security;
   using System.Security.Principal;
   using LSA_HANDLE = IntPtr;

   [StructLayout(LayoutKind.Sequential)]
   struct LSA_OBJECT_ATTRIBUTES
   {
      internal int Length;
      internal IntPtr RootDirectory;
      internal IntPtr ObjectName;
      internal int Attributes;
      internal IntPtr SecurityDescriptor;
      internal IntPtr SecurityQualityOfService;
   }

   [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
   struct LSA_UNICODE_STRING
   {
      internal ushort Length;
      internal ushort MaximumLength;
      [MarshalAs(UnmanagedType.LPWStr)]
      internal string Buffer;
   }

   sealed class Win32Sec
   {
      [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
      internal static extern uint LsaOpenPolicy(
         LSA_UNICODE_STRING[] SystemName,
         ref LSA_OBJECT_ATTRIBUTES ObjectAttributes,
         int AccessMask,
         out IntPtr PolicyHandle
      );

      [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
      internal static extern uint LsaAddAccountRights(
         LSA_HANDLE PolicyHandle,
         IntPtr pSID,
         LSA_UNICODE_STRING[] UserRights,
         int CountOfRights
      );

      [DllImport("advapi32")]
      internal static extern int LsaNtStatusToWinError(int NTSTATUS);

      [DllImport("advapi32")]
      internal static extern int LsaClose(IntPtr PolicyHandle);

   }

   sealed class Sid : IDisposable
   {
      public IntPtr pSid = IntPtr.Zero;
      public SecurityIdentifier sid = null;

      public Sid(string account)
      {
         sid = (SecurityIdentifier) (new NTAccount(account)).Translate(typeof(SecurityIdentifier));
         Byte[] buffer = new Byte[sid.BinaryLength];
         sid.GetBinaryForm(buffer, 0);

         pSid = Marshal.AllocHGlobal(sid.BinaryLength);
         Marshal.Copy(buffer, 0, pSid, sid.BinaryLength);
      }

      public void Dispose()
      {
         if (pSid != IntPtr.Zero)
         {
            Marshal.FreeHGlobal(pSid);
            pSid = IntPtr.Zero;
         }
         GC.SuppressFinalize(this);
      }
      ~Sid()
      {
         Dispose();
      }
   }


   public sealed class LsaWrapper : IDisposable
   {
      enum Access : int
      {
         POLICY_READ = 0x20006,
         POLICY_ALL_ACCESS = 0x00F0FFF,
         POLICY_EXECUTE = 0X20801,
         POLICY_WRITE = 0X207F8
      }
      const uint STATUS_ACCESS_DENIED = 0xc0000022;
      const uint STATUS_INSUFFICIENT_RESOURCES = 0xc000009a;
      const uint STATUS_NO_MEMORY = 0xc0000017;

      IntPtr lsaHandle;

      public LsaWrapper()
         : this(null)
      { }
      // // local system if systemName is null
      public LsaWrapper(string systemName)
      {
         LSA_OBJECT_ATTRIBUTES lsaAttr;
         lsaAttr.RootDirectory = IntPtr.Zero;
         lsaAttr.ObjectName = IntPtr.Zero;
         lsaAttr.Attributes = 0;
         lsaAttr.SecurityDescriptor = IntPtr.Zero;
         lsaAttr.SecurityQualityOfService = IntPtr.Zero;
         lsaAttr.Length = Marshal.SizeOf(typeof(LSA_OBJECT_ATTRIBUTES));
         lsaHandle = IntPtr.Zero;
         LSA_UNICODE_STRING[] system = null;
         if (systemName != null)
         {
            system = new LSA_UNICODE_STRING[1];
            system[0] = InitLsaString(systemName);
         }

         uint ret = Win32Sec.LsaOpenPolicy(system, ref lsaAttr,
         (int) Access.POLICY_ALL_ACCESS, out lsaHandle);
         if (ret == 0)
            return;
         if (ret == STATUS_ACCESS_DENIED)
         {
            throw new UnauthorizedAccessException();
         }
         if ((ret == STATUS_INSUFFICIENT_RESOURCES) || (ret == STATUS_NO_MEMORY))
         {
            throw new OutOfMemoryException();
         }
         throw new Win32Exception(Win32Sec.LsaNtStatusToWinError((int) ret));
      }

      public void AddPrivileges(string account, string privilege)
      {
         uint ret = 0;
         using (Sid sid = new Sid(account))
         {
            LSA_UNICODE_STRING[] privileges = new LSA_UNICODE_STRING[1];
            privileges[0] = InitLsaString(privilege);
            ret = Win32Sec.LsaAddAccountRights(lsaHandle, sid.pSid, privileges, 1);
         }
         if (ret == 0)
            return;
         if (ret == STATUS_ACCESS_DENIED)
         {
            throw new UnauthorizedAccessException();
         }
         if ((ret == STATUS_INSUFFICIENT_RESOURCES) || (ret == STATUS_NO_MEMORY))
         {
            throw new OutOfMemoryException();
         }
         throw new Win32Exception(Win32Sec.LsaNtStatusToWinError((int) ret));
      }

      public void Dispose()
      {
         if (lsaHandle != IntPtr.Zero)
         {
            Win32Sec.LsaClose(lsaHandle);
            lsaHandle = IntPtr.Zero;
         }
         GC.SuppressFinalize(this);
      }
      ~LsaWrapper()
      {
         Dispose();
      }
      // helper functions

      static LSA_UNICODE_STRING InitLsaString(string s)
      {
         // Unicode strings max. 32KB
         if (s.Length > 0x7ffe)
            throw new ArgumentException("String too long");
         LSA_UNICODE_STRING lus = new LSA_UNICODE_STRING();
         lus.Buffer = s;
         lus.Length = (ushort) (s.Length * sizeof(char));
         lus.MaximumLength = (ushort) (lus.Length + sizeof(char));
         return lus;
      }
   }
}
'@




    if (-not ($server.EndsWith(".ap.aderant.com", "CurrentCultureIgnoreCase"))) {
        $server = "$server.$((gwmi WIN32_ComputerSystem).Domain)"
    }

    [PSCredential]$credentials = Get-Credential "ADERANT_AP\service.tfsbuild.ap"
    $session = New-PSSession -ComputerName $server -Credential $credentials -Authentication Credssp -ErrorAction Stop

    $setupScriptBlock = {
        param (
            [PSCredential]$credentials,
            [bool]$skipDownload
        )

        # Make me fast
        $powerPlan = Get-WmiObject -Namespace root\cimv2\power -Class Win32_PowerPlan -Filter "ElementName = 'High Performance'"
        $powerPlan.Activate()

        # Make me admin
        Add-LocalGroupMember -Group Administrators -Member ADERANT_AP\tfsbuildservice$ -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group Administrators -Member $credentials.UserName -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group docker-users -Member ADERANT_AP\tfsbuildservice$ -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group docker-users -Member $credentials.UserName -ErrorAction SilentlyContinue
        Add-LocalGroupMember -Group Administrators -Member ADERANT_AP\SG_AP_Dev_Operations -ErrorAction SilentlyContinue

        [string]$scriptsDirectory = "$env:SystemDrive\Scripts"

        New-Item -Path $scriptsDirectory -ItemType Directory -ErrorAction SilentlyContinue

        Push-Location $scriptsDirectory

        $credentials | Export-Clixml -Path "$scriptsDirectory\credentials.xml"

        if (-not $skipDownload) {
            Write-Host "Downloading build agent zip"
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $currentProgressPreference = $ProgressPreference
            $ProgressPreference = "SilentlyContinue"

            try {
                Invoke-WebRequest -Uri http://go.microsoft.com/fwlink/?LinkID=851123 -OutFile "vsts.agent.zip" -UseBasicParsing
            } finally {
                $ProgressPreference = $currentProgressPreference
            }
        }

        Import-Module ServerManager

        if (-not (Get-WindowsFeature | Where-Object {$_.Name -eq "Hyper-V"}).InstallState -eq "Installed") {
            Enable-WindowsOptionalFeature -Online -FeatureName:Microsoft-Hyper-V -All
        }

        # Return the machine specific script home
        return $scriptsDirectory
    }

    [string]$scriptsDirectory = (Invoke-Command -Session $session -ScriptBlock $setupScriptBlock -ArgumentList $credentials, $skipAgentDownload.IsPresent)[1]
    
    
    
    Invoke-Command -Session $session -ScriptBlock {
        param (
              [string]$code
        )
          
        Add-Type $code
        $m = [Willys.LsaSecurity.LsaWrapper]::new()
        $m.AddPrivileges("ADERANT_AP\tfsbuildservice$", "SeAssignPrimaryTokenPrivilege")
        $m.AddPrivileges("ADERANT_AP\tfsbuildservice$", "SeIncreaseQuotaPrivilege")
        $m.Dispose()
                  
    } -ArgumentList $lsaCode

    Write-Host "Generating Scheduled Tasks"

    <# 
    ============================================================
    Setup Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        param (
            [string]$scriptsDirectory,
            $credentials,
            [string]$agentPool
        )

        if (-not [string]::IsNullOrWhiteSpace($agentPool)) {
            Write-Host "Setting agent pool: $agentPool"
            [Environment]::SetEnvironmentVariable('AgentPool', $agentPool, 'Machine')
        }

        $STTrigger = New-ScheduledTaskTrigger -AtStartup
        [string]$STName = "Setup Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\setup-agent-host.ps1" -WorkingDirectory $scriptsDirectory
        #Configure when to stop the task and how long it can run for. In this example it does not stop on idle and uses the maximum possible duration by setting a timelimit of 0
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User $credentials.UserName -Password $credentials.GetNetworkCredential().Password -Settings $STSettings -RunLevel Highest -Force
    } -ArgumentList $scriptsDirectory, $credentials, $agentPool


    <# 
    ============================================================
    Clean up agent Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $STTrigger = New-ScheduledTaskTrigger -AtStartup
        [string]$STName = "Cleanup Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\cleanup-agent-host.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User $credentials.UserName -Password $credentials.GetNetworkCredential().Password -Settings $STSettings -RunLevel Highest -Force
    } -ArgumentList $scriptsDirectory, $credentials


    <# 
    ============================================================
    Reboot Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $STTrigger = New-ScheduledTaskTrigger -Daily -At 11pm
        [string]$STName = "Restart Agent Host"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\restart-agent-host.ps1" -WorkingDirectory $scriptsDirectory
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
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
        [string]$STName = "Refresh Agent Host Scripts"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        #Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\refresh-agent-host.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        #Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force
     } -ArgumentList $scriptsDirectory


    <# 
    ============================================================
    Docker Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        [string]$docker = "$env:ProgramFiles\Docker\Docker\Docker for Windows.exe"

        if (Test-Path $docker) {
            if ((Get-WmiObject -Class Win32_Service -Filter "Name='docker'") -eq $null) {
                Write-Host "Registering Docker service"
                & "$env:ProgramFiles\Docker\Docker\resources\dockerd.exe" --register-service
            }

            $interval = New-TimeSpan -Minutes 1
            $STTrigger = New-ScheduledTaskTrigger -AtStartup -RandomDelay ($interval)
            
            [string]$STName = "Run Docker for Windows"
            Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue

            #Action to run as
            $STAction = New-ScheduledTaskAction -Execute $docker
            $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

            #Register the new scheduled task
            Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger -User $credentials.UserName -Password $credentials.GetNetworkCredential().Password -Settings $STSettings -RunLevel Highest -Force
        }
    } -ArgumentList $scriptsDirectory, $credentials

    <#
    ============================================================
    Cleanup space Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $interval = New-TimeSpan -Minutes 5
        $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
        [string]$STName = "Reclaim Space"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        # Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\make-free-space-vnext.ps1" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        # Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force 
    } -ArgumentList $scriptsDirectory

    <# 
    ============================================================
    Cleanup NuGet Task
    ============================================================
    #>
    Invoke-Command -Session $session -ScriptBlock {
        $interval = New-TimeSpan -Hours 2
        $STTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval $interval
        [string]$STName = "Remove NuGet Cache"
        
        Unregister-ScheduledTask -TaskName $STName -Confirm:$false -ErrorAction SilentlyContinue
        
        # Action to run as
        $STAction = New-ScheduledTaskAction -Execute "$Env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File $scriptsDirectory\Build.Infrastructure\Src\Scripts\make-free-space-vnext.ps1 -strategy nuget" -WorkingDirectory $scriptsDirectory        
        $STSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -Compatibility Win8

        $principal = New-ScheduledTaskPrincipal -UserID tfsbuildservice$ -LogonType Password -RunLevel Highest
        # Register the new scheduled task
        Register-ScheduledTask $STName -Action $STAction -Trigger $STTrigger –Principal $principal -Settings $STSettings -Force 
    } -ArgumentList $scriptsDirectory

    Invoke-Command -Session $session -ScriptBlock {
        Remove-Item "$scriptsDirectory\Build.Infrastructure" -Force -Recurse -ErrorAction SilentlyContinue
        & git clone "http://tfs.ap.aderant.com:8080/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure" "$scriptsDirectory\Build.Infrastructure" -q
    } -ArgumentList $scriptsDirectory, $credentials

    if ($restart.IsPresent) {
        Write-Host "Restarting build agent: $($server)"
        shutdown.exe /r /f /t 0 /m \\$server /d P:4:1
    }
}