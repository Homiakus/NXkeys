package nxmenu

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/homiakus/nxkeys/internal/config"
)

func TestParseResolveAndGenerateOverlay(t *testing.T) {
	path := filepath.Join(t.TempDir(), "ug_modeling.men")
	menu := `VERSION 139
BUTTON UG_MODELING_SKETCH
LABEL &Sketch
SYNONYMS Create Sketch, Profile
ACCELERATOR Z
BUTTON UG_MODELING_EXTRUDE
LABEL Extrude
`
	if err := os.WriteFile(path, []byte(menu), 0o644); err != nil {
		t.Fatal(err)
	}
	catalog := NewCatalog()
	if err := catalog.ParseFile(path); err != nil {
		t.Fatal(err)
	}
	binding := config.Binding{Shortcut: "Ctrl+3", Command: config.CommandRef{Name: "Sketch", Aliases: []string{"Create Sketch"}}, Enabled: true}
	resolution := catalog.Resolve(binding)
	if resolution.Status != Resolved || resolution.CommandID != "UG_MODELING_SKETCH" {
		t.Fatalf("unexpected resolution: %#v", resolution)
	}
	overlay := string(GenerateOverlay(139, "UG_GATEWAY_MAIN_MENUBAR", []Resolution{resolution}, nil, false))
	for _, expected := range []string{"EDIT UG_GATEWAY_MAIN_MENUBAR", "BUTTON UG_MODELING_SKETCH", "ACCELERATOR Ctrl+3"} {
		if !strings.Contains(overlay, expected) {
			t.Fatalf("overlay missing %q:\n%s", expected, overlay)
		}
	}
}

func TestAmbiguousResolution(t *testing.T) {
	catalog := NewCatalog()
	catalog.Commands["A"] = &Command{ID: "A", Labels: []string{"Pattern Feature"}}
	catalog.Commands["B"] = &Command{ID: "B", Labels: []string{"Feature Pattern"}}
	resolution := catalog.Resolve(config.Binding{Shortcut: "Ctrl+8", Command: config.CommandRef{Name: "Pattern Feature"}, Enabled: true})
	if resolution.Status != Resolved {
		t.Fatalf("exact match should win: %#v", resolution)
	}
}

func TestWordBoundarySimilarity(t *testing.T) {
	// "end" is a substring of "blend", but not at a word boundary
	score1 := similarity("blend", "end")
	if score1 >= 0.62 {
		t.Errorf("expected low similarity for non-word boundary match: blend vs end, got %f", score1)
	}

	// "sketch" is a substring of "create sketch", matching on word boundary
	score2 := similarity("create sketch", "sketch")
	if score2 < 0.78 {
		t.Errorf("expected high similarity for word boundary match: create sketch vs sketch, got %f", score2)
	}
}
