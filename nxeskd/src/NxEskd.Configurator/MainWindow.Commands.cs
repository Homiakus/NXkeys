using System.Text;
using System.Windows;
using NxEskd.Core.Configuration;
using NxEskd.Core.Runtime;

namespace NxEskd.Configurator;

public partial class MainWindow
{
    private void Request(DrawingCommand command)
    {
        if (!SaveCurrent()) return;
        if (!ConfirmRiskyExecution(command)) return;

        if (string.IsNullOrWhiteSpace(_requestPath))
        {
            MessageBox.Show(this,
                "Профиль проверен и сохранён. Запустите требуемую команду из меню NX.",
                "ЕСКД-генератор",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            CommandRequest.Create(command, _document.ProfilePath, _nxPartPath,
                command == DrawingCommand.Preview).SaveAtomic(_requestPath);
            _closeAfterRequest = true;
            try { DialogResult = true; } catch (InvalidOperationException) { }
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Ошибка запроса NX", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ConfirmRiskyExecution(DrawingCommand command)
    {
        if (command is not (DrawingCommand.Generate or DrawingCommand.Update)) return true;

        var actions = new List<string>();
        if (JsonNavigator.GetBool(_document.Root, "$.output.allowOverwriteExisting", false))
            actions.Add("разрешена перезапись существующего выходного PRT");
        if (JsonNavigator.GetBool(_document.Root, "$.output.allowOverwriteReleasedDocument", false))
            actions.Add("разрешена перезапись выпущенного или утверждённого документа");
        if (JsonNavigator.GetBool(_document.Root,
                "$.execution.idempotency.deleteManagedObjectsMissingFromConfig", false))
            actions.Add(JsonNavigator.GetBool(_document.Root,
                    "$.execution.idempotency.confirmManagedDeletion", false)
                ? "разрешено удаление stale managed-объектов текущего profile/scope"
                : "запрошено удаление stale managed-объектов, но подтверждение профиля отсутствует");

        if (actions.Count == 0) return true;

        var message = new StringBuilder()
            .AppendLine("Для этого запуска включены опасные разрешения:")
            .AppendLine()
            .AppendLine(string.Join(Environment.NewLine, actions.Select(action => "• " + action)))
            .AppendLine()
            .AppendLine("Продолжать только после проверки Preview и целевых путей файлов.")
            .ToString();
        return MessageBox.Show(this, message,
                   "Подтверждение опасных операций",
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Warning,
                   MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private bool ConfirmDiscardOrSave()
    {
        if (!_document.IsDirty(RawJsonBox.Text)) return true;
        var answer = MessageBox.Show(this,
            "Профиль изменён. Сохранить изменения перед продолжением?",
            "Несохранённые изменения",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return answer switch
        {
            MessageBoxResult.Yes => SaveCurrent(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Generate);
    private void Update_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Update);
    private void ValidateNx_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Validate);
    private void Preview_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Preview);
    private void Inventory_Click(object sender, RoutedEventArgs e) => Request(DrawingCommand.Inventory);
}
