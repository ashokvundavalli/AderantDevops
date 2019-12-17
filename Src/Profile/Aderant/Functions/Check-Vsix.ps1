function Check-Vsix() {
    [CmdletBinding()]
    param (
        [parameter(Mandatory = $true)][string] $vsixName,
        [parameter(Mandatory = $true)][string] $vsixId,
        [parameter(Mandatory = $false)][string] $idInVsixmanifest = $vsixId
    )

    Begin {
        [Reflection.Assembly]::Load("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089") | Out-Null

        function Output-VSIXLog {
            $errorsOccurred = $false
            $temp = $env:TEMP
            $lastLogFile = Get-ChildItem $temp | Where { $_.Name.StartsWith("VSIX") } | Sort LastWriteTime | Select -last 1
            if ($lastLogFile -ne $null) {
                $logFileContent = Get-Content $lastLogFile.FullName
                foreach ($line in $logFileContent) {
                    if ($line.Contains("Exception")) {
                        $errorsOccurred = $true
                        Write-Host -ForegroundColor Red $line
                        notepad $lastLogFile.FullName
                    }
                }
            }
            return $errorsOccurred
        }

        function InstallVsix() {
            try {
                $vsixFile = gci -Path $global:ShellContext.BuildToolsDirectory -File -Filter "$vsixName.vsix" -Recurse | Select-Object -First 1

                if (-not ($vsixFile)) {
                    return
                }

                Write-Host "Installing $vsixName..."

                # uninstall the extension
                Write-Host "Uninstalling $vsixName..."

                $vsInstallPath = $env:VS140COMNTOOLS
                $vsix = "$vsInstallPath..\IDE\VSIXInstaller.exe"

                Start-Process -FilePath $vsix -ArgumentList "/q /uninstall:$($vsixId)" -Wait -PassThru | Out-Null

                if ($vsixFile.Exists) {
                    Write-Host "Installing VSIX..."

                    Start-Process -FilePath $vsix -ArgumentList "/quiet $($vsixFile.FullName)" -Wait -PassThru | Out-Null
                    $errorsOccurred = Output-VSIXLog

                    if (-not $errorsOccurred) {
                        Write-Host "Updated $($vsixName). Restart Visual Studio for the changes to take effect."
                    } else {
                        Write-Host -ForegroundColor Yellow "Something went wrong here. If you open Visual Studio and go to 'Tools -> Extensions and Updates' check if there is the '$vsixName' extension installed and disabled. If so, remove it by hitting 'Uninstall' and try again."
                    }
                } else {
                    Write-Host -ForegroundColor Yellow "No $vsixName VSIX found"
                }
            } catch {
                Write-Host "Exception occurred while restoring packages" -ForegroundColor Red
                Write-Host $_ -ForegroundColor Red
            }
        }
    }

    Process {
        if (-Not $idInVsixmanifest) {
            $idInVsixmanifest = $vsixId
        }

        $version = ""

        $currentVsixFile = Join-Path -Path $global:ShellContext.BuildToolsDirectory -ChildPath "$vsixName.vsix"

        $extensionsFolder = Join-Path -Path $env:LOCALAPPDATA -ChildPath \Microsoft\VisualStudio\14.0\Extensions\
        $developerTools = Get-ChildItem -Path $extensionsFolder -Recurse -Filter "$vsixName.dll" -Depth 1

        $developerTools.ForEach( {
                $manifest = Join-Path -Path $_.DirectoryName -ChildPath extension.vsixmanifest
                if (Test-Path $manifest) {
                    [xml]$manifestContent = Get-Content $manifest
                    $manifestVersion = $manifestContent.PackageManifest.Metadata.Identity.Version

                    $version = [System.Version]::Parse($manifestVersion)
                }
            })

        $zipFile = $null
        $reader = $null

        if ($version -eq "") {
            Write-Host -ForegroundColor Red " $vsixName for Visual Studio is not installed."
            Write-Host -ForegroundColor Red " If you want it, install them manually from $currentVsixFile"
        } else {
            # Bail out if we have already checked if this version is installed (most often the case)
            $lastVsixCheckCommit = $global:ShellContext.GetRegistryValue("", "LastVsixCheckCommit")
            if ($lastVsixCheckCommit -ne $null) {
                if ($lastVsixCheckCommit -eq $global:ShellContext.CurrentCommit) {
                    Write-Debug "CurrentCommit: $($global:ShellContext.CurrentCommit)"
                    Write-Debug "LastVsixCheckCommit: $($lastVsixCheckCommit)"

                    Write-Host -ForegroundColor DarkGreen "Your $vsixName is up to date."
                    return
                }
            }

            Write-Host " * Found installed version $version"

            if (-not (Test-Path $currentVsixFile)) {
                Write-Host -ForegroundColor Red "Error: could not find file $currentVsixFile"
                return
            }

            $zipFile = [System.IO.Compression.ZipFile]::OpenRead($currentVsixFile)
            $rawFiles = $zipFile.Entries

            foreach ($rawFile in $rawFiles) {
                if ($rawFile.Name -eq "extension.vsixmanifest") {
                    try {
                        $archiveEntryStream = $rawFile.Open()

                        $reader = [System.IO.StreamReader]::new($archiveEntryStream)
                        [xml]$currentManifestContent = $reader.ReadToEnd()

                        $foundVersion = [System.Version]::Parse($currentManifestContent.PackageManifest.Metadata.Identity.Version)
                        Write-Host " * Current version is $foundVersion"

                        if ($foundVersion -gt $version) {
                            Write-Host
                            Write-Host "Updating $vsixName..."
                            InstallVsix
                        } else {
                            Write-Host -ForegroundColor DarkGreen "Your $vsixName is up to date."
                        }
                    } finally {
                        $archiveEntryStream.Dispose()
                        $reader.Dispose()
                    }
                    break
                }
            }
        }
    }

    End {
        if ($zipFile) {
            $zipFile.Dispose()
        }
        if ($reader) {
            $reader.Dispose()
        }
    }
}