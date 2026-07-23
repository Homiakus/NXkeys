using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NXKeys.Protocol;

namespace NXKeys.StateMachines.Tests
{
    internal static class ProtocolInvariantTests
    {
        [ModuleInitializer]
        internal static void Run()
        {
            NxCommandRequest request = ValidRequest();
            string json = JsonSerializer.Serialize(request, NxProtocolJson.WriteOptions);
            Require(json.Contains("\"command_id\"", StringComparison.Ordinal), "Protocol must serialize command_id.");
            Require(json.Contains("\"expected_context_revision\"", StringComparison.Ordinal), "Protocol must serialize expected_context_revision.");
            Require(json.Contains("\"confirmation_accepted\"", StringComparison.Ordinal), "Protocol must serialize confirmation_accepted.");

            NxCommandRequest roundTrip = JsonSerializer.Deserialize<NxCommandRequest>(json, NxProtocolJson.ReadOptions);
            Require(roundTrip != null && roundTrip.CommandId == request.CommandId, "Protocol round-trip failed.");

            NxCommandRequest expired = ValidRequest();
            expired.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O");
            RequireThrows(expired.Validate, "Expired request must be rejected.");

            NxCommandRequest destructive = ValidRequest();
            destructive.Destructive = true;
            destructive.ConfirmationAccepted = false;
            RequireThrows(destructive.Validate, "Unconfirmed destructive request must be rejected.");

            destructive.ConfirmationAccepted = true;
            destructive.Validate();
            Console.WriteLine("[OK] Протокол: snake_case, expiry и destructive confirmation.");
        }

        private static NxCommandRequest ValidRequest()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new NxCommandRequest
            {
                SchemaVersion = NxProtocolConstants.SchemaVersion,
                RequestId = Guid.NewGuid().ToString("N"),
                Action = "execute_command",
                CommandId = "UG_TEST_COMMAND",
                CommandName = "Test",
                CreatedUtc = now.ToString("O"),
                ExpiresUtc = now.AddSeconds(15).ToString("O"),
                ExpectedContextRevision = 1,
                ExpectedSelectionCount = 0,
                ConfirmationAccepted = true
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void RequireThrows(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
