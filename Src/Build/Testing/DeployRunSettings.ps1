param(         
  [string]$Destination,
  [string[]]$Paths
)

Set-StrictMode -Version Latest

[xml]$xml = Get-Content -Path "$PSScriptRoot\default.runsettings"
$assemblyResolution = $xml.RunSettings.MSTest.AssemblyResolution

foreach ($path in $Paths) {
    $directoryElement = $xml.CreateElement("Directory")
    $directoryElement.SetAttribute("path", $path)
    $directoryElement.SetAttribute("includeSubDirectories", "true")

    $assemblyResolution.AppendChild($directoryElement)
}

$sw = [System.IO.StringWriter]::new()
$writer = New-Object System.Xml.XmlTextWriter($sw)
$writer.Formatting = [System.Xml.Formatting]::Indented
$xml.WriteContentTo($writer)

Set-Content -LiteralPath $Destination -Value $sw.ToString()