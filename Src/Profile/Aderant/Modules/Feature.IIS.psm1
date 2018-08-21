<#
.SYNOPSIS
Looks for web applications registered in IIS that do not map to the file system

.PARAMETER virtualPath
Virtual path to search within

.EXAMPLE
Hunt-Zombies -VirtualPath 'Expert_Local'

Finds all the zombie applications in Expert_Local 
#>
function Hunt-Zombies {
    param(
        [Parameter(Mandatory = $false)] [string] $virtualPath = ''
    )
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Warning "You do not have Administrator rights to run this script`nPlease re-run this script as an Administrator"
        Break
    }

    Import-Module WebAdministration
    Import-Module ApplicationServer

    if ([String]::IsNullOrWhitespace($virtualPath)) {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site'
    } else {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site' -VirtualPath $virtualPath
    }
    
    foreach ($webApp in $expertWebApplications) {
        if (-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
            if ($webApp.ApplicationName) {
                $iisPath = $webApp.IISPath
                $filePath = $webApp.PhysicalPath
                Write-Output "Found zombie web application $iisPath, could not find path $filePath."
            }    
        }
    }    
    Write-Output 'Zombie hunt complete.'
}

Export-ModuleMember Hunt-Zombies

<#
.SYNOPSIS
Removes web applications registered in IIS that do not map to the file system

.PARAMETER virtualPath
Virtual path to search within

.EXAMPLE
Remove-Zombies -VirtualPath 'Expert_Local'

Removes all the zombie applications in Expert_Local 
#>
function Remove-Zombies {
    param(
        [Parameter(Mandatory = $false)] [string] $virtualPath = ''
    )

    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Warning "You do not have Administrator rights to run this script`nPlease re-run this script as an Administrator"
        Break
    }

    Import-Module WebAdministration
    Import-Module ApplicationServer

    if ([String]::IsNullOrWhitespace($virtualPath)) {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site'
    } else {
        $expertWebApplications = get-ASApplication -SiteName 'Default Web Site' -VirtualPath $virtualPath
    }
    
    foreach ($webApp in $expertWebApplications) {
        if (-not ((Test-Path $webApp.IISPath) -band (Test-Path $($webApp.PhysicalPath)))) {
            if ($webApp.ApplicationName) {
                $iisPath = $webApp.IISPath
                Remove-Item -Path $iisPath
                Write-Output "Removed zombie web application $iisPath"
            }
        }
    }
    Write-Output 'Zombie removal complete.'
}

Export-ModuleMember -Function Remove-Zombies

<#
.Synopsis
    Cleans the old cache files created by IIS Dynamic Compilation.
.Description
    Following files will be deleted in the module:
        *\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files\*
    Only the files which created X time ago would be removed.
.PARAMETER Days
    Removed all the caches which created/modified $days ago.
.PARAMETER Directory
    Specify the destination of the IIS Cache.
.EXAMPLE
    CleanupIISCache -days 0
#>
function CleanupIISCache {
    param(
        [Parameter(Mandatory = $false)][int] $days = 1,
        [Parameter(Mandatory = $false)][string] $directory = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files'
    )

    $lastWriteTime = (Get-Date).AddDays( - $days)
    Get-ChildItem $directory | Where-Object { $_.LastWriteTime -lt $lastWriteTime} | Remove-Item -Recurse -Force
}

Export-ModuleMember -Function CleanupIISCache

<# 
.Synopsis 
    Get the process Ids and App pool names of all running IIS Worker Processes.  Handy for deciding which w3wp process to attach to in VS.
.Description   
    Get the process Ids and App pool names of all running IIS Worker Processes.  Handy for deciding which w3wp process to attach to in VS.
.PARAMETER all
    Switch - boolean value to return all IIS Worker Processes otherwise just get ones using ExpertApplications_Local App pool (most common thing people debug)
.EXAMPLE
    wpid -all
#>
function Get-WorkerProcessIds() {
    param ([switch] $all = $false)

    $process = "w3wp.exe"
    $processObjects = Get-WmiObject Win32_Process -Filter "name = '$process'" | Select-Object Handle, CommandLine

    ForEach ($processObject in $processObjects) {
        $commandLine = $processObject.CommandLine.Substring($processObject.CommandLine.IndexOf("`"") + 1)
        $commandLineAndHandle = $commandLine.Substring(0, $commandLine.IndexOf("`"")) + " --> " + $processObject.Handle
        
        If ($all -eq $true) {
            Write-Host $commandLineAndHandle -ForegroundColor Green
        } Elseif ($commandLine.StartsWith("ExpertApplications_")) {
            Write-Host $commandLineAndHandle -ForegroundColor Green
        }
    }
}

Export-ModuleMember -Function Get-WorkerProcessIds