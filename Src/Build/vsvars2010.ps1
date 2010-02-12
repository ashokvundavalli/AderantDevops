##
# Sets environment vaiables used with a Visual Studio 2010 build 
##
##

$VS2010Key = $null

if (test-path HKLM:SOFTWARE\Wow6432Node\Microsoft\VisualStudio\10.0) {
    $VS2010Key = get-itemproperty HKLM:SOFTWARE\Wow6432Node\Microsoft\VisualStudio\10.0
}
else {
    if (test-path HKLM:SOFTWARE\Microsoft\VisualStudio\10.0) {
        $VS2010Key = get-itemproperty HKLM:SOFTWARE\Microsoft\VisualStudio\10.0
    }
}

if ($VS2010Key -ne $null) {
    $vsPath = split-path $VS2010Key.InstallDir -Parent | split-path -Parent
    $vcPath = join-path $vsPath "VC"

    if (test-path $vsPath) {
        write-host "Setting environment for Microsoft Visual Studio 2010."

        # Determine installation directory of Platform SDK

        $WindowsSdkDir = $null

        if (test-path "HKLM:SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows") {
            $WindowsSdkDir = (get-itemproperty "HKLM:SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows").CurrentInstallFolder
        }
        else {
            if (test-path "HKLM:SOFTWARE\Microsoft\Microsoft SDKs\Windows") {
                $WindowsSdkDir = (get-itemproperty "HKLM:SOFTWARE\Microsoft\Microsoft SDKs\Windows").CurrentInstallFolder
            }
            else {
                if (test-path "HKCU:SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows") {
                    $WindowsSdkDir = (get-itemproperty "HKCU:SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows").CurrentInstallFolder
                }
                else {
                    if (test-path "HKCU:SOFTWARE\Microsoft\Microsoft SDKs\Windows") {
                        $WindowsSdkDir = (get-itemproperty "HKCU:SOFTWARE\Microsoft\Microsoft SDKs\Windows").CurrentInstallFolder
                    }
                    else {
                        $WindowsSdkDir = join-path $vcPath "PlatformSDK"
                    }
                }
            }
        }

        $FrameworkKey = get-itemproperty HKLM:SOFTWARE\Microsoft\.NETFramework
        $env:FrameworkDir = $FrameworkKey.InstallRoot
        $env:FrameworkVersion = $VS2010Key."CLR Version"

        $env:DevEnvDir = $VS2010Key.InstallDir

        # PATH environment settings

        $paths = @()
        $paths += $env:DevEnvDir
        $paths += join-path $vcPath "BIN"
        $paths += join-path $vsPath "Common7\Tools"
        $paths += join-path $env:FrameworkDir $env:FrameworkVersion
        $paths += join-path $vcPath "VCPackages"
        if (test-path (join-path $WindowsSdkDir "bin"))
            { $paths += join-path $WindowsSdkDir "bin" }

        $pathText = [string]::Join(";",$paths)
        $env:PATH = $pathText + ";" + $env:PATH

        # INCLUDE environment settings

        $includes = @()

        if (test-path (join-path $vcPath "atlmfc\include"))
            { $includes += join-path $vcPath "atlmfc\include" }
        if (test-path (join-path $vcPath "include"))
            { $includes += join-path $vcPath "include" }
        if (test-path (join-path $WindowsSdkDir "include"))
            { $includes += join-path $WindowsSdkDir "include" }

        if ($includes.Count -gt 0)
        {
            $includeText = [string]::Join(";",$includes)
            $env:INCLUDE = $includeText + ";" + $env:INCLUDE
        }

        # LIB environment settings

        $libs = @()

        if (test-path (join-path $vcPath "atlmfc\lib"))
            { $libs += join-path $vcPath "atlmfc\lib" }
        if (test-path (join-path $vcPath "lib"))
            { $libs += join-path $vcPath "lib" }
        if (test-path (join-path $WindowsSdkDir "lib"))
            { $libs += join-path $WindowsSdkDir "lib" }

        if ($libs.Count -gt 0)
        {
            $libText = [string]::Join(";",$libs)
            $env:LIB = $libText + ";" + $env:LIB
        }

        # LIBPATH environment settings

        $libpaths = @()

        $libpaths += join-path $env:FrameworkDir $env:FrameworkVersion
        if (test-path (join-path $vcPath "atlmfc\lib"))
            { $libpaths += join-path $vcPath "atlmfc\lib" }
        if (test-path (join-path $vcPath "lib"))
            { $libpaths += join-path $vcPath "lib" }
        
        $libpathsText = [string]::Join(";",$libpaths)
        $env:LIBPATH = $libpathsText + ";" + $env:LIBPATH

        $env:VSINSTALLDIR = $vsPath
        $env:VCINSTALLDIR = $vcPath
        $env:WINDOWSSDKDIR = $WindowsSdkDir
    }
    else {
        write-error "Couldn't find the Visual Studio 2010 installation directory."
    }
}