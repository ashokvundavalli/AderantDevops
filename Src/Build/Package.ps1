<#
#>
[CmdletBinding()]
param([string]$repository)

begin {    
    
}

process {        
    $packResult = [Aderant.Build.Packaging.Packager]::Package($repository)    

    # TODO: Abstract to property
    $paket = [System.IO.Path]::Combine($Env:EXPERT_BUILD_FOLDER, "paket.exe")

    if (-not $IsDesktopBuild) {
        $url = "http://packages.ap.aderant.com/packages/"
        gci -Path $packResult.OutputPath -Filter *.nupkg | % { & $paket push file $_.FullName url $url apikey " " }
    }
}

end {
  
}

