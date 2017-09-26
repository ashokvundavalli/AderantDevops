param(
    [parameter(Mandatory=$true)] 
    [ValidateNotNullOrEmpty()] 
    [string]$password
)

Set-StrictMode -Version 2.0

$ErrorActionPreference = 'Stop'

<#
    This script takes care of fetching all identities known to TFS then downloading the user photo (if any) from Office 365 and assigning that photo
    to the Team Foundation identity
#>

#Load assemblies
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Client.dll")
[Void][Reflection.Assembly]::LoadFrom("C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Microsoft.TeamFoundation.Build.Client.dll")

$collectionUri = [System.Uri]::new("http://tfs:8080/tfs/aderant")

function GetUsers 
{
    # Connect to TFS
    $tpc = [Microsoft.TeamFoundation.Client.TfsTeamProjectCollection]::new($collectionUri)
    $tpc.EnsureAuthenticated()
 
    $identityServiceType = [Microsoft.TeamFoundation.Framework.Client.FilteredIdentityService]
    $managementService2Type = [Microsoft.TeamFoundation.Framework.Client.IIdentityManagementService2]
    
    $identityService = $tpc.GetService($identityServiceType)
    $managementService2 = $tpc.GetService($managementService2Type)

    [string[]]$fetchProperties = @("Microsoft.TeamFoundation.Identity.Image.Id", "Microsoft.TeamFoundation.Identity.Image.Data", "Microsoft.TeamFoundation.Identity.Image.Type")

    $allusers = @()

    $ignorePatterns = @("*service*", "*tester*" ,"*build*", "*admin*", "$")

    Write-Host "Searching for users..."   
    
    foreach ($user in $identityService.SearchForUsers("")) {
        if ($user.UniqueName -like "ADERANT_EU*") {
            continue
        }

        $continue = $true
        foreach ($ignorePattern in $ignorePatterns) {
             if ($user.UniqueName -ilike $ignorePattern) {
                $continue = $false
             }
        }

        if (-not ($continue)) {
            continue
        }

        $identity = $managementService2.ReadIdentity([Microsoft.TeamFoundation.Framework.Common.IdentitySearchFactor]::AccountName, $user.UniqueName, [Microsoft.TeamFoundation.Framework.Common.MembershipQuery]::Direct, [Microsoft.TeamFoundation.Framework.Common.ReadIdentityOptions]::ExtendedProperties, $fetchProperties, [Microsoft.TeamFoundation.Framework.Common.IdentityPropertyScope]::Both)
        $allusers += $identity
    }
    
    [hashtable]$return = @{} 
    $return.Service = $managementService2
    $return.Users = $allusers
    
    return $return
}


function SetTeamFoundationProperties($identityService, $user, $imageBytes, $format) 
{
    Write-Host "Updating image for $($user.UniqueName)"

    $user.SetProperty("Microsoft.TeamFoundation.Identity.Image.Data", $imageBytes)
    $user.SetProperty("Microsoft.TeamFoundation.Identity.Image.Type", $format)
    $user.SetProperty("Microsoft.TeamFoundation.Identity.Image.Id", [Guid]::NewGuid().ToByteArray())
    $user.SetProperty("Microsoft.TeamFoundation.Identity.CandidateImage.Data", $null)
    $user.SetProperty("Microsoft.TeamFoundation.Identity.CandidateImage.UploadDate", $null)                    

    $identityService.UpdateExtendedProperties($user)
}


function ProcessUser($user, $identityService, [bool]$removeAvatar)
{    
    if ($removeAvatar) {
        $user.SetProperty("Microsoft.TeamFoundation.Identity.Image.Data", $null)
        $user.SetProperty("Microsoft.TeamFoundation.Identity.Image.Type", $null)
        $user.SetProperty("Microsoft.TeamFoundation.Identity.Image.Id", $null)
        $user.SetProperty("Microsoft.TeamFoundation.Identity.CandidateImage.Data", $null)
        $user.SetProperty("Microsoft.TeamFoundation.Identity.CandidateImage.UploadDate", $null)

        Write-Host "Removing avatar for $($user.UniqueName)"
        $identityService.UpdateExtendedProperties($user)
        return
    }
    
    if (-not $user.IsActive) {
        return
    }    
            
    $hashTable = @{}
    foreach ($prop in $user.GetProperties()) {
        $hashTable.Add($prop.Key, $prop.Value)
    }

    Write-Host "Getting user photo for $($user.UniqueName)"
    
    $mail = $user.GetProperty("Mail")
    if ($mail) {
        $uri = "https://outlook.office365.com/EWS/Exchange.asmx/s/GetUserPhoto?email={0}&size=HR648X648" -f $mail

        $stream = $null
        $bitmap = $null
        try {
            $client = [System.Net.WebClient]::new()
            $client.Headers[ "Accept" ] = "/"
            $client.Credentials = [System.Net.NetworkCredential]::new("service.tfsbuild.ap@aderant.com", $password)
            $imageBytes = $client.DownloadData($uri)
            $client.Dispose()

            $stream = [System.IO.MemoryStream]::new($imageBytes)
            $bitmap = [System.Drawing.Image]::FromStream($stream)

            $format = ""
            if ([System.Drawing.Imaging.ImageFormat]::Jpeg.Equals($bitmap.RawFormat)) {                
                $format = "image/jpg"
            } 
          
            if ([System.Drawing.Imaging.ImageFormat]::Png.Equals($bitmap.RawFormat)) {
                $format = "image/png"
            }
    
            if ($format) {
                $converter = [System.Drawing.ImageConverter]::new()
                [byte[]]$imageBytes = $converter.ConvertTo($bitmap, [byte[]])
    
                SetTeamFoundationProperties $identityService $user $imageBytes $format
            }
        } catch {
            if ($_.Exception.InnerException.Response.StatusCode -ne 404) {
                Write-Host "Error updating $($user.UniqueName)"
                Write-Host $_                
                Write-Host $_.Exception.ToString()
                Write-Error $_
                return
            }
            Write-Host "...No image found"
        } finally {
            if ($stream) {
                $stream.Dispose()
                $bitmap.Dispose()                
            }
        }
    } else {
        Write-Host "No address for $($user.UniqueName)"
    }
}


function Sync() {
    $returnValue = GetUsers  

    $service = $returnValue.Service
    $users = $returnValue.Users    
  
    foreach ($user in $users) {
        ProcessUser $user $service $false
    }
}

Sync