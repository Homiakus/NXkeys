#!/usr/bin/env pwsh
# Patch: Add Curve Selection Rules to ALL selection_filters blocks in nx2512-pro-hybrid.json
# Uses System.Text.Json to safely modify the profile.

$profilePath = Join-Path $PSScriptRoot "nx2512-pro-hybrid.json"

# Write a C# inline script
$csCode = @'
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

class Patcher
{
    static readonly string CurveRulesJson = @"[
        {
            ""slot"": """",
            ""submenu_key"": """",
            ""submenu_label"": ""Curve Rules"",
            ""input_key"": ""S"",
            ""path"": [""S"", ""C"", ""S""],
            ""path_labels"": [""Select"", ""Curve Rule"", ""Single / Inferred Curve Rule""],
            ""aliases"": [],
            ""search_aliases"": [""Single / Inferred Curve Rule"", ""Single Curve"", ""UG_SC_INFERRED_CURVE_SELECTION""],
            ""icon_hint"": ""selection"",
            ""display_order"": 9010,
            ""command"": { ""id"": ""UG_SC_INFERRED_CURVE_SELECTION"", ""name"": ""Single / Inferred Curve Rule"" },
            ""action"": ""set_selection_filter"",
            ""selection_type"": ""curve"",
            ""enabled"": true,
            ""requires_selection"": false,
            ""destructive"": false,
            ""confirm_before_execute"": false,
            ""fallback"": """",
            ""notes"": ""Single / Inferred Curve Selection Rule for Extrude and modeling operations"",
            ""catalog_refs"": [],
            ""frequency"": ""support"",
            ""resolution_status"": ""existing"",
            ""resolution_candidates"": [],
            ""support_kind"": ""selection_filter""
        },
        {
            ""slot"": """",
            ""submenu_key"": """",
            ""submenu_label"": ""Curve Rules"",
            ""input_key"": ""C"",
            ""path"": [""S"", ""C"", ""C""],
            ""path_labels"": [""Select"", ""Curve Rule"", ""Connected Curves Rule""],
            ""aliases"": [],
            ""search_aliases"": [""Connected Curves Rule"", ""Connected Curves"", ""UG_ROUTE_CONNECTED_CURVES""],
            ""icon_hint"": ""selection"",
            ""display_order"": 9011,
            ""command"": { ""id"": ""UG_ROUTE_CONNECTED_CURVES"", ""name"": ""Connected Curves Rule"" },
            ""action"": ""set_selection_filter"",
            ""selection_type"": ""curve"",
            ""enabled"": true,
            ""requires_selection"": false,
            ""destructive"": false,
            ""confirm_before_execute"": false,
            ""fallback"": """",
            ""notes"": ""Connected Curves Selection Rule for Extrude and modeling operations"",
            ""catalog_refs"": [],
            ""frequency"": ""support"",
            ""resolution_status"": ""existing"",
            ""resolution_candidates"": [],
            ""support_kind"": ""selection_filter""
        },
        {
            ""slot"": """",
            ""submenu_key"": """",
            ""submenu_label"": ""Curve Rules"",
            ""input_key"": ""I"",
            ""path"": [""S"", ""C"", ""I""],
            ""path_labels"": [""Select"", ""Curve Rule"", ""Stop at Intersection Rule""],
            ""aliases"": [],
            ""search_aliases"": [""Stop at Intersection Rule"", ""Stop at Intersection"", ""UG_SC_STOP_AT_INTERSECTION""],
            ""icon_hint"": ""selection"",
            ""display_order"": 9012,
            ""command"": { ""id"": ""UG_SC_STOP_AT_INTERSECTION"", ""name"": ""Stop at Intersection Rule"" },
            ""action"": ""set_selection_filter"",
            ""selection_type"": ""curve"",
            ""enabled"": true,
            ""requires_selection"": false,
            ""destructive"": false,
            ""confirm_before_execute"": false,
            ""fallback"": """",
            ""notes"": ""Stop at Intersection Selection Rule for Extrude and modeling operations"",
            ""catalog_refs"": [],
            ""frequency"": ""support"",
            ""resolution_status"": ""existing"",
            ""resolution_candidates"": [],
            ""support_kind"": ""selection_filter""
        }
    ]";

    static int Main(string[] args)
    {
        string path = args[0];
        string raw = File.ReadAllText(path);
        var doc = JsonNode.Parse(raw, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var curveRules = JsonNode.Parse(CurveRulesJson).AsArray();
        var modules = doc["modules"].AsArray();
        int patched = 0;

        foreach (var module in modules)
        {
            var sets = module["command_sets"]?.AsArray();
            if (sets == null) continue;
            foreach (var set in sets)
            {
                if (set["id"]?.GetValue<string>() != "selection_filters") continue;
                var commands = set["commands"]?.AsArray();
                if (commands == null) continue;

                // Check if already patched
                bool exists = false;
                foreach (var cmd in commands)
                {
                    var id = cmd?["command"]?["id"]?.GetValue<string>();
                    if (id == "UG_SC_INFERRED_CURVE_SELECTION") { exists = true; break; }
                }
                if (exists) continue;

                // Add curve rules
                foreach (var rule in curveRules)
                {
                    commands.Add(rule.DeepClone());
                }
                patched++;
            }
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string output = doc.ToJsonString(options);
        File.WriteAllText(path, output);
        Console.WriteLine($"[OK] Patched {patched} selection_filters blocks with Curve Rules.");
        return 0;
    }
}
'@

$tempDir = Join-Path $env:TEMP "nxkeys-patcher"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item $tempDir -ItemType Directory -Force | Out-Null

$csFile = Join-Path $tempDir "Patcher.cs"
$csprojFile = Join-Path $tempDir "Patcher.csproj"

Set-Content $csFile $csCode -Encoding UTF8
Set-Content $csprojFile @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
"@ -Encoding UTF8

dotnet run --project $tempDir -- $profilePath

# Cleanup
Remove-Item $tempDir -Recurse -Force

# Remove generated profiles
$generatedFiles = @(
    (Join-Path $PSScriptRoot "nx2512-pro-full.generated.json"),
    (Join-Path $PSScriptRoot "nx2512-pro-main.generated.json")
)
foreach ($f in $generatedFiles) {
    if (Test-Path $f) {
        Remove-Item $f -Force
        Write-Host "[OK] Removed generated profile: $(Split-Path $f -Leaf)"
    }
}

Write-Host "[OK] One main working profile: nx2512-pro-hybrid.json"
