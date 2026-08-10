# Архитектурные решения NXKeys

ADR фиксируют причины решений, которые сложно восстановить только по коду. Они не заменяют current architecture/runtime docs и не должны выдавать future design за уже реализованный contract.

Текущий runtime baseline: [../RUNTIME_V8.md](../RUNTIME_V8.md).

## Статусы

- `Accepted` — решение действует;
- `Superseded` — заменено более новым ADR;
- `Deprecated` — сохраняется для compatibility, но не рекомендуется;
- `Proposed` — обсуждается и не является current contract.

## Индекс

| ADR | Решение | Статус |
|---|---|---|
| [0001](0001-profile-layers.md) | Legacy: intent catalog → bootstrap → generated K3–K5 → installed compatibility profile | **Superseded by 0003** |
| [0002](0002-file-queue-at-most-once.md) | Local file queue с at-most-once recovery | Accepted |
| [0003](0003-v8-runtime-profile-and-legacy-catalog-separation.md) | `nx2512-v8-profile.json` как default runtime; K1–K5 pipeline как catalog/compatibility layer | **Accepted** |

## Важное уточнение к ADR-0002

ADR-0002 остаётся действующим в части file queue lifecycle и at-most-once recovery. Current IPC schema 4 дополнительно использует authenticated launch session, HMAC, source-process validation, anti-replay state и profile permissions. Эти security controls расширяют trust boundary, не отменяя сам queue/recovery decision.

## Шаблон

```markdown
# ADR-NNNN: Название

- Status: Proposed
- Date: YYYY-MM-DD

## Context

Какая проблема требует решения и какие ограничения подтверждены.

## Decision

Что принято.

## Consequences

Положительные последствия, издержки и ограничения.

## Alternatives considered

Рассмотренные варианты и причины отказа.

## Verification

Какими tests, validators или runtime evidence проверяется решение.
```

## Правила

1. Один ADR описывает одно решение.
2. Используйте точные paths/contracts из кода.
3. Неподтверждённые будущие изменения получают статус `Proposed`.
4. При замене старого решения пометьте его `Superseded` и добавьте ссылку на новое.
5. Изменение profile schema, IPC security boundary, default runtime profile, deployment boundary или execution guarantee обычно требует ADR.
6. Исторический ADR не переписывается как будто новое решение действовало раньше; меняется статус и добавляется replacement ADR.
