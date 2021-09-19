[CmdletBinding(SupportsShouldProcess=$true)]
param (
    # The full path of a PowerShell script to invoke.
    [Parameter(Mandatory=$true, Position=0, ParameterSetName='Script')]
    [ValidateNotNullOrEmpty()]
    [string]
    $Script,

    # Parameters to pass to the script.
    [Parameter(Mandatory=$false, Position=1, ParameterSetName='Script')]
    [ValidateNotNullOrEmpty()]
    [string]
    $Parameters,

    # The PowerShell command to invoke.
    [Parameter(Mandatory=$true, Position=2, ParameterSetName='Command')]
    [ValidateNotNullOrEmpty()]
    [string]
    $Command,

    # The name of the transcript to produce if a command is being invoked.
    [Parameter(Mandatory=$false, Position=3)]
    [ValidateNotNullOrEmpty()]
    [string]
    $TranscriptName
)

begin {
    Set-StrictMode -Version "Latest"
    $InformationPreference = "Continue"
    $VerbosePreferemce = "Continue"
    $ErrorActionPreference = "Stop"

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

            # The transcript to upload as an attachment.
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
}

process {
    switch ($PSCmdlet.ParameterSetName) {
        'Script' {
            if (-not (Test-Path -Path $Script)) {
                Write-Error -Message "$Script does not exist." -ErrorAction 'Continue'
                exit -1
            }

            if (-not [string]::IsNullOrWhiteSpace($TranscriptName)) {
                $name = $TranscriptName
            } else {
                $name = [System.IO.Path]::GetFileNameWithoutExtension($Script)
            }
            break
        }
        'Command' {
            if ([string]::IsNullOrWhiteSpace($TranscriptName)) {
                Write-Error 'A transcript name must be provided for commands.'
                exit -1
            }

            $name = $TranscriptName
            break
        }
    }

    [string]$transcript = [System.IO.Path]::Combine($workingDirectory, $name + ".log.txt")

    try {
        Start-Transcript -Path $transcript -Force

        switch ($PSCmdlet.ParameterSetName) {
            'Script' {
                Write-Information -MessageData "Invoking PowerShell script: '$Script'."

                if (-not [string]::IsNullOrWhiteSpace($Parameters)) {
                    Write-Information -MessageData "Parameters: $Parameters"
                    & $Script $Parameters
                } else {
                    & $Script
                }

                break
            }
            'Command' {
                & $Command

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