package engine

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/homiakus/nxkeys/internal/config"
	"github.com/homiakus/nxkeys/internal/discovery"
	"github.com/homiakus/nxkeys/internal/nxmenu"
)

func TestBuildPlanAndApplyDryRun(t *testing.T) {
	root := t.TempDir()
	cfg := config.Config{
		SchemaVersion: 1,
		Profile:       config.Profile{Name: "test", NXVersion: "2512.6000"},
		Deployment: config.DeploymentConfig{
			Mode:              "managed-wrapper",
			ManagedRoot:       filepath.Join(root, "managed"),
			BackupRoot:        filepath.Join(root, "backups"),
			OverlayFilename:   "generated.men",
			MenuScriptVersion: 139,
			MainMenubarID:     "UG_GATEWAY_MAIN_MENUBAR",
			AtomicWrites:      true,
		},
		Keyboard: []config.Binding{{Shortcut: "Ctrl+3", Command: config.CommandRef{Name: "Sketch"}, Enabled: true}},
	}
	runtimeState := Runtime{
		Config: cfg,
		Resolutions: []nxmenu.Resolution{{
			Binding:   cfg.Keyboard[0],
			Status:    nxmenu.Resolved,
			CommandID: "UG_MODELING_SKETCH",
			Label:     "Sketch",
		}},
		Conflicts: map[string][]nxmenu.Command{},
	}
	plan, err := BuildPlan(runtimeState)
	if err != nil {
		t.Fatal(err)
	}
	if len(plan.Actions) < 5 {
		t.Fatalf("expected full deployment plan, got %d actions", len(plan.Actions))
	}
	result, err := Apply(plan, cfg, true)
	if err != nil {
		t.Fatal(err)
	}
	if len(result.Changed) == 0 {
		t.Fatal("dry run should report changes")
	}
	if _, err := os.Stat(filepath.Join(root, "managed")); !os.IsNotExist(err) {
		t.Fatal("dry run must not create managed files")
	}
}

func TestBuildPlanDoesNotModifyUserMTXForRadials(t *testing.T) {
	root := t.TempDir()
	profileDir := filepath.Join(root, "Siemens", "Designcenter2512")
	if err := os.MkdirAll(profileDir, 0o755); err != nil {
		t.Fatal(err)
	}
	userMtx := filepath.Join(profileDir, "user.mtx")
	if err := os.WriteFile(userMtx, []byte("<NX_PROFILES/>\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	cfg := config.Config{
		SchemaVersion: 1,
		Profile:       config.Profile{Name: "test", NXVersion: "2512.6000"},
		Deployment: config.DeploymentConfig{
			Mode:              "managed-wrapper",
			ManagedRoot:       filepath.Join(root, "managed"),
			BackupRoot:        filepath.Join(root, "backups"),
			OverlayFilename:   "generated.men",
			MenuScriptVersion: 139,
			MainMenubarID:     "UG_GATEWAY_MAIN_MENUBAR",
			AtomicWrites:      true,
		},
		Keyboard: []config.Binding{{Shortcut: "Ctrl+3", Command: config.CommandRef{Name: "Sketch"}, Enabled: true}},
		Radials:  []config.RadialMenu{{Name: "Application Radial 1", Trigger: "Ctrl+Shift+MB1", Enabled: true}},
	}
	runtimeState := Runtime{
		Config: cfg,
		Discovery: discovery.Result{
			ProfileDirs: []string{profileDir},
		},
		Resolutions: []nxmenu.Resolution{{
			Binding:   cfg.Keyboard[0],
			Status:    nxmenu.Resolved,
			CommandID: "UG_MODELING_SKETCH",
			Label:     "Sketch",
		}},
		Conflicts: map[string][]nxmenu.Command{},
	}
	plan, err := BuildPlan(runtimeState)
	if err != nil {
		t.Fatal(err)
	}
	for _, action := range plan.Actions {
		if action.Path == userMtx {
			t.Fatalf("plan must not modify active user.mtx: %#v", action)
		}
	}
}
