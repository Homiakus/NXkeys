using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NX2512_HotkeyStudio.Models;
using NX2512_HotkeyStudio.Services;
using NX2512_HotkeyStudio.UI;

internal static class Program
{
    private static void Main()
    {
        var first = Command("UG_TEST_FIRST", "First", new[] { "C", "F", "E" });
        var module = new ModuleConfig
        {
            ID = "modeling",
            Label = "Modeling",
            LeaderPrefix = "M",
            CommandSets = new List<ModuleCommandSet>
            {
                new ModuleCommandSet { ID = "primary", Commands = new List<ModuleCommand> { first } }
            }
        };
        var config = new Config { Modules = new List<ModuleConfig> { module } };
        EditableCommandPathPolicy.Normalize(config);
        Assert(EditableCommandPathPolicy.Validate(config).Count == 0,
            "Canonical path with empty legacy fields must be valid.");

        EditableCommandPathPolicy.ApplyEditedPath(first, "E → F → M", "Edit → Feature → Modify");
        Assert(first.Path.SequenceEqual(new[] { "E", "F", "M" }), "Editor must update canonical Path.");
        Assert(first.PathLocked && first.PathSource == "user", "Edited paths must be user-owned and locked.");
        Assert(first.SubmenuKey == "E" && first.InputKey == "M", "Legacy projection must follow canonical Path.");

        var duplicate = Command("UG_TEST_DUPLICATE", "Duplicate", new[] { "E", "F", "M" });
        module.CommandSets[0].Commands.Add(duplicate);
        List<string> duplicateProblems = EditableCommandPathPolicy.Validate(config);
        Assert(duplicateProblems.Any(value => value.Contains("повторяется", StringComparison.OrdinalIgnoreCase)),
            "Duplicate canonical paths must be rejected.");

        duplicate.Path = new List<string> { "E", "F" };
        List<string> prefixProblems = EditableCommandPathPolicy.Validate(config);
        Assert(prefixProblems.Any(value => value.Contains("команда/подменю", StringComparison.OrdinalIgnoreCase)),
            "Terminal-prefix conflicts must be rejected.");

        var item = new LeaderSequenceItem
        {
            PathLabels = new List<string> { "Create", "Feature", "Extrude" },
            SubmenuLabel = "Legacy root"
        };
        Assert(CommandMenuPolicy.ResolveMenuLabel("F", new[] { item }, 2) == "Feature",
            "Nested menu label must use the matching PathLabels depth.");

        string canonicalRibbon = Path.Combine("custom", "application", "profiles", "All", "rbn_nxkeys.rtb");
        string legacyRibbon = Path.Combine("custom", "startup", "nxkeys_ribbon.rtb");
        Assert(string.Equals(NxRibbonLayout.CanonicalRelativePath, canonicalRibbon, StringComparison.OrdinalIgnoreCase),
            "NXKeys must deploy exactly one canonical ribbon tab under application/profiles/All.");
        Assert(NxRibbonLayout.LegacyRelativePaths.Contains(legacyRibbon, StringComparer.OrdinalIgnoreCase),
            "The former startup ribbon copy must be registered for cleanup.");
        string ribbon = NxRibbonLayout.BuildTabFile(170);
        Assert(ribbon.Split(new[] { "TITLE NXKeys" }, StringSplitOptions.None).Length - 1 == 1,
            "The generated ribbon must contain one NXKeys tab title.");

        string overlay = OverlayGenerator.GenerateOverlay(170, "UG_GATEWAY_MAIN_MENUBAR",
            new List<ResolutionResult>(), new Dictionary<string, List<ConflictItem>>(), false);
        Assert(overlay.Contains("TOOLBAR_LABEL Leader Key", StringComparison.Ordinal),
            "Leader Key requires a compact ribbon label.");
        Assert(overlay.Contains("TOOLBAR_LABEL NXKeys Studio", StringComparison.Ordinal),
            "NXKeys Studio requires a compact ribbon label.");
        Assert(!overlay.Contains("Ð", StringComparison.Ordinal) && !overlay.Contains("Ñ", StringComparison.Ordinal),
            "Generated menu labels must not contain UTF-8 mojibake.");

        VerifySketchIntentGrammar();
        VerifyPhaseZeroHardening();
        VerifyAuthenticatedIpc();
        VerifyBridgeInbox();
        VerifyProfileDraftSession();

        Console.WriteLine("[OK] Profile draft, Sketch grammar, authenticated IPC and background Bridge inbox regressions.");
    }





    private static void VerifyProfileDraftSession()
    {
        string sourceConfig = FindRepositoryProfileFile();
        Config source = Config.Load(sourceConfig);
        var session = new ProfileDraftSession(source);
        string initialTrigger = session.Draft.LeaderKey.TriggerKey;
        string changedTrigger = string.Equals(initialTrigger, "F12", StringComparison.OrdinalIgnoreCase) ? "CapsLock" : "F12";

        Assert(!session.IsDirty && !session.CanUndo && !session.CanRedo,
            "A new draft session must start clean.");
        Assert(session.CaptureMutation("Trigger", draft => draft.LeaderKey.TriggerKey = changedTrigger),
            "A real draft mutation must be captured.");
        Assert(session.IsDirty && session.CanUndo && session.Draft.LeaderKey.TriggerKey == changedTrigger,
            "Captured draft mutation must be visible and undoable.");
        Assert(session.Undo() && session.Draft.LeaderKey.TriggerKey == initialTrigger && session.CanRedo,
            "Undo must restore the previous immutable snapshot.");
        Assert(session.Redo() && session.Draft.LeaderKey.TriggerKey == changedTrigger,
            "Redo must restore the changed snapshot.");
        Assert(session.BuildDiff().Contains("Draft SHA-256", StringComparison.Ordinal),
            "Draft diff must contain a reproducible digest.");
        session.AcceptSavedState();
        Assert(!session.IsDirty && !session.CanUndo && !session.CanRedo,
            "Accepting an atomic save must reset draft history.");
        Assert(!session.CaptureMutation("No-op", _ => { }),
            "No-op UI events must not pollute undo history.");
    }

    private static void VerifyBridgeInbox()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "nxkeys-inbox-" + Guid.NewGuid().ToString("N"));
        string pending = Path.Combine(tempRoot, "pending");
        string processing = Path.Combine(tempRoot, "processing");
        Directory.CreateDirectory(pending);
        Directory.CreateDirectory(processing);
        int admitted = 0;
        try
        {
            using (var inbox = new NXKeys.BridgeCore.BridgeRequestInbox(
                pending,
                processing,
                _ => false,
                (_, _) => { },
                _ => System.Threading.Interlocked.Increment(ref admitted)))
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var request = new NXKeys.Protocol.NxCommandRequest
                {
                    RequestId = "inbox-test",
                    Action = NXKeys.Protocol.NxProtocolActions.ExecuteCommand,
                    CommandId = "UG_TEST",
                    CommandName = "Inbox test",
                    ModuleId = "modeling",
                    CreatedUtc = now.ToString("O"),
                    ExpiresUtc = now.AddMinutes(1).ToString("O"),
                    ConfirmationAccepted = true
                };
                string requestPath = Path.Combine(pending, request.RequestId + ".request.json");
                File.WriteAllText(requestPath,
                    System.Text.Json.JsonSerializer.Serialize(request, NXKeys.Protocol.NxProtocolJson.WriteOptions));
                inbox.Start();
                inbox.Signal();

                NXKeys.BridgeCore.BridgeRequestClaim claim = null;
                bool claimed = System.Threading.SpinWait.SpinUntil(
                    () => inbox.TryDequeue(out claim), TimeSpan.FromSeconds(5));
                Assert(claimed && claim != null, "Background inbox must claim a valid request.");
                Assert(claim.RequestId == request.RequestId && File.Exists(claim.ProcessingPath),
                    "Claimed request must be atomically moved to processing.");
                Assert(admitted == 1, "Admission callback must run exactly once.");

                string oversizedId = "oversized";
                File.WriteAllText(
                    Path.Combine(pending, oversizedId + ".request.json"),
                    new string('X', NXKeys.Protocol.NxProtocolConstants.MaxRequestPayloadBytes + 1));
                inbox.Signal();
                NXKeys.BridgeCore.BridgeRequestRejection rejection = null;
                bool rejected = System.Threading.SpinWait.SpinUntil(
                    () => inbox.TryDequeueRejected(out rejection), TimeSpan.FromSeconds(5));
                Assert(rejected && rejection != null && rejection.RequestId == oversizedId,
                    "Oversized requests must be rejected off the NX UI thread.");
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static void VerifyAuthenticatedIpc()
    {
        Assert(NXKeys.Protocol.NxProtocolConstants.SchemaVersion == 4,
            "Authenticated transport requires IPC schema 4.");
        string sourceConfig = FindRepositoryProfileFile();
        NXKeys.Protocol.NxBridgePermissionSet permissions =
            NXKeys.Protocol.NxBridgePermissionSet.FromProfileFile(sourceConfig);
        Assert(permissions.Permissions.Count > 0, "Profile permission set must not be empty.");
        NXKeys.Protocol.NxCommandPermission permission = permissions.Permissions
            .First(item => string.Equals(item.Action, NXKeys.Protocol.NxProtocolActions.ExecuteCommand,
                StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.CommandId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = new NXKeys.Protocol.NxCommandRequest
        {
            RequestId = "authenticated-test",
            Action = permission.Action,
            CommandId = permission.CommandId,
            CommandName = "Authenticated test",
            ModuleId = permission.ModuleId,
            TargetApplicationId = permission.TargetApplicationId,
            SelectionFilter = permission.SelectionFilter,
            CreatedUtc = now.ToString("O"),
            ExpiresUtc = now.AddMinutes(1).ToString("O"),
            SourceProcessId = Environment.ProcessId,
            Destructive = permission.Destructive,
            ConfirmationAccepted = permission.ConfirmationRequired
        };
        byte[] secret = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        string sessionId = Guid.NewGuid().ToString("N");
        string clientId = Guid.NewGuid().ToString("N");
        NXKeys.Protocol.NxRequestAuthenticator.Sign(
            request, sessionId, clientId, secret, permissions.ProfileDigest, 1);
        request.Validate();
        Assert(NXKeys.Protocol.NxRequestAuthenticator.Verify(
                request, sessionId, secret, permissions.ProfileDigest, out string verificationError),
            "Signed request must verify: " + verificationError);
        Assert(permissions.TryGetPermission(request, out NXKeys.Protocol.NxCommandPermission resolved) &&
               resolved.CommandId == permission.CommandId,
            "Signed request must be admitted by the exact profile allowlist.");

        string originalName = request.CommandName;
        request.CommandName = originalName + " tampered";
        Assert(!NXKeys.Protocol.NxRequestAuthenticator.Verify(
                request, sessionId, secret, permissions.ProfileDigest, out _),
            "Any signed payload mutation must invalidate the HMAC.");
        request.CommandName = originalName;
        NXKeys.Protocol.NxRequestAuthenticator.Sign(
            request, sessionId, clientId, secret, permissions.ProfileDigest, 2);

        var replay = new NXKeys.Protocol.NxReplayGuard(128);
        Assert(replay.TryAccept(request, out _), "First authenticated request must pass anti-replay.");
        Assert(!replay.TryAccept(request, out string replayError) &&
               replayError.IndexOf("nonce", StringComparison.OrdinalIgnoreCase) >= 0,
            "Repeated nonce must be rejected.");

        var unauthorized = new NXKeys.Protocol.NxCommandRequest
        {
            Action = NXKeys.Protocol.NxProtocolActions.ExecuteCommand,
            CommandId = "UG_NOT_IN_PROFILE",
            ModuleId = permission.ModuleId
        };
        Assert(!permissions.TryGetPermission(unauthorized, out _),
            "Unknown command must not be admitted by the profile allowlist.");
    }

    private static void VerifyPhaseZeroHardening()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = new NXKeys.Protocol.NxCommandRequest
        {
            RequestId = "phase-zero-test",
            Action = "unexpected_action",
            CommandId = "UG_TEST",
            CreatedUtc = now.ToString("O"),
            ExpiresUtc = now.AddMinutes(1).ToString("O"),
            ConfirmationAccepted = true
        };
        AssertThrows<InvalidOperationException>(() => request.Validate(),
            "Unknown protocol actions must be rejected fail-closed.");

        request.Action = NXKeys.Protocol.NxProtocolActions.ExecuteCommand;
        request.Validate();
        request.CommandName = new string('X', NXKeys.Protocol.NxProtocolConstants.MaxTextFieldLength + 1);
        AssertThrows<InvalidOperationException>(() => request.Validate(),
            "Oversized protocol fields must be rejected.");

        var firstContext = new NXKeys.Protocol.NxContextSnapshot
        {
            Status = "running",
            ModuleId = "modeling",
            SelectionCount = 1,
            SelectionState = "single",
            SelectionFingerprint = "AAA"
        };
        var secondContext = new NXKeys.Protocol.NxContextSnapshot
        {
            Status = "running",
            ModuleId = "modeling",
            SelectionCount = 1,
            SelectionState = "single",
            SelectionFingerprint = "BBB"
        };
        Assert(firstContext.SemanticFingerprint() != secondContext.SemanticFingerprint(),
            "Selection identity must participate in the semantic context revision.");

        string sourceConfig = FindRepositoryProfileFile();
        string sourceJson = File.ReadAllText(sourceConfig);
        string futureJson = System.Text.RegularExpressions.Regex.Replace(sourceJson, "\"schema_version\":\\s*\\d+", "\"schema_version\": 999");
        Assert(futureJson != sourceJson, "Test profile schema marker was not found.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "nxkeys-phase-zero-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string futurePath = Path.Combine(tempRoot, "future.json");
            File.WriteAllText(futurePath, futureJson);
            AssertThrows<InvalidOperationException>(() => Config.Load(futurePath),
                "Future profile schema must be rejected before migration.");

            Config loaded = Config.Load(sourceConfig);
            string savedPath = Path.Combine(tempRoot, "saved.json");
            loaded.Save(savedPath);
            Config roundTrip = Config.Load(savedPath);
            Assert(roundTrip.SchemaVersion == Config.CurrentSchemaVersion,
                "Atomic profile save must produce a readable current-schema profile.");
            Assert(!Directory.EnumerateFiles(tempRoot, ".nxkeys-*.tmp").Any(),
                "Atomic profile save must not leave temporary files.");

            string previousBridgeRoot = Environment.GetEnvironmentVariable("NXKEYS_BRIDGE_ROOT");
            string isolatedBridgeRoot = Path.Combine(tempRoot, "bridge");
            Environment.SetEnvironmentVariable("NXKEYS_BRIDGE_ROOT", isolatedBridgeRoot);
            try
            {
                Directory.CreateDirectory(isolatedBridgeRoot);
                File.WriteAllText(Path.Combine(isolatedBridgeRoot, "context.json"), "{ broken json");
                NxTransportReadResult<NxBridgeContext> read = NxCommandBridgeClient.ReadContextDetailed();
                Assert(read.Status == NxTransportReadStatus.Corrupt,
                    "Corrupt context must be distinguishable from an offline Bridge.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("NXKEYS_BRIDGE_ROOT", previousBridgeRoot);
            }
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private static string FindRepositoryProfileFile()
    {
        try { return FindRepositoryFile(Path.Combine("config", "nx2512-v8-profile.json")); }
        catch (FileNotFoundException) { return FindRepositoryFile(Path.Combine("config", "nx2512-pro-hybrid.json")); }
    }

    private static string FindRepositoryFile(string relativePath)
    {
        string current = Directory.GetCurrentDirectory();
        for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            string candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = Path.GetDirectoryName(current);
        }
        throw new FileNotFoundException("Repository file was not found.", relativePath);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void VerifySketchIntentGrammar()
    {
        var commands = new List<ModuleCommand>
        {
            SketchCommand("UG_SKETCH_LINE", "Line", new[] { "C", "L" }, "K5", 1),
            SketchCommand("UG_SKETCH_LINE_BY_TWO_POINTS", "Line by Two Points", new[] { "C", "L", "2" }, "K3", 2),
            SketchCommand("UG_SKETCH_RECTANGLE", "Rectangle", new[] { "C", "R" }, "K5", 3),
            SketchCommand("UG_SKETCH_TRIM", "Trim", new[] { "E", "T" }, "K5", 4),
            SketchCommand("UG_MODELING_CHAMFER_FEATURE", "Sketch Chamfer", new[] { "C", "G", "C", "H" }, "K3", 5),
            SketchCommand("UG_SKETCH_STUDIO_SPLINE", "Studio Spline", new[] { "M", "Z", "S" }, "K4", 6),
            SketchCommand("UG_SKETCH_USER_CUSTOM", "User Custom", new[] { "U", "S", "X" }, "K3", 7, true)
        };
        commands[0].Aliases = new List<List<string>> { new List<string> { "Q", "W" } };
        var module = new ModuleConfig
        {
            ID = "sketch",
            Label = "Sketch",
            Enabled = true,
            CommandSets = new List<ModuleCommandSet>
            {
                new ModuleCommandSet { ID = "sketch", Label = "Sketch", Commands = commands }
            }
        };

        MnemonicPathGenerator.Apply(new[] { module });
        Assert(PathOf(commands, "UG_SKETCH_LINE").SequenceEqual(new[] { "C", "L" }),
            "Line must use Create -> Line.");
        Assert(PathOf(commands, "UG_SKETCH_LINE_BY_TWO_POINTS").SequenceEqual(new[] { "C", "V", "L", "2" }),
            "Line variants must live under the explicit variant branch.");
        Assert(PathOf(commands, "UG_SKETCH_RECTANGLE").SequenceEqual(new[] { "C", "R" }),
            "Rectangle must use Create -> Rectangle.");
        Assert(PathOf(commands, "UG_SKETCH_TRIM").SequenceEqual(new[] { "E", "T" }),
            "Trim must use Edit -> Trim.");
        Assert(PathOf(commands, "UG_MODELING_CHAMFER_FEATURE").SequenceEqual(new[] { "E", "H" }),
            "The shared NX chamfer BUTTON ID must keep Sketch semantics in Sketch context.");
        Assert(PathOf(commands, "UG_SKETCH_STUDIO_SPLINE").Take(2).SequenceEqual(new[] { "C", "G" }),
            "Unknown Sketch geometry must remain in the Create -> Geometry family.");
        Assert(PathOf(commands, "UG_SKETCH_USER_CUSTOM").SequenceEqual(new[] { "U", "S", "X" }),
            "User-locked paths must remain untouched.");
        Assert(commands.First(item => item.Command.ID == "UG_SKETCH_LINE").Aliases.Count == 0,
            "Legacy positional aliases must be removed from generated Sketch intents.");

        List<string> paths = commands.Select(item => string.Concat(item.Path)).OrderBy(value => value.Length).ToList();
        for (int left = 0; left < paths.Count; left++)
            for (int right = left + 1; right < paths.Count; right++)
                Assert(!paths[right].StartsWith(paths[left], StringComparison.OrdinalIgnoreCase),
                    "Sketch paths must remain prefix-free: " + paths[left] + " / " + paths[right]);
    }

    private static ModuleCommand SketchCommand(
        string id,
        string name,
        IEnumerable<string> path,
        string frequency,
        int order,
        bool locked = false) => new ModuleCommand
    {
        Enabled = true,
        Path = path.ToList(),
        PathLocked = locked,
        PathSource = locked ? "user" : "generated",
        Frequency = frequency,
        DisplayOrder = order,
        Command = new CommandRef { ID = id, Name = name }
    };

    private static IReadOnlyList<string> PathOf(IEnumerable<ModuleCommand> commands, string id) =>
        commands.First(item => string.Equals(item.Command.ID, id, StringComparison.OrdinalIgnoreCase)).Path;

    private static ModuleCommand Command(string id, string name, IEnumerable<string> path) => new ModuleCommand
    {
        Enabled = true,
        Path = path.ToList(),
        Command = new CommandRef { ID = id, Name = name }
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
