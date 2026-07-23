package config

import (
	"os"
	"path/filepath"
	"testing"
)

func TestLoadAndValidate(t *testing.T) {
	t.Setenv("LOCALAPPDATA", filepath.Join(t.TempDir(), "local"))
	path := filepath.Join(t.TempDir(), "config.json")
	content := `{
  "schema_version": 1,
  "profile": {"name": "test", "nx_version": "2512.6000"},
  "scan": {},
  "deployment": {"mode": "managed-wrapper"},
  "keyboard": [
    {"shortcut":"Ctrl+3","command":{"name":"Sketch"},"scope":"Modeling","enabled":true}
  ],
  "radials": [],
  "role_deployment": {"enabled":false}
}`
	if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
		t.Fatal(err)
	}
	cfg, err := Load(path)
	if err != nil {
		t.Fatal(err)
	}
	if cfg.Deployment.ManagedRoot == "" || cfg.Deployment.BackupRoot == "" {
		t.Fatal("defaults were not applied")
	}
	if cfg.Keyboard[0].Command.Name != "Sketch" {
		t.Fatalf("unexpected command: %#v", cfg.Keyboard[0])
	}
	if len(cfg.Modules) == 0 {
		t.Fatal("v1 config should receive default modular command sets")
	}
}

func TestDuplicateShortcutRejected(t *testing.T) {
	cfg := Config{
		SchemaVersion: 2,
		Profile:       Profile{Name: "test"},
		Deployment: DeploymentConfig{
			Mode:        "managed-wrapper",
			ManagedRoot: t.TempDir(),
			BackupRoot:  t.TempDir(),
		},
		Keyboard: []Binding{
			{Shortcut: "Ctrl+3", Command: CommandRef{Name: "Sketch"}, Enabled: true},
			{Shortcut: "ctrl+3", Command: CommandRef{Name: "Extrude"}, Enabled: true},
		},
	}
	if err := cfg.Validate(); err == nil {
		t.Fatal("expected duplicate shortcut error")
	}
}

func TestV2DuplicateModuleSlotRejected(t *testing.T) {
	cfg := Config{
		SchemaVersion: 2,
		Profile:       Profile{Name: "test"},
		Deployment: DeploymentConfig{
			Mode:        "managed-wrapper",
			ManagedRoot: t.TempDir(),
			BackupRoot:  t.TempDir(),
		},
		Modules: []ModuleConfig{{
			ID:      "modeling",
			Label:   "Modeling",
			Enabled: true,
			CommandSets: []ModuleCommandSet{{
				ID:    "primary",
				Label: "Primary",
				Commands: []ModuleCommand{
					{Slot: "N", Command: CommandRef{Name: "Sketch"}},
					{Slot: "n", Command: CommandRef{Name: "Extrude"}},
				},
			}},
		}},
	}
	if err := cfg.Validate(); err == nil {
		t.Fatal("expected duplicate module slot error")
	}
}

func TestDefaultRootsNarrowed(t *testing.T) {
	tempDir := t.TempDir()
	siemensDir := filepath.Join(tempDir, "Siemens")
	if err := os.Mkdir(siemensDir, 0o755); err != nil {
		t.Fatal(err)
	}
	t.Setenv("ProgramFiles", tempDir)
	t.Setenv("LOCALAPPDATA", "")
	t.Setenv("APPDATA", "")
	t.Setenv("ProgramFiles(x86)", "")

	roots := DefaultRoots()
	foundSiemens := false
	for _, root := range roots {
		if root == siemensDir {
			foundSiemens = true
		}
		if root == tempDir {
			t.Errorf("default roots should not include broad ProgramFiles root %q directly", tempDir)
		}
	}
	if !foundSiemens {
		t.Errorf("expected to find %q in default roots, got %v", siemensDir, roots)
	}
}
