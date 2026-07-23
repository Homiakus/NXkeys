package tui

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"

	tea "charm.land/bubbletea/v2"
	"charm.land/lipgloss/v2"

	"github.com/homiakus/nxkeys/internal/config"
	"github.com/homiakus/nxkeys/internal/engine"
	"github.com/homiakus/nxkeys/internal/nxmenu"
)

type screen int

const (
	screenDashboard screen = iota
	screenDiscovery
	screenBindings
	screenPlan
	screenBackups
	screenLogs
)

var screenNames = []string{"Dashboard", "Discovery", "Bindings", "Plan", "Backups", "Logs"}

type scanDoneMsg struct {
	runtime engine.Runtime
	err     error
}

type planDoneMsg struct {
	plan engine.Plan
	err  error
}

type applyDoneMsg struct {
	result engine.ApplyResult
	err    error
}

type restoreDoneMsg struct {
	id  string
	err error
}

type Model struct {
	configPath string
	cfg        config.Config
	runtime    engine.Runtime
	plan       engine.Plan
	screen     screen
	cursor     int
	busy       bool
	dryRun     bool
	confirm    string
	status     string
	logs       []string
	lastApply  engine.ApplyResult
	configs    []string
}

func Run(configPath string, cfg config.Config) error {
	model := NewModel(configPath, cfg)
	program := tea.NewProgram(model)
	_, err := program.Run()
	return err
}

func findConfigFiles() []string {
	var files []string
	entries, _ := os.ReadDir(".")
	for _, entry := range entries {
		if !entry.IsDir() && strings.HasSuffix(strings.ToLower(entry.Name()), ".json") {
			files = append(files, entry.Name())
		}
	}
	entries, _ = os.ReadDir("config")
	for _, entry := range entries {
		if !entry.IsDir() && strings.HasSuffix(strings.ToLower(entry.Name()), ".json") {
			files = append(files, filepath.Join("config", entry.Name()))
		}
	}
	sort.Strings(files)
	return files
}

func NewModel(configPath string, cfg config.Config) Model {
	configs := findConfigFiles()
	cursor := 0
	for i, f := range configs {
		if filepath.Clean(f) == filepath.Clean(configPath) {
			cursor = i
			break
		}
	}
	return Model{
		configPath: configPath,
		cfg:        cfg,
		dryRun:     cfg.Deployment.DryRun,
		status:     "Ready. Press s to scan NX files or select profile.",
		logs:       []string{"NXKeys started with " + configPath},
		configs:    configs,
		cursor:     cursor,
	}
}

func (m Model) Init() tea.Cmd {
	return nil
}

func (m Model) Update(message tea.Msg) (tea.Model, tea.Cmd) {
	switch message := message.(type) {
	case tea.KeyPressMsg:
		key := message.String()
		if m.busy && key != "q" && key != "ctrl+c" {
			return m, nil
		}
		if m.confirm != "" {
			switch key {
			case "y", "Y", "enter":
				action := m.confirm
				m.confirm = ""
				m.busy = true
				if action == "apply" {
					m.status = "Applying plan with backups..."
					return m, applyCmd(m.plan, m.cfg, m.dryRun)
				}
				if action == "restore" {
					m.status = "Restoring latest backup..."
					return m, restoreCmd(m.cfg)
				}
			case "n", "N", "esc":
				m.logs = appendLog(m.logs, "Confirmation cancelled")
				m.confirm = ""
			}
			return m, nil
		}
		switch key {
		case "ctrl+c", "q":
			return m, tea.Quit
		case "tab", "right", "l":
			m.screen = (m.screen + 1) % screen(len(screenNames))
			m = m.resetCursorForScreen()
		case "shift+tab", "left", "h":
			m.screen--
			if m.screen < 0 {
				m.screen = screen(len(screenNames) - 1)
			}
			m = m.resetCursorForScreen()
		case "up", "k":
			if m.cursor > 0 {
				m.cursor--
			}
		case "down", "j":
			if m.screen == screenDashboard {
				if m.cursor < len(m.configs)-1 {
					m.cursor++
				}
			} else {
				m.cursor++
			}
		case "enter":
			if m.screen == screenDashboard && len(m.configs) > 0 {
				newPath := m.configs[m.cursor]
				newCfg, err := config.Load(newPath)
				if err != nil {
					m.status = "Load failed: " + err.Error()
					m.logs = appendLog(m.logs, m.status)
				} else {
					m.configPath = newPath
					m.cfg = newCfg
					m.dryRun = newCfg.Deployment.DryRun
					m.runtime = engine.Runtime{}
					m.plan = engine.Plan{}
					m.status = "Loaded " + newPath
					m.logs = appendLog(m.logs, m.status)
				}
			}
		case "s":
			m.busy = true
			m.status = fmt.Sprintf("Scanning NX %s installations, profiles and menu files...", m.cfg.Profile.NXVersion)
			m.logs = appendLog(m.logs, "Scan started")
			return m, scanCmd(m.cfg)
		case "p":
			if len(m.runtime.Resolutions) == 0 {
				m.status = "Scan first: no command catalog is loaded."
				break
			}
			m.busy = true
			m.status = "Building safe change plan..."
			return m, planCmd(m.runtime)
		case "a":
			if len(m.plan.Actions) == 0 {
				m.status = "Build a plan first with p."
				break
			}
			m.confirm = "apply"
		case "r":
			m.confirm = "restore"
		case "d":
			m.dryRun = !m.dryRun
			m.status = fmt.Sprintf("Dry-run: %v", m.dryRun)
		case "1", "2", "3", "4", "5", "6":
			m.screen = screen(int(key[0] - '1'))
			m = m.resetCursorForScreen()
		}
	case scanDoneMsg:
		m.busy = false
		if message.err != nil {
			m.status = "Scan failed: " + message.err.Error()
			m.logs = appendLog(m.logs, m.status)
			return m, nil
		}
		m.runtime = message.runtime
		m.status = fmt.Sprintf("Scan complete: %d files, %d installations, %d commands.", len(m.runtime.Discovery.Files), len(m.runtime.Discovery.Installations), len(m.runtime.Catalog.Commands))
		m.logs = appendLog(m.logs, m.status)
		return m, planCmd(m.runtime)
	case planDoneMsg:
		m.busy = false
		if message.err != nil {
			m.status = "Plan failed: " + message.err.Error()
			m.logs = appendLog(m.logs, m.status)
			return m, nil
		}
		m.plan = message.plan
		m.status = fmt.Sprintf("Plan ready: %d actions, %d resolved, %d unresolved, %d ambiguous.", len(m.plan.Actions), m.plan.Resolved, m.plan.Unresolved, m.plan.Ambiguous)
		m.logs = appendLog(m.logs, m.status)
	case applyDoneMsg:
		m.busy = false
		if message.err != nil {
			m.status = "Apply failed: " + message.err.Error()
			m.logs = appendLog(m.logs, m.status)
			return m, nil
		}
		m.lastApply = message.result
		m.status = fmt.Sprintf("Apply complete: %d changed, %d skipped, backup %s.", len(message.result.Changed), len(message.result.Skipped), message.result.BackupID)
		if message.result.DryRun {
			m.status = fmt.Sprintf("Dry-run complete: %d files would change.", len(message.result.Changed))
		}
		m.logs = appendLog(m.logs, m.status)
	case restoreDoneMsg:
		m.busy = false
		if message.err != nil {
			m.status = "Restore failed: " + message.err.Error()
		} else {
			m.status = "Restored backup " + message.id
		}
		m.logs = appendLog(m.logs, m.status)
	}
	return m, nil
}

func (m Model) View() tea.View {
	body := m.render()
	view := tea.NewView(body)
	return view
}

func (m Model) render() string {
	header := titleStyle.Render(fmt.Sprintf("NXKeys · Siemens NX %s shortcut configurator", m.cfg.Profile.NXVersion))
	tabs := m.renderTabs()
	content := ""
	switch m.screen {
	case screenDashboard:
		content = m.renderDashboard()
	case screenDiscovery:
		content = m.renderDiscovery()
	case screenBindings:
		content = m.renderBindings()
	case screenPlan:
		content = m.renderPlan()
	case screenBackups:
		content = m.renderBackups()
	case screenLogs:
		content = m.renderLogs()
	}
	footer := helpStyle.Render("1–6 tabs  s scan  p plan  a apply  d dry-run  r restore  q quit")
	status := statusStyle.Render(m.status)
	if m.busy {
		status = busyStyle.Render("◆ ") + status
	}
	if m.confirm != "" {
		status = warningStyle.Render("Confirm " + m.confirm + "? [y/N]")
	}
	return strings.Join([]string{header, tabs, panelStyle.Render(content), status, footer}, "\n")
}

func (m Model) renderTabs() string {
	var parts []string
	for i, name := range screenNames {
		label := fmt.Sprintf(" %d %s ", i+1, name)
		if screen(i) == m.screen {
			parts = append(parts, activeTabStyle.Render(label))
		} else {
			parts = append(parts, inactiveTabStyle.Render(label))
		}
	}
	return strings.Join(parts, " ")
}

func (m Model) renderDashboard() string {
	steps := []struct {
		name   string
		done   bool
		detail string
	}{
		{"Load and validate JSON", true, m.configPath},
		{"Scan NX files", len(m.runtime.Discovery.Files) > 0, fmt.Sprintf("%d candidates", len(m.runtime.Discovery.Files))},
		{"Resolve NX BUTTON identifiers", len(m.runtime.Resolutions) > 0, fmt.Sprintf("%d commands resolved", countStatus(m.runtime.Resolutions, nxmenu.Resolved))},
		{"Build safe deployment plan", len(m.plan.Actions) > 0, fmt.Sprintf("%d file actions", len(m.plan.Actions))},
		{"Apply with backup", len(m.lastApply.Changed) > 0, m.lastApply.BackupID},
	}
	var lines []string
	lines = append(lines, sectionStyle.Render("Progressive setup"), "")
	for index, step := range steps {
		mark := mutedStyle.Render("○")
		if step.done {
			mark = successStyle.Render("●")
		}
		lines = append(lines, fmt.Sprintf("%s  %d. %-32s %s", mark, index+1, step.name, mutedStyle.Render(step.detail)))
	}
	lines = append(lines, "", sectionStyle.Render("Safety model"))
	lines = append(lines,
		"• Generates a MenuScript overlay instead of rewriting Siemens installation menus.",
		"• Uses atomic writes and a SHA-256 backup manifest before every change.",
		"• Refuses ambiguous command-name matches; set command.id in JSON.",
		"• Radial menus are deployed only from an exported .mtx role template.",
		"", fmt.Sprintf("Dry-run: %s", boolBadge(m.dryRun)))
	return strings.Join(lines, "\n")
}

func (m Model) renderDiscovery() string {
	var lines []string
	lines = append(lines, sectionStyle.Render("Detected NX installations"))
	if len(m.runtime.Discovery.Installations) == 0 {
		lines = append(lines, mutedStyle.Render("No scan result. Press s."))
	}
	for _, installation := range m.runtime.Discovery.Installations {
		lines = append(lines, fmt.Sprintf("%s NX %s  %s", successStyle.Render("●"), installation.Version, installation.Executable))
	}
	lines = append(lines, "", sectionStyle.Render("Candidate files"))
	max := 18
	for i, file := range m.runtime.Discovery.Files {
		if i >= max {
			lines = append(lines, mutedStyle.Render(fmt.Sprintf("… %d more", len(m.runtime.Discovery.Files)-max)))
			break
		}
		lines = append(lines, fmt.Sprintf("%-14s %s", file.Kind, file.Path))
	}
	lines = append(lines, "", mutedStyle.Render(fmt.Sprintf("Visited %d files in %d directories", m.runtime.Discovery.FilesVisited, m.runtime.Discovery.DirectoriesSeen)))
	return strings.Join(lines, "\n")
}

func (m Model) renderBindings() string {
	var lines []string
	lines = append(lines, sectionStyle.Render("Keyboard bindings resolved from NX .men files"))
	if len(m.runtime.Resolutions) == 0 {
		return strings.Join(append(lines, mutedStyle.Render("Press s to scan and resolve commands.")), "\n")
	}
	for i, resolution := range m.runtime.Resolutions {
		mark := successStyle.Render("✓")
		if resolution.Status == nxmenu.Ambiguous {
			mark = warningStyle.Render("?")
		}
		if resolution.Status == nxmenu.Unresolved {
			mark = errorStyle.Render("×")
		}
		line := fmt.Sprintf("%s %-14s %-30s → %s", mark, resolution.Binding.Shortcut, truncate(resolution.Binding.Command.Name, 30), resolution.CommandID)
		if i == m.cursor%len(m.runtime.Resolutions) {
			line = selectedStyle.Render(line)
		}
		lines = append(lines, line)
	}
	return strings.Join(lines, "\n")
}

func (m Model) renderPlan() string {
	var lines []string
	lines = append(lines, sectionStyle.Render("Planned file operations"))
	if len(m.plan.Actions) == 0 {
		return strings.Join(append(lines, mutedStyle.Render("Press p after scanning.")), "\n")
	}
	for i, action := range m.plan.Actions {
		mark := mutedStyle.Render("=")
		if action.WillChange {
			mark = warningStyle.Render("+")
		}
		line := fmt.Sprintf("%s %-11s %s", mark, action.Kind, action.Path)
		if i == m.cursor%len(m.plan.Actions) {
			line = selectedStyle.Render(line)
		}
		lines = append(lines, line)
	}
	lines = append(lines, "", fmt.Sprintf("Resolved %d · Unresolved %d · Ambiguous %d", m.plan.Resolved, m.plan.Unresolved, m.plan.Ambiguous))
	for _, warning := range first(m.plan.Warnings, 5) {
		lines = append(lines, warningStyle.Render("! "+warning))
	}
	return strings.Join(lines, "\n")
}

func (m Model) renderBackups() string {
	var lines []string
	lines = append(lines, sectionStyle.Render("Backup and recovery"))
	lines = append(lines,
		"Every changed file is captured before modification.",
		"Manifest fields: original path, backup path, mode, before/after SHA-256.",
		"Restore refuses to overwrite files changed after NXKeys unless forced from CLI.",
		"",
		fmt.Sprintf("Backup root: %s", m.cfg.Deployment.BackupRoot),
		fmt.Sprintf("Last backup: %s", valueOr(m.lastApply.BackupID, "none")),
		"",
		warningStyle.Render("Press r to restore the latest backup."),
	)
	return strings.Join(lines, "\n")
}

func (m Model) renderLogs() string {
	lines := []string{sectionStyle.Render("Session log")}
	lines = append(lines, firstFromEnd(m.logs, 24)...)
	return strings.Join(lines, "\n")
}

func scanCmd(cfg config.Config) tea.Cmd {
	return func() tea.Msg {
		ctx, cancel := context.WithTimeout(context.Background(), 90*time.Second)
		defer cancel()
		runtimeState, err := engine.Analyze(ctx, cfg)
		return scanDoneMsg{runtime: runtimeState, err: err}
	}
}

func planCmd(runtimeState engine.Runtime) tea.Cmd {
	return func() tea.Msg {
		plan, err := engine.BuildPlan(runtimeState)
		return planDoneMsg{plan: plan, err: err}
	}
}

func applyCmd(plan engine.Plan, cfg config.Config, dryRun bool) tea.Cmd {
	return func() tea.Msg {
		result, err := engine.Apply(plan, cfg, dryRun)
		return applyDoneMsg{result: result, err: err}
	}
}

func restoreCmd(cfg config.Config) tea.Cmd {
	return func() tea.Msg {
		id, err := engine.RestoreLatest(cfg, false)
		return restoreDoneMsg{id: id, err: err}
	}
}

func countStatus(resolutions []nxmenu.Resolution, status nxmenu.ResolutionStatus) int {
	count := 0
	for _, resolution := range resolutions {
		if resolution.Status == status {
			count++
		}
	}
	return count
}

func appendLog(logs []string, value string) []string {
	return append(logs, time.Now().Format("15:04:05")+"  "+value)
}

func first(values []string, count int) []string {
	if len(values) < count {
		count = len(values)
	}
	return values[:count]
}

func firstFromEnd(values []string, count int) []string {
	if len(values) <= count {
		return values
	}
	return values[len(values)-count:]
}

func truncate(value string, width int) string {
	runes := []rune(value)
	if len(runes) <= width {
		return value
	}
	if width < 2 {
		return string(runes[:width])
	}
	return string(runes[:width-1]) + "…"
}

func boolBadge(value bool) string {
	if value {
		return warningStyle.Render("ON")
	}
	return successStyle.Render("OFF")
}

func valueOr(value, fallback string) string {
	if strings.TrimSpace(value) == "" {
		return fallback
	}
	return value
}

func (m Model) resetCursorForScreen() Model {
	if m.screen == screenDashboard {
		m.cursor = 0
		for i, f := range m.configs {
			if filepath.Clean(f) == filepath.Clean(m.configPath) {
				m.cursor = i
				break
			}
		}
	} else {
		m.cursor = 0
	}
	return m
}


var (
	accent           = lipgloss.Color("#7C5CFC")
	secondary        = lipgloss.Color("#42D3A5")
	warning          = lipgloss.Color("#F2B84B")
	danger           = lipgloss.Color("#FF6B6B")
	muted            = lipgloss.Color("#7D8491")
	titleStyle       = lipgloss.NewStyle().Bold(true).Foreground(accent).Padding(0, 1)
	activeTabStyle   = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color("#FFFFFF")).Background(accent)
	inactiveTabStyle = lipgloss.NewStyle().Foreground(muted)
	panelStyle       = lipgloss.NewStyle().Border(lipgloss.RoundedBorder()).BorderForeground(lipgloss.Color("#3B3F4A")).Padding(1, 2).Width(112)
	sectionStyle     = lipgloss.NewStyle().Bold(true).Foreground(secondary)
	selectedStyle    = lipgloss.NewStyle().Bold(true).Foreground(lipgloss.Color("#FFFFFF")).Background(lipgloss.Color("#3B315F"))
	successStyle     = lipgloss.NewStyle().Foreground(secondary)
	warningStyle     = lipgloss.NewStyle().Foreground(warning)
	errorStyle       = lipgloss.NewStyle().Foreground(danger)
	mutedStyle       = lipgloss.NewStyle().Foreground(muted)
	statusStyle      = lipgloss.NewStyle().Foreground(lipgloss.Color("#C7CBD1")).Padding(0, 1)
	busyStyle        = lipgloss.NewStyle().Foreground(accent).Bold(true)
	helpStyle        = lipgloss.NewStyle().Foreground(muted).Padding(0, 1)
)
