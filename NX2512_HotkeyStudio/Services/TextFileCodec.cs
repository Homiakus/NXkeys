using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NX2512_HotkeyStudio.Services
{
    public static class TextFileCodec
    {
        private sealed class TextDocument
        {
            public Encoding Encoding { get; set; }
            public bool EmitBom { get; set; }
            public string NewLine { get; set; }
            public string Text { get; set; }
        }

        public static byte[] AppendUniquePath(string filePath, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Добавляемый путь не задан.", nameof(value));
            byte[] source = File.Exists(filePath) ? File.ReadAllBytes(filePath) : Array.Empty<byte>();
            TextDocument document = Decode(source);
            string normalizedValue = Path.GetFullPath(value.Trim().Trim('"'));
            var lines = document.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

            bool exists = lines.Any(line =>
            {
                string candidate = line.Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("#") || candidate.StartsWith("!")) return false;
                try { return string.Equals(Path.GetFullPath(candidate), normalizedValue, StringComparison.OrdinalIgnoreCase); }
                catch { return string.Equals(candidate, normalizedValue, StringComparison.OrdinalIgnoreCase); }
            });

            if (!exists)
            {
                while (lines.Count > 0 && string.IsNullOrEmpty(lines[lines.Count - 1])) lines.RemoveAt(lines.Count - 1);
                lines.Add(normalizedValue);
                lines.Add(string.Empty);
            }

            string text = string.Join(document.NewLine, lines);
            return Encode(document.Encoding, document.EmitBom, text);
        }

        private static TextDocument Decode(byte[] bytes)
        {
            Encoding encoding;
            bool emitBom = false;
            int offset = 0;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(false, true);
                emitBom = true;
                offset = 3;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = new UnicodeEncoding(false, false, true);
                emitBom = true;
                offset = 2;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                encoding = new UnicodeEncoding(true, false, true);
                emitBom = true;
                offset = 2;
            }
            else if (LooksUtf16(bytes, false))
            {
                encoding = new UnicodeEncoding(false, false, true);
            }
            else if (LooksUtf16(bytes, true))
            {
                encoding = new UnicodeEncoding(true, false, true);
            }
            else
            {
                encoding = new UTF8Encoding(false, true);
            }

            string text;
            try { text = encoding.GetString(bytes, offset, bytes.Length - offset); }
            catch (DecoderFallbackException)
            {
                encoding = Encoding.Default;
                emitBom = false;
                offset = 0;
                text = encoding.GetString(bytes);
            }

            string newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            return new TextDocument { Encoding = encoding, EmitBom = emitBom, NewLine = newLine, Text = text };
        }

        private static bool LooksUtf16(byte[] bytes, bool bigEndian)
        {
            if (bytes.Length < 4) return false;
            int zeroes = 0;
            int samples = Math.Min(bytes.Length, 200);
            int expectedParity = bigEndian ? 0 : 1;
            for (int index = expectedParity; index < samples; index += 2)
            {
                if (bytes[index] == 0) zeroes++;
            }
            return zeroes >= Math.Max(2, samples / 8);
        }

        private static byte[] Encode(Encoding encoding, bool emitBom, string text)
        {
            byte[] body = encoding.GetBytes(text ?? string.Empty);
            if (!emitBom) return body;
            byte[] preamble = encoding.GetPreamble();
            if (preamble.Length == 0) return body;
            var result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }
    }
}
