# Модель безопасности NXKeys

## Принцип минимального глобального вмешательства

NXKeys устанавливает только 12 прямых системных ускорителей. Все профессиональные операции проходят через контекстный Leader. Это уменьшает конфликты с ролями NX и не делает контекстно-зависимые команды глобальными.

Полная карта из 1169 намерений не означает автоматическое включение 1169 кнопок. Команда активируется только при наличии надёжного точного `BUTTON ID` из каталога конкретной установки.

## Контекстный scope

Команда видима и исполняема только в допустимом модуле, определённом из свежего Bridge context.

Leader не активирует рабочий набор, если:

- `context.json` отсутствует;
- контекст устарел;
- Bridge не имеет `status=running`;
- приложение не сопоставлено с профилем;
- confidence ниже policy;
- активен блокирующий modal dialog.

Скрытая автоматическая смена приложения запрещена. Явный `Tab`/`Shift+Tab` подтверждается новым `application_id`, `module_id` и revision.

## Разрешение `BUTTON ID`

Компилятор полной карты использует базовый профиль, runtime probe и `06_ui_commands_buttons.csv`.

Статусы:

- `existing` — точный ID уже известен;
- `resolved` — найден надёжный кандидат;
- `ambiguous` — кандидаты слишком близки, команда отключена;
- `unresolved` — подходящего кандидата нет, команда отключена.

Инвариант: включённая команда не может иметь пустой или выдуманный `command.id`.

## Guards

До dispatch проверяются:

- protocol schema и request expiry;
- module/application;
- context freshness, revision и confidence;
- modal dialog и active command;
- Work Part / Display Part;
- selection count и selected types;
- `selection_type`;
- destructive/confirmation policy.

Bridge повторяет критические проверки перед фактическим вызовом NX.

## Selection safety

`UG_SEL_*` маршрутизируются как:

```text
action = set_selection_filter
```

Bridge применяет global NXOpen selection filters. Псевдокнопка не запускается как обычная команда меню.

`requires_selection` не означает обязательный preselection для каждой интерактивной команды. Жёсткий минимум задаёт policy. Это позволяет открыть штатный NX dialog и выполнить выбор внутри него без ложной блокировки.

## Подтверждение

`destructive=true` или `confirm_before_execute=true` переводят HFSM в `AwaitingConfirmation`. Только `Enter` создаёт запрос с `confirmation_accepted=true`.

Типовые опасные операции:

- Replace/Remove Component;
- Delete Operation;
- Postprocess;
- Solve/Delete Simulation Object;
- Delete/Remove Route Object;
- Replace Feature Template;
- удаление тел, элементов, граней и импортированных данных.

Флаг определяется базовым профилем, full-map heuristics и policy, но перед production-применением должен быть проверен на целевой роли.

## Надёжная очередь

```text
pending → processing → completed | failed
```

Гарантии:

- атомарная запись запроса;
- атомарный claim;
- уникальный `request_id`;
- ограниченный срок действия;
- проверка expected context;
- отсутствие автоматического повтора после возможного исполнения;
- `interrupted_unknown` после прерывания NX;
- отдельный result JSON.

## Deployment safety

NXKeys не должен:

- изменять системные файлы Siemens;
- записывать конфигурацию во все найденные профили;
- менять глобальный `PATH`;
- подменять `UGII_USER_DIR`;
- редактировать бинарный `.mtx`;
- удалять файл, отсутствующий в предыдущем package manifest;
- выполнять commit до проверки staging SHA-256;
- оставлять частично установленный пакет без rollback.

CommandBridge и MenuScript размещаются только внутри управляемого package layout. Допустимые копии определяет `package-manifest.json`; наличие artifact в `custom/application` или управляемом `custom/startup` не считается ошибкой само по себе. Ошибка — неконтролируемая ручная копия вне manifest или несколько конфликтующих версий.

## Конфигурационные инварианты

- source profile `schema_version` — 3 или 4;
- runtime model — schema 5;
- IPC — schema 3;
- ровно 12 базовых прямых сочетаний;
- 14 активных модулей базового профиля;
- 1169 намерений и 32 раздела полной карты;
- `K1–K5` для каждой записи полного каталога;
- уникальные внутренние module prefixes;
- prefix-free canonical paths и aliases внутри модуля;
- точный `BUTTON ID` каждой включённой команды;
- производные Leader-последовательности строятся из modules и не являются отдельным источником истины.

## Production checklist

1. Выполнить `validate-full-command-map.mjs`.
2. Скомпилировать профиль из каталога конкретной NX.
3. Просмотреть `full-command-resolution.md`.
4. Выполнить deployment dry-run.
5. Проверить package manifest и backup.
6. Тестировать сначала `existing` и `resolved` безопасные команды.
7. Проверить selection filters.
8. Проверить destructive-команды на копии данных.
9. Зафиксировать runtime probe для целевой роли и лицензии.

## Ограничения

CI проверяет структуру, контракты и тестовые NXOpen stubs. Он не подтверждает лицензию, чувствительность каждой кнопки, корректность корпоративного постпроцессора или поведение каждой UI-команды внутри конкретной NX.
