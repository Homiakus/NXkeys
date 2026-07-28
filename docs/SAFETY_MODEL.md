# Модель безопасности NXKeys

## Область главного профиля

Главный профиль включает все 885 намерений K3–K5. K1–K2 исключены из стандартного runtime не из-за запрета, а чтобы основной интерфейс соответствовал заданному рабочему охвату и не перегружался низкоприоритетными функциями.

## Запрет выдуманных IDs

Иерархический каталог задаёт функцию и путь, но не гарантирует `BUTTON ID`. Поэтому:

- `existing` — доверенный точный ID;
- `resolved` — надёжно найденный ID;
- `ambiguous` — отключён;
- `unresolved` — отключён.

`ambiguous` и `unresolved` остаются видимыми в отчёте и поисковой карте, но не могут создать IPC-запрос на выполнение.

## Контекстные guards

Перед dispatch проверяются:

- свежесть и confidence контекста;
- активный module/application;
- Work Part и Display Part;
- modal dialog и active command;
- selection count и selected types;
- action и selection filter;
- точный command ID;
- destructive/confirmation policy.

Bridge повторно проверяет ревизию, приложение, выбор и срок действия request.

## Selection-фильтры

`UG_SEL_*` используют `action: set_selection_filter`. Bridge применяет global selection members NXOpen вместо попытки запустить псевдокнопку как обычную команду.

## Подтверждение

`destructive=true` или `confirm_before_execute=true` переводит HFSM в `AwaitingConfirmation`. Только `Enter` создаёт request с `confirmation_accepted=true`.

## Очередь

```text
pending → processing → completed | failed
```

Гарантии:

- атомарная запись и claim;
- уникальный request ID;
- expiration;
- отсутствие автоматического повтора после возможного исполнения;
- `interrupted_unknown` после аварийного завершения NX;
- отдельный result-файл.

## Deployment

NXKeys:

- не редактирует системные файлы Siemens;
- не подменяет глобальный `PATH` и `UGII_USER_DIR`;
- не изменяет бинарный `.mtx` без явной настройки;
- проверяет staging SHA-256;
- ведёт `package-manifest.json`;
- удаляет только ранее управляемые файлы;
- выполняет rollback при ошибке.

## Проверка главного scope

CI отклоняет профиль, если:

- `selected_intents` не равно 885;
- `selected_frequencies` отличается от K3/K4/K5;
- отсутствует хотя бы один K3–K5 `catalog_ref`;
- присутствует K1/K2 reference;
- enabled-команда не имеет ID;
- путь конфликтует с другим путём или alias.

## Граница доказательности

CI подтверждает структуру, код и NXOpen contract stubs. Фактическая чувствительность команды и лицензия проверяются только в целевой Siemens NX. Перед production обязательно изучите `main-profile-resolution.md` и протестируйте destructive-команды на копии данных.
