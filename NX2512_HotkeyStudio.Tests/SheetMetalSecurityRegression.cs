using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NXKeys.Protocol;

internal static class SheetMetalSecurityRegression
{
    [ModuleInitializer]
    internal static void Verify()
    {
        const string v8 = """
        {
          "schema_version": 8,
          "operations": [
            {
              "operation_id": "sheetmetal.flange",
              "paths": { "leader": ["C", "F"], "secondary_aliases": [] },
              "command_name": "Flange",
              "adapter": {
                "kind": "button_id",
                "value": "UG_SHEET_METAL_FLANGE",
                "status": "tbd_adapter"
              },
              "availability": { "applications": ["sheetmetal"] }
            }
          ]
        }
        """;

        NxBridgePermissionSet permissions = NxBridgePermissionSet.FromProfileJson(v8);

        var execute = new NxCommandRequest
        {
            Action = NxProtocolActions.ExecuteCommand,
            CommandId = "UG_SBSM_FLANGE_FEATURE",
            ModuleId = "v8_h",
            TargetApplicationId = string.Empty,
            SelectionFilter = string.Empty
        };
        if (!permissions.TryGetPermission(execute, out NxCommandPermission executePermission) ||
            !string.Equals(executePermission.CommandId, "UG_SBSM_FLANGE_FEATURE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Legacy UG_SHEET_METAL_FLANGE must authorize canonical UG_SBSM_FLANGE_FEATURE.");

        var switchRequest = new NxCommandRequest
        {
            Action = NxProtocolActions.SwitchModule,
            CommandId = "UG_APP_SBSM",
            ModuleId = "v8_h",
            TargetApplicationId = "UG_APP_SBSM",
            SelectionFilter = string.Empty
        };
        if (!permissions.TryGetPermission(switchRequest, out NxCommandPermission switchPermission))
            throw new InvalidOperationException(
                "V8 sheetmetal availability must generate a UG_APP_SBSM switch permission.");

        if (!string.Equals(NxBridgePermissionSet.CanonicalApplicationId("UG_APP_SHEETMETAL"),
                "UG_APP_SBSM", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sheet Metal application alias canonicalization regressed.");

        string[] expected =
        {
            "UG_SBSM_FLANGE_FEATURE",
            "UG_APP_SBSM"
        };
        string[] actual = permissions.Permissions
            .Select(permission => permission.CommandId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        foreach (string id in expected)
            if (!actual.Contains(id, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Canonical permission is missing: " + id);
    }
}
