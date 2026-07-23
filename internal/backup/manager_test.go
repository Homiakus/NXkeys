package backup

import (
	"os"
	"path/filepath"
	"testing"
)

func TestBackupAndRestore(t *testing.T) {
	root := t.TempDir()
	target := filepath.Join(root, "target.txt")
	if err := os.WriteFile(target, []byte("before"), 0o644); err != nil {
		t.Fatal(err)
	}
	manager := Manager{Root: filepath.Join(root, "backups")}
	session, err := manager.New("test")
	if err != nil {
		t.Fatal(err)
	}
	entry, err := session.Capture(target)
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(target, []byte("after"), 0o644); err != nil {
		t.Fatal(err)
	}
	entry.AfterSHA256, _ = HashFile(target)
	session.Add(entry)
	if err := session.Finalize(); err != nil {
		t.Fatal(err)
	}
	if err := Restore(session.Manifest, false); err != nil {
		t.Fatal(err)
	}
	data, err := os.ReadFile(target)
	if err != nil {
		t.Fatal(err)
	}
	if string(data) != "before" {
		t.Fatalf("unexpected restored data %q", data)
	}
}
