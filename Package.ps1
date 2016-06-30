& $paket pack output $PSScriptRoot\Bin\Packages buildconfig Debug buildplatform AnyCPU

$url = "http://packages.ap.aderant.com/packages/"
#gci ".\bin\Packages\" -Filter *.nupkg | % { & $paket push file $_.FullName url $url apikey "abc" }