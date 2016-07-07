param(
    [System.Reflection.Assembly]$assembly, 
    $packagesPath
)

begin {

    $script:dependencies = @{}
    $script:referencePaths = @()
    
    $exlcude = @("Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp", "Microsoft.CodeAnalysis.Workspaces", "System.Diagnostics")

    function LoadAssembly() {
    param(        
        $reference
    )

    $order = @("net462\", "net46\", "net45\", "netstandard", "portable-")

    if (Test-Path $reference.FullName) {
        return [System.Reflection.Assembly]::LoadFrom($reference.FullName)
    } else {
        try {
            return [System.Reflection.Assembly]::Load($reference)
        } catch [System.IO.FileNotFoundException] {

        }

        # Probe for the best match
        $name = $reference.Name 
        if (-not $name.EndsWith(".dll")) {
            $name = ($name + ".dll")
        }

        $files = gci -Path $packagesPath -Filter $name -File -Recurse

        $files = $files | Sort-Object { 
            for ($i = 0; $i -le $order.Count; $i++) {
                if ($_.FullName -like ("*{0}*" -f $order[$i])) {
                    return $i
                }
            }

            return -1
        } 

        return LoadAssembly ($files | Select-Object -First 1)    
        }
    }

    function GetReferencesForAssembly() {
    param(
        [System.Reflection.Assembly]$parentAssembly,
        $reference,
        [bool]$referencePath
    )   

    if (-not ($reference -is [System.Reflection.Assembly])) {
        $asm = LoadAssembly $reference

        $asmVersion = $asm.GetName().Version
        if ($asmVersion -ne $reference.Version) {
            #throw "Loaded $asmVersion assembly does not match $reference"
        }

    } else {
        $asm = $reference
    }
        
    $isInCache = $false

    if ($asm -ne $null) {
        $isInCache = $asm.GlobalAssemblyCache
    } else {
        Write-Host "Load failure for $reference"
        return
    }   

    if (-not $isInCache) {
        if ($referencePath) {
            $path = (new-object System.Uri $asm.CodeBase).LocalPath
        
            Write-Host ("Resolved reference path to " + [System.IO.Path]::GetDirectoryName($path))
            $script:referencePaths += [System.IO.Path]::GetDirectoryName($path)
            return
        }
    
        if (-not ($dependencies.ContainsKey([string]$asm.FullName))) {
            Write-Host "Adding runtime dependency $($asm.FullName) from $($asm.CodeBase) of $parentAssembly"

            $script:dependencies.Add([string]$asm.FullName, $asm.CodeBase)
                            
            GetReferences $asm
        } else {
            Write-Host "Ignoring $($asm.FullName)"
        }
    }
    }

    function GetReferences() {
        param(
            [System.Reflection.Assembly]$asm
        )

        $references = $asm.GetReferencedAssemblies()
    
        foreach ($reference in $references) {
        
            $referencePath = $false
            foreach ($excludeItem in $exlcude) {
                if ($reference.Name -like $excludeItem) {
                    $referencePath = $true
                }
            }                       
        
            Write-Host "Finding referenced assembly: $($reference.FullName) of $asm"
            GetReferencesForAssembly $asm $reference $referencePath
        }
    }  
}  

process { 
    GetReferences $assembly
    
    $results = [PSCustomObject]@{"References"=$script:dependencies; "ReferencePaths"=$script:referencePaths;}

    return $results
}
