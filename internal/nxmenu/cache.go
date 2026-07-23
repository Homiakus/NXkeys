package nxmenu

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

type catalogCacheFile struct {
	NXVersion string  `json:"nx_version"`
	Signature string  `json:"signature"`
	CreatedAt string  `json:"created_at"`
	Catalog   Catalog `json:"catalog"`
}

func BuildCatalogCached(paths []string, nxVersion string, enabled bool) (Catalog, []string) {
	if !enabled {
		return BuildCatalog(paths)
	}
	signature, cachePath := catalogCachePath(paths, nxVersion)
	if cachePath != "" {
		if cached, ok := readCatalogCache(cachePath, nxVersion, signature); ok {
			return cached, []string{"catalog cache hit: " + cachePath}
		}
	}
	catalog, warnings := BuildCatalog(paths)
	if cachePath != "" {
		if err := writeCatalogCache(cachePath, nxVersion, signature, catalog); err != nil {
			warnings = append(warnings, "catalog cache write failed: "+err.Error())
		}
	}
	return catalog, warnings
}

func catalogCachePath(paths []string, nxVersion string) (string, string) {
	var lines []string
	for _, path := range paths {
		info, err := os.Stat(path)
		if err != nil {
			lines = append(lines, filepath.Clean(path)+"|missing")
			continue
		}
		lines = append(lines, fmt.Sprintf("%s|%d|%d", filepath.Clean(path), info.Size(), info.ModTime().UnixNano()))
	}
	sort.Strings(lines)
	sum := sha256.Sum256([]byte(strings.Join(lines, "\n")))
	signature := hex.EncodeToString(sum[:])
	root := os.Getenv("LOCALAPPDATA")
	if strings.TrimSpace(root) == "" {
		root = os.TempDir()
	}
	cacheDir := filepath.Join(root, "NXKeys", "cache")
	version := strings.NewReplacer("\\", "_", "/", "_", ":", "_", " ", "_").Replace(nxVersion)
	if strings.TrimSpace(version) == "" {
		version = "unknown"
	}
	return signature, filepath.Join(cacheDir, "catalog-"+version+"-"+signature[:16]+".json")
}

func readCatalogCache(path, nxVersion, signature string) (Catalog, bool) {
	data, err := os.ReadFile(path)
	if err != nil {
		return Catalog{}, false
	}
	var payload catalogCacheFile
	if err := json.Unmarshal(data, &payload); err != nil {
		return Catalog{}, false
	}
	if payload.NXVersion != nxVersion || payload.Signature != signature || payload.Catalog.Commands == nil {
		return Catalog{}, false
	}
	return payload.Catalog, true
}

func writeCatalogCache(path, nxVersion, signature string, catalog Catalog) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	payload := catalogCacheFile{
		NXVersion: nxVersion,
		Signature: signature,
		CreatedAt: time.Now().UTC().Format(time.RFC3339Nano),
		Catalog:   catalog,
	}
	data, err := json.Marshal(payload)
	if err != nil {
		return err
	}
	return os.WriteFile(path, data, 0o644)
}
