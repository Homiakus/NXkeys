package nxmenu

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"unicode"

	"github.com/homiakus/nxkeys/internal/config"
)

type Command struct {
	ID           string   `json:"id"`
	Labels       []string `json:"labels"`
	Synonyms     []string `json:"synonyms"`
	Messages     []string `json:"messages"`
	Accelerators []string `json:"accelerators"`
	Sources      []string `json:"sources"`
}

type Catalog struct {
	Commands map[string]*Command `json:"commands"`
}

type ResolutionStatus string

const (
	Resolved   ResolutionStatus = "resolved"
	Ambiguous  ResolutionStatus = "ambiguous"
	Unresolved ResolutionStatus = "unresolved"
)

type CandidateMatch struct {
	ID    string  `json:"id"`
	Label string  `json:"label"`
	Score float64 `json:"score"`
}

type Resolution struct {
	Binding    config.Binding   `json:"binding"`
	Status     ResolutionStatus `json:"status"`
	CommandID  string           `json:"command_id,omitempty"`
	Label      string           `json:"label,omitempty"`
	Candidates []CandidateMatch `json:"candidates,omitempty"`
	Reason     string           `json:"reason,omitempty"`
}

func NewCatalog() Catalog {
	return Catalog{Commands: map[string]*Command{}}
}

func BuildCatalog(paths []string) (Catalog, []string) {
	catalog := NewCatalog()
	var warnings []string
	for _, path := range paths {
		if err := catalog.ParseFile(path); err != nil {
			warnings = append(warnings, fmt.Sprintf("%s: %v", path, err))
		}
	}
	return catalog, warnings
}

func (c Catalog) ParseFile(path string) error {
	file, err := os.Open(path)
	if err != nil {
		return err
	}
	defer file.Close()

	var current *Command
	scanner := bufio.NewScanner(file)
	buffer := make([]byte, 0, 64*1024)
	scanner.Buffer(buffer, 2*1024*1024)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" || strings.HasPrefix(line, "!") || strings.HasPrefix(line, "#") {
			continue
		}
		key, value := splitDirective(line)
		switch strings.ToUpper(key) {
		case "BUTTON", "TOGGLE_BUTTON":
			id := firstToken(value)
			if id == "" || strings.ContainsAny(id, `"'`) {
				current = nil
				continue
			}
			current = c.ensure(id)
			appendUnique(&current.Sources, filepath.Clean(path))
		case "LABEL", "TOOLBAR_LABEL":
			if current != nil {
				appendUnique(&current.Labels, cleanValue(value))
			}
		case "SYNONYMS":
			if current != nil {
				for _, item := range splitSynonyms(value) {
					appendUnique(&current.Synonyms, item)
				}
			}
		case "MESSAGE":
			if current != nil {
				appendUnique(&current.Messages, cleanValue(value))
			}
		case "ACCELERATOR":
			if current != nil {
				appendUnique(&current.Accelerators, cleanValue(value))
			}
		}
	}
	return scanner.Err()
}

func (c Catalog) ensure(id string) *Command {
	key := strings.ToUpper(strings.TrimSpace(id))
	if command, ok := c.Commands[key]; ok {
		return command
	}
	command := &Command{ID: id}
	c.Commands[key] = command
	return command
}

func (c Catalog) ResolveBindings(bindings []config.Binding) []Resolution {
	result := make([]Resolution, 0, len(bindings))
	for _, binding := range bindings {
		result = append(result, c.Resolve(binding))
	}
	return result
}

func (c Catalog) Resolve(binding config.Binding) Resolution {
	resolution := Resolution{Binding: binding}
	if !binding.Enabled {
		resolution.Status = Unresolved
		resolution.Reason = "binding disabled"
		return resolution
	}
	if id := strings.TrimSpace(binding.Command.ID); id != "" {
		if command, ok := c.Commands[strings.ToUpper(id)]; ok {
			resolution.Status = Resolved
			resolution.CommandID = command.ID
			resolution.Label = bestLabel(command)
			return resolution
		}
		// Fallback for explicit IDs not in catalog (e.g. system commands, ribbon buttons)
		resolution.Status = Resolved
		resolution.CommandID = id
		if binding.Command.Name != "" {
			resolution.Label = binding.Command.Name
		} else {
			resolution.Label = id
		}
		return resolution
	}

	queries := append([]string{binding.Command.Name}, binding.Command.Aliases...)
	var matches []CandidateMatch
	for _, command := range c.Commands {
		score := scoreCommand(queries, command)
		if score <= 0 {
			continue
		}
		matches = append(matches, CandidateMatch{ID: command.ID, Label: bestLabel(command), Score: score})
	}
	sort.Slice(matches, func(i, j int) bool {
		if matches[i].Score == matches[j].Score {
			return matches[i].ID < matches[j].ID
		}
		return matches[i].Score > matches[j].Score
	})
	if len(matches) == 0 || matches[0].Score < 0.62 {
		resolution.Status = Unresolved
		resolution.Reason = "no sufficiently strong label/id match"
		resolution.Candidates = take(matches, 5)
		return resolution
	}
	if len(matches) > 1 && matches[0].Score-matches[1].Score < 0.08 {
		resolution.Status = Ambiguous
		resolution.Reason = "multiple NX commands have similar labels; set command.id explicitly in JSON"
		resolution.Candidates = take(matches, 5)
		return resolution
	}
	resolution.Status = Resolved
	resolution.CommandID = matches[0].ID
	resolution.Label = matches[0].Label
	resolution.Candidates = take(matches, 3)
	return resolution
}

func (c Catalog) Conflicts(shortcut string, exceptID string) []Command {
	needle := normalizeShortcut(shortcut)
	var conflicts []Command
	for _, command := range c.Commands {
		if strings.EqualFold(command.ID, exceptID) {
			continue
		}
		for _, accelerator := range command.Accelerators {
			if normalizeShortcut(accelerator) == needle && needle != "" {
				conflicts = append(conflicts, *command)
				break
			}
		}
	}
	sort.Slice(conflicts, func(i, j int) bool { return conflicts[i].ID < conflicts[j].ID })
	return conflicts
}

func GenerateOverlay(version int, menubarID string, resolutions []Resolution, conflicts map[string][]Command, clearConflicts bool) []byte {
	if version <= 0 {
		version = 139
	}
	if version > 139 {
		version = 139
	}
	if menubarID == "" {
		menubarID = "UG_GATEWAY_MAIN_MENUBAR"
	}
	var builder strings.Builder
	builder.WriteString("! Generated by NXKeys. Do not edit manually.\n")
	builder.WriteString("! Safe MenuScript overlay: no Siemens installation files are replaced.\n")
	builder.WriteString(fmt.Sprintf("VERSION %d\n", version))
	builder.WriteString(fmt.Sprintf("EDIT %s\n\n", menubarID))
	builder.WriteString("MODIFY\n")
	cleared := map[string]bool{}
	if clearConflicts {
		for _, resolution := range resolutions {
			if resolution.Status != Resolved {
				continue
			}
			for _, conflict := range conflicts[resolution.Binding.Shortcut] {
				key := strings.ToUpper(conflict.ID)
				if cleared[key] {
					continue
				}
				cleared[key] = true
				builder.WriteString(fmt.Sprintf("\n    ! Clear detected conflict: %s\n", sanitizeComment(bestLabel(&conflict))))
				builder.WriteString(fmt.Sprintf("    BUTTON %s\n", conflict.ID))
				builder.WriteString("    ACCELERATOR\n")
			}
		}
	}
	for _, resolution := range resolutions {
		if resolution.Status != Resolved || !resolution.Binding.Enabled {
			continue
		}
		builder.WriteString(fmt.Sprintf("\n    ! %s [%s]\n", sanitizeComment(resolution.Binding.Command.Name), sanitizeComment(resolution.Binding.Scope)))
		builder.WriteString(fmt.Sprintf("    BUTTON %s\n", resolution.CommandID))
		builder.WriteString(fmt.Sprintf("    ACCELERATOR %s\n", resolution.Binding.Shortcut))
	}
	builder.WriteString("\nEND_OF_MODIFY\n")
	return []byte(builder.String())
}

func splitDirective(line string) (string, string) {
	fields := strings.Fields(line)
	if len(fields) == 0 {
		return "", ""
	}
	key := fields[0]
	value := strings.TrimSpace(strings.TrimPrefix(line, key))
	return key, value
}

func firstToken(value string) string {
	fields := strings.Fields(value)
	if len(fields) == 0 {
		return ""
	}
	return strings.TrimSpace(fields[0])
}

func cleanValue(value string) string {
	value = strings.TrimSpace(value)
	value = strings.Trim(value, `"'`)
	value = strings.ReplaceAll(value, "&", "")
	return strings.TrimSpace(value)
}

func splitSynonyms(value string) []string {
	value = cleanValue(value)
	parts := strings.FieldsFunc(value, func(r rune) bool { return r == ',' || r == ';' })
	var result []string
	for _, part := range parts {
		part = strings.TrimSpace(part)
		if part != "" {
			result = append(result, part)
		}
	}
	return result
}

func appendUnique(values *[]string, value string) {
	value = strings.TrimSpace(value)
	if value == "" {
		return
	}
	for _, existing := range *values {
		if strings.EqualFold(existing, value) {
			return
		}
	}
	*values = append(*values, value)
}

func bestLabel(command *Command) string {
	if command == nil {
		return ""
	}
	if len(command.Labels) > 0 {
		return command.Labels[0]
	}
	return command.ID
}

func scoreCommand(queries []string, command *Command) float64 {
	var haystacks []string
	haystacks = append(haystacks, command.ID)
	haystacks = append(haystacks, command.Labels...)
	haystacks = append(haystacks, command.Synonyms...)
	best := 0.0
	for _, query := range queries {
		for _, haystack := range haystacks {
			if score := similarity(query, haystack); score > best {
				best = score
			}
		}
	}
	return best
}

func containsWords(longer, shorter string) bool {
	wordsL := strings.Fields(longer)
	wordsS := strings.Fields(shorter)
	if len(wordsS) == 0 {
		return false
	}
	if len(wordsS) > len(wordsL) {
		return false
	}
	for i := 0; i <= len(wordsL)-len(wordsS); i++ {
		match := true
		for j := 0; j < len(wordsS); j++ {
			if wordsL[i+j] != wordsS[j] {
				match = false
				break
			}
		}
		if match {
			return true
		}
	}
	return false
}

func similarity(a, b string) float64 {
	a = normalizeText(a)
	b = normalizeText(b)
	if a == "" || b == "" {
		return 0
	}
	if a == b {
		return 1
	}
	isSubstring := false
	var shorterLen, longerLen int
	if len(a) < len(b) {
		if containsWords(b, a) {
			isSubstring = true
			shorterLen = len([]rune(a))
			longerLen = len([]rune(b))
		}
	} else {
		if containsWords(a, b) {
			isSubstring = true
			shorterLen = len([]rune(b))
			longerLen = len([]rune(a))
		}
	}
	if isSubstring {
		return 0.78 + 0.2*float64(shorterLen)/float64(longerLen)
	}
	tokensA := tokenSet(a)
	tokensB := tokenSet(b)
	intersection := 0
	for token := range tokensA {
		if tokensB[token] {
			intersection++
		}
	}
	union := len(tokensA) + len(tokensB) - intersection
	jaccard := 0.0
	if union > 0 {
		jaccard = float64(intersection) / float64(union)
	}
	distance := levenshtein([]rune(a), []rune(b))
	maxLen := len([]rune(a))
	if len([]rune(b)) > maxLen {
		maxLen = len([]rune(b))
	}
	levScore := 1 - float64(distance)/float64(maxLen)
	return 0.58*jaccard + 0.42*levScore
}

func normalizeText(value string) string {
	value = strings.ToLower(value)
	value = strings.Map(func(r rune) rune {
		if unicode.IsLetter(r) || unicode.IsDigit(r) {
			return r
		}
		return ' '
	}, value)
	return strings.Join(strings.Fields(value), " ")
}

func tokenSet(value string) map[string]bool {
	set := map[string]bool{}
	for _, token := range strings.Fields(value) {
		set[token] = true
	}
	return set
}

func levenshtein(a, b []rune) int {
	if len(a) == 0 {
		return len(b)
	}
	if len(b) == 0 {
		return len(a)
	}
	previous := make([]int, len(b)+1)
	for j := range previous {
		previous[j] = j
	}
	for i := 1; i <= len(a); i++ {
		current := make([]int, len(b)+1)
		current[0] = i
		for j := 1; j <= len(b); j++ {
			cost := 0
			if a[i-1] != b[j-1] {
				cost = 1
			}
			current[j] = min(previous[j]+1, current[j-1]+1, previous[j-1]+cost)
		}
		previous = current
	}
	return previous[len(b)]
}

func min(values ...int) int {
	best := values[0]
	for _, value := range values[1:] {
		if value < best {
			best = value
		}
	}
	return best
}

func take(values []CandidateMatch, n int) []CandidateMatch {
	if len(values) < n {
		n = len(values)
	}
	return append([]CandidateMatch(nil), values[:n]...)
}

func normalizeShortcut(value string) string {
	value = strings.ToLower(strings.TrimSpace(value))
	value = strings.ReplaceAll(value, " ", "")
	return value
}

func sanitizeComment(value string) string {
	value = strings.ReplaceAll(value, "\r", " ")
	value = strings.ReplaceAll(value, "\n", " ")
	return strings.TrimSpace(value)
}
