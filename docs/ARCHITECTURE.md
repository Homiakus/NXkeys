# Архитектура NXKeys

## 1. Цель

NXKeys построен как единый C#-контур для Siemens NX 2512. Все основные операции используют общие модели конфигурации, единый сканер, один deployment engine и один launcher.

Основные цели:

- исключить расхождение нескольких реализаций развёртывания;
- не изменять установку Siemens;
- обеспечить транзакционную установку;
- выполнять команды только через проверенный контекст NX;
- разделить UI-команды и низкоуровневый NXOpen/UFUN-каталог;
- сохранять возможность полного восстановления.

## 2. Компоненты

```text
config/nx2512-pro-hybrid.json
              │
              ▼
NX2512_HotkeyStudio
 ├─ WinForms UI
 ├─ C# CLI
 ├─ NxScanner
 ├─ CommandResolver
 ├─ DeploymentEngine
 ├─ NxRuntimeService
 ├─ LeaderKeyEngine
 ├─ BackupEngine
 └─ HealthService
      │
      ├───────────────┐
      ▼               ▼
managed package   bridge queue
      │               │
      ▼               ▼
C# NX launcher    NX2512_CommandBridge.dll
      │               │
      └──────► Siemens NX ◄──────┘

NX2512_ControlCenter
 ├─ Adaptive Leader overview
 ├─ coverage metrics
 ├─ bridge context
 └─ NX API Explorer

NX2512_Catalog_Studio
 └─ NXOpen/UFUN/UI catalog export
```

Go CLI/TUI удалён. Каталоги `cmd/`, `internal/`, `go.mod` и Go build-скрипты больше не входят в архитектуру.

## 3. Источник истины

Единственный редактируемый источник конфигурации:

```text
config/nx2512-pro-hybrid.json
```

Производные файлы:

- `nxkeys_generated.men`;
- `nxkeys_ribbon.rtb`;
- `nxkeys_toolbar.tbr`;
- `custom_dirs.dat`;
- CMD launchers;
- отчёт разрешения;
- radial-план;
- `package-manifest.json`.

Производные файлы не редактируются вручную.

## 4. Сканирование

`NxScanner` собирает корни из:

- `scan.roots`;
- `scan.install_hints`;
- `scan.profile_hints`;
- `UGII_BASE_DIR`;
- `UGII_ROOT_DIR`;
- `UGII_USER_PROFILE_DIR`;
- `UGII_SITE_DIR`;
- `UGOPEN`;
- `UGII_CUSTOM_DIRECTORY_FILE`;
- стандартных каталогов Siemens в Program Files и AppData.

Ограничения:

- `max_depth`;
- `max_files`;
- symbolic links/reparse points не обходятся без `follow_symlinks: true`;
- launcher-файлы учитываются только при наличии NX-маркеров;
- ошибки доступа сохраняются как предупреждения.

## 5. API-каталог

Порядок разрешения каталога:

1. явный `--catalog`;
2. переменная `NXKEYS_CATALOG_DIR`;
3. наиболее свежий каталог под `%LOCALAPPDATA%\NXKeys\catalog`.

Абсолютные пути рабочего компьютера разработчика запрещены.

Ключ кэша включает:

- пути MenuScript-файлов;
- размеры;
- время изменения;
- все используемые CSV API-каталога;
- версию схемы сканера;
- версию NX.

Кэш сохраняет как команды, так и crosswalk entries.

## 6. План развёртывания

`DeploymentEngine.BuildPlan`:

1. разрешает горячие клавиши;
2. определяет конфликты;
3. формирует MenuScript;
4. формирует отчёт разрешения;
5. формирует radial-план;
6. показывает managed root и число подтверждённых команд.

Неразрешённые и неоднозначные команды не должны попадать в активный overlay.

## 7. Транзакционный deployment

`DeploymentEngine.ApplyPlan` использует следующие фазы.

### 7.1. Проверка

- валидируется профиль;
- проверяется план;
- проверяется состояние NX;
- проверяются обязательные артефакты;
- вычисляются полные пути;
- запрещается неявный выбор existing `custom_dirs.dat`.

### 7.2. Формирование набора

В управляемый набор входят:

- HotkeyStudio runtime;
- канонический профиль;
- MenuScript;
- ribbon/toolbar;
- C# launcher;
- Bridge только в `custom\application`;
- Control Center в отдельной подпапке;
- отчёты;
- роль `.mtx`, только если включена явно.

### 7.3. Staging

Каждый файл сначала сохраняется во временный staging-каталог:

```text
%LOCALAPPDATA%\NXKeys\staging\<guid>
```

После записи проверяется SHA-256.

### 7.4. Backup

До изменения целевых файлов создаётся backup manifest. Он включает:

- изменяемые файлы;
- новые файлы;
- файлы, которые будут удалены как устаревшие;
- package manifest.

### 7.5. Commit

Запись выполняет `AtomicFileWriter`:

1. создаёт временный файл в каталоге назначения;
2. записывает с `WriteThrough`;
3. выполняет `Flush(true)`;
4. сохраняет локальную rollback-копию существующего файла;
5. заменяет целевой файл;
6. восстанавливает исходный файл при ошибке replace/move;
7. повторяет операцию при временной блокировке.

### 7.6. Удаление устаревших файлов

Удаляются только пути, которые:

- перечислены в предыдущем `package-manifest.json`;
- находятся внутри текущего `managed_root`;
- отсутствуют в новом наборе.

Произвольные пользовательские файлы не удаляются.

### 7.7. Package manifest

`package-manifest.json` записывается последним и содержит:

- schema version;
- package version;
- target NX;
- managed root;
- дату;
- относительный путь каждого файла;
- SHA-256;
- размер;
- обязательность.

### 7.8. Post-check

После commit-фазы проверяются:

- существование каждого файла;
- соответствие SHA-256;
- отсутствие выхода относительных путей за managed root.

### 7.9. Rollback

Любое исключение после создания backup вызывает принудительное восстановление backup manifest.

## 8. Existing custom directories

Режим `existing-custom-dirs` больше не сканирует и не изменяет все подпапки Siemens.

Требуется явный путь:

```json
{
  "deployment": {
    "mode": "existing-custom-dirs",
    "existing_custom_dirs_file": "D:\\NX\\custom_dirs.dat",
    "patch_existing_custom_dirs": true
  }
}
```

`TextFileCodec` сохраняет:

- UTF-8 / UTF-8 BOM;
- UTF-16 LE/BE;
- системную кодировку при невозможности строгого UTF-8 decode;
- CRLF или LF;
- существующие строки.

## 9. Запуск NX

Сгенерированный CMD не формирует среду NX самостоятельно. Он вызывает:

```text
NX2512_HotkeyStudio.exe launch
```

`NxRuntimeService`:

1. разрешает `ugraf.exe` из профиля, environment hints и стандартных путей;
2. предпочитает путь, соответствующий версии профиля;
3. проверяет файл;
4. проверяет `custom_dirs.dat`;
5. запускает Leader Engine через single-instance signal;
6. создаёт дочерний процесс NX;
7. задаёт только `UGII_CUSTOM_DIRECTORY_FILE`;
8. передаёт аргументы через `ProcessStartInfo.ArgumentList`.

`PATH` и `UGII_USER_DIR` не изменяются.

## 10. Определение запущенного NX

Единый `NxRuntimeService` проверяет процессы:

- `ugraf`;
- `run_nx`;
- `nx` только при подтверждении по пути или описанию Siemens/Designcenter/NXBIN.

Эта же логика используется launcher, installer и health-check.

## 11. Leader Engine

`LeaderKeyEngine` работает вне процесса NX и использует:

- глобальный keyboard hook;
- single-instance mutex;
- event handles для GUI/toggle/start;
- context-файл Bridge;
- последовательности профиля;
- подтверждение опасных команд.

`AdaptiveLeaderPolicy` ранжирует команды по модулю, выбору, рабочей детали, модальному диалогу, частоте и давности использования.

## 12. Command Bridge

Bridge загружается только из:

```text
custom\application\NX2512_CommandBridge.dll
```

Вторая DLL в `custom\startup` не создаётся.

Файловый протокол:

```text
HotkeyStudio/Leader
       │
       ▼
bridge\pending\<request>.json
       │
       ▼
NX2512_CommandBridge внутри NX
       │
       ├─ completed
       └─ failed
```

Bridge проверяет `MenuButton`, availability, sensitivity и результат вызова.

## 13. Control Center

Control Center не заменяет runtime engine. Он:

- показывает профиль;
- отображает bridge context;
- рассчитывает базовые метрики;
- ранжирует Leader-команды;
- объясняет недоступность;
- ищет NXOpen/UFUN/UI candidates.

## 14. Catalog Studio

Catalog Studio формирует CSV-наборы:

- NXOpen assemblies/namespaces/types;
- members;
- entry points;
- UI buttons;
- UFUN functions;
- UI → API candidates.

Crosswalk является эвристикой и не считается доказательством эквивалентности API-вызова UI-команде.

## 15. Build и CI

Репозиторий содержит только C#-исходники и PowerShell build/install wrappers.

CI:

- проверяет отсутствие `.go` и `go.mod`;
- валидирует JSON;
- собирает HotkeyStudio;
- публикует HotkeyStudio;
- выполняет C# `validate`;
- собирает и публикует Control Center;
- проверяет deployment-инварианты;
- формирует Windows x64 artifact.

CommandBridge собирается локально, поскольку требует проприетарные NXOpen DLL установленной версии Siemens NX.
