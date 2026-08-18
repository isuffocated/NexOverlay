$ErrorActionPreference = "Stop"

$root = Get-Location
$utf8 = New-Object System.Text.UTF8Encoding($false)

Write-Host "Continuing clean public-history setup..." -ForegroundColor Cyan

# Must be inside the NexOverlay git repo.
$inside = git rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0 -or $inside.Trim() -ne "true") {
    throw "Current directory is not a git repository."
}

$branch = (git branch --show-current).Trim()

Write-Host "Current branch: $branch" -ForegroundColor DarkGray

if ($branch -ne "nexoverlay-public-root" -and $branch -ne "main") {
    throw "Unexpected branch '$branch'. Expected nexoverlay-public-root or main."
}

# Keep our one-off patch/setup scripts out of the public repository.
$gitignorePath = Join-Path $root ".gitignore"

if (Test-Path $gitignorePath) {
    $gitignore = [System.IO.File]::ReadAllText($gitignorePath)
}
else {
    $gitignore = ""
}

$patterns = @(
    "prepare-*.ps1",
    "final-*.ps1",
    "fix-*.ps1",
    "step-*.ps1",
    "polish-*.ps1",
    "refine-*.ps1"
)

foreach ($pattern in $patterns) {
    if ($gitignore -notmatch "(?m)^" + [regex]::Escape($pattern) + "$") {
        if ($gitignore.Length -gt 0 -and !$gitignore.EndsWith("`n")) {
            $gitignore += "`r`n"
        }

        $gitignore += $pattern + "`r`n"
    }
}

[System.IO.File]::WriteAllText(
    $gitignorePath,
    $gitignore,
    $utf8
)

Write-Host ""
Write-Host "Clearing Git index only (working files stay untouched)..." -ForegroundColor Cyan

# On an orphan branch the index can contain staged entries inherited
# from the previous branch. Force-remove them from index only.
git rm -r --cached -f . 2>$null | Out-Null

if ($LASTEXITCODE -ne 0) {
    # If the index is already effectively empty, continue.
    Write-Host "Index remove returned non-zero; checking status before continuing..." -ForegroundColor Yellow
}

Write-Host "Rebuilding index from current working tree..." -ForegroundColor Cyan

git add -A

if ($LASTEXITCODE -ne 0) {
    throw "git add -A failed."
}

Write-Host ""
Write-Host "Files staged for the public root commit:" -ForegroundColor Cyan
git status --short

Write-Host ""
Write-Host "Creating clean initial commit..." -ForegroundColor Cyan

git commit -m "feat: initial NexOverlay beta"

if ($LASTEXITCODE -ne 0) {
    throw "Initial public commit failed."
}

# Make this clean root the local main branch.
git branch -M main

if ($LASTEXITCODE -ne 0) {
    throw "Could not rename branch to main."
}

# Ensure the intended identity and remote are still in place.
git config user.name "isuffocated"
git config user.email "314837406+isuffocated@users.noreply.github.com"

$remoteUrl = "https://github.com/isuffocated/NexOverlay.git"

$origin = git remote get-url origin 2>$null

if ($LASTEXITCODE -eq 0) {
    git remote set-url origin $remoteUrl
}
else {
    git remote add origin $remoteUrl
}

Write-Host ""
Write-Host "Clean public root created." -ForegroundColor Green

Write-Host ""
Write-Host "Log:" -ForegroundColor Cyan
git log --oneline --decorate -5

Write-Host ""
Write-Host "Status:" -ForegroundColor Cyan
git status

Write-Host ""
Write-Host "Remote:" -ForegroundColor Cyan
git remote -v

Write-Host ""
Write-Host "NEXT COMMAND:" -ForegroundColor Green
Write-Host "git push -u origin main" -ForegroundColor White
