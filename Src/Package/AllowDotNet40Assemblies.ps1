# Workaround for PowerShell 2.0 not being able to load .NET 4.0 assemblies 
New-Item $pshome\powershell.exe.config -ErrorAction SilentlyContinue -ItemType File -Value '<?xml version="1.0"?> 
<configuration> 
    <startup useLegacyV2RuntimeActivationPolicy="true"> 
        <supportedRuntime version="v4.0.30319"/> 
        <supportedRuntime version="v2.0.50727"/> 
    </startup> 
</configuration>'

New-Item $pshome\powershell_ise.exe.config -ErrorAction SilentlyContinue -ItemType File -Value '<?xml version="1.0"?> 
<configuration> 
    <startup useLegacyV2RuntimeActivationPolicy="true"> 
        <supportedRuntime version="v4.0.30319"/> 
        <supportedRuntime version="v2.0.50727"/> 
    </startup> 
</configuration>'