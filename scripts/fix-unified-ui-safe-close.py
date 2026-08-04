from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / "NX2512_HotkeyStudio" / "UI" / "HotkeyStudioForm.cs"
text = path.read_text(encoding="utf-8")

old = '''        private void SaveConfig()
        {
            try
            {
                EditableCommandPathPolicy.Normalize(config);
                ValidateEditableCommands();
                config.Save(configPath);
                NxCommandBridgeClient.ConfigureSecurity(configPath);
                draftSession.AcceptSavedState();
                dirty = false;
                UpdateHistoryButtons();
                status.Text = "Профиль сохранён атомарно";
                RefreshAll();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
'''
new = '''        private bool SaveConfig()
        {
            try
            {
                moduleGrid.EndEdit();
                PersistModuleGrid();
                EditableCommandPathPolicy.Normalize(config);
                ValidateEditableCommands();
                config.Save(configPath);
                NxCommandBridgeClient.ConfigureSecurity(configPath);
                draftSession.AcceptSavedState();
                dirty = false;
                UpdateHistoryButtons();
                status.Text = "Профиль сохранён атомарно";
                RefreshAll();
                return true;
            }
            catch (Exception exception)
            {
                dirty = draftSession.IsDirty;
                status.Text = "Профиль не сохранён: " + exception.Message;
                MessageBox.Show(exception.Message, "NXKeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
'''
if text.count(old) != 1:
    raise RuntimeError("SaveConfig block was not found exactly once.")
text = text.replace(old, new, 1)

old_close = '''                if (answer == DialogResult.Cancel) { eventArgs.Cancel = true; return; }
                if (answer == DialogResult.Yes) SaveConfig();
'''
new_close = '''                if (answer == DialogResult.Cancel) { eventArgs.Cancel = true; return; }
                if (answer == DialogResult.Yes && !SaveConfig())
                {
                    eventArgs.Cancel = true;
                    return;
                }
'''
if text.count(old_close) != 1:
    raise RuntimeError("OnFormClosing save block was not found exactly once.")
text = text.replace(old_close, new_close, 1)
path.write_text(text, encoding="utf-8", newline="\n")
print("Unified UI safe-close behavior applied.")
