# Shows the help for an AddIn module
function Show-ExtensionHelp($module) {   
    if ($module -eq $null) {
        return
    }
    
    Write-Host "The following additional aliases are defined"
    Write-Host ""
    
    $aliasHelp = New-Object "System.Collections.Generic.Dictionary[string, string]"
    $commandHelp = New-Object "System.Collections.Generic.Dictionary[string, string]"
    
    foreach ($command in $module.ExportedFunctions.Values) {
        $help = ExtractSynopsisFromHelp $command
        
        if ($help -ne $null) {
            $alias = Get-Alias -Definition $command -ErrorAction SilentlyContinue
            
            if ($alias -ne $null) {
                $aliasHelp.Add($alias.Name, $help)
            } else {
                $commandHelp.Add($command.Name, $help)
            }            
        }
    }
    
    WriteCommands $aliasHelp    
    WriteCommands $commandHelp
}


function WriteCommands($commandDictionary) {
    foreach ($key in $commandDictionary.Keys) {
        $cmd = $key.PadRight(30)
        Write-Host "$cmd -> "$commandDictionary[$key]
    }
}


function ExtractSynopsisFromHelp([string]$command) {
    $help = Get-Help $command
    
    # show the first line of the documentation
    
    if ($help.Details.Description -ne $null) {
        $desc = $help.Synopsis
        return $desc
    }
    return $null
}

Export-ModuleMember -function Show-ExtensionHelp