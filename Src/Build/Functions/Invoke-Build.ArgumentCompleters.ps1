function InvokeBuild_ArgumentCompletion {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)
        
    $targets = @(
        "BuildAndPackage",
        "RunTests"
    )    

    $targets | Where-Object { $_ -like "$wordToComplete*" } |
      Sort-Object |
        ForEach-Object {            
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
        }
}

Register-ArgumentCompleter -CommandName Invoke-Build2 -ParameterName Target -ScriptBlock $function:InvokeBuild_ArgumentCompletion