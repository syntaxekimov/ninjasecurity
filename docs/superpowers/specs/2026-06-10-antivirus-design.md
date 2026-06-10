# AppName — Antivirus & System Security Suite
**Дата:** 2026-06-10  
**Статус:** Утверждено  
**Название проекта:** TBD (placeholder: AppName)

---

## 1. Обзор

Десктопное антивирусное приложение для Windows с GUI, нацеленное на обычного пользователя. Объединяет лучшие open-source наработки: базы сигнатур ClamAV, YARA-правила, интеграцию MalSearcher. Дополнительно включает инструменты оптимизации системы. Архитектура спроектирована под будущее расширение на Linux и Android.

**Целевая аудитория:** Обычный пользователь Windows. Минимум кнопок, максимум автоматики. Технические детали скрыты.

---

## 2. Платформа и стек

| Параметр | Решение |
|---|---|
| Платформа v1 | Windows 10/11 |
| Будущие платформы | Linux, Android |
| Язык | C# (.NET 8+) |
| UI-фреймворк | Avalonia UI (кросс-платформенный, поддерживает Windows/Linux/Android) |
| UI-паттерн | MVVM + CommunityToolkit.Mvvm |
| БД | SQLite (via Entity Framework Core) |
| IPC | Named Pipes (Service ↔ GUI) |
| Протокол IPC | JSON-сообщения |
| Антивирусный движок | ClamAV (libclamav) + YARA-python bindings via .NET interop |
| Хранилище баз | %AppData%\AppName\Databases\ |
| Карантин | %AppData%\AppName\Quarantine\ |

---

## 3. Архитектура

### 3.1 Два процесса

```
┌─────────────────────────────────────────────┐
│  AppName.Service  (Windows Service)         │
│  ─ запускается при старте Windows           │
│  ─ работает без GUI                         │
│                                             │
│  ┌──────────────┐  ┌──────────────────────┐ │
│  │ ScanEngine   │  │ RealTimeGuard        │ │
│  │ (ClamAV+YARA)│  │ (FileSystemWatcher)  │ │
│  └──────────────┘  └──────────────────────┘ │
│  ┌──────────────┐  ┌──────────────────────┐ │
│  │ Quarantine   │  │ ProcessMonitor       │ │
│  │ Manager      │  │ (каждые 30 сек)      │ │
│  └──────────────┘  └──────────────────────┘ │
│  ┌──────────────┐  ┌──────────────────────┐ │
│  │ SystemOpt-   │  │ SignatureUpdater      │ │
│  │ imizer       │  │ (ClamAV+YARA+Mal)    │ │
│  └──────────────┘  └──────────────────────┘ │
│                                             │
│  IpcServer ←──── Named Pipes ────→          │
└─────────────────────────────────────────────┘
                       ↕ JSON messages
┌─────────────────────────────────────────────┐
│  AppName.App  (Avalonia Desktop)            │
│                                             │
│  IpcClient ──── Named Pipes ──→ Service     │
│                                             │
│  Views: Dashboard, Scan, Quarantine,        │
│         Optimize, Processes, Settings       │
│                                             │
│  TrayIcon (уведомления, быстрый скан)       │
└─────────────────────────────────────────────┘
```

### 3.2 IPC-протокол (Named Pipes, JSON)

**Команды GUI → Сервис:**
```json
{ "cmd": "StartScan", "type": "Quick" }
{ "cmd": "StartScan", "type": "Full" }
{ "cmd": "StartScan", "type": "Custom", "path": "C:\\Users\\..." }
{ "cmd": "StopScan" }
{ "cmd": "QuarantineAction", "action": "Delete", "id": "uuid" }
{ "cmd": "QuarantineAction", "action": "Restore", "id": "uuid" }
{ "cmd": "UpdateSignatures" }
{ "cmd": "GetStatus" }
```

**События Сервис → GUI:**
```json
{ "event": "StatusChanged", "protected": true, "serviceRunning": true }
{ "event": "ScanProgress", "percent": 42, "currentFile": "C:\\..." }
{ "event": "ThreatFound", "path": "C:\\...", "name": "Trojan.Win32.X" }
{ "event": "ScanComplete", "threats": 2, "scanned": 15432 }
{ "event": "SignaturesUpdated", "date": "2026-06-10" }
```

---

## 4. Функциональные требования

### 4.1 Сканер (ScanEngine)

Три слоя проверки каждого файла:

1. **Хеш-проверка** (~1 мс/файл)
   - Вычислить SHA256 файла
   - Поиск в локальной SQLite-базе известных угроз
   - Поиск в базе MalSearcher (локальная копия)

2. **Сигнатуры ClamAV** (~5 мс/файл)
   - Использовать libclamav через P/Invoke или ClamAV.NET-обёртку
   - Базы: `main.cvd`, `daily.cvd`, `bytecode.cvd`

3. **YARA-правила** (~20 мс/файл)
   - Набор правил из репозитория Yara-Rules/rules
   - Поддержка пользовательских .yar файлов

**Типы сканирования:**

| Тип | Что проверяет | Примерное время |
|---|---|---|
| Быстрый | %TEMP%, %APPDATA%, %SystemRoot%\System32, автозагрузка, запущенные процессы | ~2 мин |
| Полный | Все диски, каждый файл | ~20-60 мин |
| Выборочный | Папка/файл по выбору пользователя | Зависит от размера |

### 4.2 Защита в реальном времени (RealTimeGuard)

- `FileSystemWatcher` на директориях: `%TEMP%`, `%TMP%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%\Downloads`, Рабочий стол, `%SystemRoot%\System32`
- Реагирует на события: `Created`, `Changed`, `Renamed`
- Фильтр по расширениям: `.exe`, `.dll`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.msi`, `.scr`
- При обнаружении: → `ScanEngine.ScanFile()` → при угрозе → автоматический карантин → Toast-уведомление → событие в GUI

### 4.3 Карантин (QuarantineManager)

- Изолированный файл шифруется (AES-256) и переносится в `%AppData%\AppName\Quarantine\`
- Оригинальный путь, имя угрозы, дата, SHA256 сохраняются в SQLite (`quarantine.db`)
- Действия: **Восстановить** (расшифровать на исходный путь) / **Удалить навсегда**
- GUI: таблица с колонками Имя файла, Угроза, Дата, Действия

### 4.4 Мониторинг процессов (ProcessMonitor)

- Опрос списка процессов каждые 30 секунд
- Новый процесс → проверка `.exe` через `ScanEngine`
- Оценка риска по эвристике:
  - Нет Authenticode-подписи → +риск
  - Запущен из `%TEMP%` или `%APPDATA%` → +риск
  - Активные сетевые соединения → +риск
- GUI: таблица процессов с цветовой индикацией риска (зелёный / жёлтый / красный)

### 4.5 Оптимизация системы (SystemOptimizer)

**Вкладка «Автозагрузка»:**
- Источники: реестр `HKCU/HKLM\...\Run`, папки Startup
- Действия: отключить / включить / удалить
- Каждый элемент проверяется через `ScanEngine` — подозрительные выделяются

**Вкладка «Временные файлы»:**
- Директории: `%TEMP%`, `%TMP%`, `%SystemRoot%\Temp`, кэш браузеров (опционально)
- Показывает объём до очистки → кнопка «Очистить»

**Вкладка «Службы»:**
- Список служб Windows со статусом
- Захардкоженный список рекомендаций (безопасно отключить)
- Действия: Stop / Disable / Enable

### 4.6 Обновление баз (SignatureUpdater)

- **ClamAV:** скачать `main.cvd`, `daily.cvd`, `bytecode.cvd` с официальных зеркал
- **YARA:** обновить набор правил из GitHub (Yara-Rules/rules)
- **MalSearcher:** загрузить/обновить локальную копию базы хешей с GitHub
- Расписание: автоматически раз в сутки при наличии интернета
- Ручное обновление из GUI (кнопка в sidebar и в Settings)

---

## 5. UI/UX

### 5.1 Визуальный стиль

- **Цветовая схема:** Светлая тема по умолчанию + тёмная тема (переключатель в хедере)
- **Акцентный цвет:** Indigo/Purple градиент (`#6366f1` → `#8b5cf6`)
- **Шрифт:** Segoe UI (Windows system font)
- **Скругления:** 10-16px border-radius на карточках
- **Тени:** Subtle, `0 1px 3px rgba(0,0,0,.08)`

### 5.2 Навигация

Боковая панель (220px, всегда видна):
- Логотип + название приложения
- Секция «Главное»: Дашборд, Сканирование, Карантин (бейдж с числом), Оптимизация, Процессы
- Секция «Система»: Настройки
- Внизу: блок с датой баз + кнопка обновить
- Активный пункт: градиентный фон pill-формы

### 5.3 Экраны

**Дашборд:**
- Hero-карточка: статус (Защищён / Угроза!) + градиентный фон
- Статистика: 3 карточки (Угрозы / Карантин / Сканов всего)
- Быстрые действия: «Запустить быстрое сканирование» + тумблер защиты реального времени
- Переключатель темы в правом верхнем углу

**Сканирование:**
- Три кнопки выбора типа (Быстрый / Полный / Выборочный)
- При выборочном: drag-n-drop или кнопка выбора папки
- Прогресс-бар + текущий файл + счётчик проверенных
- Список найденных угроз (появляется в процессе)
- Кнопки «Поместить всё в карантин» / «Игнорировать»

**Карантин:**
- Таблица: Имя файла | Тип угрозы | Дата | Действия
- Кнопки: Восстановить / Удалить для каждого файла
- Кнопка «Очистить всё»

**Оптимизация:**
- Три вкладки: Автозагрузка / Temp-файлы / Службы
- Кнопка «Анализировать» → показывает результаты → «Применить»

**Процессы:**
- Таблица: Имя | PID | Путь | Подпись | Риск (цвет)
- Кнопка «Завершить» для подозрительных
- Обновление каждые 30 сек (или кнопка обновить)

**Настройки:**
- Расписание автосканирования
- Директории под защитой (добавить/удалить)
- Расширения для мониторинга
- Обновление баз (авто/ручное, источники)
- Тема (светлая/тёмная/системная)
- Версия приложения и баз

### 5.4 Трей

- Иконка: зелёный щит = защищён, красный = угроза, серый = сервис не запущен
- Контекстное меню: Открыть / Быстрое сканирование / Выход
- Toast-уведомления при обнаружении угрозы

---

## 6. Структура проекта

**Примечание по ClamAV:** `nClam` подключается к запущенному процессу `clamd.exe`. `clamd.exe` (ClamAV for Windows) бандлится в поставку приложения и запускается как дочерний процесс внутри `AppName.Service`. Это стандартный подход для Windows-приложений, использующих ClamAV.

```
AppName/
├── AppName.sln
├── src/
│   ├── AppName.Service/          # Windows Service
│   │   ├── Engine/
│   │   │   ├── ScanEngine.cs
│   │   │   ├── RealTimeGuard.cs
│   │   │   ├── QuarantineManager.cs
│   │   │   ├── ProcessMonitor.cs
│   │   │   ├── SystemOptimizer.cs
│   │   │   └── SignatureUpdater.cs
│   │   ├── Ipc/
│   │   │   └── IpcServer.cs
│   │   ├── Data/
│   │   │   └── AppDbContext.cs   # SQLite: quarantine, scan history, settings
│   │   └── Worker.cs             # .NET Worker Service entry point
│   │
│   └── AppName.App/              # Avalonia UI
│       ├── Views/
│       │   ├── DashboardView.axaml
│       │   ├── ScanView.axaml
│       │   ├── QuarantineView.axaml
│       │   ├── OptimizerView.axaml
│       │   ├── ProcessesView.axaml
│       │   └── SettingsView.axaml
│       ├── ViewModels/
│       │   └── *ViewModel.cs     # CommunityToolkit.Mvvm
│       ├── Ipc/
│       │   └── IpcClient.cs
│       └── TrayIcon/
│           └── TrayIconManager.cs
│
├── databases/                    # Базы сигнатур (gitignore)
│   ├── clamav/
│   ├── yara/
│   └── malsearcher/
│
└── docs/
    └── superpowers/specs/
        └── 2026-06-10-antivirus-design.md
```

---

## 7. Зависимости (NuGet)

| Пакет | Назначение |
|---|---|
| Avalonia | UI фреймворк |
| CommunityToolkit.Mvvm | MVVM helpers |
| Microsoft.Extensions.Hosting | Windows Service hosting |
| Entity Framework Core + SQLite | База данных |
| nClam | ClamAV .NET клиент (подключается к clamd демону по TCP) |
| dnYara | YARA .NET bindings |
| Hardcodet.NotifyIcon.Wpf | Tray icon (или Avalonia.Controls.TrayIcon) |

---

## 8. Не входит в v1

- Сетевой мониторинг / файрвол
- Веб-защита (блокировка URL)
- Сканирование email
- Облачная проверка (VirusTotal API)
- Поддержка Linux и Android (v2+)
- Авто-обновление самого приложения

---

## 9. Будущие версии

- **v2:** Вынести `ScanEngine` + `RealTimeGuard` в отдельный Windows Service (уже предусмотрено архитектурой). Порт на Linux.
- **v3:** Android-версия (Avalonia Android). Сканирование APK, мониторинг разрешений.
