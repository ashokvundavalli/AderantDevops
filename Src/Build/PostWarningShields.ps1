[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [bool]$inError, 

    [Parameter(Mandatory=$true)]
    [string]$thisBuild, 

    [Parameter(Mandatory=$true)]
    [string]$lastBuild
)

$ErrorActionPreference = 'Stop'

[void][System.Reflection.Assembly]::Load("System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")

function GetWidth() {
    param([Xml]$doc)   
   
    return $doc.svg.width
}

function GetHeight() {
    param([Xml]$doc)
  
    return $doc.svg.height
}

$color = $null    
if ($inError) {
    $color = [System.Drawing.Color]::Red
} else {
    $color = [System.Drawing.Color]::Green
}

# Import the sheild rendering function(s) into this scope, this helps with passing complex types to the function. 
. $PSScriptRoot\DrawShield.ps1

[string]$lastBuildSvg = DrawShield -subject "last build" -status $lastBuild -color ([System.Drawing.Color]::Green) -style Plastic
[string]$thisBuildSvg = DrawShield -subject "this build" -status $thisBuild -color $color -style Plastic 

$items = @()
$items += $lastBuildSvg
$items += $thisBuildSvg

$tempDir = [System.IO.Path]::GetTempPath() + [System.IO.Path]::GetRandomFileName()
New-Item -ItemType Directory -Path $tempDir -Force -ErrorAction SilentlyContinue | Out-Null

$tempFile = "$tempDir\Warnings.md"

$stream = [System.IO.StreamWriter] $tempFile

foreach ($svgText in $items) {
    $width = GetWidth $svgText
    $height = GetHeight $svgText
    
    # What we have here is much research (attempts) at finding an escape pattern that works acrosss all browsers (IE, Firefox and Chrome)            
    $encodedData = [System.Uri]::EscapeDataString($svgText)
    $encodedData = $encodedData.Replace("(", "%28")
    $encodedData = $encodedData.Replace(")", "%29")
    $encodedData = $encodedData.Replace("%2F", "/")
    $encodedData = $encodedData.Replace("%0D%0A", "%0A")    
                
    $div = [string]::Format("<div style=`"background: url(data:image/svg+xml,{0}) no-repeat; height: {1}px; width: {2}px;`"></div>", @($encodedData, $height, $width))

    $stream.WriteLine($div)
}

$stream.WriteLine($str)

$stream.Close()
$stream.Dispose()

# Since uploads of files are queued and processed asynchronously we cannot remove this file here 
# as we will remove it before the agent has uploaded it to TFS
Write-Output "##vso[task.addattachment type=Distributedtask.Core.Summary;name=Warnings;]$tempFile"