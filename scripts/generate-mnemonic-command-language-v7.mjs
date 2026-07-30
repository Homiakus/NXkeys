import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const argv = process.argv.slice(2);
const valueOf = (name, fallback = '') => {
  const index = argv.indexOf(name);
  return index >= 0 && index + 1 < argv.length ? argv[index + 1] : fallback;
};
const profilePath = path.resolve(valueOf('--profile', 'config/nx2512-pro-main.generated.json'));
const outputPath = path.resolve(valueOf('--out', 'docs/MNEMONIC_COMMAND_LANGUAGE.md'));
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const profile = readJson(profilePath);
const policyVersion = Number(profile.full_command_catalog?.sequence_policy_version ?? 0);
if (policyVersion !== 7) throw new Error(`Expected sequence policy v7, got v${policyVersion || 'unknown'} in ${profilePath}.`);
if (Number(profile.schema_version) !== 6) throw new Error(`Expected profile schema 6, got ${profile.schema_version}.`);

const moduleNames = {
  modeling: 'Моделирование',
  sketch: 'Эскиз',
  assembly: 'Сборка',
  drafting: 'Чертёж',
  pmi: 'PMI и аннотации модели',
  surface: 'Поверхностное моделирование',
  sheet_metal: 'Листовой металл',
  manufacturing: 'Обработка / CAM',
  simulation: 'Расчёты / Simulation',
  routing: 'Трассировка',
  mold: 'Проектирование пресс-форм',
  reuse: 'Библиотека повторного использования',
  inspect_view: 'Просмотр и анализ',
  selection_object: 'Выбор объектов'
};
const selectionNames = {
  body: ['Тела', 'Ограничивает выбор телами.'],
  face: ['Грани', 'Ограничивает выбор гранями.'],
  edge: ['Рёбра', 'Ограничивает выбор рёбрами.'],
  feature: ['Элементы построения', 'Ограничивает выбор элементами построения.'],
  component: ['Компоненты сборки', 'Ограничивает выбор компонентами сборки.'],
  curve: ['Кривые', 'Ограничивает выбор кривыми.'],
  datum: ['Базовые объекты', 'Ограничивает выбор плоскостями, осями и другими базовыми объектами.'],
  reset: ['Сброс фильтра', 'Сбрасывает активный фильтр выбора.'],
  all: ['Выбрать всё', 'Выбирает все доступные объекты в текущем контексте NX.'],
  none: ['Снять выбор', 'Снимает текущий выбор.']
};
const selectionTypeNames = {
  body: 'тела', face: 'грани', edge: 'рёбра', feature: 'элементы построения',
  component: 'компоненты', curve: 'кривые', datum: 'базовые объекты',
  sketch: 'объекты эскиза', view: 'виды', operation: 'операции', tool: 'инструменты',
  all: 'объекты текущего контекста'
};
const actionRoots = [
  ['C', 'Create', 'создать или добавить объект'],
  ['E', 'Edit', 'изменить существующий объект'],
  ['T', 'Transform', 'переместить, отразить или размножить'],
  ['X', 'Remove', 'удалить, убрать или подавить'],
  ['P', 'Process', 'рассчитать, сгенерировать или выполнить процесс'],
  ['I', 'Inspect', 'измерить, проверить или проанализировать'],
  ['V', 'View', 'изменить отображение или ориентацию'],
  ['S', 'Select', 'управлять выбором и фильтрами'],
  ['A', 'Annotate', 'создать размер, PMI, символ или примечание'],
  ['M', 'Manage', 'управлять слоями, материалами, библиотеками и навигаторами'],
  ['F', 'File', 'выполнить файловую операцию'],
  ['G', 'Go', 'перейти в другое приложение NX'],
  ['U', 'Utilities', 'открыть служебную функцию или настройку'],
  ['H', 'Help', 'открыть справку, поиск или диагностику']
];
const escapeCell = value => String(value ?? '').replaceAll('|', '\\|').replaceAll('\n', '<br>');
const keyPath = value => (Array.isArray(value) ? value : []).map(token => String(token).trim().toUpperCase()).filter(Boolean);
const pathText = value => keyPath(value).join(' → ');
const pathCode = value => `\`${pathText(value)}\``;
const containsRussian = value => /[А-Яа-яЁё]/.test(String(value ?? ''));

function commandRussianName(command) {
  const english = String(command.command?.name ?? '').trim();
  for (const alias of command.search_aliases ?? []) {
    const text = String(alias ?? '').trim();
    if (text && text !== english && containsRussian(text) && !text.includes(' / ')) return text;
  }
  return english || String(command.command?.id ?? 'Без названия');
}

function sourceArea(command) {
  const english = String(command.command?.name ?? '').trim();
  const russianName = commandRussianName(command);
  const candidates = (command.search_aliases ?? [])
    .map(value => String(value ?? '').trim())
    .filter(value => value && value !== english && value !== russianName && containsRussian(value));
  return candidates.at(-1) ?? '';
}

function aliasesText(command) {
  const aliases = (command.aliases ?? []).map(pathCode).filter(Boolean);
  return aliases.length ? aliases.join(', ') : '—';
}

function conditionsText(command) {
  const conditions = [];
  if (command.requires_selection) {
    const type = selectionTypeNames[command.selection_type] ?? command.selection_type ?? 'подходящие объекты';
    conditions.push(`нужен выбор: ${type}`);
  }
  if (command.destructive) conditions.push('может изменить или удалить данные');
  if (command.confirm_before_execute) conditions.push('требует подтверждения NXKeys');
  return conditions.length ? conditions.join('; ') : 'без обязательного предварительного выбора';
}

function purposeText(command) {
  if (command.action === 'set_selection_filter') {
    return selectionNames[command.selection_type]?.[1] ?? 'Изменяет режим выбора NX.';
  }
  if (command.action === 'switch_module') {
    const target = moduleNames[command.target_module_id] ?? command.target_module_id ?? 'другое приложение';
    return `Переключает активное приложение Siemens NX на «${target}».`;
  }
  const name = commandRussianName(command);
  const area = sourceArea(command);
  let text = `Намерение профиля — вызвать «${name}»; Bridge передаёт в Siemens NX идентификатор ${command.command?.id ?? 'без ID'}.`;
  if (area) text += ` Область исходного каталога: ${area}.`;
  if (command.requires_selection) {
    const type = selectionTypeNames[command.selection_type] ?? command.selection_type ?? 'подходящие объекты';
    text += ` Команда применяется к выбранным объектам (${type}).`;
  }
  return text;
}

function rowKind(command) {
  if (command.action === 'set_selection_filter') return 'selection';
  if (command.action === 'switch_module') return 'switch';
  return 'command';
}

const modules = (profile.modules ?? []).filter(module => module && module.enabled !== false);
const rows = [];
for (const module of modules) {
  for (const set of module.command_sets ?? []) {
    for (const command of set.commands ?? []) {
      if (command && command.enabled !== false) rows.push({ module, set, command });
    }
  }
}
const uniqueIds = new Set(rows.map(row => row.command.command?.id).filter(Boolean));
const uniqueCatalogRefs = new Set(rows.flatMap(row => row.command.catalog_refs ?? []));
const supportRows = rows.filter(row => row.command.frequency === 'support').length;
const catalogRows = rows.filter(row => (row.command.catalog_refs ?? []).length > 0).length;

const directShortcuts = (profile.keyboard ?? []).filter(binding => binding && binding.enabled !== false);
const lines = [];
lines.push('# NXKeys Mnemonic Command Language — v7', '');
lines.push('> Этот документ описывает только действующий контракт **sequence policy v7**. Предыдущие версии и отклонённые варианты из него удалены. Полный каталог ниже формируется из включённых строк актуального runtime-профиля schema 6.', '');
lines.push('## Что именно перечислено', '');
lines.push(`- выбранный source scope: **${profile.full_command_catalog?.selected_intents ?? '—'}** намерений K3–K5 из ${profile.full_command_catalog?.source_intents ?? '—'};`);
lines.push(`- реально вызываемых runtime-строк: **${rows.length}**;`);
lines.push(`- уникальных \`BUTTON ID\`: **${uniqueIds.size}**;`);
lines.push(`- строк, связанных с разрешёнными catalog intents: **${catalogRows}** (${uniqueCatalogRefs.size} уникальных intent ID);`);
lines.push(`- служебных строк выбора и переходов: **${supportRows}**;`);
lines.push(`- активных контекстных модулей: **${modules.length}**.`, '');
lines.push('В список включены только команды с `enabled: true`. Неоднозначные и неразрешённые команды остаются в профиле отключёнными и здесь не показаны, потому что их нельзя безопасно вызвать.', '');
lines.push('Русское название описывает **намерение source catalog**. Фактический вызов определяется колонкой `BUTTON ID`: именно этот идентификатор Bridge передаёт Siemens NX. Даже статус `resolved` требует проверки на целевой установке NX 2512, если название намерения и реальное поведение ID расходятся.', '');
lines.push('Поля `workflow_controls.accept_ok/apply/cancel/back_previous_step` не входят в каталог вызываемых команд: в текущем профиле у них нет подтверждённых исполняемых ID.', '');
lines.push('## Как вводится команда', '');
lines.push('```text', 'CapsLock → действие → объект/область → команда → вариант', '```', '');
lines.push('Внутренний префикс активного модуля добавляет runtime. Пользователь его не вводит. Например, в Modeling последовательность `CapsLock → C → E` вызывает Extrude, а в другом модуле тот же путь может означать другую контекстную команду.', '');
lines.push('### Управление Leader HUD', '');
lines.push('| Клавиша | Действие |', '|---|---|');
lines.push('| `CapsLock` | открыть Leader для автоматически определённого активного модуля; повторное нажатие закрывает его |');
lines.push('| двойной `CapsLock` | открыть закреплённый sticky-режим, если он разрешён настройкой |');
lines.push('| буквы и цифры | вводить токены мнемонического пути |');
lines.push('| `Space` | перейти к поиску команд текущего модуля |');
lines.push('| `Enter` | подтвердить путь или запустить первый результат поиска |');
lines.push('| `Backspace` | удалить последний токен; на корневом уровне закрыть Leader |');
lines.push('| `Esc` | отменить ввод и закрыть Leader |');
lines.push('| `Tab` / `Shift+Tab` | перейти к следующему / предыдущему доступному модулю |', '');
lines.push('## Корни действий v7', '');
lines.push('| Токен | Английская мнемоника | Значение |', '|---|---|---|');
for (const [token, english, meaning] of actionRoots) lines.push(`| \`${token}\` | ${english} | ${meaning} |`);
lines.push('', '`S*` зарезервирован для универсальных действий выбора, `G*` — для переходов между приложениями NX. Эти пути резервируются до размещения обычных команд.', '');
lines.push('## Приоритет длины пути', '');
lines.push('| Частота | Максимальная длина | Назначение |', '|---|---:|---|');
lines.push('| `K5` | 2 | наиболее частые команды |');
lines.push('| `K4` | 3 | часто используемые команды |');
lines.push('| `K3` | 4 | рабочий основной каталог |');
lines.push('| `K2` / `K1` | 5 | остаются в source catalog, но не входят в основной runtime |');
lines.push('| `support` | 2 | выбор и переходы между модулями |', '');
lines.push('## Прямые системные сочетания', '');
lines.push('Эти сочетания работают вне мнемонической последовательности и входят в подтверждённую базовую политику.', '');
lines.push('| Сочетание | Команда | Пояснение | BUTTON ID |', '|---|---|---|---|');
for (const binding of directShortcuts) {
  lines.push(`| \`${escapeCell(binding.shortcut)}\` | ${escapeCell(binding.command?.name)} | ${escapeCell(binding.notes)} | \`${escapeCell(binding.command?.id)}\` |`);
}
lines.push('', '## Универсальные пути выбора', '');
lines.push('| Путь | Действие | Пояснение |', '|---|---|---|');
const selectionOrder = ['body', 'face', 'edge', 'feature', 'component', 'curve', 'datum', 'reset', 'all', 'none'];
for (const type of selectionOrder) {
  const row = rows.find(item => item.command.action === 'set_selection_filter' && item.command.selection_type === type);
  if (!row) continue;
  const [name, purpose] = selectionNames[type] ?? [type, 'Изменяет выбор.'];
  lines.push(`| ${pathCode(row.command.path)} | ${name} | ${purpose} |`);
}
lines.push('', '## Переходы между приложениями NX', '');
lines.push('| Путь | Приложение | Пояснение |', '|---|---|---|');
const switchRows = [];
for (const row of rows) {
  if (row.command.action !== 'switch_module') continue;
  const key = `${pathText(row.command.path)}|${row.command.target_module_id}`;
  if (!switchRows.some(item => item.key === key)) switchRows.push({ key, row });
}
switchRows.sort((a, b) => pathText(a.row.command.path).localeCompare(pathText(b.row.command.path)));
for (const { row } of switchRows) {
  const target = moduleNames[row.command.target_module_id] ?? row.command.target_module_id;
  lines.push(`| ${pathCode(row.command.path)} | ${escapeCell(target)} | Переключает активное приложение NX на «${escapeCell(target)}». |`);
}
lines.push('', 'Переходы не добавляются в контекст `sketch` и `selection_object`. Модуль также не получает переход на самого себя.', '');
lines.push('## Полный список вызываемых команд по модулям', '');
lines.push('Путь всегда вводится после `CapsLock`. Одинаковая последовательность может вызывать разные команды в разных модулях: активный модуль является частью маршрута, хотя его префикс скрыт от пользователя.', '');

for (const module of modules) {
  const moduleRows = rows.filter(row => row.module === module);
  const regular = moduleRows.filter(row => rowKind(row.command) === 'command');
  const selection = moduleRows.filter(row => rowKind(row.command) === 'selection');
  const switches = moduleRows.filter(row => rowKind(row.command) === 'switch');
  const displayName = moduleNames[module.id] ?? module.label ?? module.id;
  lines.push(`<details>`, `<summary><strong>${escapeCell(displayName)} — ${moduleRows.length} вызываемых строк</strong></summary>`, '');
  if (regular.length) {
    lines.push('### Команды NX', '');
    lines.push('| Путь | Алиасы | Русское намерение | Что фактически вызывается | BUTTON ID | Статус | Частота | Условия |', '|---|---|---|---|---|---|---|---|');
    regular.sort((a, b) => pathText(a.command.path).localeCompare(pathText(b.command.path)) || String(a.command.command?.id).localeCompare(String(b.command.command?.id)));
    for (const { command } of regular) {
      lines.push(`| ${pathCode(command.path)} | ${aliasesText(command)} | ${escapeCell(commandRussianName(command))} | ${escapeCell(purposeText(command))} | \`${escapeCell(command.command?.id)}\` | \`${escapeCell(command.resolution_status ?? 'existing')}\` | \`${escapeCell(command.frequency)}\` | ${escapeCell(conditionsText(command))} |`);
    }
    lines.push('');
  }
  if (selection.length) {
    lines.push('### Выбор и фильтры', '');
    lines.push('| Путь | Действие | Пояснение | BUTTON ID |', '|---|---|---|---|');
    selection.sort((a, b) => pathText(a.command.path).localeCompare(pathText(b.command.path)));
    for (const { command } of selection) {
      const [name] = selectionNames[command.selection_type] ?? [commandRussianName(command)];
      lines.push(`| ${pathCode(command.path)} | ${escapeCell(name)} | ${escapeCell(purposeText(command))} | \`${escapeCell(command.command?.id)}\` |`);
    }
    lines.push('');
  }
  if (switches.length) {
    lines.push('### Переходы в другие приложения', '');
    lines.push('| Путь | Целевой модуль | BUTTON ID |', '|---|---|---|');
    switches.sort((a, b) => pathText(a.command.path).localeCompare(pathText(b.command.path)));
    for (const { command } of switches) {
      const target = moduleNames[command.target_module_id] ?? command.target_module_id;
      lines.push(`| ${pathCode(command.path)} | ${escapeCell(target)} | \`${escapeCell(command.command?.id)}\` |`);
    }
    lines.push('');
  }
  lines.push('</details>', '');
}

lines.push('## Правила безопасности', '');
lines.push('- команда исполняется только при `enabled: true` и наличии точного `BUTTON ID`;');
lines.push('- `ambiguous` и `unresolved` строки не вызываются;');
lines.push('- команды с `requires_selection` проверяют необходимый контекст выбора;');
lines.push('- потенциально разрушительные команды требуют подтверждения согласно policy;');
lines.push('- переход в модуль завершается только после подтверждения нового context revision;');
lines.push('- действия выбора маршрутизируются отдельно от обычного вызова `BUTTON ID`;');
lines.push('- модальное окно NX может заблокировать обычный dispatch.', '');
lines.push('## Обновление документа', '');
lines.push('Документ генерируется скриптом из скомпилированного main-профиля v7:', '');
lines.push('```powershell');
lines.push('node .\\scripts\\compile-main-command-map.mjs `');
lines.push('  --out .\\config\\nx2512-pro-main.generated.json `');
lines.push('  --report .\\docs\\generated\\main-profile-resolution.md');
lines.push('');
lines.push('node .\\scripts\\generate-mnemonic-command-language-v7.mjs `');
lines.push('  --profile .\\config\\nx2512-pro-main.generated.json `');
lines.push('  --out .\\docs\\MNEMONIC_COMMAND_LANGUAGE.md');
lines.push('```', '');
lines.push('После изменения sequence policy, curated mappings, source catalog или profile compiler документ должен быть пересоздан и проверен вместе с runtime-профилем.', '');

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${lines.join('\n')}\n`, 'utf8');
console.log(`[mnemonic-v7-doc] ${rows.length} callable rows, ${uniqueIds.size} unique BUTTON IDs -> ${outputPath}`);
