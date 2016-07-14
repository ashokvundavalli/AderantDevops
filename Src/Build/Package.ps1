<#
#>
[CmdletBinding()]
param([string]$repository)

begin {    
}

process {        
    $packResult = [Aderant.Build.Packaging.Packager]::Package($repository)    

    # TODO: Abstract to property
    $paket = [System.IO.Path]::Combine($Env:EXPERT_BUILD_FOLDER, "Build", "paket.exe")

    if (-not $global:IsDesktopBuild) {
        Write-Host "Pushing packages from: $($packResult.OutputPath)"
        
        gci -Path $packResult.OutputPath -Filter *.nupkg | % { & $paket push file $_.FullName url "http://packages.ap.aderant.com/packages/" apikey " " }


        ##vso[artifact.associate type=filepath;artifactname=MyFileShareDrop]\\MyShare\MyDropLocation
    }
}

end {
  
}

