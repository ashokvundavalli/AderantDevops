<#
.SYNOPSIS
    Recursively investigates folders for test result (trx) files
    and determines if any of them are running slowly.

    Maximum test duration defines the maximum number of seconds a test may run 
    for before being flagged as slow.
.PARAMETER path
    The location where the test result files are stored.

    Directories will be investigated recursively.
    Files will be processed individually.
.PARAMETER depth
    The depth of directories to search.
.PARAMETER maxTestDuration
    The maximum test duration, in seconds, a test may run for before being
    flagged as a slow test. Value defaults to 10 seconds if it is not supplied.
.OUTPUTS
    Returns to the console a list of tests which exceed the
    given maximum test duration variable.

    Saves a copy of the results (SlowTestReport.csv) to the specified path.

    If path is a file, saves a result to the parent folder
    of the supplied file.

    e.g c:\TestResults\Test.trx would save a SlowTestReport.csv to
    c:\TestResults\SlowTestReport.csv

    An amazing (probably) fortune.
.EXAMPLE
    .\SlowTestReporter.ps1 -path 'C:\TestResultsDirectory'

    Would recursively examine the c:\TestResultsDirectory folder
    and determine if test result files exist and output tests that
    exceed the maxTestDuration (default 10).
.EXAMPLE
    .\SlowTestReporter.ps1 -path 'C:\TestResultsDirectory\TestResult.trx'

    Would process the file and determine if test result files exist and
    output tests that exceed the maxTestDuration (default 10).
.EXAMPLE
    .\SlowTestReporter.ps1

    Would ask the user to supply a path. Then inspects the folder
    and determine if test result files exist and output tests that
    exceed the maxTestDuration (default 10).
.EXAMPLE
    .\SlowTestReporter.ps1 -path 'C:\TestResultsDirectory' -depth 2

    Would search the supplied path and navigate two folders deep
    searching for test result files. Will process any files found.
.EXAMPLE
    .\SlowTestReporter.ps1 -path 'C:\TestResultsDirectory' -maxTestDuration 2

    Would search the given path recusively for all trx files. Would process
    those and report all tests that exceed the given max test duration of 2
    seconds.
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]
    $path,

    [Uint32]
    $depth = 0,

    [Uint32]
    $maxTestDuration = 5
)
begin{
    $InformationPreference = 'Continue'
    Set-StrictMode -Version 'Latest'
    $ErrorActionPreference = 'Stop'

    function GetTrxFilesFromDir() {
        param (
            [string]
            $path
        )
        # Exit if the given path is invalid
        if (-not (Test-Path $path)) {
            Write-Error -MessageData "The directory or file '$path' does not exist."
        }
    
        if (Test-Path -path $path -PathType Leaf) {
            # If the specified path is a file, do not perform a recursive search.
            $trxFiles = Get-ChildItem $path
        } else {
            # Find trx files located in $path recursively
            if ($depth -gt 0) {
                $trxFiles = (Get-ChildItem -Filter *.trx -Path $path -Depth $depth)
            } else {
                $trxFiles = (Get-ChildItem -Filter *.trx -Path $path -Recurse)
            }
        }
    
        # Exit script if no trx files found.
        if ($null -eq $trxFiles) {
            Write-Warning "No TRX files found in '$path'"
            Write-Warning "Exiting..."
            break
        }
    
        $trxFileNames = $trxFiles.FullName
    
        Write-Information -MessageData "The following test files were found:"
        Write-Information -MessageData "$([string]::new('-', 36))"
        $trxFileNames | ForEach-Object { Write-Information -MessageData $_ }
        Write-Information -MessageData $([Environment]::NewLine)
        
        $trxFileNames
    }
    
    # Returns false if object is null or the object does not have the property.
    function HasProperty() {
        param (
            [Object]
            $object,
    
            [string]
            $propertyName
        )
    
        $hasProperty = $false
        if ($null -ne $object) {
            $hasProperty = $propertyName -in $object.PSobject.Properties.Name
        }
    
        return $hasProperty
    }
    
    function GenerateTrxResult{
        param (
            [Parameter(ValueFromPipeline=$true)]
            [System.Collections.Generic.List[string]]
            $testResultFiles,
    
            [Int]
            $maxTestDuration
        )
        process {
            foreach ($file in $testResultFiles) {
                # Skip line if a file cannot be found
                if (-not (Test-Path $file)) {
                    Write-Warning "Unable to find requested file: '$file'"
                    continue
                }
        
                # Pipe the interesting information
                GetSlowTests $file $maxTestDuration | ForEach-Object {
                    [PSCustomObject] @{
                        TestName=($_.testName);
                        Duration=($_.duration);
                        Outcome=($_.outcome);
                        Location=($file)
                    }
                }
            }
        }
    }
    
    function GetSlowTests() {
        [CmdletBinding()]
        param (
            [string]
            $file,
    
            [int]
            $maxTestDuration
        )

        [string]$durationPropertyName = 'duration'
    
        # Convert trx into xml for processing
        [xml]$xmlTrx = Get-Content -path $file

        # Select all the test results that exceed the max duration
        $xmlTrx.TestRun.Results.UnitTestResult | 
        Where-Object {
            if (-not (HasProperty $_ $durationPropertyName)) {
                $duration = [TimeSpan]::Zero
            } else {
                $duration = ($_.duration -as [TimeSpan])
            }
    
            if ($null -ne ($duration)) {
                $myDuration = New-TimeSpan -Seconds $maxTestDuration
                $duration.TotalSeconds -gt $myDuration.TotalSeconds
            }
        }
    }
    
    function GetOutputFilePath() {
        param (
            [string[]]
            $trxFileDir
        )
    
        # Determine if the passed in $path is a file or directory.
        # Gets the underlying directory if file.
        if (Test-Path -path $trxFileDir -PathType Leaf) {
            $trxFileDir = Split-Path $trxFileDir
        }
    
        # Create our file to output
        Join-Path $trxFileDir -ChildPath "SlowTestReport.csv"
    }
    
    function ReportResults {
        param (
            [System.Collections.Generic.List[PSCustomObject]]
            $slowTests,
    
            [string]
            $outFile,
    
            [int]
            $maxTestDuration
        )

        process {
            if ($null -eq $slowTests -or $slowTests.Count -eq 0) {
                Write-Information -MessageData "No slow tests found! $([Environment]::NewLine)"
                PrintMessages $slowTests $outFile $maxTestDuration
                return
            }
            $slowTests | Export-Csv -Path $outFile -NoTypeInformation
            PrintMessages $slowTests $outFile $maxTestDuration
        }
    }
    
    function PrintMessages {
        param (
            [System.Collections.Generic.List[PSCustomObject]]
            $slowTests,
    
            [string]
            $outFile,
    
            [int]
            $maxTestDuration
        )
        try {
            $slowTests | Format-Table

            $count = 0
            if ($null -ne $slowTests) {$count = $slowTests.Count}
        
            switch ($count) {
                0 { Write-Information -MessageData "No tests slower than ($maxTestDuration) seconds were found." }
                1 { Write-Information -MessageData ("One test found that exceeded the maximum " +
                                                    "test duration of ($maxTestDuration) seconds.") }
                Default { Write-Information -MessageData ("$( $slowTests.Count ) tests found that exceeded the " +
                                                            "maximum test duration of ($maxTestDuration) seconds.") }
            }
            Write-Information -MessageData "Results written to: $outFile $([Environment]::NewLine)"
        } finally {
            # Very important. Do not remove as the file breaks.
            PrintFortune
        }
    }
    
    function PrintFortune() {
        [string[]]$fortunes = @(
            "You smell nice today"
            "I like the way you move"
            "Ahh, that's the stuff"
            "My life for Auir"
            "Metamorphosis completed"
            "Racoons, they are dancing on the head of a pin with the angels. They are laughing"
            "Only you <PERSON_NAME_HERE> could solve <PROBLEM_DESCRIPTION_HERE>"
            "Do you always make this look so easy?"
            "Excellent mouthfeel"
            "I'm so glad you're here today"
            "Looks like it's back to the before-fore times"
            "Your sound card works perfectly"
            "Good news everyone!"
            "Everything is exactly how you left it"
            "    The thing with engineers from DevOps
    Is it's quite easy to set them off.
    If you wanna make 'em mad,
    Or even just sad,
    Post a build failure and don't post the logs!")
        Write-Information -MessageData (
            (Get-Random -InputObject $fortunes) + ([Environment]::NewLine))
    }
}
process {
    $outputFilePath = GetOutputFilePath $path
    $slowTests = GetTrxFilesFromDir -Path $path | GenerateTrxResult -MaxTestDuration $maxTestDuration
    ReportResults -SlowTests $slowTests -OutFile $outputFilePath -MaxTestDuration $maxTestDuration
}
