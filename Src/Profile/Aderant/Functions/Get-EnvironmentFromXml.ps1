function Get-EnvironmentFromXml([string]$xpath) {
    #I'd love it if this returned an object model representation such as Environment.expertPath or Environment.networkSharePath
    if ([string]::IsNullOrEmpty($xpath)) {
        Write-Host -ForegroundColor Yellow "You need to specify an xpath expression";
        return $null;
    }
    if (Test-Path variable:global:BranchBinariesDirectory) {
        $environmentXmlPath = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml");
        [xml]$xml = Get-Content $environmentXmlPath;
        $returnValue = Select-Xml $xpath $xml;
        return $returnValue;
    } else {
        Write-Host -ForegroundColor Yellow "I don't know where your Branch Binaries Directory is.";
    }
    return $null;
}