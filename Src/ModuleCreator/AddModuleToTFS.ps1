param(
   [string] $moduleName = $(throw 'moduleName is required'), 
   [string] $DestinationFolder= $(throw 'DestinationFolder is required') ,
   [string] $TfsProject = $(throw 'TfsProject is required') ,
   [boolean] $AddToTFS =  $true
   
)

begin{

    Function Execute($expr){
        $output = invoke-expression $expr
        write $output
    }

    Set-Alias tf "C:\Program Files (x86)\Microsoft Visual Studio 9.0\Common7\IDE\tf.exe"
    
}

process{  
    #Files to branch from Common\Build
    $filesToBranch = "Aderant.Build.targets","CopyToDrop.ps1","CreateDirectories.ps1", "FrameworkKey.snk", "GetModuleDependancies.ps1", "RM.TransformTemplates.Targets", "TFSBuild.proj"

    if($AddToTFS){
        #pushd to correct folder
        $ModuleFolder = $DestinationFolder + '\' + $ModuleName
        pushd $ModuleFolder
        
        # Delete any existing items from Dependencies folder making sure one exists.
        [string]$destinationPath = $ModuleFolder + '\Dependencies\'
        if([System.IO.Directory]::Exists($destinationPath)){
            Remove-Item $destinationPath\* -Recurse -Force
        }    
        
        #Add module to TFS
        Execute("tf add * /recursive")
        
        popd
        

        #check in delete
        Execute('tf checkin $/' + $TfsProject + '/Main/Modules/' + $moduleName + ' /recursive /comment:"Add performed by create_module.ps1" /noprompt')
        
        #Branch common files
        foreach($file in $filesToBranch){
            .\BranchBuildFileToModules.ps1 $TfsProject $moduleName $file 
        }
    }else{
        #copy common files
        foreach($file in $filesToBranch){
            $FileTocopy = "C:\Source.Modularisation\" + $TfsProject + "\Common\Build\" + $file
            $FileTarget = "C:\Source.Modularisation\" + $TfsProject + "\Modules\" + $moduleName + "\Build"
            copy-item $FileTocopy -destination $FileTarget
        }        
    }
    
	pushd ..\Build
	.\GetModuleDependancies.ps1 C:\Source.Modularisation\$TfsProject\Modules\$ModuleName $managedBinariesFolder
	popd        
        
      
}
   
end{
    #write "Common files branched: $moduleName"

    
}


