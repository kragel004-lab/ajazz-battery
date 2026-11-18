param(
    [string]$Repo,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Info($text) { Write-Host $text -ForegroundColor Cyan }
function Warn($text) { Write-Host $text -ForegroundColor Yellow }

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git не найден в PATH. Установите Git for Windows.'
}

$changes = git status --porcelain
if ($changes) {
    Warn 'Есть незакоммиченные изменения. Сначала:'
    Write-Host '  git add .'
    Write-Host '  git commit -m "..."'
    $changes | Select-Object -First 10
    exit 1
}

if (-not $Repo) {
    $existing = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $existing) {
        $Repo = $existing
    }
    else {
        Warn 'Адрес репозитория не задан.'
        Write-Host ''
        Write-Host '1) Создайте ПУСТОЙ репозиторий:  https://github.com/new'
        Write-Host '   (без README, .gitignore и лицензии)'
        Write-Host '2) Запустите с его адресом:'
        Write-Host '   .\publish.ps1 -Repo https://github.com/ЛОГИН/ИМЯ.git'
        if (-not $Check) {
            Start-Process 'https://github.com/new'
        }
        exit 1
    }
}

if ($Repo -match '[А-Яа-яЁё]') {
    throw "В адресе репозитория кириллица: $Repo"
}

Info "Репозиторий: $Repo"
Info "Ветка:       main"
Info "Коммитов:    $((git rev-list --count HEAD))"

if ($Check) {
    Write-Host ''
    Write-Host 'Режим проверки: ничего не меняю, push не выполняю.'
    exit 0
}

git remote remove origin 2>$null | Out-Null
git remote add origin $Repo
git branch -M main
Info "origin → $(git remote get-url origin)"
Write-Host ''

Write-Host 'Отправляю коммиты. Если откроется окно Git Credential Manager —'
Write-Host 'выберите «Sign in with your browser» и подтвердите вход в GitHub.'
Write-Host ''

git push -u origin main
if ($LASTEXITCODE -ne 0) {
    Warn 'Push не удался. Частые причины:'
    Write-Host '  • в репозитории уже есть файлы (README) → git pull --rebase origin main, затем повторить'
    Write-Host '  • вместо пароля нужен токен → см. GIT.md, раздел «Вариант A»'
    Write-Host '  • нет прав на этот репозиторий (опечатка в логине/имени)'
    exit 1
}

$web = $Repo -replace '\.git$', ''
Info 'Готово! Код на GitHub.'
Write-Host "  репозиторий: $web"
Write-Host "  релизы:      $web/releases/new  (приложите dist\AJAZZ-Battery-Setup.exe и AJAZZ-Battery-Portable.zip)"
if (-not $Check) {
    Start-Process "$web/releases/new"
}
