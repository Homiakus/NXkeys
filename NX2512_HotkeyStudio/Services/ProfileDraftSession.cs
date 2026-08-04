using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Services
{
    public sealed class ProfileDraftSession
    {
        private sealed class Snapshot
        {
            public string Label { get; }
            public string Json { get; }
            public Snapshot(string label, string json) { Label = label ?? string.Empty; Json = json ?? string.Empty; }
        }

        private readonly Stack<Snapshot> undo = new Stack<Snapshot>();
        private readonly Stack<Snapshot> redo = new Stack<Snapshot>();
        private string acceptedJson;

        public Config Draft { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public bool IsDirty => !string.Equals(Serialize(Draft), acceptedJson, StringComparison.Ordinal);
        public string UndoLabel => CanUndo ? undo.Peek().Label : string.Empty;
        public string RedoLabel => CanRedo ? redo.Peek().Label : string.Empty;

        public ProfileDraftSession(Config source)
        {
            Draft = Clone(source ?? throw new ArgumentNullException(nameof(source)));
            acceptedJson = Serialize(Draft);
        }

        public bool CaptureMutation(string label, Action<Config> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            string before = Serialize(Draft);
            try
            {
                mutation(Draft);
                Draft.ApplyDefaults();
            }
            catch
            {
                Draft = Deserialize(before);
                throw;
            }
            string after = Serialize(Draft);
            if (string.Equals(before, after, StringComparison.Ordinal)) return false;
            undo.Push(new Snapshot(label, before));
            while (undo.Count > 50)
            {
                Snapshot[] retained = undo.Reverse().Skip(1).ToArray();
                undo.Clear();
                foreach (Snapshot snapshot in retained) undo.Push(snapshot);
            }
            redo.Clear();
            return true;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            Snapshot previous = undo.Pop();
            redo.Push(new Snapshot(previous.Label, Serialize(Draft)));
            Draft = Deserialize(previous.Json);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            Snapshot next = redo.Pop();
            undo.Push(new Snapshot(next.Label, Serialize(Draft)));
            Draft = Deserialize(next.Json);
            return true;
        }

        public void AcceptSavedState()
        {
            acceptedJson = Serialize(Draft);
            undo.Clear();
            redo.Clear();
        }

        public string BuildDiff(int maximumLines = 120)
        {
            string[] before = acceptedJson.Replace("\r\n", "\n").Split('\n');
            string[] after = Serialize(Draft).Replace("\r\n", "\n").Split('\n');
            var builder = new StringBuilder();
            builder.AppendLine("Профиль: " + (Draft.Profile?.Name ?? string.Empty));
            builder.AppendLine("Baseline SHA-256: " + Digest(acceptedJson));
            builder.AppendLine("Draft SHA-256:    " + Digest(Serialize(Draft)));
            builder.AppendLine("Модулей: " + (Draft.Modules?.Count ?? 0));
            builder.AppendLine("Команд runtime: " + (Draft.LeaderKey?.Sequences?.Count ?? 0));
            builder.AppendLine();

            int emitted = 0;
            int count = Math.Max(before.Length, after.Length);
            for (int index = 0; index < count && emitted < maximumLines; index++)
            {
                string left = index < before.Length ? before[index] : string.Empty;
                string right = index < after.Length ? after[index] : string.Empty;
                if (string.Equals(left, right, StringComparison.Ordinal)) continue;
                if (left.Length > 0) { builder.AppendLine("- " + left); emitted++; }
                if (right.Length > 0 && emitted < maximumLines) { builder.AppendLine("+ " + right); emitted++; }
            }
            if (emitted == 0) builder.AppendLine("Изменений нет.");
            else if (emitted >= maximumLines) builder.AppendLine("… diff сокращён; показано до " + maximumLines + " строк.");
            return builder.ToString();
        }

        private static Config Clone(Config source) => Deserialize(Serialize(source));

        private static Config Deserialize(string json)
        {
            Config result = JsonSerializer.Deserialize<Config>(json, ReadOptions()) ?? new Config();
            result.ApplyDefaults();
            return result;
        }

        private static string Serialize(Config value) => JsonSerializer.Serialize(value, WriteOptions());

        private static JsonSerializerOptions ReadOptions() => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private static JsonSerializerOptions WriteOptions() => new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static string Digest(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))
                .ToLowerInvariant();
    }
}
