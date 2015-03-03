param ([Parameter(Mandatory=$True)]
		[String] $directory)

begin{
    write "test binaries directory is $directory"
}

process{
	
	if (test-path $directory){
		write "$directory exists"
		write "Getting child items..."
		$allOrderedTestFiles = Get-ChildItem $directory -Include *.orderedtest -Recurse
	} else {
		write "Binaries directory $directory doesnt exist"
	}
	
    if ($allOrderedTestFiles.Length -gt 0){
		write "The following ordered tests have been found:"
		foreach ($test in $allOrderedTestFiles){
			write $test
		}
	} else {
		write "No ordered test were found"
	}

    if ($allOrderedTestFiles -ne $null -and $allOrderedTestFiles.Length -gt 0){
        foreach ($file in $allOrderedTestFiles){
            [xml]$doc = Get-Content $file.FullName
            $testName = $file.Name
        
            $orderedTestNode = $doc.GetElementsByTagName("OrderedTest")
            if ($orderedTestNode -ne $null){
                $orderedTestNode[0].Attributes["storage"].Value = $directory+'\'+$testName
            }

            $testLinkNodes = $doc.GetElementsByTagName("TestLink")
            if ($testLinkNodes -ne $null){
                for ($i = 0; $i -lt $testLinkNodes.Count; $i++){
                    $storage = $testLinkNodes[$i].Attributes["storage"].Value;
                    $testDllString = $storage.Split('\')
                    $testDll = $testDllString[$testDllString.Length-1]
                    $testLinkNodes[$i].Attributes["storage"].Value = (".\"+$testDll)
                }
            }

            $destFileName = Join-Path $directory $testName
            write "Copying $testName to $destFileName"
            $doc.Save($destFileName)
        }
    }
}