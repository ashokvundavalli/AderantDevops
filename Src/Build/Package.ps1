[CmdletBinding()]
param(
    [string]$repository,
    [switch]$replicate
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

    [string] $tags = ''

    # Try to get version number from the current git branch infomation.
    try {
        $text = (& $gitVersion $repository /output json | Out-String )    

        $versionJson = ConvertFrom-Json $text
        $versionSem = $versionJson.FullSemVer

		$tags = "repo:" + $Env:BUILD_REPOSITORY_NAME + " branch:" + $versionJson.BranchName + " sha:" + $versionJson.Sha + " build:" + $Env:BUILD_BUILDID
		Write-Host $tags

        if (-not $versionSem) {
            Write-Host "##vso[task.logissue type=error;] Unable to get gitversion. The last returned text is: $text"
        } else {
            Write-Host "Full version is $versionSem"
			$tags +=  " buildNumber:" + $versionSem
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

    $packResult = New-ExpertPackage -Repository $repository -Version $version -Replicate $replicate.IsPresent

    if ($packResult) {
        if (Test-Path $packResult.OutputPath) {

            $packages = gci -Path $packResult.OutputPath -Filter *.nupkg | Where { $_.Name.EndsWith($version + $_.Extension) }

			# modify nuspec file to include commit hash value in the tags section (only for CI build)
			if ($Env:BUILD_REPOSITORY_NAME) {
				foreach ($packageFile in $packages) {
                
					$zipFilePath = [IO.Path]::ChangeExtension($packageFile.FullName, '.zip')
					Rename-Item -Path $packageFile.FullName -NewName $zipFilePath
					$extractedDirectoryName = $packageFile.Basename
					$extractedDirectoryPath = Join-Path $packResult.OutputPath $extractedDirectoryName
					New-Item -Force -ItemType Directory -Path $extractedDirectoryPath
					Expand-Archive $zipFilePath -DestinationPath $extractedDirectoryPath

					$nuspecFile = (gci -Path $extractedDirectoryPath -Filter *.nuspec)[0].FullName

					$nuspecXml = [xml] (Get-Content $nuspecFile)
					$metadataNode = $nuspecXml.package.metadata

					$tagsNode = $nuspecXml.CreateElement('tags', 'http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd')
					$tagsNode.set_InnerText($tags)
					$metadataNode.AppendChild($tagsNode)

					$nuspecXml.Save($nuspecFile)

					gci $nuspecFile | Compress-Archive -DestinationPath $zipFilePath -Update
					Rename-Item -Path $zipFilePath -NewName $packageFile.FullName

					Remove-Item $extractedDirectoryPath -Recurse -Force
				}
			}

            if (($Env:BUILD_SOURCEBRANCHNAME -eq "master"  -or $Env:BUILD_SOURCEBRANCH -like "*releases/*" -or $Env:BUILD_SOURCEBRANCH -like "refs/heads/dev" -or $Env:BUILD_SOURCEBRANCH -like "refs/heads/patch/81SP1" -or $Env:BUILD_SOURCEBRANCH -like "refs/heads/update/82*") -and -not $global:IsDesktopBuild) {
                $packagingProcess = [Aderant.Build.Packaging.PackageProcessor]::new($Host.UI)

                $buildNumber = ("{0} {1}" -f $Env:BUILD_REPOSITORY_NAME, $versionSem)
                $packagingProcess.UpdateBuildNumber($buildNumber)

                Write-Host "Pushing packages from: $($packResult.OutputPath)"
                
                gci -Path $packResult.OutputPath -Filter *.nupkg | Where-Object { $_.Name -NotMatch "Aderant.Database.Backup" } | % { PushPackage $_.FullName }

                if ($Env:BUILD_SOURCEBRANCHNAME -eq "master") {
                    # Associate the package to the build. This allows TFS garbage collect the outputs when the build is deleted      
                    $packagingProcess.AssociatePackagesToBuild($packages)
                }

                # Special dropping location for database backup due to the size: \\dfs.aderant.com\Packages\ExpertDatabase.
                if ($env:BUILD_DEFINITIONNAME -contains "Database") {
                    gci -Path $packResult.OutputPath -Filter Aderant.Database.Backup*.nupkg | % { xcopy /i /y $_.FullName "\\dfs.aderant.com\Packages\ExpertDatabase\$versionSem\" }
                }
            }
        }
    } else {
        Write-Host "Packaging skipped"
    }
}

end {
  
}

