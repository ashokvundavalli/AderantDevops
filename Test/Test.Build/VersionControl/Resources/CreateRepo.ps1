git init Repo
cd repo
echo "Some text" > master.txt
git add .
git commit -m "Added master.txt"
echo "Some text more text" >> master.txt

# Create saturn branch
git checkout -b "saturn"
echo "Some text" > saturn.txt
git add .
git commit -m "Added saturn.txt"