[CmdetBinding()]
param
(
   [Parameter(Mandatory=$true)][string] $Repository,
   [Parameter(Mandatory=$false)][string] $Version
)

$ErrorActionPreference = 'Stop'

Trace-VstsEnteringInvocation $MyInvocation

Write-Host $Repository
Write-Host $Version
Write-Host $distributedTaskContext