#Requires -RunAsAdministrator

function global:Install-VisualStudioComponents {
    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Continue'
        $InformationPreference = 'Continue'

        [string[]]$versions = @('2017', '2019')
        [string[]]$editions = @('Professional', 'Enterprise')
        [string]$visualStudioInstaller = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
    }

    process {
        foreach ($version in $versions) {
            [string]$vs = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\$version"

            foreach ($edition in $editions) {
                [string]$vsPath = [System.IO.Path]::Combine($vs, $edition)

                if ([System.IO.Directory]::Exists($vsPath)) {
                    Write-Information -MessageData "Installing required components for Visual Studio $version $edition."

                    $process = $null

                    try {
                        $process = Start-Process -FilePath $visualStudioInstaller -ArgumentList @(
                                'modify',
                                "--installPath `"$vsPath`"",
                                '--passive',
                                '--norestart',
                                '--add Microsoft.VisualStudio.Component.VSSDK',
                                '--add Microsoft.VisualStudio.Component.DslTools',
                                '--add Microsoft.VisualStudio.Component.Workflow',
                                '--add Microsoft.VisualStudio.Component.Roslyn.Compiler',
                                '--add Microsoft.Component.MSBuild',
                                '--add Microsoft.VisualStudio.Component.TextTemplating'
                            ) -PassThru -Wait

                        switch ($process.ExitCode) {
                            0 {
                                Write-Information -MessageData "Visual Studio Installer exited with code: $($process.ExitCode)."
                                break
                            }                            
                            3010 {
                                Write-Warning -Message "Visual Studio Installer exited with code: $($process.ExitCode) - Operation completed successfully, but install requires reboot before it can be used."
                                break
                            }
                            default {
                                Write-Error -Message "Visual Studio Installer exited with code: $($process.ExitCode). Please run the installer manually, or see https://docs.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2019#error-codes for details."
                            }
                        }
                    } finally {
                        if ($null -ne $process) {
                            $process.Dispose()
                            $process = $null
                        }
                    }
                } else {
                    Write-Debug "Skipped installing required components for Visual Studio $version $edition as it was not found at path: '$vsPath'."
                }
            }
        }
    }
}