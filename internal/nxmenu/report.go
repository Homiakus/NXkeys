package nxmenu

import (
	"encoding/json"
	"fmt"
	"sort"
	"strings"

	"github.com/homiakus/nxkeys/internal/config"
)

func RadialPlanMarkdown(profile config.Profile, radials []config.RadialMenu) []byte {
	var builder strings.Builder
	builder.WriteString("# NX radial menu deployment plan\n\n")
	builder.WriteString(fmt.Sprintf("Profile: **%s**  \nNX version: **%s**\n\n", profile.Name, profile.NXVersion))
	builder.WriteString("> NX radial toolbar layout is stored in the user role (.mtx). NXKeys does not rewrite opaque role files. Configure the layout manually once and export the role, or enable role_deployment with that exported .mtx template.\n\n")
	for _, radial := range radials {
		if !radial.Enabled {
			continue
		}
		builder.WriteString(fmt.Sprintf("## %s\n\n", radial.Name))
		builder.WriteString(fmt.Sprintf("Trigger: `%s`\n\n", radial.Trigger))
		builder.WriteString("| Direction | Command | Explicit ID | Notes |\n|---|---|---|---|\n")
		items := append([]config.RadialItem(nil), radial.Items...)
		sort.SliceStable(items, func(i, j int) bool { return directionOrder(items[i].Direction) < directionOrder(items[j].Direction) })
		for _, item := range items {
			builder.WriteString(fmt.Sprintf("| `%s` | %s | `%s` | %s |\n", item.Direction, escapePipe(item.Command.Name), item.Command.ID, escapePipe(item.Notes)))
		}
		builder.WriteString("\n")
	}
	return []byte(builder.String())
}

func ModuleRadialPlanMarkdown(profile config.Profile, modules []config.ModuleConfig, legacyRadials []config.RadialMenu) []byte {
	var builder strings.Builder
	builder.WriteString("# NX modular radial menu deployment plan\n\n")
	builder.WriteString(fmt.Sprintf("Profile: **%s**  \nNX version: **%s**\n\n", profile.Name, profile.NXVersion))
	builder.WriteString("> Active NX module chooses the visible command set. Direction semantics, selection priorities and confirmation rules stay consistent across modules. Native NX radial layouts are still deployed through an exported .mtx role; this file is the checked source of truth.\n\n")
	builder.WriteString("## Common direction semantics\n\n")
	builder.WriteString("| Direction | Meaning |\n|---|---|\n")
	for _, direction := range []string{"N", "NE", "E", "SE", "S", "SW", "W", "NW"} {
		builder.WriteString(fmt.Sprintf("| `%s` | %s |\n", direction, config.DefaultSlotSemantics()[direction]))
	}
	builder.WriteString("\n")

	for _, module := range modules {
		if !module.Enabled {
			continue
		}
		builder.WriteString(fmt.Sprintf("## %s (`%s`)\n\n", module.Label, module.ID))
		if len(module.NXApplicationIDs) > 0 {
			builder.WriteString(fmt.Sprintf("NX application ids: `%s`  \n", strings.Join(module.NXApplicationIDs, "`, `")))
		}
		if module.LeaderPrefix != "" {
			builder.WriteString(fmt.Sprintf("Leader prefix: `%s`  \n", module.LeaderPrefix))
		}
		if module.SwitchCommand.Name != "" || module.SwitchCommand.ID != "" {
			builder.WriteString(fmt.Sprintf("Switch command: %s `%s`  \n", escapePipe(module.SwitchCommand.Name), module.SwitchCommand.ID))
		}
		builder.WriteString("\n")

		for _, set := range module.CommandSets {
			if len(set.Commands) == 0 {
				continue
			}
			builder.WriteString(fmt.Sprintf("### %s\n\n", set.Label))
			builder.WriteString("| Slot | Command | Explicit ID | Selection | Confirmation |\n|---|---|---|---|---|\n")
			commands := append([]config.ModuleCommand(nil), set.Commands...)
			sort.SliceStable(commands, func(i, j int) bool { return directionOrder(commands[i].Slot) < directionOrder(commands[j].Slot) })
			for _, command := range commands {
				selection := ""
				if command.RequiresSelection {
					selection = "required"
				}
				confirmation := ""
				if command.ConfirmBeforeExecute || command.Destructive {
					confirmation = "confirm"
				}
				builder.WriteString(fmt.Sprintf("| `%s` | %s | `%s` | %s | %s |\n",
					command.Slot,
					escapePipe(command.Command.Name),
					command.Command.ID,
					selection,
					confirmation))
			}
			builder.WriteString("\n")
		}

		for _, radial := range module.Radials {
			writeRadial(&builder, radial)
		}
	}

	if len(legacyRadials) > 0 {
		builder.WriteString("## Legacy radials\n\n")
		for _, radial := range legacyRadials {
			writeRadial(&builder, radial)
		}
	}
	return []byte(builder.String())
}

func ModuleRadialPlanJSON(modules []config.ModuleConfig, legacyRadials []config.RadialMenu) []byte {
	payload := struct {
		Modules       []config.ModuleConfig `json:"modules"`
		LegacyRadials []config.RadialMenu   `json:"legacy_radials,omitempty"`
	}{
		Modules:       modules,
		LegacyRadials: legacyRadials,
	}
	data, _ := json.MarshalIndent(payload, "", "  ")
	return append(data, '\n')
}

func writeRadial(builder *strings.Builder, radial config.RadialMenu) {
	if !radial.Enabled {
		return
	}
	builder.WriteString(fmt.Sprintf("### %s\n\n", radial.Name))
	builder.WriteString(fmt.Sprintf("Trigger: `%s`", radial.Trigger))
	if radial.Module != "" {
		builder.WriteString(fmt.Sprintf("  \nModule: `%s`", radial.Module))
	}
	if radial.Kind != "" {
		builder.WriteString(fmt.Sprintf("  \nKind: `%s`", radial.Kind))
	}
	builder.WriteString("\n\n| Direction | Command | Explicit ID | Notes |\n|---|---|---|---|\n")
	items := append([]config.RadialItem(nil), radial.Items...)
	sort.SliceStable(items, func(i, j int) bool { return directionOrder(items[i].Direction) < directionOrder(items[j].Direction) })
	for _, item := range items {
		builder.WriteString(fmt.Sprintf("| `%s` | %s | `%s` | %s |\n", item.Direction, escapePipe(item.Command.Name), item.Command.ID, escapePipe(item.Notes)))
	}
	builder.WriteString("\n")
}

func RadialPlanJSON(radials []config.RadialMenu) []byte {
	data, _ := json.MarshalIndent(radials, "", "  ")
	return append(data, '\n')
}

func ResolutionReportMarkdown(resolutions []Resolution, conflicts map[string][]Command) []byte {
	var builder strings.Builder
	builder.WriteString("# NXKeys resolution report\n\n")
	builder.WriteString("| Shortcut | Requested command | Status | Resolved BUTTON | Existing conflicts |\n|---|---|---|---|---|\n")
	for _, resolution := range resolutions {
		var conflictNames []string
		for _, conflict := range conflicts[resolution.Binding.Shortcut] {
			conflictNames = append(conflictNames, fmt.Sprintf("%s (`%s`)", bestLabel(&conflict), conflict.ID))
		}
		builder.WriteString(fmt.Sprintf("| `%s` | %s | **%s** | `%s` | %s |\n",
			resolution.Binding.Shortcut,
			escapePipe(resolution.Binding.Command.Name),
			resolution.Status,
			resolution.CommandID,
			escapePipe(strings.Join(conflictNames, "; "))))
		if resolution.Status != Resolved {
			builder.WriteString(fmt.Sprintf("|  |  | Reason |  | %s |\n", escapePipe(resolution.Reason)))
			for _, candidate := range resolution.Candidates {
				builder.WriteString(fmt.Sprintf("|  | candidate | %.3f | `%s` | %s |\n", candidate.Score, candidate.ID, escapePipe(candidate.Label)))
			}
		}
	}
	return []byte(builder.String())
}

func directionOrder(direction string) int {
	switch strings.ToUpper(direction) {
	case "N":
		return 0
	case "NE":
		return 1
	case "E":
		return 2
	case "SE":
		return 3
	case "S":
		return 4
	case "SW":
		return 5
	case "W":
		return 6
	case "NW":
		return 7
	default:
		return 99
	}
}

func escapePipe(value string) string {
	return strings.ReplaceAll(value, "|", "\\|")
}
