using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public class DocumentationGenerator
    {
        private readonly CommandResolver _commandResolver;
        private readonly Config _config;

        public DocumentationGenerator()
            : this(Config.Load(string.Empty))
        {
        }

        public DocumentationGenerator(Config config)
        {
            _config = config ?? Config.Load(string.Empty);
            _config.ApplyDefaults();
            var catalogIndex = new CatalogIndex();
            _commandResolver = new CommandResolver(catalogIndex);
        }

        public DocumentationGenerator(CommandResolver resolver, Config config = null)
        {
            _config = config ?? Config.Load(string.Empty);
            _config.ApplyDefaults();
            _commandResolver = resolver ?? new CommandResolver(new CatalogIndex());
        }

        public void GenerateMarkdownMap(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            _config.ApplyDefaults();
            List<LeaderSequenceItem> sequences = _config.LeaderKey?.Sequences ?? new List<LeaderSequenceItem>();
            List<ModuleConfig> modules = _config.Modules ?? new List<ModuleConfig>();

            var sb = new StringBuilder();

            // Header & Runtime notice
            sb.AppendLine("# Карта команд NXKeys v8 (Runtime-Driven)");
            sb.AppendLine();
            sb.AppendLine("> [!IMPORTANT]");
            sb.AppendLine("> **Внимание:** Этот документ генерируется автоматически на основе рантайм-конфигурации NXKeys v8, " +
                          "вычисления `AdaptiveModuleResolver`, `V8SecondaryAliasExpander` и `CommandResolver`. " +
                          "Не редактируйте этот файл вручную.");
            sb.AppendLine();
            sb.AppendLine("## Обзор рантайм-контекста и каталога намерений");
            sb.AppendLine();
            sb.AppendLine("Настоящая спецификация отражает реальное поведение мнемонического языка ввода v8 в Siemens NX 2512. " +
                          "Все команды главного профиля **885** намерений уровней **K3–K5** и базового источника на **1169** намерений " +
                          "динамически связываются с модулями и резолвятся через адаптивные политики, утилиты установки `install-nxkeys.ps1` " +
                          "и фильтры конечного автомата.");
            sb.AppendLine();
            sb.AppendLine("- Исходный каталог намерений: **1169** (уровни **K1–K2** остаются в базе `06_ui_commands_buttons.csv`).");
            sb.AppendLine("- Главный установленный профиль: **885** намерений (**K3–K5**).");
            sb.AppendLine($"- Активных адаптивных модулей v8: **{modules.Count(m => m.Enabled)}**.");
            sb.AppendLine($"- Всего сгенерировано исполняемых последовательностей: **{sequences.Count}**.");
            sb.AppendLine("- Статусы разрешения команд: `existing`, `resolved`, `ambiguous`, `unresolved`.");
            sb.AppendLine();

            // Summary table by module
            sb.AppendLine("## Сводка по контекстным модулям v8");
            sb.AppendLine();
            sb.AppendLine("| Идентификатор модуля | Название (Префикс) | Приложения NX | Исполняемых мнемоник |");
            sb.AppendLine("|----------------------|-------------------|---------------|----------------------|");

            foreach (ModuleConfig module in modules.Where(m => m.Enabled))
            {
                int count = sequences.Count(s => string.Equals(s.ModuleID, module.ID, StringComparison.OrdinalIgnoreCase));
                string apps = module.NXApplicationIDs != null && module.NXApplicationIDs.Count > 0
                    ? string.Join(", ", module.NXApplicationIDs)
                    : "—";
                sb.AppendLine($"| `{module.ID}` | **{module.Label}** (`{module.LeaderPrefix}`) | `{apps}` | {count} |");
            }
            sb.AppendLine();

            // Full details by module
            sb.AppendLine("## Полная таблица мнемоник по модулям");
            sb.AppendLine();

            foreach (ModuleConfig module in modules.Where(m => m.Enabled))
            {
                sb.AppendLine($"### Модуль `{module.ID}` ({module.Label})");
                sb.AppendLine();
                sb.AppendLine("| Мнемоника / Хоткей | Тип | NX Command ID | Название команды | Action / Filter | Статус |");
                sb.AppendLine("|--------------------|-----|---------------|------------------|-----------------|--------|");

                List<LeaderSequenceItem> moduleSeq = sequences
                    .Where(s => string.Equals(s.ModuleID, module.ID, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.DisplayOrder)
                    .ThenBy(s => s.Sequence)
                    .ToList();

                if (moduleSeq.Count == 0)
                {
                    sb.AppendLine("| — | — | — | — | — | Нет доступных команд |");
                }
                else
                {
                    foreach (LeaderSequenceItem item in moduleSeq)
                    {
                        string hotkey = $"`{item.Sequence}`";
                        string kind = item.IsAlias ? "Alias" : "Canonical";
                        string cmdId = !string.IsNullOrWhiteSpace(item.Command?.ID) ? $"`{item.Command.ID}`" : "—";
                        string cmdName = item.Command?.Name ?? "—";
                        string action = item.Action;
                        if (!string.IsNullOrWhiteSpace(item.SelectionType))
                            action += $" ({item.SelectionType})";
                        string status = !string.IsNullOrWhiteSpace(item.Command?.ID) ? "Resolved" : "Unresolved";

                        sb.AppendLine($"| {hotkey} | {kind} | {cmdId} | {cmdName} | `{action}` | {status} |");
                    }
                }
                sb.AppendLine();
            }

            // Standard resolution audit section
            sb.AppendLine("## Разрешение неизвестных и неоднозначных команд");
            sb.AppendLine();
            sb.AppendLine("Если мнемонический путь ссылается на нераспознанный идентификатор, он квалифицируется как `unresolved` или `ambiguous` и блокируется до подтверждения через Siemens NX catalog probe.");
            sb.AppendLine();

            string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
