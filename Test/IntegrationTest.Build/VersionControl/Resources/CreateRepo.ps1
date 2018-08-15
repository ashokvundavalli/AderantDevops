& git init
Add-Content -Path "master.txt" -Value  "Some text"
& git add "master.txt"
& git commit -m "Added master.txt"

# Create saturn branch
& git checkout -b "saturn"
Add-Content -Path "saturn.txt" -Value  "Some text"
& git add "saturn.txt"
& git commit -m "Added saturn.txt"

Add-Content -Path "saturn.txt" -Value "Some text more text"
Add-Content -Path "master.txt" -Value "Some text more text"