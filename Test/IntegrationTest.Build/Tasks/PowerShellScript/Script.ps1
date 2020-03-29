[CmdletBinding()]
param(
  [Parameter(Mandatory=$true, HelpMessage="MyArg")]
  [string]$MyArg
)

Write-Information $MyArg
$MyArg