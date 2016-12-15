<#
.Synopsis
    Functions relating to the modules
.Example

.Remarks
#>

    $global:IsTeamBuild = $Env:TF_BUILD -ne $null

    ###
    # Loads the local dependency manifest
    ###
    Function global:LoadManifest([string]$manifestPath) {
        $path = Join-Path -Path $manifestPath -ChildPath "DependencyManifest.xml"
        if (Test-Path $path) {
            return Get-Content $path -Force
        }
        Write-Warning "No dependency manifest exists at $manifestPath"
    }

    ###
    # Loads the branch expert manifest
    ###
    Function global:LoadExpertManifest([string]$buildScriptsDirectory) {
        return Get-Content ($buildScriptsDirectory + "\..\Package\ExpertManifest.xml")
    }

    <#
    .Synopsis
        Finds the module within the Product Manifest
    .Description
        Performs a case insenstive search for a module
    .Parameter modules
        The Modules node of the Product Manifest
    .Parameter name
        The name of the module
    #>
    Function global:FindModuleFromManifest([System.Xml.XmlNode]$modules, [string]$name) {
        Write-Debug "Looking for module $name"

        $name = $name.ToLowerInvariant()
        return [System.Xml.XmlNode]$modules.SelectSingleNode("Module[translate(@Name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') = '$name']")
    }

    ##
    # Resolves the path to the binaries for the given module
    ##
    Function global:GetPathToBinaries([System.Xml.XmlNode]$module, [string]$dropPath){

        $action = FindGetActionTag $module
        Switch ($action)
        {
          "local"  { LocalPathToModuleBinariesFor $module }
          "local-external-module"  { LocalPathToThirdpartyBinariesFor $module }
          "current-branch-external-module"  { ThirdpartyBinariesPathFor  $module $dropPath $action}
          "other-branch-external-module"  { ThirdpartyBinariesPathFor  $module $dropPath $action}
          "other-branch"  { ServerPathToModuleBinariesFor $module $dropPath $action}
          "current-branch"  { ServerPathToModuleBinariesFor $module $dropPath $action}
          "specific-path" { ServerPathToModuleBinariesFor $module $module.Path $action}
          "specific-path-external-module" { ThirdpartyBinariesPathFor $module $module.Path $action}
          Default { throw "invalid action [$action]"}
        }
    }

    ##
    # Resolves the path to the binaries for the given module
    ##
    Function global:GetLocalPathToBinaries([System.Xml.XmlNode]$module, [string]$localPath){

        if((IsThirdParty $module) -or (IsHelp $module)){
            return LocalPathToThirdpartyBinariesFor $module $localPath
        }else{
            return LocalPathToModuleBinariesFor $module $localPath
        }
    }

    ##
    # Find the get action for this module
    ##
    Function global:FindGetActionTag([System.Xml.XmlNode]$module) {

        [bool]$getModuleLocally=$false
        [bool]$getModuleFromAnotherBranch=$false
        [bool]$getModuleFromSpecificPath=$false

        if ($module.HasAttribute("GetAction")) {
            if ($module.GetAction.ToLower().Equals("branch")) {
                $getModuleFromAnotherBranch = $true
            } elseif ($module.GetAction.ToLower().Equals("specificdroplocation")) {
                $getModuleFromSpecificPath = $true
            } elseif ($module.GetAction.ToLower().Equals("local")) {
                $getModuleLocally = $true
            }
        }

        if ($getModuleFromAnotherBranch) {
            if ((IsThirdparty $module) -or (IsHelp $module)) {
                return "other-branch-external-module"
            }
            return "other-branch"
        } elseif ($getModuleLocally){
            if ((IsThirdparty $module) -or (IsHelp $module)) {
                return "local-external-module"
            }
            return "local"
        } elseif ($getModuleFromSpecificPath){
            if ((IsThirdparty $module) -or (IsHelp $module)) {
                return "specific-path-external-module"
            }
            return "specific-path"
        } else {
            if ((IsThirdparty $module) -or (IsHelp $module)) {
                return "current-branch-external-module"
            }
            return "current-branch"
        }
    }

    ##
    # Change and Test the drop path to a new branch
    ##
    Function global:ChangeBranch([string]$dropPath, [string]$branchName) {
        if ($dropPath.IndexOf($branchName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $dropPath
        }

        $branchName = $branchName.ToLower()
        $dropPath = $dropPath.ToLower()

        if ($dropPath.IndexOf("main", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $start = $dropPath.Substring(0, $dropPath.LastIndexOf("expertsuite\"))
        } elseif ($dropPath.IndexOf("dev", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $start = $dropPath.Substring(0, $dropPath.LastIndexOf("dev\"))
        } elseif ($dropPath.IndexOf("releases", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $start = $dropPath.Substring(0, $dropPath.LastIndexOf("releases\"))
        }

        $changedRoot = (Join-Path $start ('\'+$branchName))

        if (Test-Path $changedRoot -ErrorAction 1) {
            return $changedRoot
        } else {
            Throw(New-Object System.IO.DirectoryNotFoundException "path to branch [$changedRoot] is invalid")
        }
    }

    ###
    # Is this a thirdparty module?
    ###
    Function global:IsThirdparty($module){
        $name = $null

        if ($module.GetType().FullName -like "System.Xml*") {
            $name = $module.Name
        } else {
            $name = $module
        }

        return $name -like "thirdparty.*"
    }

    ###
    # Is this the help module?
    ###
    Function global:IsHelp([System.Xml.XmlNode]$module){
        return ($module.Name.ToLower().Contains(".help") -and -not $module.Name.ToLower().Contains("admin"))
    }

    ##
    # Local binaries path
    ##
    Function global:LocalPathToModuleBinariesFor([System.Xml.XmlNode]$module, [string]$localPath){
        if(!$localPath) {
            $localPath = [System.IO.Path]::Combine($env:ExpertDevBranchFolder, "Modules")
        }
        $localModulePath = Join-Path -Path (Join-Path -Path $localPath -ChildPath $module.Name ) -ChildPath '\Bin\Module'
        return $localModulePath
    }

    ##
    # Local thirdparty binaries path
    ##
    Function global:LocalPathToThirdpartyBinariesFor([System.Xml.XmlNode]$module, [string]$localPath){
        if(!$localPath) {
            $localPath = [System.IO.Path]::Combine($env:ExpertDevBranchFolder, "Modules", "ThirdParty")
        }
        $localThirdpartyModulePath = Join-Path -Path (Join-Path -Path $localPath -ChildPath $module.Name ) -ChildPath '\Bin'
        return $localThirdpartyModulePath
    }

    ##
    # Thirdparty binaries path from the drop
    ##
    Function global:ThirdpartyBinariesPathFor([System.Xml.XmlNode]$module, [string]$dropPath, [string]$action="current-branch-external-module"){
        if (!$dropPath) {
            $rootPath = (Get-DropRootPath)
        } else {
            $rootPath = $dropPath
        }

        if ($action.Equals("other-branch-external-module") -and ![string]::IsNullOrEmpty($module.Path)) {
            $rootPath = ChangeBranch $rootPath $module.Path
        }

        if ($action.Equals("specific-path-external-module") -and ![string]::IsNullOrEmpty($module.Path)){
            $rootPath = $module.Path
        }

        return (Join-Path $rootPath  ($module.Name+'\Bin'))
    }

    ##
    # Versioned binaries path from the drop
    ##
    Function global:ServerPathToModuleBinariesFor([System.Xml.XmlNode]$module, [string]$dropPath, [string]$action="current-branch"){
        if (!$dropPath) {
            $rootPath = (Get-DropRootPath)
        } else {
            $rootPath = $dropPath
        }

        if ($action.Equals("other-branch") -and ![string]::IsNullOrEmpty($module.Path)) {
            $rootPath = ChangeBranch $rootPath $module.Path
        }

        if ($action.Equals("specific-path") -and ![string]::IsNullOrEmpty($module.Path)) {
            $rootPath = $module.Path
        }

        $binModule = '\Bin\Module'

        $pathToModuleAssemblyVersion = Join-Path -Path (Join-Path $rootPath $module.Name) -ChildPath $module.AssemblyVersion

        if ($module.HasAttribute("FileVersion")) {
            $modulePath = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion  -ChildPath $module.FileVersion) -ChildPath $binModule
        } else {
            $modulePath = PathToLatestSuccessfulBuild $pathToModuleAssemblyVersion -suppressThrow
        }
        return $modulePath
    }

    ##
    # Versioned test binaries path from the drop
    ##
    Function global:ServerPathToModuleTestBinariesFor([System.Xml.XmlNode]$module, [string]$dropPath) {
        if (!$dropPath) {
            $rootPath = (Get-DropRootPath)
        } else {
            $rootPath = $dropPath
        }

        $pathToModuleAssemblyVersion = Join-Path -Path (Join-Path -Path $rootPath -ChildPath $module.Name) -ChildPath $module.AssemblyVersion

        if ($module.HasAttribute("FileVersion")) {
            $testBinPath = Join-Path -Path (Join-Path -Path $pathToModuleAssemblyVersion  -ChildPath $module.FileVersion) -ChildPath '\Bin\Test'
        } else {
            [string]$latestSuccessfulPath = PathToLatestSuccessfulBuild $pathToModuleAssemblyVersion -suppressThrow
            $testBinPath = Join-Path -Path $latestSuccessfulPath -ChildPath '..\Test'

            if ($testBinPath -eq $null) {
                return $null
            }
        }

        $path = [string][System.IO.Path]::GetFullPath($testBinPath)

        Write-Host "Server path to test binaries for module $($module.Name) is $path"

        return $path
    }

    ###
    # Find the last successfully build in the drop location.
    ###
    Function global:PathToLatestSuccessfulBuild([string]$pathToModuleAssemblyVersion, [switch]$suppressThrow) {
        $sortedFolders = SortedFolders $pathToModuleAssemblyVersion
        [bool]$noBuildFound = $true
        [string]$pathToLatestSuccessfulBuild = $null

        foreach ($folderName in $sortedFolders) {
            $buildLog = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\BuildLog.txt"
            $pathToLatestSuccessfulBuild = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\Bin\Module"
            [string]$buildFailed = $null

            if ((Test-Path $buildLog) -and (CheckBuild $buildLog) -and (test-path $pathToLatestSuccessfulBuild)) {
                return $pathToLatestSuccessfulBuild
            }
        }

        if ($noBuildFound -and -not $suppressThrow) {
            throw "No latest build found for $pathToModuleAssemblyVersion"
        }

        return $null
    }

    Function global:LatestSuccesfulBuildNumber($module, [string]$dropPath){
        $pathToModuleAssemblyVersion = (Join-Path -Path (Join-Path -Path $dropPath -ChildPath $module.Name) -ChildPath $module.AssemblyVersion)
        $sortedFolders = SortedFolders $pathToModuleAssemblyVersion
        [bool]$noBuildFound = $true
        [string]$pathToLatestSuccessfulBuild = $null

        foreach ($folderName in $sortedFolders) {
            $buildLog = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\BuildLog.txt"
            $pathToLatestSuccessfulBuild = Join-Path -Path( Join-Path -Path $pathToModuleAssemblyVersion -ChildPath $folderName.Name ) -ChildPath "\Bin\Module"
            [string]$buildFailed = $null

            if ((Test-Path $buildLog) -and (CheckBuild $buildLog) -and (test-path $pathToLatestSuccessfulBuild)) {
                return $folderName.Name
            }
        }

        if ($noBuildFound) {
            throw "No latest build number found for $pathToModuleAssemblyVersion"
        }
    }

    ###
    # Find the last successfully package (Build all and package) build in the drop location.
    ###
    Function global:PathToLatestSuccessfulPackage([string]$pathToPackages, [string]$packageZipName){

		# Pad the build index within the same day so the names can be sorted in alphabet order, e.g. 16 -> 0016, 1 -> 0001
		$ToNatural= { [regex]::Replace($_, '\d+',{$args[0].Value.Padleft(4)})}

		$packagingFolders = (dir -Path $pathToPackages |
				where {$_.PsIsContainer -and $_.name.Contains(".BuildAll")} |
				sort $ToNatural -Descending)
        
        foreach ($folderName in $packagingFolders) {
            Write-Info "Testing $folderName"
            
            $buildLog = (Join-Path -Path( Join-Path -Path $pathToPackages -ChildPath $folderName ) -ChildPath "\BuildLog.txt")
            [string]$pathToLatestSuccessfulPackage = (Join-Path -Path( Join-Path -Path $pathToPackages -ChildPath $folderName ) -ChildPath $packageZipName)

            if (Test-Path $pathToLatestSuccessfulPackage) {
                if (CheckBuild $buildLog) {               
                    return $pathToLatestSuccessfulPackage
                } else {
                    Write-Warning "Rejected failed build: $folderName"
                }                
            } else {
                Write-Warning "Rejected $folderName as it doesn't contain a package."
            }
        }
        
        Write-Error "No latest build found for [$pathToPackages]"        
    }

    ###
    # Pads each section of the folder name (which is in the format 1.8.3594.41082) with zeroes, so that an alpha sort
    # can be used because each section will now be of the same length.
    ###
    Function global:SortedFolders( [string]$parentFolder ) {
        if (test-path $parentFolder) {
            $sortedFolders =  (dir -Path $parentFolder |
                        where {$_.PsIsContainer} |
                        Sort-Object {$_.name.Split(".")[0].PadLeft(4,"0")+"."+ $_.name.Split(".")[1].PadLeft(4,"0")+"."+$_.name.Split(".")[2].PadLeft(8,"0")+"."+$_.name.Split(".")[3].PadLeft(8,"0")+"." } -Descending  |
                        select name)

            return $sortedFolders
        } else {
            write-Debug "$parentFolder could not be found because it does not exist"
        }
    }

    ###
    # Delete the files contained in the directory excluding the file name provided
    ###
    Function global:DeleteContentsFromExcludingFile([string]$directory, [string]$excludeFile){
        if (Test-Path $directory){
            Remove-Item $directory\* -Recurse -Force -Exclude $excludeFile
        }
    }

    ###
    # Delete the files contained in the directory
    ###
    Function global:DeleteContentsFrom([string]$directory){
        if (Test-Path $directory) {
            Remove-Item $directory\* -Recurse -Force
        }
    }

    ##
    #
    ##
    Function global:RemoveReadOnlyAttribute($productDirectory){
        Push-Location $productDirectory | attrib -R /S
        Pop-Location
    }

    function global:InvokeRobocopy() {
        try {
            $params = $args
            if ($args -contains "/MT" -and $args -notcontains "/NP") {
                $params += "/NP"
            }

            Write-Host "Calling robocopy with args: $args"
            
            $classes = @("Lonely", "Tweaked", "Same", "Changed", "Newer", "New File", "Older", "[*]Extra File", "Mismatched")
            $maxClassLength = ($classes | Measure-Object -Maximum -Property Length).Maximum
            $regex = "({0})" -f ($classes -join "|")
            $split = [Regex]::new($regex, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

            Write-Debug "Max length: $maxClassLength"

            [string]$robocopyTool = Invoke-Expression "cmd /c where robocopy.exe"

            Invoke-Tool -FileName $robocopyTool -Arguments ($params -join " ") | 
                ForEach {
                    try {
                    if ($_ -and 
                        -not [string]::IsNullOrEmpty($_)) {
                        $parts = $split.Split($_)

                        if ($parts) {
                            [array]$parts = $parts.Where({ -not [string]::IsNullOrWhiteSpace($_) })

                            $parts[0] = "    {0}" -f ($parts[0].PadRight($maxClassLength))

                            if ($parts.Count -gt 1) {
                                $parts[1] = $parts[1].Trim()
                            }
                            $line = ($parts -join " ")

                            # We don't care about extra files in the destination
                            if ($line -like "*EXTRA File*") {
                                Write-Debug $line
                                return
                            }

                            # New files or overwrites are interesting
                            if ($line -like "*New File*") {
                                Write-Success $line
                                return
                            }

                            if ($line -like "*Newer*") {
                                Write-Info $line
                                return
                            }

                            # Source < Destination
                            if ($line -like "*older*") {
                                Write-Host $line -ForegroundColor DarkGray
                                return
                            }
                            
                            Write-Debug $line
                            return
                        }
                        Write-Host $_                       
                    }
                } catch {
                    Write-Debug $_
                }               
            }
        } finally {
            # robocopy has non-standard exit values that are documented here: https://support.microsoft.com/en-us/kb/954404
            # Exit codes 0-8 are considered success, while all other exit codes indicate at least one failure.
            # Some build systems treat all non-0 return values as failures, so we massage the exit code into
            # something that they can understand.            
            if ($global:LASTEXITCODE -lt 8) {
                $global:LASTEXITCODE = 0
            }
        }
    }

    ##
    # Mimic the folder structure and files from one location to another.
    # Using RoboCopy for simplicity as Copy-Item wasn't giving us what we required
    ##
    Function global:CopyContents([string]$copyFrom, [string]$copyTo) {
       if ($copyFrom.EndsWith('\')) {
           $copyFrom = $copyFrom.Remove($copyFrom.LastIndexOf('\'))
       }

       if ($copyTo.EndsWith('\')) {
           $copyTo =  $copyTo.Remove($copyTo.LastIndexOf('\'))
       }
       if (!(Test-Path $copyTo)) {
           New-Item -ItemType Directory -Path $copyTo
       }

       robocopy $($copyFrom.Trim()) $($copyTo.Trim()) /E /XX /NJH /NJS /R:3 /W:5 /NS /NDL /MT /A-:R /XO /V /FP
    }

    ##
    #
    ##
    Function global:CopyModuleBinariesDirectory([string]$from, [string]$to, [bool]$includePdbFiles) {
        Write "Copying $from to $to"
        Write-Debug "Include pdbs? [$includePdbFiles]"
        if ($includePdbFiles) {
            robocopy $from $to /XD service.tfsbuild* /E /XO /NJH /NJS /NP /NFL /NDL /MT /A-:R
        } else {
            robocopy $from $to /XD service.tfsbuild* /XF *.pdb /E /XO /NJH /NJS /NP /NFL /NDL /MT /A-:R
        }
    }

    <#
    Copy only files from the bin\module and bin\test that are built as part of this module for the drop
    i.e. they are not dependencies
    #>
    Function global:CopyBinFilesForDrop([string]$modulePath, [string]$toModuleDropPath, [switch]$testBreak = $false, [switch]$buildBreak = $false, [switch]$suppressUniqueCheck = $false) {
       $dropBinModulePath = Join-Path $toModuleDropPath Bin\Module
       $binTestPath = Join-Path $modulePath Bin\Test
       $dropBinTestPath = Join-Path $toModuleDropPath Bin\Test

       if (!(Test-Path $dropBinModulePath)) {
           New-Item -ItemType Directory -Path $dropBinModulePath | Out-Null
       }

       ResolveAndCopyUniqueBinModuleContent -modulePath $modulePath -copyToDirectory $dropBinModulePath -suppressUniqueCheck:$suppressUniqueCheck

       if ($testBreak -or $buildBreak) {
            New-Item -ItemType File -Path "$toModuleDropPath\build.failed" | Out-Null
        } else {
            New-Item -ItemType File -Path "$toModuleDropPath\build.succeeded" | Out-Null
        }

       if (Test-Path $binTestPath) {
            if (!(Test-Path $dropBinTestPath)) {
                New-Item -ItemType Directory -Path $dropBinTestPath | Out-Null
            }

            if ($testBreak) {
                Write-Host "Copying test directory due to test failue"
                CopyContents -copyFrom $binTestPath -copyTo $dropBinTestPath
            } else {
                if ($toModuleDropPath -ilike "*Web.*") {
                    # We need to copy all files for web applications because they are zipped up and not in the
                    # ExpertSource like all other files.
                    Write-Host "Copying web application files to drop for web application integration tests."
                    CopyContents -copyFrom $binTestPath -copyTo $dropBinTestPath
                } else {
                    Write-Host "Copying integration test artifacts to drop"

                    # *.dll* as we need both the .dll and the .config
                    Get-ChildItem $modulePath\* -Recurse -Include IntegrationTest*.dll*, IntegrationTest*.pdb, *.rsd, *.rds, *rdl | % { Copy-Item $_.FullName $dropBinTestPath -Force }

                    # We also want any Test Helper assemblies
                    Get-ChildItem $modulePath\* -Recurse -Include *Helper*.dll -Exclude *System*.dll | % { Copy-Item $_.FullName $dropBinTestPath -Force}
                }
            }

            Write-Host "Copying test results"
            Get-ChildItem $modulePath -Recurse -Filter *.trx | % { Copy-Item $_.FullName $dropBinTestPath -Force }
        }
    }

    <#
    Copies only built files, i.e. excludes items that are dependencies, from Bin\Module
    #>
    Function global:ResolveAndCopyUniqueBinModuleContent([string]$modulePath, [string]$copyToDirectory, [switch]$suppressUniqueCheck = $false) {
        $dependenciesPath = Join-Path $modulePath Dependencies
        $binPath = Join-Path $modulePath Bin\Module

        if ($suppressUniqueCheck) {
            Write-Host "Suppressing unique content check"
        }

        if ((Test-Path $dependenciesPath) -and !($suppressUniqueCheck)) {
            $jobFile = [System.IO.Path]::GetRandomFileName() + ".RCJ"

            $jobFile = Join-Path ([System.IO.Path]::GetTempPath()) -ChildPath $jobFile

            Measure-Command {
                Write-Host "Calculating hashes..."
				# *.exe.config files can have exactly matching contents as they are generated automatically.
                $a = gci -Recurse -Path $binPath | Where-Object {$_.FullName -notlike "*.exe.config"} | Get-FileHash
                $b = gci -Recurse -Path $dependenciesPath | Get-FileHash

                $hashes = $b | Select-Object -ExpandProperty Hash

                [string]$content = $a | where { $hashes -contains $_.Hash } | Select -ExpandProperty Path                

                $sb = [System.Text.StringBuilder]::new()
                $sb.AppendLine("/XF")
                $sb.AppendLine("*.pfx")
                $sb.AppendLine("*.trx")
                $sb.AppendLine($content)

                [System.IO.File]::WriteAllText($jobFile, $sb.ToString(), [System.Text.Encoding]::ASCII)

                Write-Output "Job File..."
                Write-Output (Get-Content $jobFile)
            }

            robocopy $binPath $copyToDirectory /S /MT /JOB:$jobFile
            Remove-Item $jobFile -Force -ErrorAction SilentlyContinue
       } else {
           Write-Host "No dependencies for $modulePath to be resolved, copying entire bin/module."
           robocopy $binPath $copyToDirectory /E /NP /NJS /NJH /MT /XF *.pfx *.trx /NS /NDL /A-:R
		   if ($ShellContext -and ($modulePath.EndsWith("Deployment") -or (Test-Path (Join-Path $binPath -ChildPath DeploymentManager.msi)))) {
			   Write-Output "Moving DeploymentManager.msi one folder up."
			   Move-Item -Path (Join-Path $copyToDirectory -ChildPath DeploymentManager.msi) -Destination (Join-Path $copyToDirectory -ChildPath ..\\) -Force
		   }
       }
    }

    <#
    Checks tail of a build log.  If build successful returns True.
    #>
    Function global:CheckBuild([string]$buildLog) {

        if (Test-Path $buildLog) {
            $noErrors = Get-Content $buildLog | select -last 10 | where {$_.Contains("0 Error(s)")}

            if ($noErrors) {
               return $true
            } else {
               return $false
            }
        } else {
            Write-Warning "No build log to check at [$buildLog]"
        }
    }

    <#
    We now need to move/copy the deployment manager files depending on the version we are working on.  There are three different scenarios:
    1. 7SP2 and earlier - all files are in Binaries folder.
    2. 7SP4 - all deployment files listed in ..\Build.Infrastructure\Src\Package\deploymentManagerFilesList.txt are moved to Binaries\DeploymentManager folder
       see GetProduct.ps1 (Function MoveDeploymentManagerFilesToFoler) for details.
    3. 8 to 8.0.1.1 - all deployment files listed in ..\Build.Infrastructure\Src\Package\deploymentManagerFilesList.txt are moved to Binaries\Deployment folder.
    #>
    Function global:MoveDeploymentFiles([string]$expertVersion, [string]$binariesDirectory, [string]$expertSourceDirectory) {
        switch ($expertVersion) {
            "8" {
                MoveDeploymentFilesV8 $binariesDirectory $expertSourceDirectory
            }
            "802" {
                MoveDeploymentFilesV802 $binariesDirectory $expertSourceDirectory
             }
            "803" {
                MoveDeploymentFilesV802 $binariesDirectory $expertSourceDirectory
             }             
            default {
                throw "Unknown manifest version $expertVersion"
            }
        }
    }

    Function global:MoveDeploymentFilesV8([string]$binariesDirectory, [string]$expertSourceDirectory){
        write "Copying Deployment files for V8."
        $deploymentDirectory = Join-Path $binariesDirectory 'Deployment'
        CreateDirectory $deploymentDirectory
        Start-Sleep -m 1500
        CopySupportingFiles $deploymentDirectory $expertSourceDirectory 'deploymentManagerFilesList.txt'

        #Copy DeploymentManager
        write "Renaming DeploymentManager.exe to Setup.exe and moving to binaries directory."
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.exe') $(Join-Path $binariesDirectory 'Setup.exe'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.pdb') $(Join-Path $binariesDirectory 'Setup.pdb'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.exe.config') $(Join-Path $binariesDirectory 'Setup.exe.config'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.exe.log4net.xml') $(Join-Path $binariesDirectory 'Setup.exe.log4net.xml'))

        #Copy DeploymentEngine
        write "Moving DeploymentEngine.exe to Deployment directory."
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentEngine.exe') $(Join-Path $deploymentDirectory 'DeploymentEngine.exe'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentEngine.exe.config') $(Join-Path $deploymentDirectory 'DeploymentEngine.exe.config'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentEngine.exe.log4net.xml') $(Join-Path $deploymentDirectory 'DeploymentEngine.exe.log4net.xml'))
    }

    Function global:MoveDeploymentFilesV802([string]$binariesDirectory, [string]$expertSourceDirectory){
        write "Copying Deployment files for V802."

        # Copy DeploymentManager
        write "Renaming DeploymentManager.exe to Setup.exe and copying to binaries directory."
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.exe') $(Join-Path $binariesDirectory 'Setup.exe'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.pdb') $(Join-Path $binariesDirectory 'Setup.pdb'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.exe.config') $(Join-Path $binariesDirectory 'Setup.exe.config'))
        [void](Copy-Item $(Join-Path $expertSourceDirectory 'DeploymentManager.exe.log4net.xml') $(Join-Path $binariesDirectory 'Setup.exe.log4net.xml'))
    }

    <#
    Finally we need to copy the license generator files depending on the version we are working on.  In theory there are three different scenarios:
    1. 7SP2 and earlier - all files are in Binaries folder. (Not yet immplemented)
    2. 7SP4 - all deployment files listed in ..\Build.Infrastructure\Src\Package\licenseGeneratorFilesList.txt are copied to Binaries\LicenseGenerator folder
       see GetProduct.ps1 (Function MoveLicenseGeneratorFiles) for details. (Not yet immplemented)
    3. 8 and later - all deployment files listed in ..\Build.Infrastructure\Src\Package\licenseGeneratorFilesList.txt are copied to Binaries\LicenseGenerator folder.
    #>

    Function global:MoveInternalFiles([string]$expertVersion, [string]$expertSourceDirectory){
        if($expertVersion -gt "8"){
            MoveInternalFilesV8 $expertSourceDirectory
        }
    }

    Function global:MoveInternalFilesV8([string]$expertSourceDirectory){
        write "Moving License Generator files for V8."
        #Create 'Internal' folder under the source directory
        $internalDirectory = Join-Path $expertSourceDirectory 'Internal'
        CreateDirectory $internalDirectory
        Start-Sleep -m 1500

        #Create 'LicenseGenerator' folder under the 'Internal' folder
        $licenseGeneratorDirectory = Join-Path $internalDirectory 'LicenseGenerator'
        CreateDirectory $licenseGeneratorDirectory
        $registrationServiceDirectory = Join-Path $internalDirectory 'RegistrationService'
        CreateDirectory $registrationServiceDirectory
        Start-Sleep -m 1500

        CopySupportingFiles $licenseGeneratorDirectory $expertSourceDirectory 'licenseGeneratorFilesList.txt'

        #Move LicenseGenerator
        write "Moving LicenseGenerator.exe to .\Internal\LicenseGenerator under binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'LicenseGenerator.exe') $(Join-Path $licenseGeneratorDirectory 'LicenseGenerator.exe'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'LicenseGenerator.pdb') $(Join-Path $licenseGeneratorDirectory 'LicenseGenerator.pdb'))

        #Move PackagePackager
        write "Moving PackagePackager.exe to .\Internal\LicenseGenerator under binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'PackagePackager.exe') $(Join-Path $licenseGeneratorDirectory 'PackagePackager.exe'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'PackagePackager.pdb') $(Join-Path $licenseGeneratorDirectory 'PackagePackager.pdb'))

        #Move RegistrationService
        write "Moving Aderant.Registration.Service.zip to .\Internal\RegistrationService under binaries directory."
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.zip') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.zip'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.SourceManifest.xml') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.SourceManifest.xml'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.SetParameters.xml') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.SetParameters.xml'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.deploy-readme.txt') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.deploy-readme.txt'))
        [void](MoveItem $(Join-Path $expertSourceDirectory 'Aderant.Registration.Service.deploy.cmd') $(Join-Path $registrationServiceDirectory 'Aderant.Registration.Service.deploy.cmd'))
    }

    <#
    Below is the helper functions used for Move/Copy deployment and internal files
    #>

    Function global:CopySupportingFiles([string]$deploymentDirectory, [string]$expertSourceDirectory, [string]$fileListContainer) {
        #Copy all supporting files.
        write "Copying deployment dependencies to Deployment directory."
        $deploymentManagerFilesListPath = $fileListContainer
        #If global:PackageScriptsDirectory is defined use this path instead of the working directory, because the build servers do not use the Aderant PS profile.
        if ($global:PackageScriptsDirectory) {$deploymentManagerFilesListPath = Join-Path $global:PackageScriptsDirectory $fileListContainer}
        get-content -Path $deploymentManagerFilesListPath | Where-Object  {-not ($_.StartsWith("#"))} | ForEach-Object {CopyItem $expertSourceDirectory\$_ $deploymentDirectory\$_ -Force}
    }

    Function global:MoveItem([string] $source, [string] $destination) {
        if(Test-Path $source){
           Move-Item -Path $source -Destination $destination -Force
        }
    }

    Function global:CopyItem([string] $source, [string] $destination) {
        if(Test-Path $source){
            #Check if destination folder exists
            $destinationFolder = Split-path -Path $destination -Parent
            if (-not(Test-Path $destinationFolder)){
                New-Item $destinationFolder -type directory
            }
           Copy-Item -Path $source -Destination $destination -Force
        }
    }

    Function global:CreateDirectory([string] $directoryPath) {
        if(!$(Test-Path($directoryPath))){
            New-Item -ItemType Directory -Path $directoryPath
        }
    }

    function global:RemoveEmptyFolders($folder) {
        $items = Get-ChildItem $folder

        foreach($item in $items) {
            if ($item.PSIsContainer) {
                RemoveEmptyFolders $item.FullName

                $subitems = Get-ChildItem -Path $item.FullName
                if ($subitems -eq $null) {
                    Remove-Item $item.FullName -Force -ErrorAction SilentlyContinue
                }
                $subitems = $null
            }
        }
    }

    Function script:FormatCopyMessage($pipeline) {
        # Workaround for /NP not being compatible with /MT which fills up stdout with copy progress
        if ($pipeline -eq $null) {
            return
        }

        if ($pipeline.Contains("%")) {
            return
        }

        if ([String]::IsNullOrEmpty($pipeline)) {
            return
        }

        if ($IsTeamBuild) {
            Write-Host $pipeline.TrimStart().PadLeft(10)
        } else {
            Write-Debug $pipeline.TrimStart().PadLeft(10)
        }
    }

    Function global:GetBranchNameFromDropPath([string]$dropPath) {
        $parts = $dropPath.TrimEnd('\').Split('\')

        if ($parts -notcontains "dev" -and $parts -notcontains "releases" -and $parts -notcontains "main") {
            return $dropPath
        }

        return [string]$parts[$parts.Length-2] + "\" + $parts[$parts.Length-1]
    }

    function global:Test-ReparsePoint([string]$path) {
        $file = Get-Item $path -Force -ea 0
        return [bool]($file.Attributes -band [IO.FileAttributes]::ReparsePoint)
    }

    Function global:WriteGetBinariesMessage([System.Xml.XmlNode]$module, [string]$dropPath) {
        $binariesText = $null
        if (IsThirdparty($module.Name) -and $module.Path -ne $null) {
            $binariesText = "Getting third party binaries "
        } else {
            $binariesText = "Getting binaries "
        }

        Write-Host $binariesText -NoNewline -ForegroundColor Gray
        Write-Host $module.Name -NoNewline -ForegroundColor Green
        Write-Host " from the branch " -ForegroundColor Gray -NoNewline
        if ([string]::IsNullOrEmpty($module.Action) -and [string]::IsNullOrEmpty($module.Path)) {
            Write-Host (GetBranchNameFromDropPath $dropPath) -ForegroundColor Green
        } else {
            Write-Host $module.Path -ForegroundColor Green
        }
    }

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

        $buildTool = [System.IO.Path]::Combine($MSBuildLocation, "MSBuild.exe")
        $projectPath = [System.IO.Path]::Combine($buildScriptDirectory, "Aderant.Build.Common.targets")
        & $buildTool $projectPath "/p:BuildScriptsDirectory=$buildScriptDirectory" "/nologo" "/m" "/nr:false"
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

    Function global:Compare-Checksums {
        param(
            [string] $localFolder,
            [string] $serverFolder
        )

        $localChildren = Get-ChildItem $localFolder -Recurse
        $serverChildren = Get-ChildItem $serverFolder -Recurse

        if ($localChildren.Count -ne $serverChildren.Count) {
            Write-Host "The number of items in directories" $localFolder "and" $serverFolder "differ"
            return $true
        }

        for ($i=0; $i -le $localChildren.Count; $i++) {
            if ($localChildren[$i].Name -ne $serverChildren[$i].Name) {
                Write-Host "The content of directories" $localFolder "and" $serverFolder "differ"
                return $true
            }
            if (-not $localChildren[$i].PSIsContainer -and $localChildren[$i].Name -ne $null -and -not $localChildren[$i].Name.EndsWith(".lic")) {
                if ((Get-FileHash -Path $localChildren[$i].FullName).Hash -ne (Get-FileHash -Path $serverChildren[$i].FullName).Hash) {
                    Write-Host $localChildren[$i].FullName
                    Write-Host "Hash:"(Get-FileHash -Path $localChildren[$i].FullName).Hash
                    Write-Host " is different from"
                    Write-Host $serverChildren[$i].FullName
                    Write-Host "Hash:"(Get-FileHash -Path $serverChildren[$i].FullName).Hash
                    return $true
                }
            }
        }

        return $false
    }


<#
.SYNOPSIS
Executes an external program.

.DESCRIPTION
Executes an external program and waits for the process to exit.

After calling this command, the exit code of the process can be retrieved from the variable $LASTEXITCODE.

.PARAMETER Encoding
This parameter not required for most scenarios. Indicates how to interpret the encoding from the external program. An example use case would be if an external program outputs UTF-16 XML and the output needs to be parsed.

.PARAMETER RequireExitCodeZero
Indicates whether to write an error to the error pipeline if the exit code is not zero.
#>
function global:Invoke-Tool {
    [CmdletBinding()]
    param(
        [ValidatePattern('^[^\r\n]*$')]
        [Parameter(Mandatory = $true)]
        [string]$FileName,
        [ValidatePattern('^[^\r\n]*$')]
        [Parameter()]
        [string]$Arguments,
        [string]$WorkingDirectory,
        [System.Text.Encoding]$Encoding,
        [switch]$RequireExitCodeZero)

    $isPushed = $false
    $originalEncoding = $null
    try {
        if ($Encoding) {
            $originalEncoding = [System.Console]::OutputEncoding
            [System.Console]::OutputEncoding = $Encoding
        }

        if ($WorkingDirectory) {
            Push-Location -LiteralPath $WorkingDirectory -ErrorAction Stop
            $isPushed = $true
        }

        $FileName = $FileName.Replace('"', '').Replace("'", "''")
        Write-Output "##[command]""$FileName"" $Arguments"
        Invoke-Expression "& '$FileName' --% $Arguments"
        Write-Verbose "Exit code: $LASTEXITCODE"
        if ($RequireExitCodeZero -and $LASTEXITCODE -ne 0) {
            throw ("Process {0} exited with code {1}" -f ([System.IO.Path]::GetFileName($FileName), $LASTEXITCODE))
        }
    } finally {
        if ($originalEncoding) {
            [System.Console]::OutputEncoding = $originalEncoding
        }

        if ($isPushed) {
            Pop-Location
        }
    }
}

<#
.SYNPOSIS
Write a message to the host in an error colour
#>
function global:Write-Error {
    [CmdletBinding()]
    param (
        [parameter(ValueFromRemainingArguments=$true)][string[]] $args
    )    

    $remainingArgs = $args | select -Skip 1

    Write-Host ("! $($args[0])" -f $remainingArgs) -ForegroundColor Red    
}

<#
.SYNPOSIS
Write a message to the host in a warning colour
#>
function global:Write-Warning {
    [CmdletBinding()]
    param (
        [parameter(ValueFromRemainingArguments=$true)][string[]] $args
    )    

    $remainingArgs = $args | select -Skip 1

    Write-Host ("! $($args[0])" -f $remainingArgs) -ForegroundColor Yellow    
}

<#
.SYNPOSIS
Write a message to the host in a neutral colour
#>
function global:Write-Info {
    [CmdletBinding()]
    param (
        [parameter(ValueFromRemainingArguments=$true)][string[]] $args
    )    

    $remainingArgs = $args | select -Skip 1

    Write-Host ("$($args[0])" -f $remainingArgs) -ForegroundColor Cyan    
}


<#
.SYNPOSIS
Write a message to the host in a green colour
#>
function global:Write-Success {
    [CmdletBinding()]
    param (
        [parameter(ValueFromRemainingArguments=$true)][string[]] $args
    )    

    $remainingArgs = $args | select -Skip 1

    Write-Host ("$($args[0])" -f $remainingArgs) -ForegroundColor Green    
}

Set-Alias robocopy InvokeRobocopy -Scope Global