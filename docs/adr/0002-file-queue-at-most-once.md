# ADR-0002: Локальная файловая очередь с at-most-once recovery

- Status: Accepted
- Date: 2026-07-30

## Context

HotkeyStudio работает как отдельный Windows-процесс, а Command Bridge загружается внутрь Siemens NX. Между ними необходимо передавать команды и контекст без сетевого сервиса и без автоматического повторного выполнения потенциально разрушительной операции после сбоя NX.

Критичное состояние: request уже захвачен Bridge, но NX завершился до записи результата. Повтор request может выполнить операцию второй раз.

## Decision

Использовать локальный file IPC root:

```text
%LOCALAPPDATA%\NXKeys\bridge
```

Очередь имеет состояния:

```text
pending → processing → completed | failed
```

Правила:

1. HotkeyStudio создаёт request с уникальным `request_id` и ограниченным lifetime.
2. Request публикуется в `pending` только после завершения записи.
3. Bridge атомарно перемещает файл в `processing`, получая ownership.
4. Перед исполнением Bridge повторно проверяет schema, expiry, context revision, application, selection и confirmation.
5. После исполнения создаётся `NxCommandResult`.
6. Request архивируется в `completed` или `failed`.
7. Повторный `request_id` не исполняется повторно.
8. Request, найденный в `processing` после recovery, получает `interrupted_unknown` и автоматически не повторяется.

Bridge отдельно публикует `context.json` и `status.json`.

## Consequences

### Положительные

- нет отдельного daemon/server dependency;
- artifacts легко исследовать при диагностике;
- атомарный move задаёт ownership;
- request expiry ограничивает выполнение устаревшего намерения;
- recovery предпочитает неизвестный результат повторному destructive dispatch;
- протокол тестируется без установленной NX.

### Издержки

- file system является частью reliability contract;
- внешние антивирусы, синхронизация и ручное редактирование могут мешать очереди;
- latency выше, чем у in-process call;
- `interrupted_unknown` требует ручной проверки состояния детали;
- local user с доступом к IPC root потенциально может изменять файлы, поэтому Bridge обязан валидировать содержимое.

## Alternatives considered

### Named pipes

Не выбраны в текущей архитектуре: потребовали бы lifecycle/reconnect protocol и усложнили бы сохранение диагностических evidence. Возможны в будущем при сохранении at-most-once semantics.

### TCP/HTTP localhost service

Отклонён для текущего scope: добавляет listener, firewall/security surface и отдельный service lifecycle.

### Автоматический retry processing request

Отклонён: невозможно доказать, что команда не была выполнена перед сбоем.

### Прямая запись результата без архивирования request

Отклонена: ухудшает трассировку и duplicate detection.

## Trust boundaries

- HotkeyStudio формирует намерение пользователя;
- file system переносит request, но не считается доверенным источником корректности;
- Bridge является последней точкой validation перед NX API;
- NX определяет фактическую availability/sensitivity команды;
- operator вручную разрешает `interrupted_unknown`.

## Verification

- protocol invariant tests проверяют schema и round-trip;
- state-machine tests проверяют expiry и confirmation;
- Bridge code восстанавливает interrupted requests;
- health/bridge-status показывают queue counts;
- runtime test в NX проверяет atomic claim и отсутствие duplicate execution.
