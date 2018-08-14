<#
.Synopsis
    Generates a System Map to the expertsource folder of the current branch
.Description
    Generates a System Map using the the default ITE information to the expertsource folder of the current branch
#>
function Generate-SystemMap() {
    $inDirectory = Join-Path $global:BranchBinariesDirectory 'ExpertSource'
    $systemMapConnectionString = (Get-SystemMapConnectionString)
    if ($systemMapConnectionString.ToLower().Contains("/pdbs:") -and $systemMapConnectionString.ToLower().Contains("/pdbd:") -and $systemMapConnectionString.ToLower().Contains("/pdbid:") -and $systemMapConnectionString.ToLower().Contains("/pdbpw:")) {
        $connectionParts = $systemMapConnectionString.Split(" ")
        Write-Debug "Connection is [$connectionParts]"
        & $inDirectory\Systemmapbuilder.exe /f:$inDirectory /o:$inDirectory\systemmap.xml /ef:Customization $connectionParts[0] $connectionParts[1] $connectionParts[2] $connectionParts[3]
    } else {
        Write-Error "Connection string is invalid for use with systemmapbuilder.exe [$systemMapConnectionString]"
    }
}