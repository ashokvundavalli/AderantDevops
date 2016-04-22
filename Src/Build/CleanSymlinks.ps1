{
[param]$path

    $dirs = Get-ChildItem $path -Name -Filter Dependencies -Depth 1 -Attributes D -Recurse 
    $dirs | ForEach-Object { 
    $targetPath = "$path\$_"
    if (Test-Path $targetPath) { 
        try {
            gci $targetPath -ErrorAction Stop
        } catch [System.IO.DirectoryNotFoundException] {
            [System.IO.Directory]::Delete($targetPath)
        }
    }
  }
}
