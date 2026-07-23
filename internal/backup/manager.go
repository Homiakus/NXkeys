package backup

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

type Manifest struct {
	ID        string    `json:"id"`
	CreatedAt time.Time `json:"created_at"`
	Tool      string    `json:"tool"`
	Profile   string    `json:"profile"`
	Entries   []Entry   `json:"entries"`
}

type Entry struct {
	OriginalPath string `json:"original_path"`
	BackupPath   string `json:"backup_path,omitempty"`
	Created      bool   `json:"created"`
	BeforeSHA256 string `json:"before_sha256,omitempty"`
	AfterSHA256  string `json:"after_sha256,omitempty"`
	Mode         uint32 `json:"mode,omitempty"`
}

type Manager struct {
	Root string
}

func (m Manager) New(profile string) (*Session, error) {
	if strings.TrimSpace(m.Root) == "" {
		return nil, errors.New("backup root is empty")
	}
	id := time.Now().Format("20060102_150405.000")
	dir := filepath.Join(m.Root, id)
	if err := os.MkdirAll(filepath.Join(dir, "files"), 0o755); err != nil {
		return nil, err
	}
	return &Session{
		Dir: dir,
		Manifest: Manifest{
			ID:        id,
			CreatedAt: time.Now(),
			Tool:      "nxkeys",
			Profile:   profile,
		},
	}, nil
}

type Session struct {
	Dir      string
	Manifest Manifest
}

func (s *Session) Capture(path string) (Entry, error) {
	entry := Entry{OriginalPath: filepath.Clean(path)}
	info, err := os.Stat(path)
	if errors.Is(err, os.ErrNotExist) {
		entry.Created = true
		return entry, nil
	}
	if err != nil {
		return entry, err
	}
	if info.IsDir() {
		return entry, fmt.Errorf("cannot back up directory as file: %s", path)
	}
	entry.Mode = uint32(info.Mode().Perm())
	hash, err := HashFile(path)
	if err != nil {
		return entry, err
	}
	entry.BeforeSHA256 = hash
	name := hash[:16] + "__" + safeName(filepath.Base(path))
	backupPath := filepath.Join(s.Dir, "files", name)
	if err := copyFile(path, backupPath, info.Mode().Perm()); err != nil {
		return entry, err
	}
	entry.BackupPath = backupPath
	return entry, nil
}

func (s *Session) Add(entry Entry) {
	s.Manifest.Entries = append(s.Manifest.Entries, entry)
}

func (s *Session) Finalize() error {
	data, err := json.MarshalIndent(s.Manifest, "", "  ")
	if err != nil {
		return err
	}
	data = append(data, '\n')
	return os.WriteFile(filepath.Join(s.Dir, "manifest.json"), data, 0o644)
}

func List(root string) ([]Manifest, error) {
	entries, err := os.ReadDir(root)
	if errors.Is(err, os.ErrNotExist) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	var manifests []Manifest
	for _, entry := range entries {
		if !entry.IsDir() {
			continue
		}
		manifestPath := filepath.Join(root, entry.Name(), "manifest.json")
		data, err := os.ReadFile(manifestPath)
		if err != nil {
			continue
		}
		var manifest Manifest
		if json.Unmarshal(data, &manifest) == nil {
			manifests = append(manifests, manifest)
		}
	}
	sort.Slice(manifests, func(i, j int) bool { return manifests[i].CreatedAt.After(manifests[j].CreatedAt) })
	return manifests, nil
}

func Load(root, id string) (Manifest, error) {
	data, err := os.ReadFile(filepath.Join(root, id, "manifest.json"))
	if err != nil {
		return Manifest{}, err
	}
	var manifest Manifest
	if err := json.Unmarshal(data, &manifest); err != nil {
		return Manifest{}, err
	}
	return manifest, nil
}

func Restore(manifest Manifest, force bool) error {
	for i := len(manifest.Entries) - 1; i >= 0; i-- {
		entry := manifest.Entries[i]
		if entry.Created {
			if _, err := os.Stat(entry.OriginalPath); err == nil {
				if !force && entry.AfterSHA256 != "" {
					current, hashErr := HashFile(entry.OriginalPath)
					if hashErr != nil {
						return hashErr
					}
					if current != entry.AfterSHA256 {
						return fmt.Errorf("refusing to remove changed file %s; use force", entry.OriginalPath)
					}
				}
				if err := os.Remove(entry.OriginalPath); err != nil {
					return err
				}
			}
			continue
		}
		if entry.BackupPath == "" {
			return fmt.Errorf("backup path missing for %s", entry.OriginalPath)
		}
		if !force && entry.AfterSHA256 != "" {
			if _, err := os.Stat(entry.OriginalPath); err == nil {
				current, hashErr := HashFile(entry.OriginalPath)
				if hashErr != nil {
					return hashErr
				}
				if current != entry.AfterSHA256 {
					return fmt.Errorf("refusing to overwrite changed file %s; use force", entry.OriginalPath)
				}
			}
		}
		if err := os.MkdirAll(filepath.Dir(entry.OriginalPath), 0o755); err != nil {
			return err
		}
		mode := os.FileMode(entry.Mode)
		if mode == 0 {
			mode = 0o644
		}
		if err := copyFile(entry.BackupPath, entry.OriginalPath, mode); err != nil {
			return err
		}
	}
	return nil
}

func HashBytes(data []byte) string {
	sum := sha256.Sum256(data)
	return hex.EncodeToString(sum[:])
}

func HashFile(path string) (string, error) {
	file, err := os.Open(path)
	if err != nil {
		return "", err
	}
	defer file.Close()
	h := sha256.New()
	if _, err := io.Copy(h, file); err != nil {
		return "", err
	}
	return hex.EncodeToString(h.Sum(nil)), nil
}

func copyFile(source, target string, mode os.FileMode) error {
	input, err := os.Open(source)
	if err != nil {
		return err
	}
	defer input.Close()
	if err := os.MkdirAll(filepath.Dir(target), 0o755); err != nil {
		return err
	}
	output, err := os.OpenFile(target, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, mode)
	if err != nil {
		return err
	}
	_, copyErr := io.Copy(output, input)
	closeErr := output.Close()
	if copyErr != nil {
		return copyErr
	}
	return closeErr
}

func safeName(value string) string {
	value = strings.Map(func(r rune) rune {
		if r >= 'a' && r <= 'z' || r >= 'A' && r <= 'Z' || r >= '0' && r <= '9' || r == '.' || r == '-' || r == '_' {
			return r
		}
		return '_'
	}, value)
	if value == "" {
		return "file"
	}
	return value
}
