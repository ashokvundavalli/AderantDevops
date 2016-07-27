param(
    [string]$Repository,
    [string]$Configuration = 'Release',    
    [string]$Platform = "AnyCPU",
    [string]$CommonArgs = "",
    [string]$MSBuildLocation
)


########################################
# Private functions.
########################################
function ConvertFrom-SerializedLoggingCommand {
    [CmdletBinding()]
    param([string]$Message)

    if (!$Message) {
        return
    }

    try {
        # Get the index of the prefix.
        $prefixIndex = $Message.IndexOf($script:loggingCommandPrefix)
        if ($prefixIndex -lt 0) {
            return
        }

        # Get the index of the separator between the command info and the data.
        $rbIndex = $Message.IndexOf(']'[0], $prefixIndex)
        if ($rbIndex -lt 0) {
            return
        }

        # Get the command info.
        $cmdIndex = $prefixIndex + $script:loggingCommandPrefix.Length
        $cmdInfo = $Message.Substring($cmdIndex, $rbIndex - $cmdIndex)
        $spaceIndex = $cmdInfo.IndexOf(' '[0])
        if ($spaceIndex -lt 0) {
            $command = $cmdInfo
        } else {
            $command = $cmdInfo.Substring(0, $spaceIndex)
        }

        # Get the area and event.
        [string[]]$areaEvent = $command.Split([char[]]@( '.'[0] ), [System.StringSplitOptions]::RemoveEmptyEntries)
        if ($areaEvent.Length -ne 2) {
            return
        }

        $areaName = $areaEvent[0]
        $eventName = $areaEvent[1]

        # Get the properties.
        $eventProperties = @{ }
        if ($spaceIndex -ge 0) {
            $propertiesStr = $cmdInfo.Substring($spaceIndex + 1)
            [string[]]$splitProperties = $propertiesStr.Split([char[]]@( ';'[0] ), [System.StringSplitOptions]::RemoveEmptyEntries)
            foreach ($propertyStr in $splitProperties) {
                [string[]]$pair = $propertyStr.Split([char[]]@( '='[0] ), 2, [System.StringSplitOptions]::RemoveEmptyEntries)
                if ($pair.Length -eq 2) {
                    $pair[1] = Format-LoggingCommandData -Value $pair[1] -Reverse
                    $eventProperties[$pair[0]] = $pair[1]
                }
            }
        }

        $eventData = Format-LoggingCommandData -Value $Message.Substring($rbIndex + 1) -Reverse
        New-Object -TypeName psobject -Property @{
            'Area' = $areaName
            'Event' = $eventName
            'Properties' = $eventProperties
            'Data' = $eventData
        }
    } catch { }
}

function Format-LoggingCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Area,
        [Parameter(Mandatory = $true)]
        [string]$Event,
        [string]$Data,
        [hashtable]$Properties)

    # Append the preamble.
    [System.Text.StringBuilder]$sb = New-Object -TypeName System.Text.StringBuilder
    $null = $sb.Append($script:loggingCommandPrefix).Append($Area).Append('.').Append($Event)

    # Append the properties.
    if ($Properties) {
        $first = $true
        foreach ($key in $Properties.Keys) {
            [string]$value = Format-LoggingCommandData $Properties[$key]
            if ($value) {
                if ($first) {
                    $null = $sb.Append(' ')
                    $first = $false
                } else {
                    $null = $sb.Append(';')
                }

                $null = $sb.Append("$key=$value")
            }
        }
    }

    # Append the tail and output the value.
    $Data = Format-LoggingCommandData $Data
    $sb.Append(']').Append($Data).ToString()
}

function Format-LoggingCommandData {
    [CmdletBinding()]
    param([string]$Value, [switch]$Reverse)

    if (!$Value) {
        return ''
    }

    if (!$Reverse) {
        foreach ($mapping in $script:loggingCommandEscapeMappings) {
            $Value = $Value.Replace($mapping.Token, $mapping.Replacement)
        }
    } else {
        for ($i = $script:loggingCommandEscapeMappings.Length - 1 ; $i -ge 0 ; $i--) {
            $mapping = $script:loggingCommandEscapeMappings[$i]
            $Value = $Value.Replace($mapping.Replacement, $mapping.Token)
        }
    }

    return $Value
}


# Don't show the logo and do not allow node reuse so all child nodes are shut down once the master
# node has completed build orchestration.
$arguments = $CommonArgs

$loggerAssembly = "$Env:AGENT_HOMEDIRECTORY\agent\Worker\Microsoft.TeamFoundation.DistributedTask.MSBuild.Logger.dll"
$arguments = "$arguments /dl:CentralLogger,`"$loggerAssembly`"*ForwardingLogger,`"$loggerAssembly`""

# Start the detail timeline.        
$detailId = [guid]::NewGuid()
$detailStartTime = [datetime]::UtcNow.ToString('O')
Write-VstsLogDetail -Id $detailId -Type Process -Name "Run MS Build" -Progress 0 -StartTime $detailStartTime -State Initialized -AsOutput        

$detailResult = 'Succeeded'
try {          
    $knownDetailNodes = @{ }
    Invoke-VstsTool -FileName $MSBuildLocation\MSBuild.exe -Arguments $arguments -RequireExitCodeZero |
        ForEach-Object {
            if ($_ -and
                $_.IndexOf($script:loggingCommandPrefix) -ge 0 -and
                ($command = ConvertFrom-SerializedLoggingCommand -Message $_)) {
                if ($command.Area -eq 'task' -and
                    $command.Event -eq 'logissue' -and
                    $command.Properties['type'] -eq 'error') {

                    # An error issue was detected. Set the result to Failed for the logdetail completed event.
                    $detailResult = 'Failed'
                } elseif ($command.Area -eq 'task' -and
                    $command.Event -eq 'logdetail' -and
                    !$NoTimelineLogger) {

                    # Record known detail nodes and manipulate the parent project ID if required.
                    $id = $command.Properties['id']
                    if (!$knownDetailNodes.ContainsKey($id)) {
                        # The detail node is new.

                        # Check if the parent project ID is null or empty.
                        $parentProjectId = $command.Properties['parentid']
                        if (!$parentProjectId -or [guid]$parentProjectId -eq [guid]::Empty) {
                            # Default the parent ID to the root ID it is a new node and does not have a parent ID.
                            $command.Properties['parentid'] = $detailId.ToString('D')
                        }

                        # Track the detail node as known.
                        $knownDetailNodes[$id] = $null
                    }

                    if ($projFile = $command.Properties['name']) {
                        # Make the project file relative.
                        if ($projFile.StartsWith("$solutionDirectory\", [System.StringComparison]::OrdinalIgnoreCase)) {
                            $projFile = $projFile.Substring($solutionDirectory.Length).TrimStart('\'[0])
                        } else {
                            $projFile = [System.IO.Path]::GetFileName($projFile)
                        }

                        # If available, add the targets to the name.
                        if ($targetNames = $command.Properties['targetnames']) {
                            $projFile = "$projFile ($targetNames)"
                        }

                        $command.Properties['name'] = $projFile
                    }
                }

                Write-LoggingCommand -Command $command -AsOutput
            } else {
                $_
            }
                    
    }

    if ($LASTEXITCODE -ne 0) {
        Write-VstsSetResult -Result Failed -DoNotThrow
    }
} finally {
    # Complete the detail timeline.            
    $detailFinishTime = [datetime]::UtcNow.ToString('O')
    Write-VstsLogDetail -Id $detailId -FinishTime $detailFinishTime -Progress 100 -State Completed -Result $detailResult -AsOutput            
}