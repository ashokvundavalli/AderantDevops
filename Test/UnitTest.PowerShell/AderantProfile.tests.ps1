Install-Module -Name Pester -Force -SkipPublisherCheck
Import-Module "$([System.IO.Directory]::GetParent([System.IO.Directory]::GetParent($PSScriptRoot)))\Src\Profile\Aderant\Aderant.psm1" -DisableNameChecking

Describe 'Compare-VSIXVersions' {
	It "Given two versions, function should return true if the stored version is newer than the installed version." {
		[bool]$result = Compare-VSIXVersions -storedVersion "1.0.0.0" -installedVersion "1.0.0.1"
		$result | Should -Be $false
	}

	Context "Filtering by Name" {
		It "Given valid -StoredVersion <StoredVersion>, -InstalledVersion <InstalledVersion>, it returns '<Expected>'" -TestCases @(
			@{ StoredVersion = "3.8.0.0"; InstalledVersion = "4.10.0.0"; Expected = $false }
			@{ StoredVersion = "3.9.0.0"; InstalledVersion = "3.7.0.0"; Expected = $true }
			@{ StoredVersion = "1.1.1.1"; InstalledVersion = "1.1.1.1"; Expected = $false }
			@{ StoredVersion = "4.76"; InstalledVersion = "4.10.0.0"; Expected = $true }
			@{ StoredVersion = "1.0"; InstalledVersion = "1.0.0.0"; Expected = $false }
			@{ StoredVersion = "4.76.0.0"; InstalledVersion = "4.8"; Expected = $true }
		) {
			param ($StoredVersion, $InstalledVersion, $Expected)

			$result = Compare-VSIXVersions -storedVersion $StoredVersion -installedVersion $InstalledVersion
			$result | Should -Be $Expected
		}
	}
}