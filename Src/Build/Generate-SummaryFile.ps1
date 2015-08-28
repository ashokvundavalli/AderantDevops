param([string] $folder)

begin { 
    $children = Get-ChildItem $folder -Recurse
    $hashes = New-Object System.Text.StringBuilder
}

process {
    for ($i=0; $i -le $children.Count; $i++) {
        if ($children[$i].Name -ne "_summary.hsh")
        {
            if (-not $children[$i].PSIsContainer -and $children[$i].Name -ne $null) {
                #Write-Host File: $children[$i].FullName
                $hash = (Get-FileHash -Path $children[$i].FullName).Hash
                $null = $hashes.AppendLine($hash)
                #Write-Host Hash: $hash
                #Write-Host
            }
            else {
                $null = $hashes.AppendLine("#")
            }
        }
    }

	Remove-Item (Join-Path $folder "_summary.hsh")
    $hashes.ToString() | Out-File (Join-Path $folder "_summary.hsh")
}

