param([string]$linkName, [string]$target, [string]$vsTestHostPath) 

begin{
    $linkPath = [System.IO.Path]::GetFullPath($linkName)
    $targetPath = [System.IO.Path]::GetFullPath($target)
    
    Function SetFrameworkHome($testBinariesDir){
        write "Setting FrameworkHome as $testBinariesDir"
        [Environment]::SetEnvironmentVariable("FrameworkHome", $testBinariesDir)        
    }
    
    Function RemoveLinkDir($dir){
        if([System.IO.Directory]::Exists($dir)){
            [System.IO.Directory]::Delete($dir)
        }
    }
    
    Function CreateLinkDir($linkPath, $targetPath){
        cmd /c mklink /D $linkPath $targetPath
    }
    
    Function UpdateVSTestHostFile($PathToVSTestHost){
        $VSTestHostFile =  Get-ChildItem ($PathToVSTestHost + "QTAgent32.exe.config")
        $VSTestHostXml = [xml](get-content $VSTestHostFile)
        if($VSTestHostXml){
            $VSTestHostXml.configuration.GetElementsByTagName("probing") |
            ForEach-Object{
                if (!$_.privatePath.contains("AderantBin")){
                    $_.privatePath = $_.privatePath + ";AderantBin;"
                }
            }
            $VSTestHostXml.Save($VSTestHostFile.FullName)
        }
    }

}
process{

    SetFrameworkHome($targetPath)
    #RemoveLinkDir($linkPath)
    #CreateLinkDir($linkPath, $targetPath)
    #UpdateVSTestHostFile($vsTestHostPath)
    
}