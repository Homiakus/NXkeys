package discovery

import (
	"bufio"
	"context"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
	"time"

	"github.com/homiakus/nxkeys/internal/config"
)

type FileKind string

const (
	KindMenu       FileKind = "menu"
	KindRole       FileKind = "role"
	KindLauncher   FileKind = "launcher"
	KindCustomDirs FileKind = "custom-dirs"
	KindExecutable FileKind = "nx-executable"
	KindProfile    FileKind = "profile"
	KindUnknown    FileKind = "unknown"
)

type Candidate struct {
	Path       string    `json:"path"`
	Kind       FileKind  `json:"kind"`
	Size       int64     `json:"size"`
	ModifiedAt time.Time `json:"modified_at"`
	Readable   bool      `json:"readable"`
	Writable   bool      `json:"writable"`
	SourceRoot string    `json:"source_root"`
}

type Installation struct {
	Root       string `json:"root"`
	Executable string `json:"executable"`
	Version    string `json:"version"`
	MenusDir   string `json:"menus_dir,omitempty"`
}

type Result struct {
	StartedAt        time.Time      `json:"started_at"`
	FinishedAt       time.Time      `json:"finished_at"`
	Roots            []string       `json:"roots"`
	Files            []Candidate    `json:"files"`
	Installations    []Installation `json:"installations"`
	CustomDirsFiles  []string       `json:"custom_dirs_files"`
	ProfileDirs      []string       `json:"profile_dirs"`
	Warnings         []string       `json:"warnings"`
	FilesVisited     int            `json:"files_visited"`
	DirectoriesSeen  int            `json:"directories_seen"`
	StoppedByFileCap bool           `json:"stopped_by_file_cap"`
}

func Scan(ctx context.Context, cfg config.Config) (Result, error) {
	result := Result{StartedAt: time.Now()}
	roots := append([]string{}, cfg.Scan.Roots...)
	roots = append(roots, cfg.Scan.InstallHints...)
	roots = append(roots, cfg.Scan.ProfileHints...)
	roots = append(roots, config.DefaultRoots()...)
	roots = appendEnvironmentTargets(roots)
	roots = existingUniqueRoots(roots)
	result.Roots = roots

	menuExt := extensionSet(cfg.Scan.MenuExtensions)
	roleExt := extensionSet(cfg.Scan.RoleExtensions)
	launcherExt := extensionSet(cfg.Scan.LauncherExtensions)
	seenFiles := map[string]bool{}
	seenInstallations := map[string]bool{}
	seenProfileDirs := map[string]bool{}
	seenCustomDirs := map[string]bool{}

	for _, root := range roots {
		if err := ctx.Err(); err != nil {
			return result, err
		}
		root = filepath.Clean(root)
		rootDepth := pathDepth(root)
		walkErr := filepath.WalkDir(root, func(path string, entry fs.DirEntry, walkErr error) error {
			if walkErr != nil {
				result.Warnings = append(result.Warnings, fmt.Sprintf("%s: %v", path, walkErr))
				if entry != nil && entry.IsDir() {
					return filepath.SkipDir
				}
				return nil
			}
			if err := ctx.Err(); err != nil {
				return err
			}
			depth := pathDepth(path) - rootDepth
			if entry.IsDir() {
				result.DirectoriesSeen++
				if shouldSkipDirectory(path, entry.Name(), depth, cfg.Scan.MaxDepth) {
					return filepath.SkipDir
				}
				if looksLikeNXProfileDir(path, cfg.Profile.NXVersion) && !seenProfileDirs[strings.ToLower(path)] {
					seenProfileDirs[strings.ToLower(path)] = true
					result.ProfileDirs = append(result.ProfileDirs, path)
				}
				return nil
			}
			kind := classifyFile(path, menuExt, roleExt, launcherExt)
			if kind == KindUnknown {
				return nil
			}
			result.FilesVisited++
			if result.FilesVisited > cfg.Scan.MaxFiles {
				result.StoppedByFileCap = true
				return fs.SkipAll
			}
			key := strings.ToLower(filepath.Clean(path))
			if seenFiles[key] {
				return nil
			}
			seenFiles[key] = true
			candidate := statCandidate(path, kind, root)
			result.Files = append(result.Files, candidate)
			if kind == KindCustomDirs && !seenCustomDirs[key] {
				seenCustomDirs[key] = true
				result.CustomDirsFiles = append(result.CustomDirsFiles, path)
			}
			if kind == KindExecutable {
				installation := detectInstallation(path)
				instKey := strings.ToLower(installation.Root + "|" + installation.Executable)
				if !seenInstallations[instKey] {
					seenInstallations[instKey] = true
					result.Installations = append(result.Installations, installation)
				}
			}
			if kind == KindLauncher {
				for _, target := range parseLauncherTargets(path) {
					if fileExists(target) {
						installation := detectInstallation(target)
						instKey := strings.ToLower(installation.Root + "|" + installation.Executable)
						if !seenInstallations[instKey] {
							seenInstallations[instKey] = true
							result.Installations = append(result.Installations, installation)
						}
					}
				}
			}
			return nil
		})
		if walkErr != nil && walkErr != fs.SkipAll && walkErr != context.Canceled {
			result.Warnings = append(result.Warnings, fmt.Sprintf("scan root %s: %v", root, walkErr))
		}
		if result.StoppedByFileCap {
			break
		}
	}

	sort.Slice(result.Files, func(i, j int) bool {
		if result.Files[i].Kind != result.Files[j].Kind {
			return result.Files[i].Kind < result.Files[j].Kind
		}
		return strings.ToLower(result.Files[i].Path) < strings.ToLower(result.Files[j].Path)
	})
	sort.Slice(result.Installations, func(i, j int) bool {
		return strings.ToLower(result.Installations[i].Root) < strings.ToLower(result.Installations[j].Root)
	})
	sort.Strings(result.CustomDirsFiles)
	sort.Strings(result.ProfileDirs)
	result.FinishedAt = time.Now()
	return result, nil
}

func appendEnvironmentTargets(roots []string) []string {
	for _, key := range []string{"UGII_CUSTOM_DIRECTORY_FILE", "UGII_USER_PROFILE_DIR", "UGII_BASE_DIR", "UGII_ROOT_DIR", "UGII_SITE_DIR"} {
		if value := os.Getenv(key); value != "" {
			if info, err := os.Stat(value); err == nil && !info.IsDir() {
				roots = append(roots, filepath.Dir(value))
			} else {
				roots = append(roots, value)
			}
		}
	}
	return roots
}

func existingUniqueRoots(paths []string) []string {
	seen := map[string]bool{}
	var roots []string
	for _, path := range paths {
		path = config.ExpandPath(path)
		if strings.TrimSpace(path) == "" {
			continue
		}
		info, err := os.Stat(path)
		if err != nil {
			continue
		}
		if !info.IsDir() {
			path = filepath.Dir(path)
		}
		path = filepath.Clean(path)
		key := strings.ToLower(path)
		if seen[key] {
			continue
		}
		seen[key] = true
		roots = append(roots, path)
	}
	return roots
}

func extensionSet(values []string) map[string]bool {
	set := map[string]bool{}
	for _, value := range values {
		value = strings.ToLower(strings.TrimSpace(value))
		if value == "" {
			continue
		}
		if !strings.HasPrefix(value, ".") {
			value = "." + value
		}
		set[value] = true
	}
	return set
}

func classifyFile(path string, menuExt, roleExt, launcherExt map[string]bool) FileKind {
	name := strings.ToLower(filepath.Base(path))
	ext := strings.ToLower(filepath.Ext(path))
	switch name {
	case "ugraf.exe", "nx.exe", "run_nx.exe":
		return KindExecutable
	case "custom_dirs.dat", "custom_dirs.txt", "ugii_custom_directory_file.dat":
		return KindCustomDirs
	case "user.mtx":
		return KindRole
	}
	if menuExt[ext] {
		return KindMenu
	}
	if roleExt[ext] {
		return KindRole
	}
	if launcherExt[ext] && launcherLooksRelevant(path) {
		return KindLauncher
	}
	return KindUnknown
}

func launcherLooksRelevant(path string) bool {
	file, err := os.Open(path)
	if err != nil {
		return false
	}
	defer file.Close()
	scanner := bufio.NewScanner(file)
	for i := 0; i < 200 && scanner.Scan(); i++ {
		line := strings.ToLower(scanner.Text())
		if strings.Contains(line, "ugii_") || strings.Contains(line, "ugraf.exe") || strings.Contains(line, "run_nx.exe") {
			return true
		}
	}
	return false
}

func parseLauncherTargets(path string) []string {
	file, err := os.Open(path)
	if err != nil {
		return nil
	}
	defer file.Close()
	var targets []string
	scanner := bufio.NewScanner(file)
	for scanner.Scan() {
		line := strings.TrimSpace(strings.Trim(scanner.Text(), `"'`))
		lower := strings.ToLower(line)
		for _, exe := range []string{"ugraf.exe", "run_nx.exe", "nx.exe"} {
			idx := strings.Index(lower, exe)
			if idx < 0 {
				continue
			}
			prefix := strings.TrimSpace(line[:idx+len(exe)])
			prefix = strings.TrimPrefix(strings.TrimPrefix(prefix, "start"), "\"")
			prefix = strings.TrimSpace(strings.Trim(prefix, `"'`))
			prefix = os.ExpandEnv(prefix)
			if filepath.IsAbs(prefix) {
				targets = append(targets, filepath.Clean(prefix))
			}
		}
	}
	return targets
}

func statCandidate(path string, kind FileKind, root string) Candidate {
	candidate := Candidate{Path: path, Kind: kind, SourceRoot: root}
	info, err := os.Stat(path)
	if err == nil {
		candidate.Size = info.Size()
		candidate.ModifiedAt = info.ModTime()
		candidate.Readable = canOpen(path, os.O_RDONLY)
		candidate.Writable = canOpen(path, os.O_WRONLY)
	}
	return candidate
}

func canOpen(path string, flag int) bool {
	file, err := os.OpenFile(path, flag, 0)
	if err != nil {
		return false
	}
	_ = file.Close()
	return true
}

func detectInstallation(executable string) Installation {
	path := filepath.Clean(executable)
	root := filepath.Dir(path)
	for i := 0; i < 5; i++ {
		base := strings.ToLower(filepath.Base(root))
		if strings.Contains(base, "nx") || directoryHasNXMarkers(root) {
			break
		}
		parent := filepath.Dir(root)
		if parent == root {
			break
		}
		root = parent
	}
	menusDir := findMenusDir(root)
	return Installation{
		Root:       root,
		Executable: path,
		Version:    versionFromPath(path),
		MenusDir:   menusDir,
	}
}

func directoryHasNXMarkers(root string) bool {
	for _, relative := range []string{filepath.Join("UGII", "menus"), filepath.Join("ugii", "menus"), "NXBIN"} {
		if info, err := os.Stat(filepath.Join(root, relative)); err == nil && info.IsDir() {
			return true
		}
	}
	return false
}

func findMenusDir(root string) string {
	for _, relative := range []string{filepath.Join("UGII", "menus"), filepath.Join("ugii", "menus"), "menus"} {
		candidate := filepath.Join(root, relative)
		if info, err := os.Stat(candidate); err == nil && info.IsDir() {
			return candidate
		}
	}
	return ""
}

func versionFromPath(path string) string {
	lower := strings.ToLower(path)
	for _, token := range []string{"nx2512.6000", "2512.6000", "nx2512", "2512", "nx2506", "2506", "nx2412", "2412"} {
		if strings.Contains(lower, token) {
			return strings.TrimPrefix(token, "nx")
		}
	}
	return "unknown"
}

func looksLikeNXProfileDir(path string, nxVersion string) bool {
	lower := strings.ToLower(path)
	base := strings.ToLower(filepath.Base(path))
	if !strings.Contains(lower, "siemens") && !strings.Contains(lower, "unigraphics") {
		return false
	}
	if strings.HasPrefix(base, "nx") {
		return true
	}
	if nxVersion != "" {
		if strings.Contains(base, strings.ToLower(nxVersion)) {
			return true
		}
		if len(nxVersion) >= 4 && strings.Contains(base, nxVersion[:4]) {
			return true
		}
	}
	return strings.Contains(base, "2512") || strings.Contains(base, "2412")
}

func shouldSkipDirectory(path, name string, depth, maxDepth int) bool {
	if depth > maxDepth {
		return true
	}
	lower := strings.ToLower(name)
	if strings.HasPrefix(lower, ".") && lower != ".config" {
		return true
	}
	for _, skip := range []string{"node_modules", ".git", "$recycle.bin", "windowsapps", "winsxs", "system volume information", "packages"} {
		if lower == skip {
			return true
		}
	}
	if runtime.GOOS == "windows" && strings.Contains(strings.ToLower(path), `\appdata\local\temp\`) {
		return true
	}
	return false
}

func pathDepth(path string) int {
	clean := filepath.Clean(path)
	volume := filepath.VolumeName(clean)
	clean = strings.TrimPrefix(clean, volume)
	clean = strings.Trim(clean, string(filepath.Separator))
	if clean == "" {
		return 0
	}
	return len(strings.Split(clean, string(filepath.Separator)))
}

func fileExists(path string) bool {
	info, err := os.Stat(path)
	return err == nil && !info.IsDir()
}
