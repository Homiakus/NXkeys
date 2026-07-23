using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NXOpen;

public sealed class CatalogStudioForm : Form
{
    private readonly Session session;
    private CatalogOptions options;
    private CatalogRunResult lastResult;
    private CancellationTokenSource cancellationSource;

    private readonly Color backColor = Color.FromArgb(18, 22, 29);
    private readonly Color surfaceColor = Color.FromArgb(28, 34, 43);
    private readonly Color elevatedColor = Color.FromArgb(36, 43, 54);
    private readonly Color borderColor = Color.FromArgb(61, 71, 86);
    private readonly Color textColor = Color.FromArgb(235, 239, 245);
    private readonly Color mutedColor = Color.FromArgb(156, 166, 181);
    private readonly Color accentColor = Color.FromArgb(61, 139, 255);
    private readonly Color successColor = Color.FromArgb(61, 201, 143);
    private readonly Color warningColor = Color.FromArgb(255, 187, 77);
    private readonly Color dangerColor = Color.FromArgb(255, 102, 102);

    private Panel contentHost;
    private Panel homePage;
    private Panel scopePage;
    private Panel pathsPage;
    private Panel runPage;
    private Panel resultsPage;
    private Panel profilesPage;

    private Label headerTitle;
    private Label headerSubtitle;
    private Label footerStatus;

    private Button runNowButton;
    private Button cancelButton;
    private ProgressBar progressBar;
    private Label progressStage;
    private Label progressMessage;
    private RichTextBox logBox;

    private TextBox outputDirectoryBox;
    private CheckBox timestampFolderCheck;
    private ListBox rootsList;
    private ListBox detectedRootsList;
    private NumericUpDown scanDepthBox;

    private CheckBox scanManagedCheck;
    private CheckBox scanUiCheck;
    private CheckBox scanUfunCheck;
    private CheckBox buildCrosswalkCheck;

    private readonly Dictionary<string, CheckBox> exportChecks =
        new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);

    private NumericUpDown candidateLimitBox;
    private NumericUpDown minimumScoreBox;
    private NumericUpDown strongScoreBox;
    private CheckBox openWhenCompleteCheck;

    private ListView resultFilesView;
    private Label resultSummaryLabel;
    private TextBox profileNameBox;

    private readonly Dictionary<Control, string> helpTexts =
        new Dictionary<Control, string>();

    public CatalogStudioForm(Session session)
    {
        this.session = session;
        options = LoadDefaultOptions();

        Text = "NX 2512 Catalog Studio";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 700);
        Size = new Size(1220, 790);
        BackColor = backColor;
        ForeColor = textColor;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowIcon = false;

        BuildInterface();
        ApplyOptionsToControls();
        RefreshDetectedRoots();
        ShowPage(homePage, "Обзор", "Полный контроль формирования каталога функций и API NX 2512");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cancellationSource != null)
        {
            DialogResult answer = MessageBox.Show(
                this,
                "Сканирование ещё выполняется. Остановить его и закрыть окно?",
                "NX Catalog Studio",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            cancellationSource.Cancel();
        }

        SaveStateSilently();
        base.OnFormClosing(e);
    }

    private void BuildInterface()
    {
        TableLayoutPanel rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = backColor
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(rootLayout);

        Panel sidebar = BuildSidebar();
        sidebar.Dock = DockStyle.Fill;
        rootLayout.Controls.Add(sidebar, 0, 0);

        TableLayoutPanel workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = backColor
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rootLayout.Controls.Add(workspace, 1, 0);

        Panel header = BuildHeader();
        header.Dock = DockStyle.Fill;
        workspace.Controls.Add(header, 0, 0);

        contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = backColor,
            Padding = new Padding(24)
        };
        workspace.Controls.Add(contentHost, 0, 1);

        Panel footer = BuildFooter();
        footer.Dock = DockStyle.Fill;
        workspace.Controls.Add(footer, 0, 2);

        homePage = BuildHomePage();
        scopePage = BuildScopePage();
        pathsPage = BuildPathsPage();
        runPage = BuildRunPage();
        resultsPage = BuildResultsPage();
        profilesPage = BuildProfilesPage();

        contentHost.Controls.Add(homePage);
        contentHost.Controls.Add(scopePage);
        contentHost.Controls.Add(pathsPage);
        contentHost.Controls.Add(runPage);
        contentHost.Controls.Add(resultsPage);
        contentHost.Controls.Add(profilesPage);
    }

    private Panel BuildSidebar()
    {
        Panel sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = Color.FromArgb(14, 18, 24),
            Padding = new Padding(16, 18, 16, 16)
        };

        Label product = new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            Text = "NX CATALOG",
            ForeColor = textColor,
            Font = new Font("Segoe UI Semibold", 15f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        Label version = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "STUDIO 2512",
            ForeColor = accentColor,
            Font = new Font("Segoe UI Semibold", 9f),
            TextAlign = ContentAlignment.TopLeft
        };

        FlowLayoutPanel navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 390,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 18, 0, 0),
            BackColor = sidebar.BackColor
        };

        navigation.Controls.Add(CreateNavigationButton(
            "⌂  Обзор",
            delegate { ShowPage(homePage, "Обзор", "Состояние, быстрый запуск и состав каталога"); }));

        navigation.Controls.Add(CreateNavigationButton(
            "◉  Состав",
            delegate { ShowPage(scopePage, "Состав каталога", "Выберите источники и выходные таблицы"); }));

        navigation.Controls.Add(CreateNavigationButton(
            "⌘  Пути",
            delegate { ShowPage(pathsPage, "Пути сканирования", "Управление каталогами NX и папкой результата"); }));

        navigation.Controls.Add(CreateNavigationButton(
            "▶  Запуск",
            delegate { ShowPage(runPage, "Запуск и журнал", "Прогресс, остановка и диагностический журнал"); }));

        navigation.Controls.Add(CreateNavigationButton(
            "▤  Результаты",
            delegate { ShowPage(resultsPage, "Результаты", "Сводка и управление созданными файлами"); }));

        navigation.Controls.Add(CreateNavigationButton(
            "⚙  Профили",
            delegate { ShowPage(profilesPage, "Профили JSON", "Сохранение, загрузка и сброс настроек"); }));

        Button close = CreateFlatButton("Закрыть", mutedColor);
        close.Dock = DockStyle.Bottom;
        close.Height = 40;
        close.Click += delegate { Close(); };

        sidebar.Controls.Add(close);
        sidebar.Controls.Add(navigation);
        sidebar.Controls.Add(version);
        sidebar.Controls.Add(product);

        return sidebar;
    }

    private Panel BuildHeader()
    {
        Panel header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = surfaceColor,
            Padding = new Padding(26, 16, 26, 12)
        };

        headerTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 31,
            ForeColor = textColor,
            Font = new Font("Segoe UI Semibold", 16f),
            TextAlign = ContentAlignment.MiddleLeft
        };

        headerSubtitle = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = mutedColor,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.TopLeft
        };

        Button quickRun = CreatePrimaryButton("Запустить полный каталог");
        quickRun.Dock = DockStyle.Right;
        quickRun.Width = 190;
        quickRun.Click += async delegate
        {
            ShowPage(runPage, "Запуск и журнал", "Прогресс, остановка и диагностический журнал");
            await StartCatalogAsync();
        };

        header.Controls.Add(quickRun);
        header.Controls.Add(headerSubtitle);
        header.Controls.Add(headerTitle);

        return header;
    }

    private Panel BuildFooter()
    {
        Panel footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            BackColor = Color.FromArgb(13, 17, 23),
            Padding = new Padding(24, 6, 16, 4)
        };

        footerStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Готово к работе",
            ForeColor = mutedColor,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Label safety = new Label
        {
            Dock = DockStyle.Right,
            Width = 330,
            Text = "Модель и роль NX не изменяются",
            ForeColor = successColor,
            TextAlign = ContentAlignment.MiddleRight
        };

        footer.Controls.Add(safety);
        footer.Controls.Add(footerStatus);
        return footer;
    }

    private Panel BuildHomePage()
    {
        Panel page = CreatePage();

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            BackColor = backColor
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateInfoCard(
            "NX OPEN API",
            "Все установленные namespace, типы, методы, свойства, Builder, Collection и Manager.",
            "Точный managed API",
            accentColor), 0, 0);

        layout.Controls.Add(CreateInfoCard(
            "UI COMMANDS",
            "BUTTON ID, label, synonym, accelerator, action и контекст из MenuScript-файлов.",
            "Команды интерфейса",
            successColor), 1, 0);

        layout.Controls.Add(CreateInfoCard(
            "OPEN C / UFUN",
            "Функции UF_* из обнаруженных заголовков, включая полные сигнатуры и файлы.",
            "Низкоуровневый API",
            warningColor), 2, 0);

        Panel workflow = CreateCard();
        workflow.Margin = new Padding(8);
        Label workflowTitle = CreateCardTitle("Управляемый рабочий процесс");
        Label workflowText = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = mutedColor,
            Font = new Font("Segoe UI", 10f),
            Text =
                "1. Настройте состав и пути.\r\n" +
                "2. Сохраните профиль JSON.\r\n" +
                "3. Запустите сканирование.\r\n" +
                "4. Остановите процесс при необходимости.\r\n" +
                "5. Откройте результаты прямо из панели.",
            Padding = new Padding(0, 12, 0, 0)
        };
        workflow.Controls.Add(workflowText);
        workflow.Controls.Add(workflowTitle);
        layout.SetColumnSpan(workflow, 2);
        layout.Controls.Add(workflow, 0, 1);

        Panel actionCard = CreateCard();
        actionCard.Margin = new Padding(8);
        Label actionTitle = CreateCardTitle("Быстрые действия");
        FlowLayoutPanel actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };

        Button fullRun = CreatePrimaryButton("Запустить полный каталог");
        fullRun.Width = 250;
        fullRun.Height = 42;
        fullRun.Click += async delegate
        {
            ShowPage(runPage, "Запуск и журнал", "Прогресс, остановка и диагностический журнал");
            await StartCatalogAsync();
        };

        Button openLast = CreateFlatButton("Открыть последний результат", textColor);
        openLast.Width = 250;
        openLast.Height = 38;
        openLast.Click += delegate
        {
            if (lastResult != null)
            {
                OpenPath(lastResult.OutputDirectory);
            }
            else
            {
                SetStatus("Результат ещё не создан", warningColor);
            }
        };

        actions.Controls.Add(fullRun);
        actions.Controls.Add(openLast);
        actionCard.Controls.Add(actions);
        actionCard.Controls.Add(actionTitle);
        layout.Controls.Add(actionCard, 2, 1);

        Panel note = CreateCard();
        note.Margin = new Padding(8);
        Label noteTitle = CreateCardTitle("Как трактовать таблицу UI → API");
        Label noteText = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = mutedColor,
            Font = new Font("Segoe UI", 9.5f),
            Text =
                "Совпадение строится эвристически. Даже уровень HIGH необходимо подтвердить " +
                "записью NX Journal: одна команда интерфейса может использовать Builder, " +
                "несколько методов или UFUN.",
            Padding = new Padding(0, 12, 0, 0)
        };
        note.Controls.Add(noteText);
        note.Controls.Add(noteTitle);
        layout.SetColumnSpan(note, 3);
        layout.Controls.Add(note, 0, 2);

        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildScopePage()
    {
        Panel page = CreatePage();

        SplitContainer split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 440,
            BackColor = backColor,
            IsSplitterFixed = false
        };

        Panel sources = CreateCard();
        sources.Dock = DockStyle.Fill;
        sources.Padding = new Padding(20);

        Label sourceTitle = CreateCardTitle("Источники данных");

        FlowLayoutPanel sourceList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 12, 0, 0)
        };

        scanManagedCheck = CreateOptionCheck(
            "NX Open managed API",
            "Сборки NXOpen*.dll, namespace, типы и методы.");
        scanUiCheck = CreateOptionCheck(
            "UI-команды и BUTTON ID",
            "MenuScript, toolbar, radial и связанные определения.");
        scanUfunCheck = CreateOptionCheck(
            "Open C / UFUN",
            "Заголовки uf_*.h и функции UF_*.");
        buildCrosswalkCheck = CreateOptionCheck(
            "Сопоставление UI → API",
            "Кандидаты NXOpen/UFUN с уровнем уверенности.");

        sourceList.Controls.Add(scanManagedCheck);
        sourceList.Controls.Add(scanUiCheck);
        sourceList.Controls.Add(scanUfunCheck);
        sourceList.Controls.Add(buildCrosswalkCheck);

        sources.Controls.Add(sourceList);
        sources.Controls.Add(sourceTitle);
        split.Panel1.Controls.Add(sources);

        Panel exports = CreateCard();
        exports.Dock = DockStyle.Fill;
        exports.Padding = new Padding(20);

        Label exportTitle = CreateCardTitle("Выходные таблицы");

        FlowLayoutPanel exportList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        AddExportCheck(exportList, "environment", "00 — Пути и окружение");
        AddExportCheck(exportList, "assemblies", "01 — NXOpen assemblies");
        AddExportCheck(exportList, "namespaces", "02 — NXOpen namespaces");
        AddExportCheck(exportList, "types", "03 — NXOpen types");
        AddExportCheck(exportList, "members", "04 — NXOpen members");
        AddExportCheck(exportList, "entries", "05 — Builder/Collection/Manager");
        AddExportCheck(exportList, "ui", "06 — UI commands / BUTTON ID");
        AddExportCheck(exportList, "ufun", "07 — UFUN functions");
        AddExportCheck(exportList, "crosswalk", "08 — UI → API candidates");
        AddExportCheck(exportList, "unmapped", "09 — Без сильного API-совпадения");
        AddExportCheck(exportList, "summary", "README — итоговый отчёт");

        FlowLayoutPanel presets = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        Button fullPreset = CreatePrimaryButton("Полный");
        fullPreset.Width = 96;
        fullPreset.Click += delegate { ApplyScopePreset("full"); };

        Button apiPreset = CreateFlatButton("Только API", textColor);
        apiPreset.Width = 110;
        apiPreset.Click += delegate { ApplyScopePreset("api"); };

        Button uiPreset = CreateFlatButton("Только UI", textColor);
        uiPreset.Width = 110;
        uiPreset.Click += delegate { ApplyScopePreset("ui"); };

        Button clearPreset = CreateFlatButton("Снять всё", warningColor);
        clearPreset.Width = 110;
        clearPreset.Click += delegate { ApplyScopePreset("none"); };

        presets.Controls.Add(fullPreset);
        presets.Controls.Add(apiPreset);
        presets.Controls.Add(uiPreset);
        presets.Controls.Add(clearPreset);

        exports.Controls.Add(exportList);
        exports.Controls.Add(presets);
        exports.Controls.Add(exportTitle);
        split.Panel2.Controls.Add(exports);

        scanManagedCheck.CheckedChanged += delegate { UpdateDependencyStates(); };
        scanUiCheck.CheckedChanged += delegate { UpdateDependencyStates(); };
        scanUfunCheck.CheckedChanged += delegate { UpdateDependencyStates(); };
        buildCrosswalkCheck.CheckedChanged += delegate { UpdateDependencyStates(); };

        page.Controls.Add(split);
        return page;
    }

    private Panel BuildPathsPage()
    {
        Panel page = CreatePage();

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel outputCard = CreateCard();
        outputCard.Margin = new Padding(8);
        Label outputTitle = CreateCardTitle("Каталог результата");

        outputDirectoryBox = CreateTextBox();
        outputDirectoryBox.Dock = DockStyle.Top;
        outputDirectoryBox.Margin = new Padding(0, 12, 0, 8);

        Button browseOutput = CreateFlatButton("Выбрать папку", textColor);
        browseOutput.Dock = DockStyle.Top;
        browseOutput.Height = 36;
        browseOutput.Click += delegate { BrowseOutputDirectory(); };

        timestampFolderCheck = CreateCheckBox(
            "Создавать отдельную папку с датой и временем");

        Panel outputBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0)
        };
        outputBody.Controls.Add(timestampFolderCheck);
        outputBody.Controls.Add(browseOutput);
        outputBody.Controls.Add(outputDirectoryBox);

        outputCard.Controls.Add(outputBody);
        outputCard.Controls.Add(outputTitle);
        layout.Controls.Add(outputCard, 0, 0);

        Panel depthCard = CreateCard();
        depthCard.Margin = new Padding(8);
        Label depthTitle = CreateCardTitle("Глубина сканирования");

        scanDepthBox = CreateNumeric(1, 30, 10);
        scanDepthBox.Width = 120;

        Label depthText = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = mutedColor,
            Text =
                "Большая глубина находит больше корпоративных файлов, " +
                "но может замедлить работу на сетевых каталогах.",
            Padding = new Padding(0, 10, 0, 0)
        };

        Panel depthBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0)
        };
        depthBody.Controls.Add(depthText);
        depthBody.Controls.Add(scanDepthBox);
        scanDepthBox.Dock = DockStyle.Top;

        depthCard.Controls.Add(depthBody);
        depthCard.Controls.Add(depthTitle);
        layout.Controls.Add(depthCard, 1, 0);

        Panel detectedCard = CreateCard();
        detectedCard.Margin = new Padding(8);
        Label detectedTitle = CreateCardTitle("Автоматически обнаруженные пути");

        detectedRootsList = CreateListBox();
        detectedRootsList.Dock = DockStyle.Fill;

        Button refreshDetected = CreateFlatButton("Обновить обнаружение", textColor);
        refreshDetected.Dock = DockStyle.Bottom;
        refreshDetected.Height = 36;
        refreshDetected.Click += delegate { RefreshDetectedRoots(); };

        detectedCard.Controls.Add(detectedRootsList);
        detectedCard.Controls.Add(refreshDetected);
        detectedCard.Controls.Add(detectedTitle);
        layout.Controls.Add(detectedCard, 0, 1);

        Panel customCard = CreateCard();
        customCard.Margin = new Padding(8);
        Label customTitle = CreateCardTitle("Дополнительные каталоги");

        rootsList = CreateListBox();
        rootsList.Dock = DockStyle.Fill;

        FlowLayoutPanel rootButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        Button addRoot = CreatePrimaryButton("Добавить");
        addRoot.Width = 110;
        addRoot.Click += delegate { AddCustomRoot(); };

        Button removeRoot = CreateFlatButton("Удалить", dangerColor);
        removeRoot.Width = 110;
        removeRoot.Click += delegate
        {
            if (rootsList.SelectedIndex >= 0)
            {
                rootsList.Items.RemoveAt(rootsList.SelectedIndex);
            }
        };

        rootButtons.Controls.Add(addRoot);
        rootButtons.Controls.Add(removeRoot);

        customCard.Controls.Add(rootsList);
        customCard.Controls.Add(rootButtons);
        customCard.Controls.Add(customTitle);
        layout.Controls.Add(customCard, 1, 1);

        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildRunPage()
    {
        Panel page = CreatePage();

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel runCard = CreateCard();
        runCard.Margin = new Padding(8);
        Label runTitle = CreateCardTitle("Управление выполнением");

        progressStage = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Ожидание запуска",
            ForeColor = textColor,
            Font = new Font("Segoe UI Semibold", 11f)
        };

        progressMessage = new Label
        {
            Dock = DockStyle.Top,
            Height = 25,
            Text = "Настройте профиль и нажмите «Запустить».",
            ForeColor = mutedColor
        };

        progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 18,
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Continuous
        };

        FlowLayoutPanel buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        runNowButton = CreatePrimaryButton("Запустить");
        runNowButton.Width = 145;
        runNowButton.Click += async delegate { await StartCatalogAsync(); };

        cancelButton = CreateFlatButton("Остановить", dangerColor);
        cancelButton.Width = 145;
        cancelButton.Enabled = false;
        cancelButton.Click += delegate
        {
            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
                cancelButton.Enabled = false;
                SetStatus("Запрошена остановка…", warningColor);
            }
        };

        Button clearLog = CreateFlatButton("Очистить журнал", mutedColor);
        clearLog.Width = 150;
        clearLog.Click += delegate { logBox.Clear(); };

        buttons.Controls.Add(runNowButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(clearLog);

        Panel runBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 0)
        };
        runBody.Controls.Add(buttons);
        runBody.Controls.Add(progressBar);
        runBody.Controls.Add(progressMessage);
        runBody.Controls.Add(progressStage);

        runCard.Controls.Add(runBody);
        runCard.Controls.Add(runTitle);
        layout.Controls.Add(runCard, 0, 0);

        Panel logCard = CreateCard();
        logCard.Margin = new Padding(8);
        Label logTitle = CreateCardTitle("Диагностический журнал");

        logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 19, 25),
            ForeColor = Color.FromArgb(204, 214, 229),
            BorderStyle = BorderStyle.None,
            Font = new Font("Cascadia Mono", 9f),
            ReadOnly = true,
            DetectUrls = false
        };

        logCard.Controls.Add(logBox);
        logCard.Controls.Add(logTitle);
        layout.Controls.Add(logCard, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildResultsPage()
    {
        Panel page = CreatePage();

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel summaryCard = CreateCard();
        summaryCard.Margin = new Padding(8);
        Label summaryTitle = CreateCardTitle("Сводка последнего запуска");

        resultSummaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = mutedColor,
            Padding = new Padding(0, 12, 0, 0),
            Text = "Каталог ещё не создавался."
        };

        summaryCard.Controls.Add(resultSummaryLabel);
        summaryCard.Controls.Add(summaryTitle);
        layout.Controls.Add(summaryCard, 0, 0);

        Panel filesCard = CreateCard();
        filesCard.Margin = new Padding(8);
        Label filesTitle = CreateCardTitle("Созданные файлы");

        resultFilesView = new ListView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 21, 28),
            ForeColor = textColor,
            BorderStyle = BorderStyle.FixedSingle,
            View = System.Windows.Forms.View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false
        };
        resultFilesView.Columns.Add("Файл", 360);
        resultFilesView.Columns.Add("Размер", 100);
        resultFilesView.Columns.Add("Путь", 600);
        resultFilesView.DoubleClick += delegate { OpenSelectedResult(); };

        FlowLayoutPanel actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        Button openFolder = CreatePrimaryButton("Открыть папку");
        openFolder.Width = 140;
        openFolder.Click += delegate
        {
            if (lastResult != null)
            {
                OpenPath(lastResult.OutputDirectory);
            }
        };

        Button openFile = CreateFlatButton("Открыть файл", textColor);
        openFile.Width = 135;
        openFile.Click += delegate { OpenSelectedResult(); };

        Button copyPath = CreateFlatButton("Копировать путь", mutedColor);
        copyPath.Width = 155;
        copyPath.Click += delegate
        {
            string path = GetSelectedResultPath();
            if (!String.IsNullOrWhiteSpace(path))
            {
                Clipboard.SetText(path);
                SetStatus("Путь скопирован", successColor);
            }
        };

        actions.Controls.Add(openFolder);
        actions.Controls.Add(openFile);
        actions.Controls.Add(copyPath);

        filesCard.Controls.Add(resultFilesView);
        filesCard.Controls.Add(actions);
        filesCard.Controls.Add(filesTitle);
        layout.Controls.Add(filesCard, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildProfilesPage()
    {
        Panel page = CreatePage();

        TableLayoutPanel layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 245));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel profileCard = CreateCard();
        profileCard.Margin = new Padding(8);
        Label profileTitle = CreateCardTitle("Профиль JSON");

        profileNameBox = CreateTextBox();

        FlowLayoutPanel profileActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };

        profileNameBox.Width = 390;
        profileActions.Controls.Add(profileNameBox);

        Button save = CreatePrimaryButton("Сохранить профиль как…");
        save.Width = 240;
        save.Click += delegate { SaveProfileAs(); };

        Button load = CreateFlatButton("Загрузить профиль…", textColor);
        load.Width = 240;
        load.Click += delegate { LoadProfile(); };

        Button reset = CreateFlatButton("Сбросить к полному профилю", warningColor);
        reset.Width = 240;
        reset.Click += delegate
        {
            options = new CatalogOptions();
            ApplyOptionsToControls();
            SetStatus("Восстановлены настройки полного каталога", successColor);
        };

        profileActions.Controls.Add(save);
        profileActions.Controls.Add(load);
        profileActions.Controls.Add(reset);

        profileCard.Controls.Add(profileActions);
        profileCard.Controls.Add(profileTitle);
        layout.Controls.Add(profileCard, 0, 0);

        Panel matchingCard = CreateCard();
        matchingCard.Margin = new Padding(8);
        Label matchingTitle = CreateCardTitle("Сопоставление UI → API");

        candidateLimitBox = CreateNumeric(1, 20, 5);
        minimumScoreBox = CreateNumeric(0, 100, 35);
        strongScoreBox = CreateNumeric(0, 100, 65);

        TableLayoutPanel matchingGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, 12, 0, 0)
        };
        matchingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        matchingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        matchingGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        matchingGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        matchingGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        matchingGrid.Controls.Add(CreateFieldLabel("Кандидатов на команду"), 0, 0);
        matchingGrid.Controls.Add(candidateLimitBox, 1, 0);
        matchingGrid.Controls.Add(CreateFieldLabel("Минимальная оценка"), 0, 1);
        matchingGrid.Controls.Add(minimumScoreBox, 1, 1);
        matchingGrid.Controls.Add(CreateFieldLabel("Сильное совпадение"), 0, 2);
        matchingGrid.Controls.Add(strongScoreBox, 1, 2);

        matchingCard.Controls.Add(matchingGrid);
        matchingCard.Controls.Add(matchingTitle);
        layout.Controls.Add(matchingCard, 1, 0);

        Panel behaviorCard = CreateCard();
        behaviorCard.Margin = new Padding(8);
        Label behaviorTitle = CreateCardTitle("Поведение");

        openWhenCompleteCheck = CreateCheckBox(
            "Открывать каталог результата после успешного запуска");

        Label behaviorText = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = mutedColor,
            Padding = new Padding(0, 14, 0, 0),
            Text =
                "Настройки сохраняются в JSON. Исходная модель, пользовательская роль, " +
                "горячие клавиши и системные файлы NX не изменяются."
        };

        Panel behaviorBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0)
        };
        behaviorBody.Controls.Add(behaviorText);
        behaviorBody.Controls.Add(openWhenCompleteCheck);
        openWhenCompleteCheck.Dock = DockStyle.Top;

        behaviorCard.Controls.Add(behaviorBody);
        behaviorCard.Controls.Add(behaviorTitle);
        layout.SetColumnSpan(behaviorCard, 2);
        layout.Controls.Add(behaviorCard, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private void ApplyScopePreset(string preset)
    {
        bool full = String.Equals(preset, "full", StringComparison.OrdinalIgnoreCase);
        bool api = String.Equals(preset, "api", StringComparison.OrdinalIgnoreCase);
        bool uiOnly = String.Equals(preset, "ui", StringComparison.OrdinalIgnoreCase);

        scanManagedCheck.Checked = full || api;
        scanUiCheck.Checked = full || uiOnly;
        scanUfunCheck.Checked = full;
        buildCrosswalkCheck.Checked = full;

        foreach (CheckBox check in exportChecks.Values)
        {
            check.Checked = false;
        }

        exportChecks["environment"].Checked = full || api || uiOnly;
        exportChecks["summary"].Checked = full || api || uiOnly;

        if (full || api)
        {
            exportChecks["assemblies"].Checked = true;
            exportChecks["namespaces"].Checked = true;
            exportChecks["types"].Checked = true;
            exportChecks["members"].Checked = true;
            exportChecks["entries"].Checked = true;
        }

        if (full || uiOnly)
        {
            exportChecks["ui"].Checked = true;
        }

        if (full)
        {
            exportChecks["ufun"].Checked = true;
            exportChecks["crosswalk"].Checked = true;
            exportChecks["unmapped"].Checked = true;
        }

        UpdateDependencyStates();

        string description = full
            ? "Выбран полный каталог"
            : api
                ? "Выбран профиль только NX Open API"
                : uiOnly
                    ? "Выбран профиль только UI-команд"
                    : "Все источники отключены";

        SetStatus(description, full || api || uiOnly ? successColor : warningColor);
    }

    private void UpdateDependencyStates()
    {
        if (exportChecks.Count == 0)
        {
            return;
        }

        SetExportEnabled(
            new[] { "assemblies", "namespaces", "types", "members", "entries" },
            scanManagedCheck.Checked);

        SetExportEnabled(
            new[] { "ui" },
            scanUiCheck.Checked);

        SetExportEnabled(
            new[] { "ufun" },
            scanUfunCheck.Checked);

        bool crosswalkAvailable =
            buildCrosswalkCheck.Checked &&
            scanUiCheck.Checked &&
            (scanManagedCheck.Checked || scanUfunCheck.Checked);

        buildCrosswalkCheck.Enabled =
            scanUiCheck.Checked &&
            (scanManagedCheck.Checked || scanUfunCheck.Checked);

        SetExportEnabled(
            new[] { "crosswalk", "unmapped" },
            crosswalkAvailable);
    }

    private void SetExportEnabled(
        IEnumerable<string> keys,
        bool enabled)
    {
        foreach (string key in keys)
        {
            CheckBox check;
            if (!exportChecks.TryGetValue(key, out check))
            {
                continue;
            }

            check.Enabled = enabled;
            check.ForeColor = enabled ? textColor : mutedColor;

            if (!enabled)
            {
                check.Checked = false;
            }
        }
    }

    private async Task StartCatalogAsync()
    {
        if (cancellationSource != null)
        {
            SetStatus("Сканирование уже выполняется", warningColor);
            return;
        }

        ReadControlsIntoOptions();

        if (!ValidateOptions())
        {
            return;
        }

        SaveStateSilently();
        PrepareRunUi();

        cancellationSource = new CancellationTokenSource();

        Progress<CatalogProgress> progress = new Progress<CatalogProgress>(
            delegate(CatalogProgress update)
            {
                if (update.Percent >= 0)
                {
                    progressBar.Value = Math.Max(
                        progressBar.Minimum,
                        Math.Min(progressBar.Maximum, update.Percent));
                    progressStage.Text = update.Stage;
                    progressMessage.Text = update.Message;
                }

                AppendLog(update);
            });

        CatalogRunResult result;

        try
        {
            CatalogOptions runOptions = options.Clone();
            result = await Task.Run(
                delegate
                {
                    return NX2512FullFunctionCatalog.RunCatalog(
                        runOptions,
                        progress,
                        cancellationSource.Token);
                });
        }
        catch (Exception ex)
        {
            result = new CatalogRunResult
            {
                Success = false,
                ErrorMessage = ex.ToString()
            };
        }
        finally
        {
            cancellationSource.Dispose();
            cancellationSource = null;
            runNowButton.Enabled = true;
            cancelButton.Enabled = false;
        }

        lastResult = result;
        UpdateResults(result);

        if (result.Success)
        {
            progressBar.Value = 100;
            progressStage.Text = "Готово";
            progressMessage.Text = "Все выбранные таблицы сформированы.";
            SetStatus("Каталог успешно создан", successColor);

            if (options.OpenOutputWhenComplete)
            {
                OpenPath(result.OutputDirectory);
            }

            ShowPage(
                resultsPage,
                "Результаты",
                "Сводка и управление созданными файлами");
        }
        else if (result.Cancelled)
        {
            progressStage.Text = "Остановлено";
            progressMessage.Text = "Операция отменена пользователем.";
            SetStatus("Сканирование остановлено", warningColor);
        }
        else
        {
            progressStage.Text = "Ошибка";
            progressMessage.Text = "Подробности сохранены в журнале.";
            AppendLog(new CatalogProgress
            {
                Stage = "Ошибка",
                Message = result.ErrorMessage
            });
            SetStatus("Сканирование завершилось с ошибкой", dangerColor);
        }
    }

    private void PrepareRunUi()
    {
        runNowButton.Enabled = false;
        cancelButton.Enabled = true;
        progressBar.Value = 0;
        progressStage.Text = "Подготовка";
        progressMessage.Text = "Запуск фонового сканирования…";
        logBox.Clear();
        AppendLog(new CatalogProgress
        {
            Stage = "Профиль",
            Message = options.ProfileName
        });
        SetStatus("Выполняется сканирование…", accentColor);
    }

    private bool ValidateOptions()
    {
        if (!options.ScanManagedApi &&
            !options.ScanUiCommands &&
            !options.ScanUfun)
        {
            MessageBox.Show(
                this,
                "Выберите хотя бы один источник данных.",
                "NX Catalog Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (options.BuildCrosswalk &&
            !options.ScanUiCommands)
        {
            MessageBox.Show(
                this,
                "Для сопоставления UI → API необходимо включить сканирование UI-команд.",
                "NX Catalog Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (options.BuildCrosswalk &&
            !options.ScanManagedApi &&
            !options.ScanUfun)
        {
            MessageBox.Show(
                this,
                "Для сопоставления UI → API включите NX Open API или Open C / UFUN.",
                "NX Catalog Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            Directory.CreateDirectory(options.OutputDirectory);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Не удалось использовать каталог результата:\r\n" + ex.Message,
                "NX Catalog Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        return true;
    }

    private void UpdateResults(CatalogRunResult result)
    {
        resultFilesView.Items.Clear();

        if (result == null)
        {
            resultSummaryLabel.Text = "Нет данных.";
            return;
        }

        if (result.Success)
        {
            resultSummaryLabel.Text =
                "NXOpen assemblies: " + result.AssemblyCount + "    " +
                "Namespaces: " + result.NamespaceCount + "    " +
                "Types: " + result.TypeCount + "    " +
                "Members: " + result.MemberCount + "\r\n" +
                "UI commands: " + result.UiCommandCount + "    " +
                "UFUN: " + result.UfunFunctionCount + "    " +
                "Crosswalk candidates: " + result.CrosswalkCandidateCount + "\r\n" +
                "Папка: " + result.OutputDirectory;
        }
        else
        {
            resultSummaryLabel.Text = result.Cancelled
                ? "Последний запуск был остановлен.\r\n" + result.OutputDirectory
                : "Последний запуск завершился ошибкой.\r\n" + result.ErrorMessage;
        }

        foreach (string path in result.GeneratedFiles)
        {
            FileInfo info = new FileInfo(path);
            ListViewItem item = new ListViewItem(info.Name);
            item.SubItems.Add(info.Exists ? FormatSize(info.Length) : "—");
            item.SubItems.Add(path);
            item.Tag = path;
            resultFilesView.Items.Add(item);
        }
    }

    private void AppendLog(CatalogProgress update)
    {
        if (update == null || String.IsNullOrWhiteSpace(update.Message))
        {
            return;
        }

        string line =
            DateTime.Now.ToString("HH:mm:ss") +
            "  [" +
            (String.IsNullOrWhiteSpace(update.Stage) ? "INFO" : update.Stage) +
            "]  " +
            update.Message +
            Environment.NewLine;

        logBox.AppendText(line);
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }

    private void ReadControlsIntoOptions()
    {
        options.ProfileName = profileNameBox.Text.Trim();
        options.OutputDirectory = outputDirectoryBox.Text.Trim();
        options.CreateTimestampedSubdirectory = timestampFolderCheck.Checked;

        options.ScanManagedApi = scanManagedCheck.Checked;
        options.ScanUiCommands = scanUiCheck.Checked;
        options.ScanUfun = scanUfunCheck.Checked;
        options.BuildCrosswalk = buildCrosswalkCheck.Checked;

        options.ExportEnvironmentRoots = exportChecks["environment"].Checked;
        options.ExportAssemblies = exportChecks["assemblies"].Checked;
        options.ExportNamespaces = exportChecks["namespaces"].Checked;
        options.ExportTypes = exportChecks["types"].Checked;
        options.ExportMembers = exportChecks["members"].Checked;
        options.ExportEntryPoints = exportChecks["entries"].Checked;
        options.ExportUiCommands = exportChecks["ui"].Checked;
        options.ExportUfunFunctions = exportChecks["ufun"].Checked;
        options.ExportCrosswalk = exportChecks["crosswalk"].Checked;
        options.ExportUnmapped = exportChecks["unmapped"].Checked;
        options.GenerateSummary = exportChecks["summary"].Checked;

        options.ScanDepth = (int)scanDepthBox.Value;
        options.CandidateLimit = (int)candidateLimitBox.Value;
        options.MinimumCandidateScore = (int)minimumScoreBox.Value;
        options.StrongCandidateScore = (int)strongScoreBox.Value;
        options.OpenOutputWhenComplete = openWhenCompleteCheck.Checked;

        options.AdditionalRoots = rootsList.Items
            .Cast<object>()
            .Select(x => x.ToString())
            .Where(x => !String.IsNullOrWhiteSpace(x))
            .ToList();

        options.Normalize();
    }

    private void ApplyOptionsToControls()
    {
        options.Normalize();

        profileNameBox.Text = options.ProfileName;
        outputDirectoryBox.Text = options.OutputDirectory;
        timestampFolderCheck.Checked = options.CreateTimestampedSubdirectory;

        scanManagedCheck.Checked = options.ScanManagedApi;
        scanUiCheck.Checked = options.ScanUiCommands;
        scanUfunCheck.Checked = options.ScanUfun;
        buildCrosswalkCheck.Checked = options.BuildCrosswalk;

        exportChecks["environment"].Checked = options.ExportEnvironmentRoots;
        exportChecks["assemblies"].Checked = options.ExportAssemblies;
        exportChecks["namespaces"].Checked = options.ExportNamespaces;
        exportChecks["types"].Checked = options.ExportTypes;
        exportChecks["members"].Checked = options.ExportMembers;
        exportChecks["entries"].Checked = options.ExportEntryPoints;
        exportChecks["ui"].Checked = options.ExportUiCommands;
        exportChecks["ufun"].Checked = options.ExportUfunFunctions;
        exportChecks["crosswalk"].Checked = options.ExportCrosswalk;
        exportChecks["unmapped"].Checked = options.ExportUnmapped;
        exportChecks["summary"].Checked = options.GenerateSummary;

        scanDepthBox.Value = options.ScanDepth;
        candidateLimitBox.Value = options.CandidateLimit;
        minimumScoreBox.Value = options.MinimumCandidateScore;
        strongScoreBox.Value = options.StrongCandidateScore;
        openWhenCompleteCheck.Checked = options.OpenOutputWhenComplete;

        rootsList.Items.Clear();
        foreach (string root in options.AdditionalRoots)
        {
            rootsList.Items.Add(root);
        }

        UpdateDependencyStates();
    }

    private void RefreshDetectedRoots()
    {
        detectedRootsList.Items.Clear();

        foreach (string root in GetDetectedRoots())
        {
            detectedRootsList.Items.Add(root);
        }

        SetStatus(
            "Обнаружено путей: " + detectedRootsList.Items.Count,
            mutedColor);
    }

    private IEnumerable<string> GetDetectedRoots()
    {
        HashSet<string> result = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        string[] variables =
        {
            "UGII_BASE_DIR",
            "UGII_ROOT_DIR",
            "UGOPEN",
            "UGII_USER_DIR",
            "UGII_SITE_DIR",
            "UGII_GROUP_DIR",
            "UGII_CUSTOM_DIRECTORY_FILE"
        };

        foreach (string variable in variables)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (!String.IsNullOrWhiteSpace(value))
            {
                result.Add(variable + " = " + value);
            }
        }

        try
        {
            string location = typeof(Session).Assembly.Location;
            result.Add("NXOpen.dll = " + location);
        }
        catch
        {
            result.Add("NXOpen.dll = встроенная сборка NX");
        }

        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    private void BrowseOutputDirectory()
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Выберите каталог для результатов NX Catalog Studio";
            dialog.SelectedPath = outputDirectoryBox.Text;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                outputDirectoryBox.Text = dialog.SelectedPath;
            }
        }
    }

    private void AddCustomRoot()
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Добавьте пользовательский или корпоративный каталог NX";

            if (dialog.ShowDialog(this) == DialogResult.OK &&
                !rootsList.Items.Contains(dialog.SelectedPath))
            {
                rootsList.Items.Add(dialog.SelectedPath);
            }
        }
    }

    private void SaveProfileAs()
    {
        ReadControlsIntoOptions();

        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Title = "Сохранить профиль NX Catalog Studio";
            dialog.Filter = "JSON profile (*.json)|*.json";
            dialog.FileName = SanitizeFileName(options.ProfileName) + ".json";

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                options.Save(dialog.FileName);
                SetStatus("Профиль сохранён", successColor);
            }
        }
    }

    private void LoadProfile()
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Title = "Загрузить профиль NX Catalog Studio";
            dialog.Filter = "JSON profile (*.json)|*.json";

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    options = CatalogOptions.Load(dialog.FileName);
                    ApplyOptionsToControls();
                    SetStatus("Профиль загружен", successColor);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Не удалось загрузить профиль",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }
    }

    private CatalogOptions LoadDefaultOptions()
    {
        string path = GetStatePath();

        try
        {
            if (File.Exists(path))
            {
                return CatalogOptions.Load(path);
            }
        }
        catch
        {
            // A damaged state file should never block the application.
        }

        return new CatalogOptions();
    }

    private void SaveStateSilently()
    {
        try
        {
            if (profileNameBox != null)
            {
                ReadControlsIntoOptions();
            }

            options.Save(GetStatePath());
        }
        catch
        {
            // State persistence is optional.
        }
    }

    private static string GetStatePath()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NX2512CatalogStudio");

        return Path.Combine(directory, "last-profile.json");
    }

    private void OpenSelectedResult()
    {
        string path = GetSelectedResultPath();
        if (!String.IsNullOrWhiteSpace(path))
        {
            OpenPath(path);
        }
    }

    private string GetSelectedResultPath()
    {
        if (resultFilesView.SelectedItems.Count == 0)
        {
            return String.Empty;
        }

        return resultFilesView.SelectedItems[0].Tag as string ?? String.Empty;
    }

    private static void OpenPath(string path)
    {
        if (String.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Не удалось открыть",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowPage(
        Panel page,
        string title,
        string subtitle)
    {
        foreach (Control control in contentHost.Controls)
        {
            control.Visible = false;
        }

        page.Visible = true;
        page.BringToFront();
        headerTitle.Text = title;
        headerSubtitle.Text = subtitle;
    }

    private Panel CreatePage()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = backColor,
            Visible = false
        };
    }

    private Panel CreateCard()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = surfaceColor,
            Padding = new Padding(18),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private Label CreateCardTitle(string text)
    {
        return new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = text,
            ForeColor = textColor,
            Font = new Font("Segoe UI Semibold", 11f),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Control CreateInfoCard(
        string overline,
        string description,
        string footer,
        Color accent)
    {
        Panel card = CreateCard();
        card.Margin = new Padding(8);

        Label accentLine = new Label
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = accent
        };

        Label overlineLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = overline,
            ForeColor = accent,
            Font = new Font("Segoe UI Semibold", 10f),
            Padding = new Padding(0, 9, 0, 0)
        };

        Label descriptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = description,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(0, 8, 0, 0)
        };

        Label footerLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Text = footer,
            ForeColor = mutedColor,
            Font = new Font("Segoe UI", 8.5f)
        };

        card.Controls.Add(descriptionLabel);
        card.Controls.Add(footerLabel);
        card.Controls.Add(overlineLabel);
        card.Controls.Add(accentLine);

        return card;
    }

    private Button CreateNavigationButton(
        string text,
        Action click)
    {
        Button button = new Button
        {
            Width = 184,
            Height = 44,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(14, 18, 24),
            ForeColor = mutedColor,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 2),
            Padding = new Padding(8, 0, 0, 0)
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = elevatedColor;
        button.FlatAppearance.MouseDownBackColor = surfaceColor;
        button.Click += delegate { click(); };
        return button;
    }

    private Button CreatePrimaryButton(string text)
    {
        Button button = new Button
        {
            Text = text,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = accentColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Cursor = Cursors.Hand,
            Padding = new Padding(10, 0, 10, 0)
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 153, 255);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 113, 224);
        return button;
    }

    private Button CreateFlatButton(
        string text,
        Color foreground)
    {
        Button button = new Button
        {
            Text = text,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = elevatedColor,
            ForeColor = foreground,
            Font = new Font("Segoe UI Semibold", 9f),
            Cursor = Cursors.Hand,
            Padding = new Padding(8, 0, 8, 0)
        };

        button.FlatAppearance.BorderColor = borderColor;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 54, 67);
        return button;
    }

    private CheckBox CreateOptionCheck(
        string title,
        string description)
    {
        CheckBox box = CreateCheckBox(title);
        box.Width = 370;
        box.Height = 58;
        box.TextAlign = ContentAlignment.TopLeft;
        box.Padding = new Padding(0, 0, 0, 18);

        ToolTip tip = new ToolTip();
        tip.SetToolTip(box, description);
        helpTexts[box] = description;
        return box;
    }

    private CheckBox CreateCheckBox(string text)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = false,
            Width = 390,
            Height = 32,
            ForeColor = textColor,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f),
            UseVisualStyleBackColor = false
        };
    }

    private void AddExportCheck(
        Control parent,
        string key,
        string text)
    {
        CheckBox check = CreateCheckBox(text);
        check.Width = 440;
        exportChecks[key] = check;
        parent.Controls.Add(check);
    }

    private TextBox CreateTextBox()
    {
        return new TextBox
        {
            BackColor = Color.FromArgb(16, 20, 27),
            ForeColor = textColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            Height = 30
        };
    }

    private ListBox CreateListBox()
    {
        return new ListBox
        {
            BackColor = Color.FromArgb(16, 20, 27),
            ForeColor = textColor,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 9f),
            HorizontalScrollbar = true
        };
    }

    private NumericUpDown CreateNumeric(
        int minimum,
        int maximum,
        int value)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            BackColor = Color.FromArgb(16, 20, 27),
            ForeColor = textColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = HorizontalAlignment.Right
        };
    }

    private Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            ForeColor = textColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void SetStatus(
        string text,
        Color color)
    {
        footerStatus.Text = text;
        footerStatus.ForeColor = color;
    }

    private static string SanitizeFileName(string value)
    {
        if (String.IsNullOrWhiteSpace(value))
        {
            return "nx2512-catalog-profile";
        }

        StringBuilder result = new StringBuilder(value.Trim());

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            result.Replace(invalid, '_');
        }

        return result.ToString();
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int index = 0;

        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return value.ToString(index == 0 ? "0" : "0.0") + " " + suffixes[index];
    }
}
