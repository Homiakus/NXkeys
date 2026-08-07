using System.IO;
using System.Windows;
using Microsoft.Win32;
using NxEskd.Core.Diagnostics;

namespace NxEskd.Configurator;

public partial class MainWindow
{
    private string? ParseArguments(string[] args)
    {
        string? profilePath = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--profile" && i + 1 < args.Length) profilePath = args[++i];
            else if (args[i] == "--request" && i + 1 < args.Length) _requestPath = args[++i];
            else if (args[i] == "--nx-part" && i + 1 < args.Length) _nxPartPath = args[++i];
        }
        return profilePath;
    }

    private static string FindDefaultProfile()
    {
        var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NxEskdGenerator", "profiles", "active-profile.json");
        if (File.Exists(user)) return user;
        var app = AppContext.BaseDirectory;
        return new[]
        {
            Path.Combine(app, "..", "config", "active-profile.json"),
            Path.Combine(app, "..", "config", "active-profile.example.json"),
            Path.Combine(app, "config", "active-profile.example.json")
        }.Select(Path.GetFullPath).FirstOrDefault(File.Exists) ?? user;
    }

    private void LoadProfile(string path)
    {
        try
        {
            _loading = true;
            var document = _document.Load(path);
            ProfilePathBox.Text = _document.ProfilePath;
            RawJsonBox.Text = _document.CurrentJson;
            BuildSections();
            ReloadTypedWorkspaces();
            DisplayValidation(_document.Validate());
            Status($"Загружен профиль {document.ProfileId}." +
                   (string.IsNullOrWhiteSpace(_nxPartPath) ? string.Empty : " Модель: " + _nxPartPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Ошибка профиля", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private ValidationReport ApplyAndValidateCurrentJson(bool rebuildSections)
    {
        try
        {
            var report = _document.ApplyRaw(RawJsonBox.Text);
            if (rebuildSections) BuildSections();
            ReloadTypedWorkspaces();
            DisplayValidation(report);
            return report;
        }
        catch (Exception ex)
        {
            var report = new ValidationReport();
            report.Add(new("CFG_JSON_PARSE_FAILED", IssueSeverity.Error, ex.Message, "$"));
            DisplayValidation(report);
            return report;
        }
    }

    private bool SaveCurrent(string? targetPath = null)
    {
        try
        {
            var report = _document.Save(RawJsonBox.Text, targetPath);
            DisplayValidation(report);
            if (report.HasErrors)
            {
                MainTabs.SelectedIndex = 6;
                MessageBox.Show(this,
                    "Профиль не сохранён. Исправьте ошибки, показанные на вкладке «Проверка».",
                    "Проверка профиля",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            _loading = true;
            RawJsonBox.Text = _document.CurrentJson;
            ProfilePathBox.Text = _document.ProfilePath;
            BuildSections();
            ReloadTypedWorkspaces();
            Status("Профиль сохранён атомарно: " + _document.ProfilePath);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OpenProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardOrSave()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "JSON profile (*.json)|*.json|All files (*.*)|*.*",
            FileName = _document.ProfilePath
        };
        if (dialog.ShowDialog(this) == true) LoadProfile(dialog.FileName);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveCurrent();

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON profile (*.json)|*.json",
            FileName = Path.GetFileName(_document.ProfilePath)
        };
        if (dialog.ShowDialog(this) == true) SaveCurrent(dialog.FileName);
    }

    private void ApplyRawJson_Click(object sender, RoutedEventArgs e)
    {
        var report = ApplyAndValidateCurrentJson(rebuildSections: true);
        Status(report.HasErrors ? "JSON применён, но содержит ошибки." : "JSON применён и проверен.");
    }

    private void RefreshRawJson_Click(object sender, RoutedEventArgs e)
    {
        if (_document.IsDirty(RawJsonBox.Text))
        {
            var answer = MessageBox.Show(this,
                "Отменить несинхронизированные изменения текста JSON и восстановить данные из формы?",
                "Обновить JSON",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }
        RawJsonBox.Text = _document.CurrentJson;
    }

    private void ValidateProfile_Click(object sender, RoutedEventArgs e)
        => ApplyAndValidateCurrentJson(rebuildSections: false);
}
