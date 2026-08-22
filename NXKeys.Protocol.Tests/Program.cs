using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NXKeys.Protocol;

namespace NXKeys.Protocol.Tests
{
    internal static class Program
    {
        private sealed class TestFailure : Exception
        {
            public TestFailure(string message) : base(message) { }
        }

        private static int failures;
        private static int total;

        private static int Main()
        {
            Run("Действия: известные поддерживаются, неизвестные нет", ActionsSupported);
            Run("Запрос: корректный execute_command проходит Validate", ValidExecuteValid);
            Run("Запрос: неизвестный action отклоняется", UnknownActionRejected);
            Run("Запрос: просроченный отклоняется", ExpiredRejected);
            Run("Запрос: destructive без подтверждения отклоняется", DestructiveWithoutConfirmRejected);
            Run("Запрос: run_capability требует capability_id", RunCapabilityRequiresId);
            Run("Запрос: run_capability с path traversal в payload_name отклоняется", RunCapabilityPathTraversalRejected);
            Run("Запрос: switch_module требует целевого приложения", SwitchModuleRequiresTarget);
            Run("Запрос: IsExpired по времени", IsExpiredDetected);
            Run("Аутентификатор: Sign заполняет конверт и HMAC", SignFillsEnvelope);
            Run("Аутентификатор: Verify принимает подписанный", VerifyAccepts);
            Run("Аутентификатор: Verify отклоняет неверный секрет", VerifyWrongSecret);
            Run("Аутентификатор: Verify отклоняет неверный session", VerifyWrongSession);
            Run("Аутентификатор: Verify отклоняет подделанный payload", VerifyTamperedPayload);
            Run("Аутентификатор: ValidateEnvelope отклоняет плохой session_id", EnvelopeBadSession);
            Run("Аутентификатор: ValidateEnvelope отклоняет неположительный sequence", EnvelopeBadSequence);
            Run("Аутентификатор: ComputeHmac требует 256-битный секрет", HmacShortSecretThrows);
            Run("Аутентификатор: CanonicalPayload детерминирован", CanonicalPayloadDeterministic);
            Run("ReplayGuard: повтор nonce отклоняется", ReplayNonceRejected);
            Run("ReplayGuard: немонотонный sequence отклоняется", ReplaySequenceRejected);
            Run("ReplayGuard: сброс sequence на 1 разрешён", ReplayRestartAllowed);
            Run("Права: PermissionKey детерминирован и канонизирует", PermissionKeyCanonicalizes);
            Run("Права: CanonicalApplicationId нормализует Sheet Metal", AppIdCanonical);
            Run("Контекст: SemanticFingerprint реагирует на выбор", FingerprintChangesOnSelection);
            Run("Контекст: IsFresh внутри окна", ContextFresh);
            Run("Контекст: IsFresh за пределами окна", ContextStale);
            Run("JSON: round-trip snake_case", JsonRoundTrip);
            Run("JSON: trailing comma отклоняется", JsonRejectsTrailingComma);
            Run("Результат: Success только для executed/completed", ResultSuccess);
            Run("Нормализация: v8-префиксы и синонимы", NormalizeModuleMappings);
            Run("Нормализация: разделяемые модули", SharedModuleDetection);
            Run("Маппинг: application id → module", ApplicationToModuleId);
            Run("Маппинг: window title → module", WindowTitleToModuleId);
            Run("Маппинг: module → application id", ModuleToApplicationId);
            Run("Маппинг: module → label", ModuleToLabel);

            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? "[OK] Все инварианты протокола выполнены (" + total + ")."
                : "[FAIL] Нарушено инвариантов: " + failures + " из " + total);
            return failures == 0 ? 0 : 1;
        }

        private static void Run(string name, Action action)
        {
            total++;
            try { action(); Console.WriteLine("  [PASS] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("  [FAIL] " + name + " => " + ex.Message); }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new TestFailure(message);
        }

        private static void AssertThrows<T>(Action action) where T : Exception
        {
            try { action(); throw new TestFailure("Ожидалось " + typeof(T).Name + ", но его нет."); }
            catch (T) { /* expected */ }
        }

        private static byte[] MakeSecret()
        {
            byte[] secret = new byte[64];
            for (int i = 0; i < secret.Length; i++) secret[i] = (byte)i;
            return secret;
        }

        private static byte[] MakeWrongSecret()
        {
            byte[] secret = new byte[64];
            for (int i = 0; i < secret.Length; i++) secret[i] = (byte)(255 - i);
            return secret;
        }

        private static string Sha256Hex(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static NxCommandRequest ValidRequest()
        {
            return new NxCommandRequest
            {
                RequestId = "req-1",
                Action = NxProtocolActions.ExecuteCommand,
                CommandId = "UG_MODELING_EXTRUDED_FEATURE",
                CommandName = "Extrude",
                Sequence = "M C E",
                ModuleId = "modeling",
                CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
                ExpectedSelectionCount = -1
            };
        }

        private static void ActionsSupported()
        {
            Assert(NxProtocolActions.IsSupported(NxProtocolActions.ExecuteCommand), "execute_command поддерживается.");
            Assert(NxProtocolActions.IsSupported("switch_module"), "switch_module поддерживается.");
            Assert(NxProtocolActions.IsSupported("set_selection_filter"), "set_selection_filter поддерживается.");
            Assert(NxProtocolActions.IsSupported("probe_command"), "probe_command поддерживается.");
            Assert(NxProtocolActions.IsSupported("run_capability"), "run_capability поддерживается.");
            Assert(!NxProtocolActions.IsSupported("delete_everything"), "неизвестное действие не поддерживается.");
        }

        private static void ValidExecuteValid()
        {
            var r = ValidRequest();
            r.Validate();
            Assert(true, "корректный execute_command проходит Validate.");
        }

        private static void UnknownActionRejected()
        {
            var r = ValidRequest();
            r.Action = "delete_everything";
            AssertThrows<InvalidOperationException>(() => r.Validate());
        }

        private static void ExpiredRejected()
        {
            var r = ValidRequest();
            r.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-5).ToString("O");
            AssertThrows<InvalidOperationException>(() => r.Validate());
        }

        private static void DestructiveWithoutConfirmRejected()
        {
            var r = ValidRequest();
            r.Destructive = true;
            r.ConfirmationAccepted = false;
            AssertThrows<InvalidOperationException>(() => r.Validate());
        }

        private static void RunCapabilityRequiresId()
        {
            var r = ValidRequest();
            r.Action = NxProtocolActions.RunCapability;
            r.CommandId = string.Empty;
            r.CapabilityId = string.Empty;
            AssertThrows<InvalidOperationException>(() => r.Validate());
        }

        private static void RunCapabilityPathTraversalRejected()
        {
            var r = ValidRequest();
            r.Action = NxProtocolActions.RunCapability;
            r.CommandId = string.Empty;
            r.CapabilityId = "nxeskd.generate";
            r.PayloadName = "../../evil.prt";
            AssertThrows<InvalidOperationException>(() => r.Validate());
        }

        private static void SwitchModuleRequiresTarget()
        {
            var r = ValidRequest();
            r.Action = NxProtocolActions.SwitchModule;
            r.CommandId = string.Empty;
            r.TargetApplicationId = string.Empty;
            AssertThrows<InvalidOperationException>(() => r.Validate());
        }

        private static void IsExpiredDetected()
        {
            var future = ValidRequest();
            future.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(60).ToString("O");
            Assert(!future.IsExpired, "будущее не истекло.");
            var past = ValidRequest();
            past.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O");
            Assert(past.IsExpired, "прошлое истекло.");
        }

        private static void SignFillsEnvelope()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            byte[] secret = MakeSecret();
            string profile = Sha256Hex("profile");
            NxRequestAuthenticator.Sign(r, session, client, secret, profile, 5);
            Assert(r.SessionId == session, "session записан.");
            Assert(r.ClientInstanceId == client, "client_instance записан.");
            Assert(Guid.TryParseExact(r.Nonce, "N", out _), "nonce — компактный GUID.");
            Assert(r.SequenceNumber == 5, "sequence_number записан.");
            Assert(!string.IsNullOrWhiteSpace(r.PayloadHmac), "payload_hmac заполнен.");
            NxRequestAuthenticator.ValidateEnvelope(r); // не должно бросить
        }

        private static void VerifyAccepts()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            byte[] secret = MakeSecret();
            string profile = Sha256Hex("profile");
            NxRequestAuthenticator.Sign(r, session, client, secret, profile, 5);
            string error;
            Assert(NxRequestAuthenticator.Verify(r, session, secret, profile, out error), "Verify для подписанного: " + error);
        }

        private static void VerifyWrongSecret()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            NxRequestAuthenticator.Sign(r, session, client, MakeSecret(), Sha256Hex("profile"), 5);
            string error;
            Assert(!NxRequestAuthenticator.Verify(r, session, MakeWrongSecret(), Sha256Hex("profile"), out error), "неверный секрет отклонён: " + error);
        }

        private static void VerifyWrongSession()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            NxRequestAuthenticator.Sign(r, session, client, MakeSecret(), Sha256Hex("profile"), 5);
            string error;
            Assert(!NxRequestAuthenticator.Verify(r, Guid.NewGuid().ToString("N"), MakeSecret(), Sha256Hex("profile"), out error), "неверный session отклонён: " + error);
        }

        private static void VerifyTamperedPayload()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            byte[] secret = MakeSecret();
            string profile = Sha256Hex("profile");
            NxRequestAuthenticator.Sign(r, session, client, secret, profile, 5);
            r.CommandId = "UG_MODELING_OTHER"; // подделка payload после подписи
            string error;
            Assert(!NxRequestAuthenticator.Verify(r, session, secret, profile, out error), "подделанный payload отклонён: " + error);
        }

        private static void EnvelopeBadSession()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            NxRequestAuthenticator.Sign(r, session, client, MakeSecret(), Sha256Hex("profile"), 5);
            r.SessionId = "not-a-guid";
            AssertThrows<InvalidOperationException>(() => NxRequestAuthenticator.ValidateEnvelope(r));
        }

        private static void EnvelopeBadSequence()
        {
            var r = ValidRequest();
            string session = Guid.NewGuid().ToString("N");
            string client = Guid.NewGuid().ToString("N");
            NxRequestAuthenticator.Sign(r, session, client, MakeSecret(), Sha256Hex("profile"), 5);
            r.SequenceNumber = 0;
            AssertThrows<InvalidOperationException>(() => NxRequestAuthenticator.ValidateEnvelope(r));
        }

        private static void HmacShortSecretThrows()
        {
            AssertThrows<InvalidOperationException>(() => NxRequestAuthenticator.ComputeHmac(ValidRequest(), new byte[4]));
        }

        private static void CanonicalPayloadDeterministic()
        {
            var a = ValidRequest();
            var b = ValidRequest();
            // ValidRequest() ставит CreatedUtc/ExpiresUtc = UtcNow — таймстемпы у a и b
            // различаются на мс, поэтому выравниваем их перед сравнением.
            b.CreatedUtc = a.CreatedUtc;
            b.ExpiresUtc = a.ExpiresUtc;
            Assert(NxRequestAuthenticator.CanonicalPayload(a) == NxRequestAuthenticator.CanonicalPayload(b), "одинаковые запросы — один payload.");
            a.CommandId = "UG_OTHER";
            Assert(NxRequestAuthenticator.CanonicalPayload(a) != NxRequestAuthenticator.CanonicalPayload(b), "изменение поля меняет payload.");
        }

        private static void ReplayNonceRejected()
        {
            var r = new NxCommandRequest
            {
                RequestId = "r1",
                Action = NxProtocolActions.ExecuteCommand,
                CommandId = "UG_X",
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
                ClientInstanceId = "client-A",
                Nonce = "n1",
                SequenceNumber = 1
            };
            var guard = new NxReplayGuard();
            string e;
            Assert(guard.TryAccept(r, out e), "первый запрос принят.");
            string e2;
            Assert(!guard.TryAccept(r, out e2), "повтор nonce отклонён.");
        }

        private static void ReplaySequenceRejected()
        {
            var guard = new NxReplayGuard();
            string e;
            Assert(guard.TryAccept(R("client-B", "n1", 5), out e), "seq=5 принят.");
            string e2;
            Assert(!guard.TryAccept(R("client-B", "n2", 3), out e2), "seq=3 после seq=5 отклонён (не монотонно).");
        }

        private static void ReplayRestartAllowed()
        {
            var guard = new NxReplayGuard();
            string e;
            Assert(guard.TryAccept(R("client-C", "n1", 4), out e), "seq=4 принят.");
            string e2;
            Assert(guard.TryAccept(R("client-C", "n2", 1), out e2), "сброс на seq=1 разрешён.");
        }

        private static NxCommandRequest R(string client, string nonce, long sequence)
        {
            return new NxCommandRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Action = NxProtocolActions.ExecuteCommand,
                CommandId = "UG_X",
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
                ClientInstanceId = client,
                Nonce = nonce,
                SequenceNumber = sequence
            };
        }

        private static void PermissionKeyCanonicalizes()
        {
            string k1 = NxBridgePermissionSet.PermissionKey("execute_command", "UG_SHEET_METAL_BEND", "sheet", "UG_APP_SHEETMETAL", "");
            string k2 = NxBridgePermissionSet.PermissionKey("execute_command", "UG_SHEET_METAL_BEND", "sheet", "UG_APP_SBSM", "");
            Assert(k1 == k2, "PermissionKey канонизирует application id (SHEETMETAL == SBSM).");
            string k3 = NxBridgePermissionSet.PermissionKey("execute_command", "UG_SKETCH_LINE", "sketch", "UG_APP_SKETCH", "");
            Assert(k1 != k3, "разная команда — разный ключ.");
        }

        private static void AppIdCanonical()
        {
            Assert(NxBridgePermissionSet.CanonicalApplicationId("UG_APP_SHEETMETAL") == "UG_APP_SBSM", "SHEETMETAL нормализуется в SBSM.");
            Assert(NxBridgePermissionSet.CanonicalApplicationId("UG_APP_MODELING") == "UG_APP_MODELING", "прочие без изменений.");
        }

        private static void FingerprintChangesOnSelection()
        {
            var c1 = new NxContextSnapshot
            {
                Status = "idle",
                ApplicationId = "UG_APP_MODELING",
                ModuleId = "modeling",
                SelectionCount = 0,
                SelectionState = "unknown",
                SelectionFingerprint = ""
            };
            var c1b = new NxContextSnapshot
            {
                Status = "idle",
                ApplicationId = "UG_APP_MODELING",
                ModuleId = "modeling",
                SelectionCount = 0,
                SelectionState = "unknown",
                SelectionFingerprint = ""
            };
            var c2 = new NxContextSnapshot
            {
                Status = "idle",
                ApplicationId = "UG_APP_MODELING",
                ModuleId = "modeling",
                SelectionCount = 1,
                SelectionState = "single",
                SelectionFingerprint = "f1"
            };
            Assert(c1.SemanticFingerprint() == c1b.SemanticFingerprint(), "одинаковое состояние — одинаковый fingerprint.");
            Assert(c1.SemanticFingerprint() != c2.SemanticFingerprint(), "изменение выбора меняет fingerprint.");
        }

        private static void ContextFresh()
        {
            var c = new NxContextSnapshot { UpdatedUtc = DateTimeOffset.UtcNow.ToString("O") };
            Assert(c.IsFresh, "свежий в окне по умолчанию (3с).");
            Assert(c.IsFreshFor(TimeSpan.FromSeconds(5)), "свежий в кастомном окне 5с.");
        }

        private static void ContextStale()
        {
            var c = new NxContextSnapshot { UpdatedUtc = DateTimeOffset.UtcNow.AddSeconds(-10).ToString("O") };
            Assert(!c.IsFresh, "устаревший за пределами окна по умолчанию.");
            Assert(c.IsFreshFor(TimeSpan.FromSeconds(20)), "свежий в большем кастомном окне.");
        }

        private static void JsonRoundTrip()
        {
            var r = ValidRequest();
            string json = System.Text.Json.JsonSerializer.Serialize(r, NxProtocolJson.WriteOptions);
            Assert(json.Contains("\"schema_version\""), "присутствует snake_case schema_version.");
            var back = System.Text.Json.JsonSerializer.Deserialize<NxCommandRequest>(json, NxProtocolJson.ReadOptions);
            Assert(back != null && back.RequestId == r.RequestId && back.Action == r.Action && back.CommandId == r.CommandId, "round-trip полей.");
        }

        private static void JsonRejectsTrailingComma()
        {
            string bad = "{ \"schema_version\": 4, \"request_id\": \"r\", }";
            AssertThrows<System.Text.Json.JsonException>(() =>
                System.Text.Json.JsonSerializer.Deserialize<NxCommandRequest>(bad, NxProtocolJson.ReadOptions));
        }

        private static void ResultSuccess()
        {
            var r = new NxCommandResult { Status = "executed" };
            Assert(r.Success, "executed — успех.");
            r.Status = "completed";
            Assert(r.Success, "completed — успех.");
            r.Status = "interrupted_unknown";
            Assert(!r.Success, "interrupted_unknown — не успех.");
        }

        private static void NormalizeModuleMappings()
        {
            Assert(NxContextNormalization.NormalizeModule("v8_m") == "modeling", "v8_m → modeling.");
            Assert(NxContextNormalization.NormalizeModule("v8_s") == "sketch", "v8_s → sketch.");
            Assert(NxContextNormalization.NormalizeModule("v8_sm") == "sheet_metal", "v8_sm → sheet_metal.");
            Assert(NxContextNormalization.NormalizeModule("v8_x") == "modeling", "неизвестный v8-суффикс → modeling.");
            Assert(NxContextNormalization.NormalizeModule("View") == "inspect_view", "View → inspect_view.");
            Assert(NxContextNormalization.NormalizeModule("cam") == "manufacturing", "cam → manufacturing.");
            Assert(NxContextNormalization.NormalizeModule("CAM / Manufacturing") == "manufacturing", "CAM / Manufacturing → manufacturing.");
            Assert(NxContextNormalization.NormalizeModule("Modeling") == "modeling", "Modeling → modeling (регистр).");
            Assert(NxContextNormalization.NormalizeModule("selection_filters") == "selection_object", "selection_filters → selection_object.");
            Assert(NxContextNormalization.NormalizeModule("inspect / view") == "inspect_view", "inspect / view → inspect_view.");
        }

        private static void SharedModuleDetection()
        {
            Assert(NxContextNormalization.IsSharedModule("inspect_view"), "inspect_view — разделяемый.");
            Assert(NxContextNormalization.IsSharedModule("selection_object"), "selection_object — разделяемый.");
            Assert(NxContextNormalization.IsSharedModule("reuse"), "reuse — разделяемый.");
            Assert(NxContextNormalization.IsSharedModule("inspect"), "inspect → нормализуется в inspect_view — разделяемый.");
            Assert(!NxContextNormalization.IsSharedModule("modeling"), "modeling — не разделяемый.");
        }

        private static void ApplicationToModuleId()
        {
            Assert(NxContextNormalization.ModuleIdFromApplication("UG_APP_MODELING") == "modeling", "MODELING → modeling.");
            Assert(NxContextNormalization.ModuleIdFromApplication("UG_APP_DRAFTING") == "drafting", "DRAFTING → drafting.");
            Assert(NxContextNormalization.ModuleIdFromApplication("UG_APP_SHEETMETAL") == "sheet_metal", "SHEETMETAL → sheet_metal.");
            Assert(NxContextNormalization.ModuleIdFromApplication("UG_APP_SFEM") == "simulation", "SFEM → simulation.");
            Assert(NxContextNormalization.ModuleIdFromApplication("UG_APP_MOLDWIZARD") == "mold", "MOLDWIZARD → mold.");
            Assert(NxContextNormalization.ModuleIdFromApplication("UG_APP_GATEWAY") == "inspect_view", "GATEWAY → inspect_view.");
        }

        private static void WindowTitleToModuleId()
        {
            Assert(NxContextNormalization.ModuleIdFromWindowTitle("Siemens NX - Modeling") == "modeling", "заголовок с 'model' → modeling.");
            Assert(NxContextNormalization.ModuleIdFromWindowTitle("NX Sheet Metal") == "sheet_metal", "заголовок с 'sheet' → sheet_metal.");
            Assert(NxContextNormalization.ModuleIdFromWindowTitle("Эскиз (Sketch)") == "sketch", "RU 'эскиз' → sketch.");
            Assert(NxContextNormalization.ModuleIdFromWindowTitle("NX Drafting") == "drafting", "заголовок с 'draft' → drafting.");
            Assert(NxContextNormalization.ModuleIdFromWindowTitle("что-то про pmi") == "pmi", "заголовок с 'pmi' → pmi.");
            Assert(NxContextNormalization.ModuleIdFromWindowTitle("unknown window") == "inspect_view", "неизвестный заголовок → inspect_view.");
        }

        private static void ModuleToApplicationId()
        {
            Assert(NxContextNormalization.ApplicationIdFromModuleId("modeling") == "UG_APP_MODELING", "modeling → UG_APP_MODELING.");
            Assert(NxContextNormalization.ApplicationIdFromModuleId("sheet_metal") == "UG_APP_SHEETMETAL", "sheet_metal → UG_APP_SHEETMETAL.");
            Assert(NxContextNormalization.ApplicationIdFromModuleId("sketch") == "UG_APP_SKETCH", "sketch → UG_APP_SKETCH.");
            Assert(NxContextNormalization.ApplicationIdFromModuleId("pmi") == "UG_APP_PMI", "pmi → UG_APP_PMI.");
            Assert(NxContextNormalization.ApplicationIdFromModuleId("unknown") == "UG_APP_GATEWAY", "unknown → UG_APP_GATEWAY.");
            // round-trip через ModuleIdFromApplication
            Assert(NxContextNormalization.ApplicationIdFromModuleId(
                NxContextNormalization.ModuleIdFromApplication("UG_APP_DRAFTING")) == "UG_APP_DRAFTING", "round-trip DRAFTING.");
        }

        private static void ModuleToLabel()
        {
            Assert(NxContextNormalization.ModuleLabelFromModule("modeling") == "Modeling", "modeling → Modeling.");
            Assert(NxContextNormalization.ModuleLabelFromModule("sheet_metal") == "Sheet Metal", "sheet_metal → Sheet Metal.");
            Assert(NxContextNormalization.ModuleLabelFromModule("drafting") == "Drafting", "drafting → Drafting.");
            Assert(NxContextNormalization.ModuleLabelFromModule("whatever") == "Inspect / View", "unknown → Inspect / View.");
        }
    }
}
