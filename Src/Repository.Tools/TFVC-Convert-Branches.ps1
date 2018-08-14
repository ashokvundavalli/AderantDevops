param (
    [Parameter(Mandatory=$true)][string]$moduleName,
    [Parameter(Mandatory=$false)][string]$tfsDirectory = "C:\TFS\ExpertSuite",
    [Parameter(Mandatory=$false)][string]$branchName = "Dev/vnext",
    [Parameter(Mandatory=$false)][string]$stagingDirectory = "C:\Temp\Staging",
    [Parameter(Mandatory=$false)][int]$changeSet,
    [switch]$gitIgnore,
    [switch]$restoreBrances
)

begin {
    Set-StrictMode -Version Latest

    function Restore-Branches {
        param (
            [Parameter(Mandatory=$true)][string[]]$branches,
            [Parameter(Mandatory=$true)][string]$moduleName
        )

        foreach ($branch in $branches) {
            try {
                Write-Output y|tfpt branches /convertToFolder "$/ExpertSuite/$branch/Modules/$moduleName"
            } catch {
            }
        }
    
        foreach ($branch in $branches) {
            try {
                Write-Output y|tfpt branches /convertToBranch "$/ExpertSuite/$branch/Modules" /recursive
            } catch {
            }
        }
    }

    Push-Location -Path $tfsDirectory

    [string[]]$branches = @(
        "Main",
        "Dev/801Time",
        "Dev/802Grad",
        "Dev/81Expert+",
        "Dev/83GA",
        "Dev/Automation",
        "Dev/BillingBase",
        "Dev/CaseV1",
        "Dev/CaseV1ATL",
        "Dev/ComputedColumns",
        "Dev/CRTVNext",
        "Dev/EmployeeIntake",
        "Dev/Eureka",
        "Dev/FrameworkNext",
        "Dev/MP2",
        "Dev/OTG",
        "Dev/Packaging",
        "Dev/PerformanceTest",
        "Dev/PM",
        "Dev/QueryService",
        "Dev/ServiceSimplification",
        "Dev/Simplification",
        "Dev/Stability",
        "Dev/Startup",
        "Dev/TaskActions",
        "Dev/TestAutomation",
        "Dev/Time81",
        "Dev/TitanSmartForm",
        "Dev/TitanTime",
        "Dev/UnicodeComply",
        "Dev/Upgrade461",
        "Dev/UXToolkit",
        "Dev/vnext",
        "Dev/VS2010",
        "Dev/Workflow",
        "Dev/WorkflowData",
        "Releases/8003",
        "Releases/8003Patch",
        "Releases/8004Patch",
        "Releases/800Hotfix",
        "Releases/800Patch",
        "Releases/8011NRF",
        "Releases/8011Patch",
        "Releases/801Patch",
        "Releases/801x",
        "Releases/8021Atkinsons",
        "Releases/8021Patch",
        "Releases/8021SM",
        "Releases/802Patch",
        "Releases/802x",
        "Releases/802xEE",
        "Releases/8030TaylorWessing",
        "Releases/8031Patch",
        "Releases/8032Allens",
        "Releases/8032Deloitte",
        "Releases/8032GibsonDunn",
        "Releases/8032HSF",
        "Releases/8033Patch",
        "Releases/8034Patch",
        "Releases/803x",
        "Releases/80ServicePack",
        "Releases/8101Patch",
        "Releases/8102Patch",
        "Releases/8110Cleary",
        "Releases/8110Patch",
        "Releases/8110Patch2834b",
        "Releases/811x",
        "Releases/81x",
        "Releases/81xHotfix",
        "Releases/8200Update",
        "Releases/82x",
        "Releases/Appstore",
        "Releases/BurgesSalmon8021",
        "Releases/CC801Patch",
        "Releases/GGSP1RTM",
        "Releases/GGSP2HF01GENU2185",
        "Releases/GGSP4",
        "Releases/GGSP4HF01GENU2176",
        "Releases/GGSP4HF02GENU",
        "Releases/GGSP4HF03GENU",
        "Releases/GGSP4HF04GENU",
        "Releases/GGSP4HF05GENU",
        "Releases/GGSP4HF1Genu",
        "Releases/GGSP4Hotfix",
        "Releases/MatterPlanning"
    )
}

process {
    if ($restoreBrances.IsPresent) {
        Restore-Branches -branches $branches -moduleName $moduleName
        return
    }

    foreach ($existingBranch In TFPT.EXE Branches /listBranches:roots) {
        $existingBranch = $existingBranch.TrimStart()

        If ($existingBranch.StartsWith("$/ExpertSuite")) {
            Write-Output y|tfpt branches /convertToFolder $($existingBranch)
        }
    }

    foreach ($branch in $branches) {
        try {
            Write-Output y|tfpt branches /convertToBranch "$/ExpertSuite/$branch/Modules/$($moduleName)" /recursive
        } catch {
        }
    }

    Write-Host "Using staging directory: $stagingDirectory"
    Set-Location -Path $stagingDirectory

    [System.Collections.ArrayList]$parameters = @(
        "--resumable",
        "http://tfs:8080/tfs/Aderant",
        "$/ExpertSuite/$($branchName)/Modules/$($moduleName)",
        "$($stagingDirectory)\$($moduleName)",
        "--batch-size=50"        
    )

    if ($changeSet -ne 0) {
        [Void]$parameters.Add("--changeset=$changeSet")
    }

    if ($gitIgnore.IsPresent) {
        [Void]$parameters.Add("--gitignore=`"$stagingDirectory\.gitignore`"")
    }

    git-tfs.exe clone @parameters

    Restore-Branches -branches $branches -moduleName $moduleName
}

end {
    Pop-Location
}