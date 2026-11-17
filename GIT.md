# Как залить проект на GitHub

В папке проекта уже создан локальный git-репозиторий и первый коммит. Осталось
завести репозиторий на GitHub и отправить туда код.

---

## 1. Создать репозиторий на GitHub

1. Откройте <https://github.com/new>.
2. **Repository name**: `ajazz-battery` (или любое другое).
3. Выберите **Public** или **Private**.
4. **Не** ставьте галочки «Add a README», «Add .gitignore», «Choose a license» — они уже есть локально, иначе получится конфликт.
5. Нажмите **Create repository**.

После создания GitHub покажет адрес вида:

```
https://github.com/ВАШ_ЛОГИН/ajazz-battery.git
```

---

## 2. Привязать адрес репозитория

В папке проекта:

```powershell
git remote add origin https://github.com/ВАШ_ЛОГИН/ajazz-battery.git
git branch -M main
```

Проверить, что адрес записался:

```powershell
git remote -v
```

---

## 3. Авторизация: откуда взять «ключ»

GitHub **не принимает пароль от аккаунта** при push. Нужен один из вариантов.

### Вариант A. Personal Access Token (проще всего, по HTTPS)

1. GitHub → аватар → **Settings** → внизу слева **Developer settings**
   (или сразу <https://github.com/settings/tokens>).
2. **Personal access tokens** → **Tokens (classic)** → **Generate new token (classic)**.
3. Note: `ajazz-battery-push`, Expiration: например 90 дней.
4. Scopes: поставьте галочку **repo** (для приватного репозитория он обязателен,
   для публичного достаточно `public_repo`).
5. **Generate token** — покажется строка вида `ghp_xxxxxxxxxxxxxxxxxxxx`.
   Скопируйте её сразу: второй раз GitHub её не покажет.

При первом `git push` Git спросит логин и пароль:

- **Username**: ваш логин GitHub;
- **Password**: вставьте **токен** (не пароль от аккаунта).

Windows-версия Git умеет запоминать это в «Диспетчере учётных данных»
(Git Credential Manager), поэтому спросит один раз.

Повторно посмотреть/удалить сохранённый токен:
**Панель управления → Диспетчер учётных данных → Учётные данные Windows →
`git:https://github.com`**.

> Токен — это пароль. Не коммитьте его в репозиторий и не вставляйте в код.

### Вариант B. SSH-ключ (удобно на постоянку, ключ лежит в файле)

1. Сгенерировать ключ (в PowerShell):

```powershell
ssh-keygen -t ed25519 -C "ваша_почта@example.com"
```

На вопросы: путь по умолчанию (`C:\Users\ВАШ_ПОЛЬЗОВАТЕЛЬ\.ssh\id_ed25519`) — Enter,
парольную фразу — можно оставить пустой.

Получатся два файла:

- `C:\Users\ВАШ_ПОЛЬЗОВАТЕЛЬ\.ssh\id_ed25519` — **приватный**, никому не отдавать;
- `C:\Users\ВАШ_ПОЛЬЗОВАТЕЛЬ\.ssh\id_ed25519.pub` — **публичный**, его и добавляем на GitHub.

2. Скопировать содержимое публичного ключа:

```powershell
Get-Content "$env:USERPROFILE\.ssh\id_ed25519.pub" | Set-Clipboard
```

3. GitHub → **Settings** → **SSH and GPG keys** → **New SSH key** →
   Title: `домашний ПК`, Key type: `Authentication Key`, вставить ключ → **Add SSH key**.

4. Проверить связь:

```powershell
ssh -T git@github.com
```

Должно ответить `Hi ВАШ_ЛОГИН! You've successfully authenticated...`

5. Переключить remote на SSH:

```powershell
git remote set-url origin git@github.com:ВАШ_ЛОГИН/ajazz-battery.git
```

### Вариант C. GitHub CLI

```powershell
winget install GitHub.cli
gh auth login
```

Выбираете `GitHub.com` → `HTTPS` → вход через браузер (или тот же токен).
Дальше `gh` сам создаст репозиторий и запушит:

```powershell
gh repo create ajazz-battery --public --source . --remote origin --push
```

---

## 4. Отправить код

```powershell
git push -u origin main
```

Если всё хорошо, в репозитории появятся файлы, а `origin/main` будет
отслеживаться. Дальше достаточно:

```powershell
git add .
git commit -m "Что изменилось"
git push
```

---

## 5. Куда класть готовые exe

В git **не нужно** коммитить `dist/` (он в `.gitignore`): файлы по 45–90 МБ
раздувают репозиторий. Для бинарников есть **Releases**:

1. Соберите пакеты: `.\build.ps1`.
2. GitHub → **Releases** → **Draft a new release**.
3. Tag: `v1.1.0`, Title: `AJAZZ Battery 1.1.0`.
4. Прикрепите файлы из `dist\`:
   - `AJAZZ-Battery-Setup.exe` — установщик;
   - `AJAZZ-Battery-Portable.zip` — портативная версия.
5. **Publish release**.

Через CLI то же самое:

```powershell
gh release create v1.1.0 dist\AJAZZ-Battery-Setup.exe dist\AJAZZ-Battery-Portable.zip --title "AJAZZ Battery 1.1.0" --notes "Первый публичный релиз"
```

---

## Частые ошибки

| Сообщение | Причина и решение |
|---|---|
| `remote: Support for password authentication was removed` | Вместо пароля нужен **токен** (вариант A) или SSH (вариант B) |
| `Permission denied (publickey)` | Публичный ключ не добавлен на GitHub, либо remote остался HTTPS |
| `fatal: remote origin already exists` | `git remote set-url origin <новый адрес>` |
| `Updates were rejected because the remote contains work...` | На GitHub уже есть коммиты (например, создали README): `git pull --rebase origin main`, затем `git push` |
| `src refspec main does not match any` | Ещё нет коммитов: `git add . && git commit -m "init"` |

---

## Кто автор коммитов

В этом репозитории автор задан локально:

```powershell
git config --local user.name  "Ваше Имя"
git config --local user.email "ваша_почта@example.com"
```

Чтобы применить ко всем репозиториям — те же команды с `--global`.
