# Политика безопасности NXKeys

NXKeys разрабатывается для Siemens NX / Designcenter NX 2512, Windows x64, .NET 8. Current runtime использует profile schema **8** и authenticated local file IPC schema **4**.

Подробная threat/control model: [docs/SAFETY_MODEL.md](docs/SAFETY_MODEL.md).

## Что считается security issue

В частности:

- выполнение другой команды вместо разрешённой operation;
- обход destructive confirmation;
- blind retry после `interrupted_unknown`;
- обход HMAC/session/source-process/anti-replay checks;
- возможность расширить profile permissions через forged request;
- несовпадение runtime и security canonicalization IDs;
- command injection через config/path/launcher/MenuScript;
- запись/удаление вне managed ownership boundaries;
- небезопасная обработка `UGII_CUSTOM_DIRECTORY_FILE`;
- подмена profile/manifest/Bridge binary/request/result;
- утечка session secret, profile-sensitive data или production information;
- numeric/keyboard hook, позволяющий выполнять commands в text-like input;
- небезопасное обновление загруженной Bridge DLL.

## Current IPC security boundary

Schema 4 request использует:

```text
session_id
client_instance_id
nonce
sequence_number
profile_digest
payload_hmac
```

Managed launcher создаёт ephemeral session capability. Queue file сам по себе не является authority.

Security issue — если обычный same-user process может обойти эту boundary простым созданием/изменением queue JSON без доступа к trusted process/session capability.

Модель не обещает защиту от process injection, чтения памяти с эквивалентными/более высокими правами или компрометации самого trusted executable.

## Как сообщить

Не публикуйте эксплуатационные детали незакрытой уязвимости в открытом issue.

Используйте GitHub private vulnerability reporting, если он включён. Если недоступен — свяжитесь с владельцем репозитория через подтверждённый GitHub-профиль и передайте минимальную информацию для установления безопасного канала.

## Что включить

- commit SHA;
- exact Windows/NX build;
- компонент;
- безопасные шаги воспроизведения;
- ожидаемое/фактическое поведение;
- impact;
- обезличенные request/result/context/log fragments;
- был ли managed launcher использован;
- был ли action destructive;
- требуется ли специальная NX role/license.

## Не отправляйте

- Siemens license files;
- proprietary NXOpen binaries;
- production parts/drawings;
- API tokens/passwords;
- session secrets/HMAC keys;
- полные corporate `.mtx`/`custom_dirs.dat`;
- неочищенные logs с персональными/корпоративными путями.

## Безопасное воспроизведение

- используйте тестовую копию детали;
- не проверяйте destructive commands на production data;
- запускайте NX через managed launcher, если исследуется штатный authenticated path;
- не отключайте HMAC/profile permission/confirmation guards ради демонстрации;
- при duplicate/unknown effect сохраните queue/context/log и **не повторяйте** command;
- после Bridge update полностью перезапускайте NX;
- для keyboard/Selection Intent issues фиксируйте active collector/focused control.

## Interactive command caveat

Для некоторых NX UI commands фактическое открытие dialog/collector может не совпадать с простым `InvokeMenuButtonAction` return value. Security/safety analysis не должен автоматически retry command только потому, что API вернул `false`: действие уже могло иметь UI effect.

## Sheet Metal / ID canonicalization

Current canonical namespace:

```text
UG_APP_SBSM
UG_SBSM_*
```

Legacy mapping допускается только в контролируемом normalization layer. Runtime permission и Bridge execution должны canonicalize одинаково.

## Границы проекта

NXKeys не является:

- sandbox Siemens NX;
- заменой NX authorization/licensing;
- EDR/OS security product;
- backup system;
- доказательством инженерной корректности command effect.

Contract tests против NXOpen stubs не подтверждают live security semantics конкретной NX workstation.

## Secrets и чувствительные данные

В version control запрещены:

- credentials/tokens;
- session secrets;
- proprietary Siemens binaries/license materials;
- персональные данные;
- реальные production files;
- закрытые network/infrastructure paths.

Используйте synthetic examples и placeholder paths.
