<#
.SYNOPSIS

.DESCRIPTION
  
#>
function Set-BuildStatus {
    [CmdletBinding()]   
    param(        
        [object]
        $Context = $TaskContext,

        [Parameter(Mandatory=$true)]
        [ValidateSet('Started', 'Completed', 'Failed')]     
        $Status,
        
        [Parameter(Mandatory=$true)]        
        $Reason
    )

    begin {
        Set-StrictMode -Version Latest      
    }

    process {
        $Context.BuildStatus = $Status
        $Context.BuildStatusReason = $Reason
    }
}