package engine

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
	"time"

	"github.com/homiakus/nxkeys/internal/backup"
	"github.com/homiakus/nxkeys/internal/config"
	"github.com/homiakus/nxkeys/internal/discovery"
	"github.com/homiakus/nxkeys/internal/nxmenu"
)

type Runtime struct {
	Config      config.Config               `json:"config"`
	Discovery   discovery.Result            `json:"discovery"`
	Catalog     nxmenu.Catalog              `json:"catalog"`
	Resolutions []nxmenu.Resolution         `json:"resolutions"`
	Conflicts   map[string][]nxmenu.Command `json:"conflicts"`
	Warnings    []string                    `json:"warnings"`
}

type ActionKind string

const (
	WriteFile ActionKind = "write-file"
	CopyFile  ActionKind = "copy-file"
)

type Action struct {
	Kind        ActionKind `json:"kind"`
	Path        string     `json:"path"`
	Source      string     `json:"source,omitempty"`
	Description string     `json:"description"`
	Content     []byte     `json:"-"`
	Mode        uint32     `json:"mode"`
	WillChange  bool       `json:"will_change"`
	BeforeHash  string     `json:"before_hash,omitempty"`
	AfterHash   string     `json:"after_hash,omitempty"`
}

type Plan struct {
	CreatedAt  time.Time `json:"created_at"`
	Profile    string    `json:"profile"`
	Actions    []Action  `json:"actions"`
	Warnings   []string  `json:"warnings"`
	Unresolved int       `json:"unresolved"`
	Ambiguous  int       `json:"ambiguous"`
	Resolved   int       `json:"resolved"`
}

type ApplyResult struct {
	BackupID string   `json:"backup_id"`
	Changed  []string `json:"changed"`
	Skipped  []string `json:"skipped"`
	DryRun   bool     `json:"dry_run"`
}

func Analyze(ctx context.Context, cfg config.Config) (Runtime, error) {
	found, err := discovery.Scan(ctx, cfg)
	if err != nil {
		return Runtime{}, err
	}
	var menuPaths []string
	for _, file := range found.Files {
		if file.Kind == discovery.KindMenu && file.Readable {
			menuPaths = append(menuPaths, file.Path)
		}
	}
	catalog, warnings := nxmenu.BuildCatalogCached(menuPaths, cfg.Profile.NXVersion, cfg.Performance.CatalogCacheEnabled)
	resolutions := catalog.ResolveBindings(cfg.Keyboard)
	conflicts := map[string][]nxmenu.Command{}
	for _, resolution := range resolutions {
		if resolution.Status == nxmenu.Resolved {
			conflicts[resolution.Binding.Shortcut] = catalog.Conflicts(resolution.Binding.Shortcut, resolution.CommandID)
		}
	}
	return Runtime{
		Config:      cfg,
		Discovery:   found,
		Catalog:     catalog,
		Resolutions: resolutions,
		Conflicts:   conflicts,
		Warnings:    append(found.Warnings, warnings...),
	}, nil
}

func BuildPlan(runtimeState Runtime) (Plan, error) {
	cfg := runtimeState.Config
	plan := Plan{CreatedAt: time.Now(), Profile: cfg.Profile.Name}
	for _, resolution := range runtimeState.Resolutions {
		switch resolution.Status {
		case nxmenu.Resolved:
			plan.Resolved++
		case nxmenu.Ambiguous:
			plan.Ambiguous++
		case nxmenu.Unresolved:
			if resolution.Binding.Enabled {
				plan.Unresolved++
			}
		}
	}
	if plan.Resolved == 0 {
		return plan, errors.New("no keyboard bindings were resolved; scan the NX installation or set command.id values in JSON")
	}
	if plan.Unresolved > 0 || plan.Ambiguous > 0 {
		plan.Warnings = append(plan.Warnings, "some bindings are unresolved or ambiguous; they will be omitted from the generated MenuScript overlay")
	}
	managedRoot := cfg.Deployment.ManagedRoot
	customRoot := filepath.Join(managedRoot, "custom")
	startupDir := filepath.Join(customRoot, "startup")
	overlayPath := filepath.Join(startupDir, cfg.Deployment.OverlayFilename)
	overlay := nxmenu.GenerateOverlay(cfg.Deployment.MenuScriptVersion, cfg.Deployment.MainMenubarID, runtimeState.Resolutions, runtimeState.Conflicts, cfg.Deployment.ClearDetectedConflicts)
	plan.Actions = append(plan.Actions, makeWriteAction(overlayPath, overlay, 0o644, "Generate NX MenuScript accelerator overlay"))

	customDirsPath := filepath.Join(managedRoot, "custom_dirs.dat")
	customDirsContent := []byte(filepath.Clean(customRoot) + "\r\n")
	if cfg.Deployment.Mode == "existing-custom-dirs" || cfg.Deployment.PatchExistingCustomDirs {
		existing := cfg.Deployment.ExistingCustomDirsFile
		if existing == "" && len(runtimeState.Discovery.CustomDirsFiles) > 0 {
			existing = runtimeState.Discovery.CustomDirsFiles[0]
		}
		if existing == "" {
			plan.Warnings = append(plan.Warnings, "no existing custom_dirs file found; falling back to managed custom_dirs.dat and launcher wrapper")
		} else {
			content, err := appendUniquePath(existing, customRoot)
			if err != nil {
				return plan, err
			}
			customDirsPath = existing
			customDirsContent = content
		}
	}
	plan.Actions = append(plan.Actions, makeWriteAction(customDirsPath, customDirsContent, 0o644, "Register managed NXKeys custom directory"))

	resolutionReport := nxmenu.ResolutionReportMarkdown(runtimeState.Resolutions, runtimeState.Conflicts)
	plan.Actions = append(plan.Actions, makeWriteAction(filepath.Join(managedRoot, "resolution-report.md"), resolutionReport, 0o644, "Write command resolution report"))
	radialMarkdown := nxmenu.RadialPlanMarkdown(cfg.Profile, cfg.Radials)
	radialJSON := nxmenu.RadialPlanJSON(cfg.Radials)
	if len(cfg.Modules) > 0 {
		radialMarkdown = nxmenu.ModuleRadialPlanMarkdown(cfg.Profile, cfg.Modules, cfg.Radials)
		radialJSON = nxmenu.ModuleRadialPlanJSON(cfg.Modules, cfg.Radials)
	}
	plan.Actions = append(plan.Actions, makeWriteAction(filepath.Join(managedRoot, "radial-menu-plan.md"), radialMarkdown, 0o644, "Write modular radial menu deployment checklist"))
	plan.Actions = append(plan.Actions, makeWriteAction(filepath.Join(managedRoot, "radial-menu-plan.json"), radialJSON, 0o644, "Write machine-readable modular radial menu plan"))

	executable := cfg.Deployment.NXExecutable
	if executable == "" {
		executable = chooseExecutable(runtimeState.Discovery.Installations, cfg.Profile.NXVersion)
	}
	versionMajor := cfg.Profile.NXVersion
	if parts := strings.Split(versionMajor, "."); len(parts) > 0 {
		versionMajor = parts[0]
	}
	wrapperName := fmt.Sprintf("launch-nx%s-with-nxkeys.cmd", versionMajor)
	wrapperPath := filepath.Join(managedRoot, wrapperName)
	wrapper := windowsWrapper(executable, customDirsPath, cfg.Profile.NXVersion)
	plan.Actions = append(plan.Actions, makeWriteAction(wrapperPath, wrapper, 0o755, "Generate NX launcher with UGII_CUSTOM_DIRECTORY_FILE"))
	if executable == "" {
		plan.Warnings = append(plan.Warnings, "NX executable was not found; set deployment.nx_executable in JSON before using the generated launcher")
	}

	if cfg.Role.Enabled {
		if _, err := os.Stat(cfg.Role.SourceMTX); err != nil {
			return plan, fmt.Errorf("role template is unavailable: %w", err)
		}
		targetDir := cfg.Role.TargetDirectory
		if targetDir == "" {
			targetDir = chooseRoleTarget(runtimeState.Discovery.ProfileDirs)
		}
		if targetDir == "" {
			return plan, errors.New("role deployment enabled but no target role directory found; set role_deployment.target_directory")
		}
		target := filepath.Join(targetDir, cfg.Role.TargetName)

		mtxContent, err := os.ReadFile(cfg.Role.SourceMTX)
		if err != nil {
			return plan, err
		}

		plan.Actions = append(plan.Actions, makeWriteAction(target, mtxContent, 0o644, "Deploy exported NX role containing radial menu layout"))
		if hasEnabledRadials(cfg.Radials) {
			plan.Warnings = append(plan.Warnings, "radial menus are not written into .mtx files; ensure role_deployment.source_mtx already contains the tested radial layout exported from NX")
		}
		if cfg.Role.SetAsDefault {
			plan.Warnings = append(plan.Warnings, fmt.Sprintf("set %s=%s in your NX launcher environment to make the deployed role default", cfg.Role.DefaultRoleEnv, target))
		}
	} else if hasEnabledRadials(cfg.Radials) || hasEnabledModuleRadials(cfg.Modules) {
		plan.Warnings = append(plan.Warnings, "radial menus require role_deployment with an exported .mtx template; active user.mtx files will not be modified")
	}

	for i := range plan.Actions {
		refreshActionState(&plan.Actions[i])
	}
	plan.Warnings = append(plan.Warnings, runtimeState.Warnings...)
	return plan, nil
}

func Apply(plan Plan, cfg config.Config, dryRun bool) (ApplyResult, error) {
	result := ApplyResult{DryRun: dryRun}
	if cfg.Deployment.RequireNXStopped && nxIsRunning() {
		return result, errors.New("NX appears to be running; close NX before applying configuration or disable deployment.require_nx_stopped")
	}
	if dryRun {
		for _, action := range plan.Actions {
			if action.WillChange {
				result.Changed = append(result.Changed, action.Path)
			} else {
				result.Skipped = append(result.Skipped, action.Path)
			}
		}
		return result, nil
	}
	manager := backup.Manager{Root: cfg.Deployment.BackupRoot}
	session, err := manager.New(cfg.Profile.Name)
	if err != nil {
		return result, err
	}
	for _, action := range plan.Actions {
		if !action.WillChange {
			result.Skipped = append(result.Skipped, action.Path)
			continue
		}
		entry, err := session.Capture(action.Path)
		if err != nil {
			return result, err
		}
		if err := executeAction(action, cfg.Deployment.AtomicWrites); err != nil {
			return result, err
		}
		entry.AfterSHA256, _ = backup.HashFile(action.Path)
		session.Add(entry)
		result.Changed = append(result.Changed, action.Path)
	}
	if err := session.Finalize(); err != nil {
		return result, err
	}
	result.BackupID = session.Manifest.ID
	return result, nil
}

func RestoreLatest(cfg config.Config, force bool) (string, error) {
	manifests, err := backup.List(cfg.Deployment.BackupRoot)
	if err != nil {
		return "", err
	}
	if len(manifests) == 0 {
		return "", errors.New("no backups found")
	}
	if err := backup.Restore(manifests[0], force); err != nil {
		return "", err
	}
	return manifests[0].ID, nil
}

func PlanJSON(plan Plan) ([]byte, error) {
	return json.MarshalIndent(plan, "", "  ")
}

func makeWriteAction(path string, content []byte, mode os.FileMode, description string) Action {
	return Action{Kind: WriteFile, Path: filepath.Clean(path), Content: content, Description: description, Mode: uint32(mode), AfterHash: backup.HashBytes(content)}
}

func makeCopyAction(source, path string, mode os.FileMode, description string) Action {
	content, _ := os.ReadFile(source)
	return Action{Kind: CopyFile, Source: filepath.Clean(source), Path: filepath.Clean(path), Content: content, Description: description, Mode: uint32(mode), AfterHash: backup.HashBytes(content)}
}

func refreshActionState(action *Action) {
	current, err := os.ReadFile(action.Path)
	if errors.Is(err, os.ErrNotExist) {
		action.WillChange = true
		return
	}
	if err != nil {
		action.WillChange = true
		return
	}
	action.BeforeHash = backup.HashBytes(current)
	action.WillChange = !bytes.Equal(current, action.Content)
}

func executeAction(action Action, atomic bool) error {
	if err := os.MkdirAll(filepath.Dir(action.Path), 0o755); err != nil {
		return err
	}
	mode := os.FileMode(action.Mode)
	if mode == 0 {
		mode = 0o644
	}
	if !atomic {
		return os.WriteFile(action.Path, action.Content, mode)
	}
	temp, err := os.CreateTemp(filepath.Dir(action.Path), ".nxkeys-*.tmp")
	if err != nil {
		return err
	}
	tempPath := temp.Name()
	defer os.Remove(tempPath)
	if _, err := temp.Write(action.Content); err != nil {
		_ = temp.Close()
		return err
	}
	if err := temp.Sync(); err != nil {
		_ = temp.Close()
		return err
	}
	if err := temp.Chmod(mode); err != nil {
		_ = temp.Close()
		return err
	}
	if err := temp.Close(); err != nil {
		return err
	}
	if runtime.GOOS == "windows" {
		bakPath := action.Path + ".bak"
		_ = os.Remove(bakPath) // Remove any old leftover backup
		hasOriginal := false
		if _, err := os.Stat(action.Path); err == nil {
			hasOriginal = true
			if err := os.Rename(action.Path, bakPath); err != nil {
				return err
			}
		}
		if err := os.Rename(tempPath, action.Path); err != nil {
			if hasOriginal {
				_ = os.Rename(bakPath, action.Path) // rollback original
			}
			return err
		}
		if hasOriginal {
			_ = os.Remove(bakPath) // cleanup backup
		}
		return nil
	}
	return os.Rename(tempPath, action.Path)
}

func appendUniquePath(path, customRoot string) ([]byte, error) {
	data, err := os.ReadFile(path)
	if err != nil && !errors.Is(err, os.ErrNotExist) {
		return nil, err
	}
	lineEnding := "\n"
	if bytes.Contains(data, []byte("\r\n")) {
		lineEnding = "\r\n"
	}
	needle := strings.ToLower(filepath.Clean(customRoot))
	for _, line := range strings.Split(strings.ReplaceAll(string(data), "\r\n", "\n"), "\n") {
		trimmed := strings.TrimSpace(strings.Trim(line, `"'`))
		if strings.ToLower(filepath.Clean(trimmed)) == needle {
			return data, nil
		}
	}
	if len(data) > 0 && !bytes.HasSuffix(data, []byte("\n")) {
		data = append(data, []byte(lineEnding)...)
	}
	data = append(data, []byte(filepath.Clean(customRoot)+lineEnding)...)
	return data, nil
}

func chooseExecutable(installations []discovery.Installation, preferredVersion string) string {
	for _, installation := range installations {
		if versionMatches(installation.Version, preferredVersion) && installation.Executable != "" {
			return installation.Executable
		}
	}
	for _, installation := range installations {
		if installation.Executable != "" {
			return installation.Executable
		}
	}
	return ""
}

func chooseRoleTarget(profileDirs []string) string {
	for _, profileDir := range profileDirs {
		for _, relative := range []string{"roles", "Roles"} {
			candidate := filepath.Join(profileDir, relative)
			if info, err := os.Stat(candidate); err == nil && info.IsDir() {
				return candidate
			}
		}
	}
	if len(profileDirs) > 0 {
		return filepath.Join(profileDirs[0], "roles")
	}
	return ""
}

func versionMatches(detected, preferred string) bool {
	d := strings.ToLower(strings.TrimPrefix(detected, "nx"))
	p := strings.ToLower(strings.TrimPrefix(preferred, "nx"))
	return d == p || (len(d) >= 4 && len(p) >= 4 && d[:4] == p[:4])
}

func windowsWrapper(executable, customDirsPath string, version string) []byte {
	versionMajor := version
	if parts := strings.Split(version, "."); len(parts) > 0 {
		versionMajor = parts[0]
	}
	if executable == "" {
		executable = fmt.Sprintf(`C:\Program Files\Siemens\NX%s\NXBIN\ugraf.exe`, versionMajor)
	}
	content := fmt.Sprintf(`@echo off
setlocal
set "UGII_CUSTOM_DIRECTORY_FILE=%s"
if not exist "%s" (
  echo NX executable was not found: %s
  echo Edit deployment.nx_executable in your configuration and re-apply.
  exit /b 2
)
start "Siemens NX %s + NXKeys" "%s" %%*
`, customDirsPath, executable, executable, version, executable)
	return []byte(content)
}

func hasEnabledRadials(radials []config.RadialMenu) bool {
	for _, radial := range radials {
		if radial.Enabled {
			return true
		}
	}
	return false
}

func hasEnabledModuleRadials(modules []config.ModuleConfig) bool {
	for _, module := range modules {
		if !module.Enabled {
			continue
		}
		if hasEnabledRadials(module.Radials) {
			return true
		}
	}
	return false
}

func nxIsRunning() bool {
	if runtime.GOOS != "windows" {
		return false
	}
	command := exec.Command("tasklist", "/FI", "IMAGENAME eq ugraf.exe", "/NH")
	output, err := command.Output()
	if err != nil {
		return false
	}
	return strings.Contains(strings.ToLower(string(output)), "ugraf.exe")
}

func SortedActions(plan Plan) []Action {
	actions := append([]Action(nil), plan.Actions...)
	sort.Slice(actions, func(i, j int) bool { return actions[i].Path < actions[j].Path })
	return actions
}
