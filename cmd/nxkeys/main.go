package main

import (
	"context"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"

	"github.com/homiakus/nxkeys/internal/backup"
	"github.com/homiakus/nxkeys/internal/config"
	"github.com/homiakus/nxkeys/internal/defaults"
	"github.com/homiakus/nxkeys/internal/engine"
	"github.com/homiakus/nxkeys/internal/tui"
)

const version = "0.1.0"

func main() {
	if err := run(os.Args[1:]); err != nil {
		fmt.Fprintln(os.Stderr, "error:", err)
		os.Exit(1)
	}
}

func run(args []string) error {
	configPath, args, err := extractConfigPath(args)
	if err != nil {
		return err
	}
	if len(args) == 0 {
		args = []string{"tui"}
	}
	command := strings.ToLower(args[0])
	commandArgs := args[1:]

	switch command {
	case "help", "-h", "--help":
		printUsage()
		return nil
	case "version", "--version", "-v":
		fmt.Println("nxkeys", version)
		return nil
	case "init":
		return initConfig(configPath, commandArgs)
	}

	cfg, err := config.Load(configPath)
	if err != nil {
		if errors.Is(err, os.ErrNotExist) {
			return fmt.Errorf("config not found: %s; run nxkeys --config %s init", configPath, configPath)
		}
		return err
	}

	switch command {
	case "tui":
		return tui.Run(configPath, cfg)
	case "validate":
		fmt.Printf("Configuration is valid: %s (%s)\n", cfg.Profile.Name, configPath)
		return nil
	case "scan":
		return scanCommand(cfg, commandArgs)
	case "plan":
		return planCommand(cfg, commandArgs)
	case "catalog":
		return catalogCommand(cfg, commandArgs)
	case "apply":
		return applyCommand(cfg, commandArgs)
	case "backups":
		return backupsCommand(cfg)
	case "restore":
		return restoreCommand(cfg, commandArgs)
	default:
		return fmt.Errorf("unknown command %q", command)
	}
}

func extractConfigPath(args []string) (string, []string, error) {
	path := "nx2512-ergo-80.json"
	var rest []string
	for i := 0; i < len(args); i++ {
		arg := args[i]
		if arg == "--config" || arg == "-c" {
			if i+1 >= len(args) {
				return "", nil, errors.New("--config requires a path")
			}
			path = args[i+1]
			i++
			continue
		}
		if strings.HasPrefix(arg, "--config=") {
			path = strings.TrimPrefix(arg, "--config=")
			continue
		}
		rest = append(rest, arg)
	}
	return config.ExpandPath(path), rest, nil
}

func initConfig(path string, args []string) error {
	flags := flag.NewFlagSet("init", flag.ContinueOnError)
	force := flags.Bool("force", false, "overwrite existing config")
	if err := flags.Parse(args); err != nil {
		return err
	}
	if _, err := os.Stat(path); err == nil && !*force {
		return fmt.Errorf("config already exists: %s; pass --force to replace", path)
	}
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil && filepath.Dir(path) != "." {
		return err
	}
	if err := os.WriteFile(path, defaults.ConfigJSON, 0o644); err != nil {
		return err
	}
	fmt.Println("Created", path)
	return nil
}

func scanCommand(cfg config.Config, args []string) error {
	flags := flag.NewFlagSet("scan", flag.ContinueOnError)
	jsonOutput := flags.Bool("json", false, "print JSON")
	timeout := flags.Duration("timeout", 90*time.Second, "scan timeout")
	if err := flags.Parse(args); err != nil {
		return err
	}
	ctx, cancel := context.WithTimeout(context.Background(), *timeout)
	defer cancel()
	runtimeState, err := engine.Analyze(ctx, cfg)
	if err != nil {
		return err
	}
	if *jsonOutput {
		data, err := json.MarshalIndent(runtimeState, "", "  ")
		if err != nil {
			return err
		}
		fmt.Println(string(data))
		return nil
	}
	printAnalysis(runtimeState)
	return nil
}

func planCommand(cfg config.Config, args []string) error {
	flags := flag.NewFlagSet("plan", flag.ContinueOnError)
	jsonOutput := flags.Bool("json", false, "print JSON")
	if err := flags.Parse(args); err != nil {
		return err
	}
	runtimeState, err := analyzeDefault(cfg)
	if err != nil {
		return err
	}
	plan, err := engine.BuildPlan(runtimeState)
	if err != nil {
		return err
	}
	if *jsonOutput {
		data, err := engine.PlanJSON(plan)
		if err != nil {
			return err
		}
		fmt.Println(string(data))
		return nil
	}
	printPlan(plan)
	return nil
}

func catalogCommand(cfg config.Config, args []string) error {
	flags := flag.NewFlagSet("catalog", flag.ContinueOnError)
	query := flags.String("query", "", "filter by command ID, label or synonym")
	jsonOutput := flags.Bool("json", false, "print JSON")
	limit := flags.Int("limit", 100, "maximum rows")
	if err := flags.Parse(args); err != nil {
		return err
	}
	runtimeState, err := analyzeDefault(cfg)
	if err != nil {
		return err
	}
	type row struct {
		ID           string   `json:"id"`
		Labels       []string `json:"labels"`
		Synonyms     []string `json:"synonyms"`
		Accelerators []string `json:"accelerators"`
		Sources      []string `json:"sources"`
	}
	needle := strings.ToLower(strings.TrimSpace(*query))
	var rows []row
	for _, command := range runtimeState.Catalog.Commands {
		haystack := strings.ToLower(strings.Join(append(append([]string{command.ID}, command.Labels...), command.Synonyms...), " "))
		if needle != "" && !strings.Contains(haystack, needle) {
			continue
		}
		rows = append(rows, row{ID: command.ID, Labels: command.Labels, Synonyms: command.Synonyms, Accelerators: command.Accelerators, Sources: command.Sources})
	}
	sort.Slice(rows, func(i, j int) bool { return rows[i].ID < rows[j].ID })
	if *limit > 0 && len(rows) > *limit {
		rows = rows[:*limit]
	}
	if *jsonOutput {
		data, err := json.MarshalIndent(rows, "", "  ")
		if err != nil {
			return err
		}
		fmt.Println(string(data))
		return nil
	}
	for _, item := range rows {
		fmt.Printf("%-42s %-36s %s\n", item.ID, strings.Join(item.Labels, " | "), strings.Join(item.Accelerators, ", "))
	}
	fmt.Printf("Displayed %d command(s).\n", len(rows))
	return nil
}

func applyCommand(cfg config.Config, args []string) error {
	flags := flag.NewFlagSet("apply", flag.ContinueOnError)
	yes := flags.Bool("yes", false, "apply without interactive confirmation")
	dryRun := flags.Bool("dry-run", cfg.Deployment.DryRun, "show changes without writing")
	if err := flags.Parse(args); err != nil {
		return err
	}
	runtimeState, err := analyzeDefault(cfg)
	if err != nil {
		return err
	}
	plan, err := engine.BuildPlan(runtimeState)
	if err != nil {
		return err
	}
	printPlan(plan)
	if !*yes && !*dryRun {
		return errors.New("refusing non-interactive apply without --yes; use the TUI for confirmation")
	}
	result, err := engine.Apply(plan, cfg, *dryRun)
	if err != nil {
		return err
	}
	if result.DryRun {
		fmt.Printf("Dry-run: %d files would change, %d are already current.\n", len(result.Changed), len(result.Skipped))
		return nil
	}
	fmt.Printf("Applied %d changes. Backup ID: %s\n", len(result.Changed), result.BackupID)
	for _, path := range result.Changed {
		fmt.Println("  changed", path)
	}
	return nil
}

func backupsCommand(cfg config.Config) error {
	manifests, err := backup.List(cfg.Deployment.BackupRoot)
	if err != nil {
		return err
	}
	if len(manifests) == 0 {
		fmt.Println("No backups found in", cfg.Deployment.BackupRoot)
		return nil
	}
	for _, manifest := range manifests {
		fmt.Printf("%s  %s  %d file(s)  %s\n", manifest.ID, manifest.CreatedAt.Format(time.RFC3339), len(manifest.Entries), manifest.Profile)
	}
	return nil
}

func restoreCommand(cfg config.Config, args []string) error {
	flags := flag.NewFlagSet("restore", flag.ContinueOnError)
	id := flags.String("id", "", "backup ID; default is latest")
	force := flags.Bool("force", false, "restore even if files changed after apply")
	yes := flags.Bool("yes", false, "confirm restore")
	if err := flags.Parse(args); err != nil {
		return err
	}
	if !*yes {
		return errors.New("restore requires --yes")
	}
	if *id == "" {
		restored, err := engine.RestoreLatest(cfg, *force)
		if err != nil {
			return err
		}
		fmt.Println("Restored", restored)
		return nil
	}
	manifest, err := backup.Load(cfg.Deployment.BackupRoot, *id)
	if err != nil {
		return err
	}
	if err := backup.Restore(manifest, *force); err != nil {
		return err
	}
	fmt.Println("Restored", manifest.ID)
	return nil
}

func analyzeDefault(cfg config.Config) (engine.Runtime, error) {
	ctx, cancel := context.WithTimeout(context.Background(), 90*time.Second)
	defer cancel()
	return engine.Analyze(ctx, cfg)
}

func printAnalysis(runtimeState engine.Runtime) {
	fmt.Printf("Profile: %s (NX %s)\n", runtimeState.Config.Profile.Name, runtimeState.Config.Profile.NXVersion)
	fmt.Printf("Installations: %d\n", len(runtimeState.Discovery.Installations))
	for _, installation := range runtimeState.Discovery.Installations {
		fmt.Printf("  NX %s  %s\n", installation.Version, installation.Executable)
	}
	fmt.Printf("Candidate files: %d\n", len(runtimeState.Discovery.Files))
	fmt.Printf("Catalog commands: %d\n", len(runtimeState.Catalog.Commands))
	for _, resolution := range runtimeState.Resolutions {
		fmt.Printf("  %-14s %-10s %-32s -> %s\n", resolution.Binding.Shortcut, resolution.Status, resolution.Binding.Command.Name, resolution.CommandID)
	}
	for _, warning := range runtimeState.Warnings {
		fmt.Println("warning:", warning)
	}
}

func printPlan(plan engine.Plan) {
	fmt.Printf("Plan: %d action(s); resolved=%d unresolved=%d ambiguous=%d\n", len(plan.Actions), plan.Resolved, plan.Unresolved, plan.Ambiguous)
	for _, action := range engine.SortedActions(plan) {
		marker := "same"
		if action.WillChange {
			marker = "change"
		}
		fmt.Printf("  %-6s %-11s %s\n", marker, action.Kind, action.Path)
	}
	for _, warning := range plan.Warnings {
		fmt.Println("warning:", warning)
	}
}

func printUsage() {
	fmt.Printf(`NXKeys %s — safe Siemens NX 2512 shortcut configurator

Usage:
  nxkeys [--config FILE]                 launch TUI
  nxkeys [--config FILE] init [--force]  create JSON config
  nxkeys [--config FILE] validate        validate config
  nxkeys [--config FILE] scan [--json]   discover NX files and resolve commands
  nxkeys [--config FILE] plan [--json]   show planned changes
  nxkeys [--config FILE] catalog [--query TEXT] [--json]
  nxkeys [--config FILE] apply --yes     apply with backups
  nxkeys [--config FILE] apply --dry-run preview changes
  nxkeys [--config FILE] backups         list backups
  nxkeys [--config FILE] restore --yes [--id ID] [--force]
  nxkeys version

`, version)
}
