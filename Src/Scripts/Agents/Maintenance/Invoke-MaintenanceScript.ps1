[CmdletBinding(SupportsShouldProcess=$true)]
param (
    # The full path of a PowerShell script to invoke.
    [Parameter(Mandatory=$true, Position=0, ParameterSetName='Script')]
    [ValidateNotNullOrEmpty()]
    [string]
    $Script,

    # Paramaters to pass to the script.
    [Parameter(Mandatory=$false, Position=1, ParameterSetName='Script')]
    [ValidateNotNull()]
    [Hashtable]
    $Parameters = @{},

    # The PowerShell command to invoke.
    [Parameter(Mandatory=$true, Position=2, ParameterSetName='Command')]
    [ValidateNotNullOrEmpty()]
    [string]
    $Command,

    # The name of the transcript to produce if a command is being invoked.
    [Parameter(Mandatory=$true, Position=3, ParameterSetName='Command')]
    [ValidateNotNullOrEmpty()]
    [string]
    $TranscriptName
)

begin {
    Set-StrictMode -Version 'Latest'
    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'

    function Send-Alert {
        [CmdletBinding()]
        param (
            # The subject of the email.
            [Parameter()]
            [ValidateNotNullOrEmpty()]
            [string]
            $Subject,

            # The content of the email.
            [Parameter()]
            [ValidateNotNullOrEmpty()]
            [string]
            $Body,

            # The transcript to upload as an attatchment.
            [Parameter()]
            [ValidateNotNullOrEmpty()]
            [string]
            $Transcript
        )

        begin {
            [string]$recipient = 'devops.ap@aderant.com'
            [string]$smtpServer = 'smtp.dev.ap.aderant.com'
        }

        process {
            Send-MailMessage -To $recipient -Subject "[DevOps Infrastructure] $Subject" -From "$Env:COMPUTERNAME@aderant.com" -Body $Body -Attachments $Transcript -SmtpServer $smtpServer
        }
    }

    function Stop-TranscriptSafe {
        try {
            Stop-Transcript
        } catch [System.Management.Automation.PSInvalidOperationException] {
            # Transcript is already stopped.
        }
    }

    [string]$workingDirectory = Join-Path -Path ([System.IO.Path]::GetPathRoot([System.Environment]::SystemDirectory)) -ChildPath 'Scripts'

    Stop-TranscriptSafe
}

process {
    switch ($PSCmdlet.ParameterSetName) {
        'Script' {
            if (-not (Test-Path -Path $Script)) {
                Write-Error -Message "$Script does not exist." -ErrorAction 'Continue'
                exit -1
            }

            [string]$name = [System.IO.Path]::GetFileNameWithoutExtension($Script)
            break
        }
        'Command' {
            [string]$name = $TranscriptName
            
            break
        }
    }
    
    [string]$transcript = Join-Path -Path $workingDirectory -ChildPath "${name}Log.txt"

    try {
        Start-Transcript -Path $transcript -Force
        
        switch ($PSCmdlet.ParameterSetName) {
            'Script' {
                Write-Information -MessageData "Invoking PowerShell script: '$Script'."
                & $Script @Parameters

                break
            }
            'Command' {
                Invoke-Expression -Command $Command
                
                break
            }
        }
    } catch [System.Exception] {
        if ($PSCmdlet.ShouldProcess('Stop Transcript, Email')) {
            Stop-TranscriptSafe        
            Send-Alert -Subject "FAILURE: $name" -Body ([string]::Concat($Env:COMPUTERNAME, [System.Environment]::NewLine, [System.Environment]::NewLine, $_.Exception.ToString())) -Transcript $transcript -WarningAction 'SilentlyContinue'
        }

        exit 1
    } finally {
        Stop-TranscriptSafe
    }
}