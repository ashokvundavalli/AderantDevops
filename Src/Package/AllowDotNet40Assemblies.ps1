# Workaround for PowerShell 2.0 not being able to load .NET 4.0 assemblies 
$contents = '<?xml version="1.0"?> 
<configuration> 
    <startup useLegacyV2RuntimeActivationPolicy="true"> 
        <supportedRuntime version="v4.0.30319"/> 
        <supportedRuntime version="v2.0.50727"/> 
    </startup>
    <runtime>
        <loadFromRemoteSources enabled="true"/>
    </runtime>
</configuration>'

New-Item $pshome\powershell.exe.config -ItemType File -Force -Value $contents
New-Item $pshome\powershell_ise.exe.config -ItemType File -Force -Value $contents