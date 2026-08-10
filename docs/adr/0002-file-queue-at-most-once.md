# ADR-0002: Локальная файловая очередь с at-most-once recovery

- Status: Accepted
- Date: 2026-07-30
- Security extension: IPC schema 4, 2026-08-10

## Context

HotkeyStudio работает как отдельный Windows process, а Command Bridge загружается внутрь Siemens NX. Между ними нужен локальный transport без сетевого service и без автоматического повторного выполнения потенциально destructive operation после сбоя NX.

Критичное состояние: request уже захвачен Bridge, но NX завершился до записи результата. Повтор request может выполнить operation второй раз.

Позже к reliability problem добавилась security problem: сам факт возможности записать JSON в `%LOCALAPPDATA%\NXKeys\bridge` не должен давать process право выполнять произвольный разрешённый NX command.

## Decision

Сохранить локальный file IPC root:

```text
%LOCALAPPDATA%\NXKeys\bridge
```

Queue lifecycle:

```text
pending → processing → completed | failed
```

At-most-once rules:

1. HotkeyStudio создаёт request с unique `request_id` и bounded lifetime.
2. Request публикуется в `pending` только после завершения записи.
3. Bridge атомарно перемещает file в `processing`, получая ownership.
4. После выполнения создаётся result и request архивируется в `completed`/`failed`.
5. Replayed/duplicate request не исполняется повторно.
6. Request, найденный в `processing` после recovery, получает `interrupted_unknown` и автоматически не retry-ится.

Bridge отдельно публикует `context.json` и `status.json`.

## Security extension — schema 4

Current queue остаётся file-based, но queue file **не является authority**.

Managed launcher создаёт ephemeral shared session capability. Request schema 4 содержит:

```text
session_id
client_instance_id
nonce
sequence_number
profile_digest
payload_hmac
```

Перед NX dispatch Bridge проверяет:

- exact protocol schema/expiry;
- HMAC/session;
- source process;
- nonce/sequence anti-replay;
- profile digest и permission;
- expected application/revision/selection fingerprint;
- confirmation policy.

Эта security extension не меняет queue ownership/recovery decision: authenticated request всё равно проходит `pending → processing → completed|failed` и не получает blind retry после unknown result.

## Consequences

### Положительные

- нет отдельного localhost daemon/listener;
- diagnostic artifacts остаются исследуемыми;
- atomic move задаёт ownership;
- expiry ограничивает stale intent;
- HMAC/session/permission отделяют transport от authority;
- recovery предпочитает unknown result повторному destructive dispatch;
- protocol/recovery тестируются без установленной NX.

### Издержки

- filesystem остаётся частью reliability contract;
- antivirus/sync/manual edits могут влиять на transport;
- latency выше in-process call;
- `interrupted_unknown` требует ручной проверки;
- authenticated model не защищает от compromise/injection в trusted process с доступом к session secret;
- managed launch становится частью штатного security lifecycle.

## Alternatives considered

### Named pipes

Не выбраны: потребовали бы отдельного lifecycle/reconnect protocol. Возможны в будущем при сохранении authenticated admission и at-most-once semantics.

### TCP/HTTP localhost service

Не выбран: добавляет listener/firewall/service surface без необходимости для current scope.

### Автоматический retry processing request

Отклонён: невозможно доказать, что NX side effect не произошёл до crash.

### Доверять ACL/наличию файла

Отклонено после security hardening: same-user file write не является достаточной authorization boundary.

## Trust boundaries

- HotkeyStudio формирует user intent и подписывает request;
- file system переносит artifacts, но не считается authority;
- session/HMAC/profile permission определяют admission;
- Bridge — последняя NXKeys validation boundary;
- NX определяет фактическую availability/sensitivity/effect;
- operator вручную разрешает `interrupted_unknown`.

## Verification

- protocol/security tests проверяют schema 4, signing и permissions;
- state-machine tests проверяют expiry/confirmation/recovery contracts;
- Bridge inbox ограничивает payload/queue/work-per-poll;
- health/bridge-status показывают queue/context/security state;
- live NX integration проверяет claim/dispatch и отсутствие duplicate execution.

Transport details: [../api.md](../api.md). Safety model: [../SAFETY_MODEL.md](../SAFETY_MODEL.md).
