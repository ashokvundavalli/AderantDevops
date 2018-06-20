Add-Type -AssemblyName "System.Runtime.Serialization.Json, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
$settings = [System.Runtime.Serialization.Json.DataContractJsonSerializerSettings]::new()
$settings.EmitTypeInformation = [System.Runtime.Serialization.EmitTypeInformation]::Always
$settings.UseSimpleDictionaryFormat = $false
$serializer = [System.Runtime.Serialization.Json.DataContractJsonSerializer]::new([hashtable], $settings)

function global:PutCacheItem() {
    [CmdletBinding()]
    param ([string]$key, $value, [string[]]$fileDependencies)
    Begin {
        Set-StrictMode -Version Latest
    }
    
    Process {
        $key = $key + ".json"
        
        $stream = [System.IO.MemoryStream]::new()
        $serializer.WriteObject($stream, $value)
        $stream.Position = 0

        $reader = [System.IO.StreamReader]::new($stream)

        $cacheItem = [pscustomobject]@{
            Key = $key
            Value = $reader.ReadToEnd()
            Dependencies = $fileDependencies
        }

        $jsonValue =  ConvertTo-Json $cacheItem

        $cacheFile = [System.IO.Path]::Combine($ShellContext.CacheDirectory, $key)
        $jsonValue | Out-File -FilePath $cacheFile -Encoding utf8 -Force
    }

    End {
        $stream.Dispose()
        $reader.Dispose()
    }
}

function global:GetCacheItem() {
    [CmdletBinding()]
    param ([string]$key)

    Begin {
        Set-StrictMode -Version Latest
    }
    
    Process {
        $stream = $null
        
        $key = $key + ".json"

        $cacheFile = [System.IO.FileInfo]::new([System.IO.Path]::Combine($ShellContext.CacheDirectory, $key))
        if ($cacheFile.Exists) {
            $cacheItem = Get-Content -Raw -Path $cacheFile | ConvertFrom-Json
                        
            if ($cacheItem.Dependencies) {
                foreach ($dependency in $cacheItem.Dependencies) {
                    $dependencyFile = [System.IO.FileInfo]::new($dependency)
                    if ($dependencyFile.LastWriteTimeUtc -gt $cacheFile.LastWriteTimeUtc) {
                        # Cached file is invalid
                        Write-Debug "
Cache invalidated for file $($cacheFile.FullName). 
$($dependencyFile.FullName): $($dependencyFile.LastWriteTimeUtc) > $($cacheFile.FullName): $($cacheFile.LastWriteTimeUtc)
"
                        return $null
                    } else {
                        Write-Debug "
Cache dependency up-to-date for file $($cacheFile.FullName). 
$($dependencyFile.FullName): $($dependencyFile.LastWriteTimeUtc) < $($cacheFile.FullName): $($cacheFile.LastWriteTimeUtc)
"
                    }
                }
            }

            Write-Debug "Cache up to date for file $($cacheFile.FullName)"
            Write-Debug ""
            Write-Debug $cacheItem.Value
            Write-Debug ""
        
            $byteArray = [System.Text.Encoding]::UTF8.GetBytes($cacheItem.Value)
            $stream = [System.IO.MemoryStream]::new($byteArray)

            try {
                $result = $serializer.ReadObject($stream)
                if ($result) {
                    return $result
                }
            } catch {
                Write-Debug $_
                $cacheFile.Delete()
            }
        }

        Write-Debug "Cache miss for $key"
        return $null
    }

    End { 
        if ($stream) {
            $stream.Dispose()
        }
    }
}