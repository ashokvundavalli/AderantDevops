﻿function Retry-Command {
    param (
        [Parameter(Mandatory=$true)][ScriptBlock]$Command,
        [Parameter(Mandatory=$false)][int]$Retries = 5,
        [Parameter(Mandatory=$false)][int]$MinutesToWait = 5
    )

    Set-StrictMode -Version 'Latest'
    Start-Transcript -Path "$Env:SystemDrive\Scripts\restart-agent-host.task.log" -Force

    # Remove cruft left behind by old script version
    Remove-Item ".\RestartAgentHostLog.txt" -ErrorAction "SilentlyContinue" -Verbose

    [int]$retrycount = 0
    [bool]$completed = $false

    while (-not $completed) {
        try {
            & $Command
            $completed = $true
        } catch {
            if ($retrycount -ge $Retries) {
                Write-Host ("Command {0} failed the maximum number of {1} times." -f $Command, $retrycount)
                throw
            } else {
                $delay = ($MinutesToWait * 60)

                Write-Host ("{0}. Retrying in {1} seconds." -f $_.Exception.Message, $delay)

                Start-Sleep -Seconds $delay
                $retrycount++
            }
        } finally {
            Stop-Transcript
        }
    }
}

Retry-Command -Command {
    $processCount = @(Get-Process MSBuild -ErrorAction SilentlyContinue).Count
    $processCount += @(Get-Process VSTest* -ErrorAction SilentlyContinue).Count
    $processCount += @(Get-Process Git -ErrorAction SilentlyContinue).Count

    if ($processCount -eq 0) {
        Write-Information "Can restart!"
        Restart-Computer -Force
    } else {
        throw "Something build related is running."
    }
}