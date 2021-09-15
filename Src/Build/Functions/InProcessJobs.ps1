function Start-JobInProcess {
    [CmdletBinding()]
    param
    (
        [string] $Name,
        [ScriptBlock] $ScriptBlock,
        $ArgumentList,
        [ScriptBlock] $CompleteAction
    )

    $state = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault2()
    $state.ThreadOptions = [System.Management.Automation.Runspaces.PSThreadOptions]::UseNewThread
    $state.DisableFormatUpdates = $true
    $state.ApartmentState = [System.Threading.ApartmentState]::MTA

    $powerShell = [PowerShell]::Create($state)

    $handle = [PSCustomObject]@{
        $Name = $Name
        CompleteAction = $CompleteAction
        AsyncResult = $null
        CompleteActionData = $null
        SourceIdentifier = [System.Guid]::NewGuid()
    }

    $null = Register-ObjectEvent $powerShell -EventName InvocationStateChanged -SourceIdentifier ($handle.SourceIdentifier) -MessageData $handle -Action {
        if ($EventArgs.InvocationStateInfo.State -eq [System.Management.Automation.PSInvocationState]::Running) {
            return
        }

        function InvokeWriteError() {
            param($errorRecord)
                # Background runspace cannot use Write-Error without a lot of gymnastics
                $ps = [PowerShell]::Create([System.Management.Automation.RunspaceMode]::CurrentRunspace)

                [void]$ps.AddScript('$Host.UI.WriteLine()')
                [void]$ps.Invoke()
                [void]$ps.Commands.Clear()

                [void]$ps.AddCommand("Write-Error")
                [void]$ps.AddParameter("ErrorRecord", $errorRecord)
                [void]$ps.AddCommand("Out-Default")
                [void]$ps.Commands.Commands[0].MergeMyResults([System.Management.Automation.Runspaces.PipelineResultTypes]::Error, [System.Management.Automation.Runspaces.PipelineResultTypes]::Output)
                [void]$ps.Invoke()
        }

        try {
            # Remove this event as we are now done
            Unregister-Event -SourceIdentifier ($Event.MessageData.SourceIdentifier)

            # Complete the async call using the wait handle
            $pipelineResult = $Sender.EndInvoke($Event.MessageData.AsyncResult)

            if ($Sender.HadErrors) {
                InvokeWriteError $Sender.Streams.Error[0]
                return
            }

            if ($EventArgs.InvocationStateInfo.State -eq [System.Management.Automation.PSInvocationState]::Failed) {
                InvokeWriteError $EventArgs.InvocationStateInfo.Reason
                return
            }

            try {
                $completeCallback = $Event.MessageData.CompleteAction
                if ($null -ne $completeCallback) {
                    $Event.MessageData.CompleteAction.Invoke($Event, $pipelineResult)
                }
            } catch {
                InvokeWriteError $_
            }
        } finally {
            $Sender.Runspace.Dispose()
            $Sender.Dispose()
        }
    }

    # Take a copy of the script block as the provided one is bound to the calling runspace
    # and will run in that context when invoked which will cause BeginInvoke to block
    $code = $ScriptBlock.ToString()

    [void]$powershell.AddScript('Set-StrictMode -Version "Latest"; [ScriptBlock]::Create($Args[0]).Invoke($Args[1])')
    [void]$powershell.AddArgument($code)
    [void]$powershell.AddArgument($ArgumentList)

    $handle.AsyncResult = $powerShell.BeginInvoke()
}