[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)]
  [string]$MyArg,

  [Parameter(Mandatory=$false)]
  [string]$MyArg1,

  [Parameter(Mandatory=$true)]
  [ValidateNotNullOrEmpty()]
  [string]$MyArg3,

  [Parameter(Mandatory=$false)]
  [bool]$MyArg4
)

$MyArg
$MyArg1
$MyArg3
$MyArg4