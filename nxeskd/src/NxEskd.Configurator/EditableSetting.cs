using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace NxEskd.Configurator;

public sealed class EditableSetting : INotifyPropertyChanged
{
    private string _value;
    private readonly Action<EditableSetting> _changed;

    public EditableSetting(string path, string name, string value, string valueType, string? description,
        Action<EditableSetting> changed)
    {
        Path = path;
        Name = name;
        _value = value;
        ValueType = valueType;
        Description = description ?? string.Empty;
        AllowedValues = SettingMetadataResolver.AllowedValues(path, valueType);
        _changed = changed;
    }

    public string Path { get; }
    public string Name { get; }
    public string ValueType { get; }
    public string Description { get; }
    public IReadOnlyList<string> AllowedValues { get; }
    public bool HasAllowedValues => AllowedValues.Count > 0;

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            _changed(this);
        }
    }

    public JsonNode? ToJsonNode()
    {
        if (Value == "null") return null;
        return ValueType switch
        {
            "boolean" => bool.TryParse(Value, out var boolean)
                ? JsonValue.Create(boolean)
                : throw new FormatException($"Поле {Path}: ожидается true или false."),
            "integer" => long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var integer)
                ? JsonValue.Create(integer)
                : throw new FormatException($"Поле {Path}: ожидается целое число."),
            "number" => double.TryParse(Value.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
                ? JsonValue.Create(number)
                : throw new FormatException($"Поле {Path}: ожидается конечное число."),
            _ => JsonValue.Create(Value)
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
