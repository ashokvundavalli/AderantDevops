Set-StrictMode -Version "Latest"
[int]$i = 1

& git -C $DeploymentItemsDirectory init
Add-Content -LiteralPath "$DeploymentItemsDirectory\master.txt" -Value  "Some text"
& git -C $DeploymentItemsDirectory add "master.txt"
& git -C $DeploymentItemsDirectory commit -m "$($i++;$i) Added master.txt"

Add-Content -LiteralPath "$DeploymentItemsDirectory\master.txt" -Value  "Some more"
& git -C $DeploymentItemsDirectory add "master.txt"
& git -C $DeploymentItemsDirectory commit -m "$($i++;$i) Modified master.txt"

Add-Content -Path "$DeploymentItemsDirectory\master.txt" -Value  "Some more!"
& git -C $DeploymentItemsDirectory add "master.txt"
& git -C $DeploymentItemsDirectory commit -m "$($i++;$i) Modified master.txt"

# Create saturn branch
& git -C $DeploymentItemsDirectory checkout -b "saturn"
Add-Content -LiteralPath "$DeploymentItemsDirectory\saturn.txt" -Value  "Some text"
& git -C $DeploymentItemsDirectory add "saturn.txt"
& git -C $DeploymentItemsDirectory commit -m "$($i++;$i) Added saturn.txt"

& git -C $DeploymentItemsDirectory checkout -b "saturn"
Add-Content -LiteralPath "$DeploymentItemsDirectory\saturn.txt" -Value  "Some more text"
& git -C $DeploymentItemsDirectory add "saturn.txt"
& git -C $DeploymentItemsDirectory commit -m "$($i++;$i) Modified saturn.txt"