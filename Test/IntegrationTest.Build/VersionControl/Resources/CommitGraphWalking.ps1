Set-StrictMode -Version "Latest"
[int]$i = 1

& git init
Add-Content -Path "master.txt" -Value  "Some text"
& git add "master.txt"
& git commit -m "$($i++;$i) Added master.txt"

Add-Content -Path "master.txt" -Value  "Some more"
& git add "master.txt"
& git commit -m "$($i++;$i) Modified master.txt"

Add-Content -Path "master.txt" -Value  "Some more!"
& git add "master.txt"
& git commit -m "$($i++;$i) Modified master.txt"

# Create saturn branch
& git checkout -b "saturn"
Add-Content -Path "saturn.txt" -Value  "Some text"
& git add "saturn.txt"
& git commit -m "$($i++;$i) Added saturn.txt"

& git checkout -b "saturn"
Add-Content -Path "saturn.txt" -Value  "Some more text"
& git add "saturn.txt"
& git commit -m "$($i++;$i) Modified saturn.txt"