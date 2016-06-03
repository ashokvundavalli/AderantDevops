function install()
{
    try {
        $cwd = (Split-Path -Parent $MyInvocation.MyCommand.Path)

        $BUILDUTIL_TAG = "master"

        $id = [Guid]::NewGuid().ToString()
        $buildutil = [System.IO.Path]::Combine($cwd, "build.infrastructure." + $id)

        gci -Path $cwd -Filter "build.infrastructure.*" | Remove-Item -Force -Recursee

        # PowerShell will raise NativeCommandError if git writes to stdout or stderr
        # therefore 2> is added and the output is eaten	
        & "git" clone http://tfs:8080/tfs/ADERANT/ExpertSuite/_git/Build.Infrastructure $buildutil 2> git_error.log        
  
        cd $buildutil

        & "git" fetch -a -v 2> git_error.log
        & "git" fetch --tags 2> git_error.log
        & "git" reset --hard $BUILDUTIL_TAG 2> git_error.log
    } finally {
        cd $cwd    
    }
    write "Build bootstrap complete"
}

install