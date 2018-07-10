    Function global:GetBuildLibraryAssemblyPath([string]$buildScriptDirectory) {
        $file = [System.IO.Path]::Combine($buildScriptDirectory, "..\Build.Tools\Aderant.Build.dll")
        return [System.IO.Path]::GetFullPath($file)
    }

    Function global:GetBuildAnalyzerLibraryAssemblyPath([string]$buildScriptDirectory) {
        $file = [System.IO.Path]::Combine($buildScriptDirectory, "..\Build.Tools\Aderant.Build.Analyzer.dll")
        return [System.IO.Path]::GetFullPath($file)
    }

    Function global:CompileBuildLibraryAssembly($buildScriptDirectory, [bool]$forceCompile) {	
        $aderantBuildAssembly = GetBuildLibraryAssemblyPath $buildScriptDirectory
        $aderantBuildAnalyzerAssembly = GetBuildAnalyzerLibraryAssemblyPath $buildScriptDirectory

        if ([System.IO.File]::Exists($aderantBuildAssembly) -and [System.IO.File]::Exists($aderantBuildAnalyzerAssembly) -and $forceCompile -eq $false) {
            return
        }

        try {
            $buildUtilities = [System.Reflection.Assembly]::Load("Microsoft.Build.Utilities.Core, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            $toolsVersion = "14.0";
            Write-Debug "Loaded MS Build 14.0"
        } catch {
            $buildUtilities = [System.Reflection.Assembly]::Load("Microsoft.Build.Utilities.v12.0, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            $toolsVersion = "12.0";
            Write-Debug "Falling back to MS Build 12.0"
        }

        $MSBuildLocation = [Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToBuildTools([Microsoft.Build.Utilities.ToolLocationHelper]::CurrentToolsVersion, [Microsoft.Build.Utilities.DotNetFrameworkArchitecture]::Bitness32)

        Write-Debug ("Resolved MS Build {0}" -f $MSBuildLocation)

        $global:MSBuildLocation = [System.IO.Path]::Combine($MSBuildLocation, "MSBuild.exe")
        $projectPath = [System.IO.Path]::Combine($buildScriptDirectory, "Aderant.Build.Common.targets")
        & $global:MSBuildLocation $projectPath "/p:BuildScriptsDirectory=$buildScriptDirectory" "/nologo" "/m" "/nr:false"
    }

    Function global:LoadLibraryAssembly([string]$buildScriptDirectory) {
        $modules = Get-Module
        foreach ($module in $modules) {
            if ($module.Name.Contains("Aderant.Build")) {
                Write-Debug "Aderant PowerShell C# module already loaded"
                return
            }
        }
		
        $file = GetBuildLibraryAssemblyPath $buildScriptDirectory
		# Looks like the environment hasn't been configured yet, set it up now
		if (-not (Test-Path $file)) {
			CompileBuildLibraryAssembly $buildScriptDirectory
		}

        # Load all DLLs to suck in the dependencies of our code
        $buildTools = [System.IO.Path]::GetDirectoryName($file)
        Write-Debug "Loading dependencies from $buildTools"

        $files = @(Get-ChildItem -Path "$buildTools\*" -Filter "*Aderant.Build*.dll")
        $files += ([System.IO.FileInfo]"$buildScriptDirectory\paket.exe")

        foreach ($item in $files) {
            $pdbFile = [System.IO.Path]::ChangeExtension($item.Name, ".pdb")
            $pdb = gci -Path $buildTools -Filter $pdbFile -File

            $assembly = $null

            if ($pdb) {
                Write-Debug "Loading $item with symbols"
                $assembly = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($item.FullName), [System.IO.File]::ReadAllBytes($pdb.FullName))
            } else {
                $assembly = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($item.FullName))
            }

            if ($item.Name -eq "Aderant.Build.dll") {
                Import-Module $assembly -DisableNameChecking -Global
            }
        }
    }