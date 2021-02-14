Set-StrictMode -Version 'Latest'
$InformationPreference = 'Continue'

[string[]]$dfsPaths = @(
    '\\SVFP311\E$\DFS\Expert-ci\prebuilts\v1',
    '\\SVFP312\E$\DFS\Expert-ci\prebuilts\v1'
)

foreach ($dfs in $dfsPaths) {
    [System.IO.DirectoryInfo[]]$shaPrefixes = Get-ChildItem -Path $dfs -Directory -Force

    if ($null -eq $shaPrefixes -or $shaPrefixes.Length -eq 0) {
        Write-Information -MessageData "Directory '$dfs' is empty."
        continue
    }

    foreach ($shaPrefix in $shaPrefixes) {
        [System.IO.DirectoryInfo[]]$shaSuffixes = Get-ChildItem -Path $shaPrefix.FullName -Directory -Force

        if ($null -eq $shaSuffixes -or $shaSuffixes.Length -eq 0) {
            # Remove empty sha prefix directories.
            Write-Information -MessageData "Removing '$($directory.FullName)' as it is empty."
            Remove-Item -Path $directory.FullName -Recurse -Force
        }

        foreach ($shaSuffix in $shaSuffixes) {
            [System.IO.DirectoryInfo[]]$builds = Get-ChildItem -Path $shaSuffix.FullName -Directory -Force

            if ($null -eq $builds -or $builds.Length -eq 0) {
                # Remove empty sha suffix directories.
                Write-Information -MessageData "Removing '$($shaSuffix.FullName)' as it is empty."
                Remove-Item -Path $shaSuffix.FullName -Recurse -Force
            }

            [DateTime]$dateTime = [System.DateTime]::UtcNow
            $dateTime = $dateTime.AddMonths(-6)

            # Get all artifacts prior to 6 months ago.
            [System.IO.DirectoryInfo[]]$buildsToDelete = $builds | Where-Object { $null -ne $_ -and (Get-Member -InputObject $_ -Name 'LastWriteTimeUtc' -MemberType 'Properties') -and $_.LastWriteTimeUtc -lt $dateTime }

            # Remove filtered artifacts.
            $buildsToDelete | Where-Object { $null -ne $_ } | ForEach-Object {
                Write-Information -MessageData "Removing '$($_.FullName)'";
                Remove-Item -Path $_.FullName -Recurse -Force
            }
        }
    }
}