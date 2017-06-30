[CmdletBinding()]
param(
    [string]$repository
)

begin {
    . $PSScriptRoot\Build-Libraries.ps1

    function PushPackage([string]$package) {
        if (-not $package) {
            throw "No package provided"
        }

        Invoke-Tool -FileName $paket -Arguments "push file $package url http://packages.ap.aderant.com/packages/ apikey `" `"" -RequireExitCodeZero | 
            ForEach-Object {
            $_
        }
    }
}

process {
    # Never package a pull request
    if ($Env:BUILD_SOURCEBRANCH -like "refs/pull/*") {
        return
    }

    # TODO: Abstract to property
    $paket = [System.IO.Path]::Combine($Env:EXPERT_BUILD_DIRECTORY, "Build", "paket.exe")
    $gitVersion = [System.IO.Path]::Combine($Env:EXPERT_BUILD_DIRECTORY, "Build", "GitVersion.exe")

    Write-Host "Calculating version for: $repository"

    # Try to get version number from the current git branch infomation.
    try {
        $text = (& $gitVersion $repository /output json | Out-String )    

        $versionJson = ConvertFrom-Json $text
        $versionSem = $versionJson.FullSemVer

        if (-not $versionSem) {
            Write-Host "##vso[task.logissue type=error;] Unable to get gitversion. The last returned text is: $text"
        } else {
            Write-Host "Full version is $versionSem"
        }
    } catch {
        Write-Error "##vso[task.logissue type=error;] Failed to get correct version number from the git repository. Returned info from GitVersion:"
        Write-Error "$text"
        
        throw "Failed to generate a version for the repository. Inspect the logs for more detail."
    }
    
    Write-Host "Creating package version"

    # For NuGet sorting we need to remove all dashes from the package name, otherwise we can't have different release channels per branch
    # as the sorting algorithm will get confused
    $version = [Aderant.Build.Packaging.Packager]::CreatePackageVersion($text)

    Write-Host "Creating package(s)..."

    $packResult = New-ExpertPackage $repository $version

    if ($packResult) {
        if (Test-Path $packResult.OutputPath) {
            [System.IO.FileInfo[]]$packages = gci -Path $packResult.OutputPath -Filter *.nupkg

            if (($Env:BUILD_SOURCEBRANCHNAME -eq "master"  -or $Env:BUILD_SOURCEBRANCH -like "*releases/*" -or $Env:BUILD_SOURCEBRANCH -like "refs/heads/dev" -or $Env:BUILD_SOURCEBRANCH -like "refs/heads/patch*") -and -not $global:IsDesktopBuild) {
                $packagingProcess = [Aderant.Build.Packaging.PackageProcessor]::new($Host.UI)

                $buildNumber = ("{0} {1}" -f $Env:BUILD_REPOSITORY_NAME, $versionSem)
                $packagingProcess.UpdateBuildNumber($buildNumber)

                Write-Host "Pushing packages from: $($packResult.OutputPath)"
                
                gci -Path $packResult.OutputPath -Filter *.nupkg | Where-Object { $_.Name -NotMatch "Aderant.Database.Backup" } | % { PushPackage $_.FullName }

                # Associate the package to the build. This allows TFS garbage collect the outputs when the build is deleted      
                $packagingProcess.AssociatePackagesToBuild($packages)

                # Special treatment for database backup as the file size is too large. Skipped zipping into a nuget but copying the raw .bak file. Always drop to \\dfs.aderant.com\Packages\ExpertDatabase with overwriting.
                if ($env:BUILD_DEFINITIONNAME -contains "Database") {
                    gci -Path $binariesDirectory -Filter Expert*.bak | % { xcopy /i /y $_.FullName "\\dfs.aderant.com\Packages\ExpertDatabase\$versionSem" }
                }
            }
        }
    } else {
        Write-Host "Packaging skipped"
    }
}

end {
  
}

