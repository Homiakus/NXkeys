# Журнал реализации интеграции NxEskd в NXKeys (Этапы E0–E12)

Этот документ содержит полный хронологический журнал выполнения каждого этапа, архитектурных решений, изменений в кодовой базе и результатов верификации интеграции модуля ЕСКД в состав NXKeys.

---

## Статус выполнения этапов

| Этап | Описание | Статус |
|:---|:---|:---:|
| **E0** | Фиксация baseline, архитектурные guards и CI gates | ✅ Завершено |
| **E1** | Сведение мнемоники и capability model к единому входу | ✅ Завершено |
| **E2** | Удаление 6 command DLL, shims и отдельного меню | ✅ Завершено |
| **E3** | Подключение NxEskd к защищённому протоколу и очереди | ✅ Завершено |
| **E4** | Неблокирующий запуск Configurator | ✅ Завершено |
| **E5** | Редизайн Configurator вокруг 4 состояний задачи | ✅ Завершено |
| **E6** | Единый пайплайн Snapshot, DAG Plan и Preview | ✅ Завершено |
| **E7** | Укрепление транзакционных границ NX | ✅ Завершено |
| **E8** | Прогресс, структурированные отчёты и recovery | ✅ Завершено |
| **E9** | Опциональная установка (--with nxeskd) и миграция | ✅ Завершено |
| **E10** | Жизненный цикл профилей и шаблонов | ✅ Завершено |
| **E11** | Интеграция генерации комплектов чертежей (Packages) | ✅ Завершено |
| **E12** | Сокращение репозитория, документация и финальная валидация | ✅ Завершено |

---

## Подробный журнал действий по этапам

### Этап E12: Сокращение репозитория, документация и финальная валидация (Завершено)
- [x] Решение `nxeskd/NxEskdDrawingAutomation.sln` стабилизировано ровно в составе 6 канонических проектов:
  - `NxEskd.Core` — предметная логика, модель данных, DAG-планировщик (без зависимостей от UI и NXOpen);
  - `NxEskd.NxRuntime` — мост выполнения в процессе NX через `NX2512_CommandBridge`;
  - `NxEskd.Configurator` — интерфейс настройки и 4-шаговый мастер рабочего процесса;
  - `NxEskd.Core.Tests` — модульные и архитектурные тесты ядра;
  - `NxEskd.Configurator.Tests` — тесты документной модели и логики UI;
  - `NxEskd.PackagePlanner` — консольная утилита планирования комплектов чертежей.
- [x] Очищены избыточные дублирующиеся каталоги API и временные файлы, централизована база знаний API NX.
- [x] Полностью актуализирован комплект канонической документации:
  - `docs/NXESKD_INTEGRATION_PLAN.md` — генеральный план интеграции;
  - `nxeskd/docs/CAPABILITY_MATRIX.md` — матрица возможностей со статусами реализации;
  - `nxeskd/docs/VERIFICATION_STATUS.md` — отчет о верификации подсистем;
  - `nxeskd/docs/ARCHITECTURE_AND_CONFIGURATOR.md` — архитектурный манифест и изоляция контекстов;
  - `nxeskd/docs/DRAWING_PACKAGES.md` — документация подсистемы формирования комплектов.
- [x] Все скрипты валидации NXKeys и тесты пройдены:
  - `validate-documentation.mjs` (28 канонических файлов, схемы 8/4);
  - `validate-capability-route-lock.mjs` (439 операций, 131 исполняемая кнопка);
  - `NxEskd.Core.Tests` (45 тестов успешно);
  - `NxEskd.Configurator.Tests` (27 тестов успешно);
  - `NX2512_HotkeyStudio.Tests` (полный набор регрессионных тестов).

---

### Этап E11: Интеграция генерации комплектов чертежей (Packages) (Завершено)
- [x] В `NxEskd.Core` реализованы `DrawingPackagePlanner` и модель `DrawingPackageManifest` для планирования и пакетной генерации чертежей сборок и многофайловых проектов.
- [x] В первый шаг мастера Configurator (`1. Исходные данные`) интегрирован выбор целевого режима: `Текущая деталь` либо `Комплект чертежей (Package)`.
- [x] Утилита `NxEskd.PackagePlanner` переведена на единый DAG-движок `DrawingPlanner` без дублирования правил ЕСКД.
- [x] Обеспечена последовательная транзакционная обработка каждого документа в пакете:
  - для каждого файла создаётся независимый `ModelSnapshot` и строится собственный DAG-план;
  - фиксация и верификация транзакции производятся для каждого файла по отдельности;
  - ошибка в одном чертеже не повреждает ранее сгенерированные файлы;
  - отмена пакета выполняется на безопасной границе между документами.
- [x] Формирование структурированного манифеста пакета с детализацией по каждому чертежу и статусом ручной проверки.

---

### Этап E10: Жизненный цикл профилей и шаблонов (Завершено)
- [x] Строгое разделение доменных моделей: профиль ЕСКД (`active-profile.json`) отделен от мнемонического клавиатурного профиля NXKeys (`nx2512-v8-profile.json`).
- [x] В `ProfileLoader.cs` обеспечена строгая валидация структуры профиля (Schema Version 1.0.0), отклонение неизвестных полей (`Reject`) и расчет SHA-256 хэша профиля.
- [x] В `ProfileEditorDocument.cs` реализована поддержка неизменяемых встроенных пресетов (ЕСКД по умолчанию, Корпусные детали, Детали вращения, Сборочные единицы) и сохранение производных пользовательских профилей.
- [x] Реализовано детерминированное разрешение путей к шаблонам чертежей (`.prt`) и форматам основной надписи с проверкой существования файлов и безопасным fallback.
- [x] Обеспечена миграция устаревших профилей из `%LOCALAPPDATA%\NxEskdGenerator\profiles` в единое хранилище `%LOCALAPPDATA%\NXKeys\profiles\nxeskd\`.

---

### Этап E9: Опциональная установка (--with nxeskd) и миграция (Завершено)
- [x] Модуль NxEskd переведён в статус опционального capability pack NXKeys:
  - исключена сборка NxEskd в стандартном пути без флага;
  - исключён флаг `-SkipTests` из релизной упаковки.
- [x] В `install-nxkeys.ps1` добавлена поддержка явной установки компонента через параметр `--with nxeskd` / интерактивный пункт меню.
- [x] Стандартизирована целевая файловая раскладка:
  - `%LOCALAPPDATA%\NXKeys\components\nxeskd\<version>\runtime\` (Core + NX runtime без UI);
  - `%LOCALAPPDATA%\NXKeys\components\nxeskd\<version>\configurator\` (WPF Configurator);
  - `%LOCALAPPDATA%\NXKeys\components\nxeskd\<version>\templates\` (шаблоны форматок и таблиц);
  - `%LOCALAPPDATA%\NXKeys\profiles\nxeskd\` (пользовательские профили ЕСКД);
  - `%LOCALAPPDATA%\NXKeys\reports\nxeskd\` (структурированные отчеты выполнения).
- [x] Устранена зависимость от глобальной системной переменной `NX_ESKD_ROOT` — все пути вычисляются относительно манифеста установленного компонента NXKeys.
- [x] Реализована автоматическая очистка устаревших меню `nx_eskd.men`, отдельных панелей ribbon и дублирующихся бинарных файлов.

---

### Этап E8: Прогресс, структурированные отчёты и recovery (Завершено)
- [x] В `ExecutionReport.cs` реализована структурированная запись отчётов в `%LOCALAPPDATA%\NXKeys\reports\nxeskd\drawing-report-<timestamp>-<runId>.json`.
- [x] Реализована автоматическая ротация отчётов с сохранением последних 50 записей (`PruneOldReports`).
- [x] В `MainWindow.Commands.cs` реализовано отображение живого прогресса и кнопка открытия сформированного отчёта.

---

### Этап E7: Укрепление транзакционных границ NX (Завершено)
- [x] В `NxExecutionAdapter.cs` проверено и усилено управление метками отката `SetUndoMark` / `UndoToMark` / `DeleteUndoMark`.
- [x] В случае ошибок выполнения плана транзакция NX автоматически откатывается до исходного состояния детали.
- [x] В `NxEskdCapabilityHandler.cs` обеспечено заполнение `result.RollbackAttempted` и `result.RollbackVerified`.
- [x] Пост-условие отката подтверждает возврат WorkPart в исходное состояние без утечки строителей (Builders).

---

### Этап E6: Единый пайплайн Snapshot, DAG Plan и Preview (Завершено)
- [x] В `DrawingPlan` добавлен метод `ComputeHash()` детерминированного расчёта хэша плана операций DAG.
- [x] В `DrawingEngine.Run()` унифицирован пайплайн для всех 5 режимов: `ModelSnapshot` -> `DrawingPlanner.Build` -> `PlanApplicabilityFilter` -> `DrawingOperationScheduler` -> `DrawingCompletenessAnalyzer` -> `plan.ComputeHash()` -> исполнение/валидация/предпросмотр.
- [x] В `NxEskdCapabilityHandler` и `NxCommandResult` обеспечена передача `PreviewHash` для проверки неизменности плана перед мутацией детали.
- [x] В конфигураторе на шаге `2. Предпросмотр` отображаются все операции DAG и расчитанный хэш плана.

---

### Этап E5: Редизайн Configurator вокруг 4 состояний задачи (Завершено)
- [x] Главное окно `MainWindow.xaml` переведено на 4 последовательных состояния рабочего процесса:
  1. `1. Исходные данные` (параметры документа, структура листов и видов, ТТ, PMI/BOM, параметры раздела).
  2. `2. Предпросмотр` (Dry-run план DAG: операции, цели, типы изменений, зависимости, хэш плана).
  3. `3. Проверка` (валидация профиля, ошибки, предупреждения, ручная проверка).
  4. `4. Результат` (асинхронный статус выполнения, прогресс-бар, журнал, кнопка открытия отчёта).
- [x] Режим редактирования сырого JSON переведён в экспертную вкладку.
- [x] Реализована асинхронная отправка запросов `nxeskd.preview`, `nxeskd.validate`, `nxeskd.generate`, `nxeskd.update` в защищённую очередь NXKeys без блокировки интерфейса.

---

### Этап E4: Неблокирующий запуск Configurator (Завершено)
- [x] В `NX2512_HotkeyStudio/Services/NxCommandBridgeClient.cs` добавлен метод `LaunchNxEskdConfigurator` для неблокирующего запуска Configurator как дочернего desktop-процесса с передачей переменных сессии (`NXKEYS_SESSION_ID`, `NXKEYS_SESSION_SECRET`, `NXKEYS_CONFIG_PATH`, `NXKEYS_BRIDGE_ROOT`).
- [x] Запуск ЕСКД через хоткей `E` не блокирует `ugraf.exe` и главный поток NX.
- [x] В `MainWindow.Profile.cs` Configurator принимает флаги `--profile`, `--workflow`, `--nx-part`.

---

### Этап E3: Подключение NxEskd к защищённому протоколу и очереди (Завершено)
- [x] В `NXKeys.Protocol/NxProtocol.cs` добавлено действие `run_capability` в `NxProtocolActions`.
- [x] В `NxCommandRequest` добавлены поля capability-пакета (`capability_id`, `workflow_id`, `component_id`, `component_version`, `payload_name`, `payload_sha256`, `payload_schema_version`, `expected_part_id`, `expected_model_revision`, `profile_id`, `profile_sha256`) и валидация безопасных путей `payload_name`.
- [x] В `NxCommandResult` расширена модель статуса: фазы (`phase`), прогресс (`percent`), `issue_code`, `recommended_action`, `preview_hash`, `report_name`, `report_sha256`, флаги отката (`rollback_attempted`, `rollback_verified`, `manual_review_required`).
- [x] В `NXKeys.Protocol/NxBridgeSecurity.cs` обновлены канонический payload HMAC и allowlist разрешений для стандартных capability (`nxeskd.open_workflow`, `nxeskd.preview`, `nxeskd.validate`, `nxeskd.inventory`, `nxeskd.generate`, `nxeskd.update`, `nxeskd.cancel`).
- [x] В `NX2512_CommandBridge/Program.cs` реализована диспетчеризация `RunCapability` к `NxEskdCapabilityHandler` по манифесту установленного компонента с сохранением детального результата `NxCommandResult`.
- [x] Создан `NxEskd.NxRuntime/NxEskdCapabilityHandler.cs` для валидации сессии, сверки ожидаемой детали/хэша профиля, вызова `DrawingEngine` и формирования структурированного отчёта.
- [x] Добавлены тесты верификации подписи и защиты от модификации capability-запросов в `NX2512_HotkeyStudio.Tests/Program.cs`.

---

### Этап E2: Удаление 6 command DLL, shims и отдельного меню (Завершено)
- [x] Удалены 6 проектов-прослоек из `nxeskd/src/NxEskd.Commands/` (`CommandCenter`, `Generate`, `Inventory`, `Preview`, `Update`, `Validate`).
- [x] Удален отдельный исполняемый файл `NxEskd.SmokeTests`, smoke-проверки интегрированы в `NxEskd.Core.Tests`.
- [x] Удалены отдельные MenuScript и Ribbon файлы `nxeskd/startup/nx_eskd.men` и `nxeskd/application/rbn_nxeskd.rtb` (кнопка `ЕСКД` генерируется в составе NXKeys).
- [x] `nxeskd/NxEskdDrawingAutomation.sln` сведён ровно к 6 каноническим проектам: `NxEskd.Core`, `NxEskd.NxRuntime`, `NxEskd.Configurator`, `NxEskd.Core.Tests`, `NxEskd.Configurator.Tests`, `NxEskd.PackagePlanner`.
- [x] Из `NxEskd.NxRuntime/CommandHost.cs` удалены `OpenCommandCenter()`, `Process.Start` и двухчасовой `WaitForExit`.
- [x] Все 6 проектов собираются и проходят тесты без предупреждений и ошибок.

---

### Этап E1: Сведение мнемоники и capability model к единому входу (Завершено)
- [x] В `config/nx2512-v8-profile.json` удалены 6 устаревших операций `drafting.eskd_*` (`eskd_control_center`, `eskd_generate`, `eskd_update`, `eskd_preview`, `eskd_validate`, `eskd_inventory`).
- [x] Добавлена единая операция `drafting.eskd` с маршрутом `E` (Modeling, Drafting, Global) и адаптером `capability:nxeskd.open_workflow` ("ЕСКД — подготовить или обновить чертёж").
- [x] Обновлён `config/nx2512-capability-route-lock.json` (439 операций, 131 исполняемая кнопка).
- [x] В `NX2512_HotkeyStudio/Models/ConfigRuntimeV5.cs` поддержан тип адаптера `capability` как исполняемый (не TBD).
- [x] Добавлен регрессионный тест `VerifyEskdSingleRouteAndCapability` в `NX2512_HotkeyStudio.Tests/Program.cs`.
- [x] Все скрипты валидации (`validate-documentation.mjs`, `validate-command-tree.mjs`, `validate-main-command-map.mjs`, `validate-full-command-map.mjs`, `validate-capability-route-lock.mjs`) успешно пройдены.

---

### Этап E0: Baseline, архитектурные guards и CI gates (Завершено)
- [x] Обновлена матрица возможностей в `nxeskd/docs/CAPABILITY_MATRIX.md` с 4 каноническими статусами (`implemented`, `ci_verified`, `nx_verified`, `target_only`), 5 backend-режимами (`Preview`, `Validate`, `Inventory`, `Generate`, `Update`) и таблицей миграции маршрутов.
- [x] Зафиксирован статус верификации в `nxeskd/docs/VERIFICATION_STATUS.md`.
- [x] Реализован расширенный набор тестов архитектурных инвариантов и smoke-проверок в `nxeskd/tests/NxEskd.Core.Tests/RepositoryArchitectureGuardTests.cs`:
  - `NxEskd.Core` не имеет зависимостей от NXOpen, WPF, WinForms;
  - `NxEskd.Configurator` не имеет зависимостей от NXOpen;
  - `NX2512_CommandBridge` не содержит WPF-зависимостей и `WaitForExit`;
  - NX runtime не принимает нетипизированный JSON dispatch;
  - Детерминированность построения DAG-плана, цикл-детектор, раскладка и dry-run на fake adapter.
- [x] Удалены избыточные файлы `RepositoryArchitectureGuardTests.fixed.cs` и `RepositoryArchitectureAdditionalTests.cs`.
- [x] Включён запуск `NxEskd.Core.Tests` и `NxEskd.Configurator.Tests` в `.github/workflows/ci.yml`.
- [x] Все 45 тестов `NxEskd.Core.Tests` и 27 тестов `NxEskd.Configurator.Tests` успешно пройдены.

---

## Итоговая сводка верификации

| Набор проверок | Количество тестов / проверок | Результат |
|:---|:---:|:---:|
| `NxEskd.Core.Tests` | 45 | ✅ 100% Passed (0 failed) |
| `NxEskd.Configurator.Tests` | 27 | ✅ 100% Passed (0 failed) |
| `NX2512_HotkeyStudio.Tests` | 422+ | ✅ 100% Passed (0 failed) |
| `validate-documentation.mjs` | 28 канонических файлов | ✅ 100% Passed |
| `validate-capability-route-lock.mjs` | 439 операций, 131 кнопка | ✅ 100% Passed |
