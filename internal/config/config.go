package config

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"runtime"
	"sort"
	"strings"
)

type Config struct {
	SchemaVersion int              `json:"schema_version"`
	Profile       Profile          `json:"profile"`
	Scan          ScanConfig       `json:"scan"`
	Deployment    DeploymentConfig `json:"deployment"`
	Keyboard      []Binding        `json:"keyboard"`
	Radials       []RadialMenu     `json:"radials"`
	Modules       []ModuleConfig   `json:"modules,omitempty"`
	Workflow      WorkflowControls `json:"workflow_controls,omitempty"`
	Performance   Performance      `json:"performance,omitempty"`
	Role          RoleDeployment   `json:"role_deployment"`
	LeaderKey     json.RawMessage  `json:"leader_key,omitempty"`
}

type Profile struct {
	Name        string `json:"name"`
	NXVersion   string `json:"nx_version"`
	Description string `json:"description"`
}

type ScanConfig struct {
	Roots              []string `json:"roots"`
	InstallHints       []string `json:"install_hints"`
	ProfileHints       []string `json:"profile_hints"`
	MenuExtensions     []string `json:"menu_extensions"`
	RoleExtensions     []string `json:"role_extensions"`
	LauncherExtensions []string `json:"launcher_extensions"`
	MaxDepth           int      `json:"max_depth"`
	MaxFiles           int      `json:"max_files"`
	FollowSymlinks     bool     `json:"follow_symlinks"`
}

type DeploymentConfig struct {
	Mode                    string `json:"mode"`
	ManagedRoot             string `json:"managed_root"`
	BackupRoot              string `json:"backup_root"`
	OverlayFilename         string `json:"overlay_filename"`
	MenuScriptVersion       int    `json:"menuscript_version"`
	MainMenubarID           string `json:"main_menubar_id"`
	NXExecutable            string `json:"nx_executable"`
	ExistingCustomDirsFile  string `json:"existing_custom_dirs_file"`
	PatchExistingCustomDirs bool   `json:"patch_existing_custom_dirs"`
	RequireNXStopped        bool   `json:"require_nx_stopped"`
	ClearDetectedConflicts  bool   `json:"clear_detected_conflicts"`
	AtomicWrites            bool   `json:"atomic_writes"`
	DryRun                  bool   `json:"dry_run"`
}

type Binding struct {
	Shortcut string     `json:"shortcut"`
	Command  CommandRef `json:"command"`
	Scope    string     `json:"scope"`
	Enabled  bool       `json:"enabled"`
	Notes    string     `json:"notes,omitempty"`
}

type CommandRef struct {
	ID      string   `json:"id,omitempty"`
	Name    string   `json:"name"`
	Aliases []string `json:"aliases,omitempty"`
}

type RadialMenu struct {
	Name    string       `json:"name"`
	Trigger string       `json:"trigger"`
	Module  string       `json:"module,omitempty"`
	Kind    string       `json:"kind,omitempty"`
	Enabled bool         `json:"enabled"`
	Items   []RadialItem `json:"items"`
}

type RadialItem struct {
	Direction string     `json:"direction"`
	Command   CommandRef `json:"command"`
	Notes     string     `json:"notes,omitempty"`
}

type ModuleConfig struct {
	ID                  string             `json:"id"`
	Label               string             `json:"label"`
	Enabled             bool               `json:"enabled"`
	NXApplicationIDs    []string           `json:"nx_application_ids,omitempty"`
	SwitchCommand       CommandRef         `json:"switch_command,omitempty"`
	LeaderPrefix        string             `json:"leader_prefix,omitempty"`
	SelectionPriorities []ModuleCommand    `json:"selection_priorities,omitempty"`
	CommandSets         []ModuleCommandSet `json:"command_sets,omitempty"`
	Radials             []RadialMenu       `json:"radials,omitempty"`
}

type ModuleCommandSet struct {
	ID            string            `json:"id"`
	Label         string            `json:"label"`
	SlotSemantics map[string]string `json:"slot_semantics,omitempty"`
	Commands      []ModuleCommand   `json:"commands"`
}

type ModuleCommand struct {
	Slot                 string     `json:"slot"`
	Command              CommandRef `json:"command"`
	RequiresSelection    bool       `json:"requires_selection,omitempty"`
	Destructive          bool       `json:"destructive,omitempty"`
	ConfirmBeforeExecute bool       `json:"confirm_before_execute,omitempty"`
	Fallback             string     `json:"fallback,omitempty"`
	Notes                string     `json:"notes,omitempty"`
}

type WorkflowControls struct {
	AcceptOK         CommandRef `json:"accept_ok,omitempty"`
	Apply            CommandRef `json:"apply,omitempty"`
	Cancel           CommandRef `json:"cancel,omitempty"`
	BackPreviousStep CommandRef `json:"back_previous_step,omitempty"`
	ConfirmDangerous bool       `json:"confirm_dangerous"`
}

type Performance struct {
	CatalogCacheEnabled bool `json:"catalog_cache_enabled"`
	LazyStudioScan      bool `json:"lazy_studio_scan"`
	BridgeWatcher       bool `json:"bridge_watcher"`
}

type RoleDeployment struct {
	Enabled         bool   `json:"enabled"`
	SourceMTX       string `json:"source_mtx"`
	TargetDirectory string `json:"target_directory"`
	TargetName      string `json:"target_name"`
	SetAsDefault    bool   `json:"set_as_default"`
	DefaultRoleEnv  string `json:"default_role_env"`
}

func Load(path string) (Config, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return Config{}, fmt.Errorf("read config: %w", err)
	}
	var cfg Config
	if err := json.Unmarshal(data, &cfg); err != nil {
		return Config{}, fmt.Errorf("parse config: %w", err)
	}
	cfg.ExpandEnvironment()
	cfg.ApplyDefaults()
	if err := cfg.Validate(); err != nil {
		return Config{}, err
	}
	return cfg, nil
}

func Save(path string, cfg Config) error {
	if err := cfg.Validate(); err != nil {
		return err
	}
	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		return err
	}
	data = append(data, '\n')
	return os.WriteFile(path, data, 0o644)
}

func (c *Config) ApplyDefaults() {
	if c.SchemaVersion == 0 {
		c.SchemaVersion = 2
	}
	if c.Profile.NXVersion == "" {
		c.Profile.NXVersion = "2512"
	}
	if c.Scan.MaxDepth <= 0 {
		c.Scan.MaxDepth = 8
	}
	if c.Scan.MaxFiles <= 0 {
		c.Scan.MaxFiles = 25000
	}
	if len(c.Scan.MenuExtensions) == 0 {
		c.Scan.MenuExtensions = []string{".men", ".tbr", ".rtb", ".gly", ".abr"}
	}
	if len(c.Scan.RoleExtensions) == 0 {
		c.Scan.RoleExtensions = []string{".mtx"}
	}
	if len(c.Scan.LauncherExtensions) == 0 {
		c.Scan.LauncherExtensions = []string{".bat", ".cmd", ".ps1"}
	}
	if c.Deployment.Mode == "" {
		c.Deployment.Mode = "managed-wrapper"
	}
	if c.Deployment.ManagedRoot == "" {
		c.Deployment.ManagedRoot = fmt.Sprintf(`%%LOCALAPPDATA%%\NXKeys\managed\NX%s`, c.Profile.NXVersion)
	}
	if c.Deployment.BackupRoot == "" {
		c.Deployment.BackupRoot = `%LOCALAPPDATA%\NXKeys\backups`
	}
	if c.Deployment.OverlayFilename == "" {
		c.Deployment.OverlayFilename = "nxkeys_generated.men"
	}
	if c.Deployment.MenuScriptVersion <= 0 || c.Deployment.MenuScriptVersion > 139 {
		c.Deployment.MenuScriptVersion = 139
	}
	if c.Deployment.MainMenubarID == "" {
		c.Deployment.MainMenubarID = "UG_GATEWAY_MAIN_MENUBAR"
	}
	if c.Role.TargetName == "" {
		c.Role.TargetName = fmt.Sprintf("NX_Pro_Hybrid_%s.mtx", c.Profile.NXVersion)
	}
	if c.Role.DefaultRoleEnv == "" {
		c.Role.DefaultRoleEnv = "UGII_DEFAULT_ROLE"
	}
	if c.Workflow.AcceptOK.Name == "" && c.Workflow.AcceptOK.ID == "" {
		c.Workflow.AcceptOK = CommandRef{Name: "OK", Aliases: []string{"Accept", "Finish", "Middle Mouse Button"}}
	}
	if c.Workflow.Apply.Name == "" && c.Workflow.Apply.ID == "" {
		c.Workflow.Apply = CommandRef{Name: "Apply"}
	}
	if c.Workflow.Cancel.Name == "" && c.Workflow.Cancel.ID == "" {
		c.Workflow.Cancel = CommandRef{Name: "Cancel", Aliases: []string{"Deselect"}}
	}
	if c.Workflow.BackPreviousStep.Name == "" && c.Workflow.BackPreviousStep.ID == "" {
		c.Workflow.BackPreviousStep = CommandRef{Name: "Back", Aliases: []string{"Previous Step"}}
	}
	c.Workflow.ConfirmDangerous = true
	if !c.Performance.CatalogCacheEnabled && !c.Performance.LazyStudioScan && !c.Performance.BridgeWatcher {
		c.Performance = Performance{CatalogCacheEnabled: true, LazyStudioScan: true, BridgeWatcher: true}
	}
	if len(c.Modules) == 0 {
		c.Modules = DefaultModules()
	}
}

func (c *Config) ExpandEnvironment() {
	expand := func(s string) string { return ExpandPath(s) }
	for i := range c.Scan.Roots {
		c.Scan.Roots[i] = expand(c.Scan.Roots[i])
	}
	for i := range c.Scan.InstallHints {
		c.Scan.InstallHints[i] = expand(c.Scan.InstallHints[i])
	}
	for i := range c.Scan.ProfileHints {
		c.Scan.ProfileHints[i] = expand(c.Scan.ProfileHints[i])
	}
	c.Deployment.ManagedRoot = expand(c.Deployment.ManagedRoot)
	c.Deployment.BackupRoot = expand(c.Deployment.BackupRoot)
	c.Deployment.NXExecutable = expand(c.Deployment.NXExecutable)
	c.Deployment.ExistingCustomDirsFile = expand(c.Deployment.ExistingCustomDirsFile)
	c.Role.SourceMTX = expand(c.Role.SourceMTX)
	c.Role.TargetDirectory = expand(c.Role.TargetDirectory)
}

var percentEnv = regexp.MustCompile(`%([A-Za-z_][A-Za-z0-9_]*)%`)

func ExpandPath(s string) string {
	if strings.TrimSpace(s) == "" {
		return ""
	}
	s = percentEnv.ReplaceAllStringFunc(s, func(token string) string {
		name := strings.Trim(token, "%")
		if v, ok := os.LookupEnv(name); ok {
			return v
		}
		return token
	})
	s = os.ExpandEnv(s)
	if strings.HasPrefix(s, "~") {
		if home, err := os.UserHomeDir(); err == nil {
			s = filepath.Join(home, strings.TrimPrefix(strings.TrimPrefix(s, "~"), string(filepath.Separator)))
		}
	}
	return filepath.Clean(s)
}

func (c Config) Validate() error {
	var problems []string
	if c.SchemaVersion != 1 && c.SchemaVersion != 2 {
		problems = append(problems, fmt.Sprintf("unsupported schema_version %d", c.SchemaVersion))
	}
	if strings.TrimSpace(c.Profile.Name) == "" {
		problems = append(problems, "profile.name is required")
	}
	if strings.TrimSpace(c.Deployment.ManagedRoot) == "" {
		problems = append(problems, "deployment.managed_root is required")
	}
	if strings.TrimSpace(c.Deployment.BackupRoot) == "" {
		problems = append(problems, "deployment.backup_root is required")
	}
	allowedModes := map[string]bool{"managed-wrapper": true, "existing-custom-dirs": true}
	if !allowedModes[c.Deployment.Mode] {
		problems = append(problems, "deployment.mode must be managed-wrapper or existing-custom-dirs")
	}
	seenShortcut := map[string]string{}
	seenCommand := map[string]string{}
	for i, binding := range c.Keyboard {
		if !binding.Enabled {
			continue
		}
		if strings.TrimSpace(binding.Shortcut) == "" {
			problems = append(problems, fmt.Sprintf("keyboard[%d].shortcut is required", i))
		}
		if strings.TrimSpace(binding.Command.ID) == "" && strings.TrimSpace(binding.Command.Name) == "" {
			problems = append(problems, fmt.Sprintf("keyboard[%d].command needs id or name", i))
		}
		shortcutKey := normalize(binding.Shortcut)
		if prev, ok := seenShortcut[shortcutKey]; ok {
			problems = append(problems, fmt.Sprintf("duplicate shortcut %q for %s and %s", binding.Shortcut, prev, binding.Command.Name))
		}
		seenShortcut[shortcutKey] = binding.Command.Name
		commandKey := normalize(binding.Command.ID)
		if commandKey == "" {
			commandKey = normalize(binding.Command.Name)
		}
		if prev, ok := seenCommand[commandKey]; ok && prev != binding.Shortcut {
			problems = append(problems, fmt.Sprintf("command %q has multiple shortcuts: %s and %s", binding.Command.Name, prev, binding.Shortcut))
		}
		seenCommand[commandKey] = binding.Shortcut
	}
	if c.Role.Enabled && strings.TrimSpace(c.Role.SourceMTX) == "" {
		problems = append(problems, "role_deployment.source_mtx is required when role deployment is enabled")
	}
	for i, radial := range c.Radials {
		if !radial.Enabled {
			continue
		}
		if radial.Name == "" || radial.Trigger == "" {
			problems = append(problems, fmt.Sprintf("radials[%d] needs name and trigger", i))
		}
		seenDirection := map[string]bool{}
		for _, item := range radial.Items {
			d := strings.ToUpper(strings.TrimSpace(item.Direction))
			if !validDirection(d) {
				problems = append(problems, fmt.Sprintf("radial %q has invalid direction %q", radial.Name, item.Direction))
			}
			if seenDirection[d] {
				problems = append(problems, fmt.Sprintf("radial %q repeats direction %q", radial.Name, d))
			}
			seenDirection[d] = true
		}
	}
	validateModules(c.Modules, &problems)
	if len(problems) > 0 {
		sort.Strings(problems)
		return errors.New("configuration is invalid:\n- " + strings.Join(problems, "\n- "))
	}
	return nil
}

func validateModules(modules []ModuleConfig, problems *[]string) {
	seenModules := map[string]bool{}
	for i, module := range modules {
		if !module.Enabled {
			continue
		}
		id := strings.TrimSpace(module.ID)
		if id == "" {
			*problems = append(*problems, fmt.Sprintf("modules[%d].id is required", i))
			continue
		}
		key := normalize(id)
		if seenModules[key] {
			*problems = append(*problems, fmt.Sprintf("duplicate module id %q", module.ID))
		}
		seenModules[key] = true
		for j, set := range module.CommandSets {
			setKey := strings.TrimSpace(set.ID)
			if setKey == "" {
				*problems = append(*problems, fmt.Sprintf("module %q command_sets[%d].id is required", module.ID, j))
			}
			seenSlots := map[string]bool{}
			for k, command := range set.Commands {
				slot := strings.ToUpper(strings.TrimSpace(command.Slot))
				if slot == "" {
					*problems = append(*problems, fmt.Sprintf("module %q command_set %q commands[%d].slot is required", module.ID, set.ID, k))
				}
				if seenSlots[slot] {
					*problems = append(*problems, fmt.Sprintf("module %q command_set %q repeats slot %q", module.ID, set.ID, slot))
				}
				seenSlots[slot] = true
				if strings.TrimSpace(command.Command.ID) == "" && strings.TrimSpace(command.Command.Name) == "" {
					*problems = append(*problems, fmt.Sprintf("module %q command_set %q slot %q needs command id or name", module.ID, set.ID, slot))
				}
			}
		}
		for j, radial := range module.Radials {
			seenDirections := map[string]bool{}
			for _, item := range radial.Items {
				d := strings.ToUpper(strings.TrimSpace(item.Direction))
				if !validDirection(d) {
					*problems = append(*problems, fmt.Sprintf("module %q radial[%d] has invalid direction %q", module.ID, j, item.Direction))
				}
				if seenDirections[d] {
					*problems = append(*problems, fmt.Sprintf("module %q radial[%d] repeats direction %q", module.ID, j, d))
				}
				seenDirections[d] = true
			}
		}
	}
}

func validDirection(v string) bool {
	switch v {
	case "N", "NE", "E", "SE", "S", "SW", "W", "NW":
		return true
	default:
		return false
	}
}

func normalize(s string) string {
	s = strings.ToLower(strings.TrimSpace(s))
	s = strings.ReplaceAll(s, " ", "")
	s = strings.ReplaceAll(s, "-", "")
	s = strings.ReplaceAll(s, "+", "")
	return s
}

func DefaultRoots() []string {
	var roots []string
	for _, key := range []string{"UGII_BASE_DIR", "UGII_ROOT_DIR", "UGII_USER_PROFILE_DIR", "UGII_SITE_DIR"} {
		if value := os.Getenv(key); value != "" {
			roots = append(roots, value)
		}
	}
	subDirs := []string{"Siemens", "Unigraphics Solutions"}
	for _, key := range []string{"LOCALAPPDATA", "APPDATA", "ProgramFiles", "ProgramFiles(x86)"} {
		if value := os.Getenv(key); value != "" {
			for _, sub := range subDirs {
				candidate := filepath.Join(value, sub)
				if info, err := os.Stat(candidate); err == nil && info.IsDir() {
					roots = append(roots, candidate)
				}
			}
		}
	}
	if home, err := os.UserHomeDir(); err == nil {
		for _, sub := range subDirs {
			candidate := filepath.Join(home, sub)
			if info, err := os.Stat(candidate); err == nil && info.IsDir() {
				roots = append(roots, candidate)
			}
		}
	}
	if runtime.GOOS != "windows" {
		for _, base := range []string{"/opt", "/usr/local"} {
			roots = append(roots, base)
			for _, sub := range subDirs {
				candidate := filepath.Join(base, sub)
				if info, err := os.Stat(candidate); err == nil && info.IsDir() {
					roots = append(roots, candidate)
				}
			}
		}
	}
	return dedupePaths(roots)
}

func DefaultModules() []ModuleConfig {
	return []ModuleConfig{
		module("modeling", "Modeling", "M", []string{"UG_APP_MODELING"}, "UG_APP_MODELING", "Modeling", []ModuleCommand{
			cmd("N", "UG_CREATE_SKETCH", "Sketch", false, false),
			cmd("NE", "UG_MODELING_EXTRUDED_FEATURE", "Extrude", false, false),
			cmd("E", "UG_MODELING_HOLE_FEATURE", "Hole", false, false),
			cmd("SE", "UG_MODELING_REVOLVED_FEATURE", "Revolve", false, false),
			cmd("S", "UG_MODELING_BLEND_FEATURE", "Edge Blend", true, false),
			cmd("SW", "UG_MODELING_CHAMFER_FEATURE", "Chamfer", true, false),
			cmd("W", "UG_MODELING_PATTERNFEATURE_FEATURE", "Pattern Feature", true, false),
			cmd("NW", "UG_MODELING_MIRRORFEATURE_FEATURE", "Mirror Feature", true, false),
		}),
		module("sketch", "Sketch", "S", []string{"UG_APP_SKETCH", "UG_APP_MODELING"}, "UG_CREATE_SKETCH", "Sketch", []ModuleCommand{
			cmd("N", "UG_SKETCH_LINE", "Line", false, false),
			cmd("NE", "UG_SKETCH_RECTANGLE", "Rectangle", false, false),
			cmd("E", "UG_SKETCH_CIRCLE", "Circle", false, false),
			cmd("SE", "UG_SKETCH_ARC", "Arc", false, false),
			cmd("S", "UG_SKETCH_TRIM", "Trim", true, false),
			cmd("SW", "UG_SKETCH_EXTEND", "Extend", true, false),
			cmd("W", "UG_SKETCH_OFFSET_CURVE", "Offset Curve", true, false),
			cmd("NW", "UG_SKETCH_CHECKER", "Sketch Checker", false, false),
		}),
		module("assembly", "Assembly", "A", []string{"UG_APP_ASSEMBLIES"}, "UG_APP_ASSEMBLIES", "Assemblies", []ModuleCommand{
			cmd("N", "UG_ASSEMBLIES_ADD_COMPONENT", "Add Component", false, false),
			cmd("NE", "UG_ASSEMBLIES_NEW_COMPONENT", "Create New Component", false, false),
			cmd("E", "UG_ASSEMBLIES_MOVE_COMPONENT", "Move Component", true, false),
			cmd("SE", "UG_ASSEMBLIES_CONSTRAINTS", "Assembly Constraints", true, false),
			cmd("S", "UG_ASSEMBLIES_REPLACE_COMPONENT", "Replace Component", true, true),
			cmd("SW", "UG_ASSEMBLIES_REMOVE_COMPONENT", "Remove Component", true, true),
			cmd("W", "UG_ASSEMBLIES_PATTERN_COMPONENT", "Pattern Component", true, false),
			cmd("NW", "UG_ASSEMBLIES_NAVIGATOR", "Assembly Navigator", false, false),
		}),
		module("drafting", "Drafting", "D", []string{"UG_APP_DRAFTING"}, "UG_APP_DRAFTING", "Drafting", []ModuleCommand{
			cmd("N", "UG_DRAFTING_BASE_VIEW", "Base View", false, false),
			cmd("NE", "UG_DRAFTING_PROJECTED_VIEW", "Projected View", false, false),
			cmd("E", "UG_DRAFTING_SECTION_VIEW", "Section View", false, false),
			cmd("SE", "UG_DRAFTING_DETAIL_VIEW", "Detail View", false, false),
			cmd("S", "UG_DRAFTING_UPDATE_VIEWS", "Update Views", false, false),
			cmd("SW", "UG_DRAFTING_VIEW_STYLE", "View Style", true, false),
			cmd("W", "UG_DRAFTING_PARTS_LIST", "Parts List", false, false),
			cmd("NW", "UG_DRAFTING_RAPID_DIMENSION", "Rapid Dimension", false, false),
		}),
		module("pmi", "PMI", "P", []string{"UG_APP_PMI"}, "UG_APP_PMI", "PMI", []ModuleCommand{
			cmd("N", "UG_PMI_RAPID_DIMENSION", "Rapid Dimension", false, false),
			cmd("NE", "UG_PMI_DATUM_FEATURE_SYMBOL", "Datum Feature Symbol", false, false),
			cmd("E", "UG_PMI_FEATURE_CONTROL_FRAME", "Feature Control Frame", false, false),
			cmd("SE", "UG_PMI_SURFACE_FINISH", "Surface Finish Symbol", false, false),
			cmd("S", "UG_PMI_NOTE", "PMI Note", false, false),
			cmd("SW", "UG_PMI_EDIT", "Edit PMI", true, false),
			cmd("W", "UG_PMI_MODEL_VIEW", "Model View", false, false),
			cmd("NW", "UG_PMI_VALIDATE", "Validate PMI", false, false),
		}),
		module("surface", "Surface", "U", []string{"UG_APP_MODELING", "UG_APP_STUDIO"}, "UG_APP_MODELING", "Surface Modeling", []ModuleCommand{
			cmd("N", "UG_MODELING_THROUGH_CURVES_FEATURE", "Through Curves", false, false),
			cmd("NE", "UG_MODELING_SWEPT_FEATURE", "Swept", false, false),
			cmd("E", "UG_MODELING_STUDIO_SURFACE_FEATURE", "Studio Surface", false, false),
			cmd("SE", "UG_MODELING_TRIM_SHEET_FEATURE", "Trim Sheet", true, false),
			cmd("S", "UG_MODELING_SEW_FEATURE", "Sew", true, false),
			cmd("SW", "UG_MODELING_UNTRIM_FEATURE", "Untrim", true, false),
			cmd("W", "UG_MODELING_EXTRACT_GEOMETRY", "Extract Geometry", true, false),
			cmd("NW", "UG_ANALYSIS_FACE_CURVATURE", "Face Curvature", true, false),
		}),
		module("sheet_metal", "Sheet Metal", "H", []string{"UG_APP_SHEETMETAL"}, "UG_APP_SHEETMETAL", "Sheet Metal", []ModuleCommand{
			cmd("N", "UG_SHEET_METAL_BASE_TAB", "Base Tab", false, false),
			cmd("NE", "UG_SHEET_METAL_FLANGE", "Flange", true, false),
			cmd("E", "UG_SHEET_METAL_CONTOUR_FLANGE", "Contour Flange", false, false),
			cmd("SE", "UG_SHEET_METAL_BEND", "Bend", true, false),
			cmd("S", "UG_SHEET_METAL_UNBEND", "Unbend", true, false),
			cmd("SW", "UG_SHEET_METAL_REBEND", "Rebend", true, false),
			cmd("W", "UG_SHEET_METAL_FLAT_PATTERN", "Flat Pattern", false, false),
			cmd("NW", "UG_SHEET_METAL_VALIDATE", "Sheet Metal Preferences", false, false),
		}),
		module("manufacturing", "CAM / Manufacturing", "C", []string{"UG_APP_MANUFACTURING"}, "UG_APP_MANUFACTURING", "Manufacturing", []ModuleCommand{
			cmd("N", "UG_CAM_CREATE_OPERATION", "Create Operation", false, false),
			cmd("NE", "UG_CAM_CREATE_TOOL", "Create Tool", false, false),
			cmd("E", "UG_CAM_GENERATE_TOOL_PATH", "Generate Tool Path", true, false),
			cmd("SE", "UG_CAM_VERIFY_TOOL_PATH", "Verify Tool Path", true, false),
			cmd("S", "UG_CAM_POSTPROCESS", "Postprocess", true, true),
			cmd("SW", "UG_CAM_DELETE_OPERATION", "Delete Operation", true, true),
			cmd("W", "UG_CAM_OPERATION_NAVIGATOR", "Operation Navigator", false, false),
			cmd("NW", "UG_CAM_INFORMATION", "Tool Path Information", true, false),
		}),
		module("simulation", "CAE / Simulation", "X", []string{"UG_APP_SFEM", "UG_APP_DESFEM"}, "UG_APP_SFEM", "Simulation", []ModuleCommand{
			cmd("N", "UG_SIM_CREATE_SOLUTION", "Create Solution", false, false),
			cmd("NE", "UG_SIM_CREATE_LOAD", "Create Load", false, false),
			cmd("E", "UG_SIM_CREATE_CONSTRAINT", "Create Constraint", false, false),
			cmd("SE", "UG_SIM_MESH", "Mesh", true, false),
			cmd("S", "UG_SIM_SOLVE", "Solve", true, true),
			cmd("SW", "UG_SIM_DELETE", "Delete Simulation Object", true, true),
			cmd("W", "UG_SIM_NAVIGATOR", "Simulation Navigator", false, false),
			cmd("NW", "UG_SIM_RESULTS", "Results", false, false),
		}),
		module("routing", "Routing", "G", []string{"UG_APP_ROUTING"}, "UG_APP_ROUTING", "Routing", []ModuleCommand{
			cmd("N", "UG_ROUTE_CREATE_ROUTE", "Create Route", false, false),
			cmd("NE", "UG_ROUTE_PLACE_PART", "Place Part", false, false),
			cmd("E", "UG_ROUTE_ADD_STOCK", "Add Stock", true, false),
			cmd("SE", "UG_ROUTE_EDIT_ROUTE", "Edit Route", true, false),
			cmd("S", "UG_ROUTE_DELETE", "Delete Route Object", true, true),
			cmd("SW", "UG_ROUTE_REMOVE_PART", "Remove Part", true, true),
			cmd("W", "UG_ROUTE_NAVIGATOR", "Routing Navigator", false, false),
			cmd("NW", "UG_ROUTE_VALIDATE", "Validate Route", false, false),
		}),
		module("mold", "Mold / Tooling", "O", []string{"UG_APP_MOLDWIZARD"}, "UG_APP_MOLDWIZARD", "Mold Wizard", []ModuleCommand{
			cmd("N", "UG_MOLD_INITIALIZE_PROJECT", "Initialize Project", false, false),
			cmd("NE", "UG_MOLD_PARTING", "Parting", true, false),
			cmd("E", "UG_MOLD_MOLD_BASE", "Mold Base", false, false),
			cmd("SE", "UG_MOLD_GATE", "Gate", true, false),
			cmd("S", "UG_MOLD_COOLING", "Cooling", false, false),
			cmd("SW", "UG_MOLD_EJECTOR", "Ejector", false, false),
			cmd("W", "UG_MOLD_LIBRARY", "Mold Library", false, false),
			cmd("NW", "UG_MOLD_VALIDATE", "Validate Mold Design", false, false),
		}),
		module("reuse", "Reuse / Templates", "R", []string{"UG_APP_MODELING"}, "UG_NAVIGATOR_REUSE_LIBRARY", "Reuse Library", []ModuleCommand{
			cmd("N", "UG_EXPRESSIONS", "Expressions", false, false),
			cmd("NE", "UG_MODELING_WAVE_LINKER", "WAVE Geometry Linker", false, false),
			cmd("E", "UG_NAVIGATOR_REUSE_LIBRARY", "Reuse Library", false, false),
			cmd("SE", "UG_CREATE_FEATURE_TEMPLATE", "Create Feature Template", true, false),
			cmd("S", "UG_REPLACE_FEATURE_TEMPLATE", "Replace Feature Template", true, true),
			cmd("SW", "UG_NAVIGATOR_PART", "Part Navigator", false, false),
			cmd("W", "UG_PARAMETER_TABLE", "Parameter Table", false, false),
			cmd("NW", "UG_COMMAND_FINDER", "Command Finder", false, false),
		}),
		module("inspect_view", "Inspect / View", "V", []string{"UG_APP_GATEWAY", "UG_APP_MODELING"}, "UG_APP_GATEWAY", "Inspect and View", []ModuleCommand{
			cmd("N", "UG_VIEW_FIT", "Fit", false, false),
			cmd("NE", "UG_VIEW_POPUP_ORIENT_TFRTRI", "Trimetric", false, false),
			cmd("E", "UG_HELP_MEASURE", "Measure", true, false),
			cmd("SE", "UG_INFO_OBJECT", "Object Information", true, false),
			cmd("S", "UG_VIEW_HIDE", "Hide", true, false),
			cmd("SW", "UG_VIEW_SHOW_ONLY", "Show Only", true, false),
			cmd("W", "UG_LAYER_SETTINGS", "Layer Settings", false, false),
			cmd("NW", "UG_COMMAND_FINDER", "Command Finder", false, false),
		}),
		selectionModule(),
	}
}

func module(id, label, prefix string, applications []string, switchID, switchName string, commands []ModuleCommand) ModuleConfig {
	return ModuleConfig{
		ID:               id,
		Label:            label,
		Enabled:          true,
		NXApplicationIDs: applications,
		SwitchCommand:    CommandRef{ID: switchID, Name: switchName},
		LeaderPrefix:     prefix,
		CommandSets: []ModuleCommandSet{{
			ID:            "primary",
			Label:         "Primary",
			SlotSemantics: DefaultSlotSemantics(),
			Commands:      commands,
		}},
		Radials: []RadialMenu{{
			Name:    label + " Radial 1 — Primary",
			Module:  id,
			Kind:    "application",
			Trigger: "Ctrl+Shift+MB1",
			Enabled: true,
			Items:   radialItems(commands),
		}},
	}
}

func selectionModule() ModuleConfig {
	commands := []ModuleCommand{
		cmd("N", "UG_SEL_BODY_PRIORITY", "Body Selection Priority", false, false),
		cmd("NE", "UG_SEL_FACE_PRIORITY", "Face Selection Priority", false, false),
		cmd("E", "UG_SEL_EDGE_PRIORITY", "Edge Selection Priority", false, false),
		cmd("SE", "UG_SEL_FEATURE_PRIORITY", "Feature Selection Priority", false, false),
		cmd("S", "UG_SEL_COMPONENT_PRIORITY", "Component Selection Priority", false, false),
		cmd("SW", "UG_SEL_CURVE_PRIORITY", "Curve Selection Priority", false, false),
		cmd("W", "UG_SEL_DATUM_PRIORITY", "Datum Selection Priority", false, false),
		cmd("NW", "UG_SEL_TYPE_RESET", "Reset Selection Filter", false, false),
	}
	m := module("selection_object", "Selection / Object", "F", []string{"UG_APP_GATEWAY", "UG_APP_MODELING"}, "UG_APP_GATEWAY", "Selection", commands)
	m.SelectionPriorities = commands
	m.Radials[0].Kind = "object"
	return m
}

func DefaultSlotSemantics() map[string]string {
	return map[string]string{
		"N":  "start/create/open primary object",
		"NE": "next main process step",
		"E":  "add object/material/dependency",
		"SE": "transform or replace",
		"S":  "finish/delete/secondary processing",
		"SW": "remove/reduce/relax",
		"W":  "structure/link/pattern",
		"NW": "inspect/measure/service command",
	}
}

func cmd(slot, id, name string, requiresSelection, destructive bool) ModuleCommand {
	return ModuleCommand{
		Slot:                 slot,
		Command:              CommandRef{ID: id, Name: name},
		RequiresSelection:    requiresSelection,
		Destructive:          destructive,
		ConfirmBeforeExecute: destructive,
	}
}

func radialItems(commands []ModuleCommand) []RadialItem {
	items := make([]RadialItem, 0, len(commands))
	for _, command := range commands {
		items = append(items, RadialItem{
			Direction: command.Slot,
			Command:   command.Command,
			Notes:     command.Notes,
		})
	}
	return items
}

func dedupePaths(paths []string) []string {
	seen := map[string]bool{}
	result := make([]string, 0, len(paths))
	for _, p := range paths {
		if strings.TrimSpace(p) == "" {
			continue
		}
		p = filepath.Clean(p)
		key := strings.ToLower(p)
		if seen[key] {
			continue
		}
		seen[key] = true
		result = append(result, p)
	}
	return result
}
