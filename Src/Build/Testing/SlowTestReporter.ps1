<#
.SYNOPSIS
    Searches test result files in a given location for tests which exceed a given timeout.
.DESCRIPTION
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

begin {
    Set-StrictMode -Version 'Latest'
    $InformationPreference = 'Continue'
    $ErrorActionPreference = 'Stop'

    function GetTrxFilesFromDir {
        [CmdletBinding()]
        [OutputType([string[]])]
        param (
            [string]
            $path,
            
            [UInt32]
            $searchDepth
        )

        process {
            Write-Debug $MyInvocation.MyCommand
            if (-not (Test-Path $path)) {
                Write-Error -Message "The directory or file '$path' does not exist."
            }
        
            # Determine the strategy for finding test result files.
            if (Test-Path -path $path -PathType Leaf) {
                $trxFiles = Get-ChildItem $path
            } else {
                $trxFiles = Get-ChildItem -Filter *.trx -Path $path -Depth $depth
            }
        
            # Exit script if no trx files found.
            if ($null -eq $trxFiles) {
                Write-Error "No .trx files found at path: '$path'."
            }
        
            $trxFileNames = $trxFiles.FullName
        
            Write-Information -MessageData 'The following test result files were found:'
            Write-Information -MessageData "$([string]::new('-', 36))"
            $trxFileNames | ForEach-Object { Write-Information -MessageData $_ }

            return $trxFileNames
        }
    }
   
    function GenerateTrxResult{
        [CmdletBinding()]
        [OutputType([System.Collections.Generic.List[PSCustomObject]])]
        param (
            [Parameter(ValueFromPipeline=$true)][string[]]
            $testResultFiles,

            [Int]
            $maxTestDuration
        )

        process {
            Write-Debug $MyInvocation.MyCommand
            [System.Collections.Generic.List[PSCustomObject]]$slowTests = [System.Collections.Generic.List[PSCustomObject]]::new()

            foreach ($file in $testResultFiles) {
                # Skip line if a file cannot be found
                if (-not (Test-Path $file)) {
                    Write-Warning "Unable to find requested file: '$file'."
                    continue
                }

                # Pipe the interesting information
                GetSlowTests -file $file -maxTestDuration $maxTestDuration | ForEach-Object {
                    $slowTests.Add(
                        [PSCustomObject]@{
                            TestName = $_.testName;
                            Duration = $_.duration;
                            Outcome = $_.outcome;
                            Location = $file;
                        })
                }
            }

            return $slowTests
        }
    }
    
    function GetSlowTests {
        [CmdletBinding()]
        param (
            [string]
            $file,

            [int]
            $maxTestDuration
        )

        process {
            Write-Debug $MyInvocation.MyCommand
            [xml]$xmlTrx = Get-Content -path $file

            # Select all the test results that exceed the max duration
            $hasResults = ($xmlTrx.TestRun.PSObject.Properties.Name -contains 'Results' -and $xmlTrx.TestRun.Results.PSObject.Properties.Name -contains 'UnitTestResult')
            if ($hasResults) {
                return $xmlTrx.TestRun.Results.UnitTestResult | 
                    Where-Object {
                        if (-not ('duration' -in $_.PSObject.Properties.Name)) {
                            $duration = [TimeSpan]::Zero
                        } else {
                            $duration = ($_.duration -as [TimeSpan])
                        }
                
                        if ($null -ne $duration) {
                            $myDuration = New-TimeSpan -Seconds $maxTestDuration
                            $duration.TotalSeconds -gt $myDuration.TotalSeconds
                        }
                    }
            }
        }
    }
    
    function GetOutputDir {
        param (
            [string]
            $trxFilePath
        )

        process {
            Write-Debug $MyInvocation.MyCommand
            
            if (Test-Path -path $trxFilePath -PathType Leaf) {
                $trxFilePath = Split-Path -Path $trxFilePath -Parent
            }
        
            # Generate our file path to output to.
            return Join-Path -Path $trxFilePath -ChildPath 'SlowTestReport.csv'
        }
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
            Write-Debug $MyInvocation.MyCommand
            if ($null -eq $slowTests -or $slowTests.Count -eq 0) {
                Write-Information -MessageData "No slow tests found! $([Environment]::NewLine)"
                PrintMessages -slowTests $slowTests.Count -maxTestDuration $maxTestDuration
                return
            }

            $slowTests | Export-Csv -Path $outFile -NoTypeInformation
            Write-Information -MessageData "Results written to: $outFile$([Environment]::NewLine)"
            Write-Output ($slowTests | Format-Table | Out-String)

            PrintMessages -slowTests $slowTests.Count -maxTestDuration $maxTestDuration
        }
    }
    
    function PrintMessages {
        param (
            [int]
            $slowTests,
            
            [int]
            $maxTestDuration
        )

        begin {
            function PrintFortune {
                [string[]]$fortunes = @(
                    "You smell nice today",
                    "I like the way you move",
                    "Ahh, that's the stuff",
                    "My life for Auir",
                    "Metamorphosis completed",
                    "Racoons, they are dancing on the head of a pin with the angels. They are laughing",
                    "Only you <PERSON_NAME_HERE> could solve <PROBLEM_DESCRIPTION_HERE>",
                    "Do you always make this look so easy?",
                    "Excellent mouthfeel",
                    "I'm so glad you're here today",
                    "Looks like it's back to the before-fore times",
                    "Your sound card works perfectly",
                    "Good news everyone!",
                    "Everything is exactly how you left it",
                    "    The thing with engineers from DevOps
            Is it's quite easy to set them off.
            If you wanna make 'em mad,
            Or even just sad,
            Post a build failure and don't post the logs!")
                Write-Information -MessageData ((Get-Random -InputObject $fortunes) + ([Environment]::NewLine))
            }
        }

        process {
            Write-Debug $MyInvocation.MyCommand
            try {
                switch ($slowTests) {
                    0 {
                        Write-Information -MessageData "No tests slower than ($maxTestDuration) seconds were found."
                    }
                    1 {
                        Write-Information -MessageData "One test found that exceeded the maximum test duration of ($maxTestDuration) seconds."
                    }
                    default {
                        Write-Information -MessageData "$slowTests tests found that exceeded the maximum test duration of ($maxTestDuration) seconds."
                    }
                }
            } finally {
                # Very important. Do not remove as the file breaks.
                PrintFortune
            }
        }
    }
}

process {
    $slowTests = GetTrxFilesFromDir -Path $path -searchDepth $depth | GenerateTrxResult -MaxTestDuration $maxTestDuration
    [string]$outputFilePath = GetOutputDir -trxFilePath $path
    ReportResults -SlowTests $slowTests -OutFile $outputFilePath -MaxTestDuration $maxTestDuration
}
