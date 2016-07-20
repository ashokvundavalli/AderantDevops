[CmdletBinding()]
param(
    [string]$repository
)

begin {

}

process {
    # TODO: Abstract to property
    $paket = [System.IO.Path]::Combine($Env:EXPERT_BUILD_FOLDER, "Build", "paket.exe")
    $gitVersion = [System.IO.Path]::Combine($Env:EXPERT_BUILD_FOLDER, "Build", "GitVersion.exe")

    $text = (& $gitVersion $repository /output json | Out-String )    

    $versionJson = ConvertFrom-Json $text
    
    # For NuGet sorting we need to remove all dashes from the package name, otherwise we can't have different release channels per branch
    # as the sorting algorithm will get confused
    $version = [Aderant.Build.Packaging.Packager]::CreatePackageVersion($text)

    $packResult = [Aderant.Build.Packaging.Packager]::Package($repository, $version)

    if (-not $global:IsDesktopBuild) {
        Write-Host "Pushing packages from: $($packResult.OutputPath)"
        
        gci -Path $packResult.OutputPath -Filter *.nupkg | % { & $paket push file $_.FullName url "http://packages.ap.aderant.com/packages/" apikey " " }        
    }

    Write-Output ("##vso[task.setvariable variable=build.buildnumber;]" + $versionJson.FullSemVer)
    Write-Output ("##vso[build.updatebuildnumber]" + $versionJson.FullSemVer)
}

end {
  
}

