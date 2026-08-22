using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NXKeys.Protocol;
using NXOpen;
using NXOpen.MenuBar;

namespace NX2512_CommandBridge
{
    /// <summary>
    /// Модуль A (in-NX): исполнение проверенного запроса внутри NX — кнопки/MenuScript,
    /// Selection Intent/глобальные фильтры, switch модуля, capability-адаптер (nxeskd).
    /// Не строит контекст (B) и не выбирает команду (C); получает допущенный запрос.
    /// </summary>
    public sealed class NxMenuCommandExecutor
    {
        private readonly UI ui;
        private readonly Action<string> log;
        private readonly string processingDirectory;

        public NxMenuCommandExecutor(UI ui, Action<string> log, string processingDirectory)
        {
            this.ui = ui;
            this.log = log ?? (_ => { });
            this.processingDirectory = processingDirectory ?? string.Empty;
        }

        public void ExecuteNxCommand(NxCommandRequest request)
        {
            string commandId = request.CommandId.Trim();
            log("Executing direct NX command: " + request.Sequence + " -> " + commandId + " (" + request.CommandName + ")");

            if (!string.IsNullOrWhiteSpace(request.SelectionFilter))
                ApplyGlobalSelectionFilter(request.SelectionFilter);

            MenuButton button = RequireRunnableButton(commandId);

            bool invoked = ui.DialogTester.InvokeMenuButtonAction(button);
            if (!invoked)
                throw new InvalidOperationException("NX did not accept InvokeMenuButtonAction for: " + commandId);
            log("Executed direct NX command: " + commandId);
        }

        public string ProbeNxCommand(string commandId)
        {
            string id = (commandId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id)) return "missing id";
            try
            {
                MenuButton button = ui.MenuBarManager.GetButtonFromName(id);
                if (button == null) return "missing: " + id;
                return "availability=" + button.ButtonAvailability + "; sensitivity=" + button.ButtonSensitivity;
            }
            catch (Exception ex)
            {
                return "missing: " + id + "; " + ex.Message;
            }
        }

        public void SwitchModule(NxCommandRequest request)
        {
            string applicationId = string.IsNullOrWhiteSpace(request.TargetApplicationId)
                ? request.CommandId
                : request.TargetApplicationId;
            if (string.IsNullOrWhiteSpace(applicationId))
                throw new InvalidOperationException("Missing target application id.");

            log("Switching NX application: " + request.ModuleId + " -> " + applicationId);
            var switchMethod = ui.MenuBarManager.GetType().GetMethod("ApplicationSwitchRequest", new[] { typeof(string) });
            if (switchMethod != null)
            {
                switchMethod.Invoke(ui.MenuBarManager, new object[] { applicationId.Trim() });
            }
            else
            {
                ExecuteNxCommand(new NxCommandRequest
                {
                    SchemaVersion = NxProtocolConstants.SchemaVersion,
                    RequestId = request.RequestId,
                    CommandId = applicationId.Trim(),
                    CommandName = request.CommandName,
                    Sequence = request.Sequence,
                    ModuleId = request.ModuleId,
                    CreatedUtc = request.CreatedUtc,
                    ExpiresUtc = request.ExpiresUtc,
                    ConfirmationAccepted = true
                });
            }
            log("Switch request accepted: " + applicationId);
        }

        public string ApplySelectionCommand(NxCommandRequest request)
        {
            string filter = !string.IsNullOrWhiteSpace(request.SelectionFilter)
                ? request.SelectionFilter
                : SelectionFilterFromCommandId(request.CommandId);
            if (string.IsNullOrWhiteSpace(filter))
                throw new InvalidOperationException("Selection filter is not defined for: " + request.CommandId);

            string normalized = NormalizeSelectionFilter(filter);
            if (normalized == "none")
            {
                ClearGlobalSelectionList();
                return "Selection cleared";
            }
            if (normalized == "all")
            {
                ExecuteRelaxedMenuButton(request.CommandId);
                return "Select all requested";
            }
            if (normalized == "reset")
            {
                ui.SelectionManager.ResetEnabledGlobalFilterMembers();
                return "Selection filters reset";
            }

            ApplyGlobalSelectionFilter(normalized);
            return "Selection filter set: " + normalized;
        }

        public NxCommandResult ExecuteCapabilityRequest(NxCommandRequest request)
        {
            string capabilityId = (request.CapabilityId ?? request.CommandId ?? string.Empty).Trim();
            log("Executing capability request: " + capabilityId + " (workflow=" + request.WorkflowId + ")");

            if (capabilityId.StartsWith("nxeskd.", StringComparison.OrdinalIgnoreCase))
            {
                Assembly handlerAssembly = ResolveInstalledCapabilityAssembly("nxeskd", "NxEskd.NxRuntime.dll");
                if (handlerAssembly == null)
                {
                    return new NxCommandResult
                    {
                        SchemaVersion = NxProtocolConstants.SchemaVersion,
                        RequestId = request.RequestId,
                        WorkflowId = request.WorkflowId,
                        Status = "blocked",
                        Phase = "preflight",
                        IssueCode = "COMPONENT_NOT_INSTALLED",
                        RecommendedAction = "Установите модуль ЕСКД через инсталлятор NXKeys.",
                        Message = "Модуль ЕСКД не установлен на данной рабочей станции.",
                        CompletedUtc = DateTimeOffset.UtcNow.ToString("O")
                    };
                }

                Type handlerType = handlerAssembly.GetType("NxEskd.NxRuntime.NxEskdCapabilityHandler") ??
                                   handlerAssembly.GetType("NxEskd.NxRuntime.CommandHost");
                if (handlerType == null)
                {
                    throw new InvalidOperationException("NxEskd capability handler type was not found in runtime assembly.");
                }

                var method = handlerType.GetMethod("ExecuteCapability", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    object resultObj = method.Invoke(null, new object[] { request, processingDirectory });
                    if (resultObj is NxCommandResult typedResult)
                        return typedResult;
                }
                throw new InvalidOperationException("ExecuteCapability method invocation failed.");
            }

            throw new InvalidOperationException("Unsupported capability: " + capabilityId);
        }

        private MenuButton RequireRunnableButton(string commandId)
        {
            string id = (commandId ?? string.Empty).Trim();
            MenuButton button;
            try
            {
                button = ui.MenuBarManager.GetButtonFromName(id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("NX menu button was not found: " + id + ". " + ex.Message);
            }
            if (button == null)
                throw new InvalidOperationException("NX menu button was not found: " + id);
            if (button.ButtonAvailability == MenuButton.AvailabilityStatus.Unavailable)
                throw new InvalidOperationException("NX menu button is unavailable in the current context: " + id);
            if (button.ButtonSensitivity == MenuButton.SensitivityStatus.Insensitive)
                throw new InvalidOperationException("NX menu button is insensitive in the current context: " + id);
            return button;
        }

        private void ApplyGlobalSelectionFilter(string filter)
        {
            string normalized = NormalizeSelectionFilter(filter);
            if (string.IsNullOrWhiteSpace(normalized) || normalized == "all") return;
            if (normalized == "none") { ClearGlobalSelectionList(); return; }
            if (normalized == "reset") { ui.SelectionManager.ResetEnabledGlobalFilterMembers(); return; }

            NXOpen.Select.FilterMember[] members = FilterMembersFor(normalized);
            if (members.Length == 0)
            {
                log("No global NX selection filter mapping for: " + filter);
                return;
            }
            ui.SelectionManager.SetEnabledGlobalFilterMembers(members);
            log("Applied global selection filter: " + normalized + " => " + string.Join(",", members.Select(value => value.ToString())));
        }

        private static NXOpen.Select.FilterMember[] FilterMembersFor(string filter)
        {
            switch (NormalizeSelectionFilter(filter))
            {
                case "edge": return ParseFilterMembers("AllEdges");
                case "face": return ParseFilterMembers("AllFaces");
                case "body": return ParseFilterMembers("AllSolidBodies", "AllSheetBodies", "AllFacetBodies");
                case "component": return ParseFilterMembers("Component");
                case "curve": return ParseFilterMembers("AllCurves", "AllConicCurves");
                case "datum": return ParseFilterMembers("DatumAxis", "DatumPlane", "CoordinateSystem");
                case "feature": return ParseFilterMembers("AllBasicFeatures", "SolidFeature", "CurveFeature", "DatumPlaneFeature", "DatumAxisFeature");
                case "operation": return ParseFilterMembers("CAMOperation");
                default: return Array.Empty<NXOpen.Select.FilterMember>();
            }
        }

        private static NXOpen.Select.FilterMember[] ParseFilterMembers(params string[] names)
        {
            var values = new List<NXOpen.Select.FilterMember>();
            foreach (string name in names ?? Array.Empty<string>())
            {
                try
                {
                    values.Add((NXOpen.Select.FilterMember)Enum.Parse(typeof(NXOpen.Select.FilterMember), name, true));
                }
                catch { }
            }
            return values.Distinct().ToArray();
        }

        private static string SelectionFilterFromCommandId(string commandId)
        {
            string id = (commandId ?? string.Empty).ToUpperInvariant();
            if (id.Contains("DESELECT")) return "none";
            if (id.Contains("SELECT_ALL")) return "all";
            if (id.Contains("RESET")) return "reset";
            if (id.Contains("EDGE")) return "edge";
            if (id.Contains("FACE")) return "face";
            if (id.Contains("BODY")) return "body";
            if (id.Contains("COMPONENT")) return "component";
            if (id.Contains("CURVE")) return "curve";
            if (id.Contains("DATUM")) return "datum";
            if (id.Contains("FEATURE")) return "feature";
            return string.Empty;
        }

        private static string NormalizeSelectionFilter(string value)
        {
            string normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            while (normalized.Contains("__", StringComparison.Ordinal)) normalized = normalized.Replace("__", "_");
            return normalized.Trim('_');
        }

        private void ClearGlobalSelectionList()
        {
            try
            {
                ui.SelectionManager.ClearGlobalSelectionList();
            }
            catch
            {
                ExecuteRelaxedMenuButton("UG_SEL_DESELECT_ALL");
            }
        }

        private void ExecuteRelaxedMenuButton(string commandId)
        {
            string id = (commandId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id)) return;
            try
            {
                MenuButton button = ui.MenuBarManager.GetButtonFromName(id);
                if (button != null) ui.DialogTester.InvokeMenuButtonAction(button);
            }
            catch (Exception ex)
            {
                log("Relaxed menu action failed for " + id + ": " + ex.Message);
            }
        }

        private Assembly ResolveInstalledCapabilityAssembly(string componentName, string assemblyFileName)
        {
            foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(loaded.GetName().Name, Path.GetFileNameWithoutExtension(assemblyFileName), StringComparison.OrdinalIgnoreCase))
                    return loaded;
            }

            string bridgeDir = Path.GetDirectoryName(typeof(NxMenuCommandExecutor).Assembly.Location) ?? string.Empty;
            string localPath = Path.Combine(bridgeDir, assemblyFileName);
            if (File.Exists(localPath))
            {
                try { return Assembly.LoadFrom(localPath); }
                catch (Exception ex) { log("Failed loading local assembly " + localPath + ": " + ex.Message); }
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string componentsDir = Path.Combine(localAppData, "NXKeys", "components", componentName);
            if (Directory.Exists(componentsDir))
            {
                foreach (string versionDir in Directory.GetDirectories(componentsDir).OrderByDescending(d => d))
                {
                    string candidate = Path.Combine(versionDir, "runtime", assemblyFileName);
                    if (File.Exists(candidate))
                    {
                        try { return Assembly.LoadFrom(candidate); }
                        catch (Exception ex) { log("Failed loading component assembly " + candidate + ": " + ex.Message); }
                    }
                }
            }

            return null;
        }
    }
}
