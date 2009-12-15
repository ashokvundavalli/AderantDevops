param([string]$linkName, [string]$target, [string]$vsTestHostPath) 

begin{
    $linkPath = [System.IO.Path]::GetFullPath($linkName)
    $targetPath = [System.IO.Path]::GetFullPath($target)
    
    Function SetMyFrameworkHome($testBinariesDir){
        write "Setting MyFrameworkHome as $testBinariesDir"
        [Environment]::SetEnvironmentVariable("MyFrameworkHome", $testBinariesDir)        
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

    SetMyFrameworkHome($targetPath)
    RemoveLinkDir($linkPath)
    CreateLinkDir($linkPath, $targetPath)
    UpdateVSTestHostFile($vsTestHostPath)
    
}