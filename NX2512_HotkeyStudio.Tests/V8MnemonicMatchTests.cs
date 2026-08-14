using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NX2512_HotkeyStudio.Models;

namespace NX2512_HotkeyStudio.Tests
{
    internal static class V8MnemonicMatchTests
    {
        [ModuleInitializer]
        internal static void VerifyMnemonicLanguage()
        {
            string profilePath = FindRepositoryFile(Path.Combine("config", "nx2512-v8-profile.json"));
            Config config = Config.Load(profilePath);

            var sketchSequences = config.LeaderKey.Sequences
                .Where(item => string.Equals(item.ModuleID, "v8_s", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var expectedMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UG_SKETCH_LINE"] = "S L",
                ["UG_SKETCH_RECTANGLE"] = "S R",
                ["UG_SKETCH_CIRCLE"] = "S C",
                ["UG_SKETCH_ARC"] = "S A",
                ["UG_SKETCH_TRIM"] = "S T",
                ["UG_SKETCH_EXTEND"] = "S E",
                ["UG_SKETCH_OFFSET_CURVE"] = "S O",
                ["UG_SKETCH_STUDIO_SPLINE"] = "S S",
                ["UG_SKETCH_POINT"] = "S P",
                ["UG_SKETCH_SLOT"] = "S W",
                ["UG_SKETCH_POLYGON"] = "S G",
                ["UG_SKETCH_ELLIPSE"] = "S I",
                ["UG_SKETCH_FILLET"] = "S F",
                ["UG_SKETCH_CHAMFER"] = "S H",
                ["UG_SKETCH_MIRROR_PATTERN"] = "S M",
                ["UG_SKETCH_MOVE_CURVES"] = "S V",
                ["UG_SKETCH_PATTERN_CURVES"] = "S Y",
                ["UG_SKETCH_CONSTRAINT_NAVIGATOR"] = "S N",
                ["UG_SKETCH_CHECKER"] = "S Z",

                ["UG_SKETCH_COINCIDENT_CONSTRAINT"] = "S K C",
                ["UG_SKETCH_HORIZONTAL_CONSTRAINT"] = "S K H",
                ["UG_SKETCH_VERTICAL_CONSTRAINT"] = "S K V",
                ["UG_SKETCH_TANGENT_CONSTRAINT"] = "S K T",
                ["UG_SKETCH_PARALLEL_CONSTRAINT"] = "S K P",
                ["UG_SKETCH_PERPENDICULAR_CONSTRAINT"] = "S K N",
                ["UG_SKETCH_CONCENTRIC_CONSTRAINT"] = "S K O",
                ["UG_SKETCH_EQUAL_LENGTH_CONSTRAINT"] = "S K E",
                ["UG_SKETCH_COLLINEAR_CONSTRAINT"] = "S K L",
                ["UG_SKETCH_MAKE_MIDPOINT_ALIGNED"] = "S K M",
                ["UG_SKETCH_SYMMETRIC_CONSTRAINT"] = "S K S",
                ["UG_SKETCH_FIXED_CONSTRAINT"] = "S K F",
                ["UG_SKETCH_AUTO_CREATE_CONSTRAINTS"] = "S K A",

                ["UG_SKETCH_RAPID_DIMENSION"] = "S D Q",
                ["UG_SKETCH_LINEAR_DIMENSION"] = "S D L",
                ["UG_SKETCH_ANGULAR_DIMENSION"] = "S D A",
                ["UG_SKETCH_RADIAL_DIMENSION"] = "S D R",
                ["UG_SKETCH_DIAMETER_DIM"] = "S D O",
                ["UG_SKETCH_PERIMETER_DIM"] = "S D P"
            };

            foreach (var kvp in expectedMappings)
            {
                var match = sketchSequences.FirstOrDefault(item =>
                    string.Equals(item.Command?.ID, kvp.Key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.Sequence, kvp.Value, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    var existing = sketchSequences.Where(item =>
                        string.Equals(item.Command?.ID, kvp.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                    string actual = existing.Count > 0
                        ? string.Join(", ", existing.Select(x => x.Sequence))
                        : "NOT FOUND";
                    throw new InvalidOperationException(
                        $"Command {kvp.Key} expected sequence '{kvp.Value}' in v8_s, got: {actual}");
                }
            }

            // Verify no duplicate sequence paths in v8_s module
            var duplicates = sketchSequences
                .GroupBy(item => item.Sequence, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .ToList();

            if (duplicates.Count > 0)
            {
                var details = string.Join("; ", duplicates.Select(group =>
                    $"[{group.Key}] -> " + string.Join(", ", group.Select(x => x.Command?.ID ?? x.Command?.Name))));
                throw new InvalidOperationException($"Duplicate mnemonic sequences in v8_s: {details}");
            }
        }

        private static string FindRepositoryFile(string relativePath)
        {
            string current = Directory.GetCurrentDirectory();
            for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                string candidate = Path.Combine(current, relativePath);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                current = Path.GetDirectoryName(current);
            }
            throw new FileNotFoundException("Repository file was not found.", relativePath);
        }
    }
}
