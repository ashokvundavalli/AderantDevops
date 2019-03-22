& git  -C $DeploymentItemsDirectory init
Add-Content -LiteralPath "$DeploymentItemsDirectory\master.txt" -Value  "Some text"
& git -C $DeploymentItemsDirectory add "master.txt"
& git -C $DeploymentItemsDirectory commit -m "Added master.txt"

# Create saturn branch
& git -C $DeploymentItemsDirectory checkout -b "saturn"
Add-Content -LiteralPath "$DeploymentItemsDirectory\saturn.txt" -Value  "Some text"
& git -C $DeploymentItemsDirectory add "saturn.txt"
& git -C $DeploymentItemsDirectory commit -m "Added saturn.txt"

Add-Content -LiteralPath "$DeploymentItemsDirectory\saturn.txt" -Value "Some text more text"
Add-Content -LiteralPath "$DeploymentItemsDirectory\master.txt" -Value "Some text more text"