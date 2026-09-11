using System.ComponentModel;
using System.Diagnostics;
using WolfPatcher.Core;

namespace WolfPatcher.Gui;

public sealed class InstallerForm : Form
{
    private readonly string _packageRoot;
    private readonly InstallerGuiConfig _config;
    private readonly string? _startupManualPath;

    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _summary = new();
    private readonly DataGridView _grid = new();
    private readonly Button _refresh = new();
    private readonly Button _browse = new();
    private readonly Button _action = new();
    private readonly Button _close = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _progressText = new();
    private readonly TextBox _details = new();
    private readonly CheckBox _showDetails = new();

    private readonly List<GameInstallationCandidate> _manualCandidates = [];
    private IReadOnlyList<InstallationCandidateAssessment> _assessments = [];
    private InstallationCandidateAssessment? _selected;

    private DiscoveryProfile? _discoveryProfile;
    private StateInspector? _inspector;
    private InstallerOperationService? _operationService;
    private GameProfile? _gameProfile;
    private BaselineDefinition? _baseline;
    private long _patchPayloadBytes;
    private bool _busy;

    public InstallerForm(string packageRoot, InstallerGuiConfig config, string? startupManualPath)
    {
        _packageRoot = packageRoot;
        _config = config;
        _startupManualPath = startupManualPath;

        BuildUi();
        Shown += async (_, _) => await InitializeAsync();
    }

    private void BuildUi()
    {
        Text = "WolfPatcher — Localização PT-BR";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 610);
        Size = new Size(960, 680);
        Font = new Font("Segoe UI", 9F);

        _title.Text = _config.DisplayName;
        _title.AutoSize = true;
        _title.Font = new Font(Font, FontStyle.Bold);
        _title.Margin = new Padding(0, 0, 0, 2);

        _subtitle.Text = string.IsNullOrWhiteSpace(_config.Publisher)
            ? _config.LocalizationName
            : $"{_config.LocalizationName} — {_config.Publisher}";
        _subtitle.AutoSize = true;
        _subtitle.Margin = new Padding(0, 0, 0, 10);

        _summary.Text = "Inicializando...";
        _summary.AutoSize = true;
        _summary.MaximumSize = new Size(900, 0);
        _summary.Margin = new Padding(0, 0, 0, 8);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add("Store", "Origem");
        _grid.Columns.Add("Status", "Status");
        _grid.Columns.Add("Action", "Ação");
        _grid.Columns.Add("Path", "Pasta do jogo");
        _grid.Columns[0].FillWeight = 15;
        _grid.Columns[1].FillWeight = 22;
        _grid.Columns[2].FillWeight = 22;
        _grid.Columns[3].FillWeight = 60;
        _grid.SelectionChanged += (_, _) => SelectionChanged();

        _refresh.Text = "Verificar novamente";
        _refresh.AutoSize = true;
        _refresh.Click += async (_, _) => await RefreshAsync();

        _browse.Text = "Escolher outra pasta...";
        _browse.AutoSize = true;
        _browse.Click += async (_, _) => await BrowseAsync();

        _action.Text = "Nenhuma ação disponível";
        _action.AutoSize = true;
        _action.Enabled = false;
        _action.Click += async (_, _) => await ExecuteSelectedAsync();

        _close.Text = "Fechar";
        _close.AutoSize = true;
        _close.Click += (_, _) => Close();

        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 30;
        _progress.Visible = false;
        _progress.Dock = DockStyle.Fill;

        _progressText.Text = "";
        _progressText.AutoSize = true;

        _showDetails.Text = "Mostrar detalhes técnicos";
        _showDetails.AutoSize = true;
        _showDetails.CheckedChanged += (_, _) =>
        {
            _details.Visible = _showDetails.Checked;
            UpdateDetailsHeight();
        };

        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Both;
        _details.WordWrap = false;
        _details.Visible = false;
        _details.Dock = DockStyle.Fill;
        _details.Font = new Font("Consolas", 8.5F);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttons.Controls.AddRange([_refresh, _browse, _action, _close]);

        var progressPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        progressPanel.Controls.Add(_progress, 0, 0);
        progressPanel.Controls.Add(_progressText, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 8
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        layout.Controls.Add(_title, 0, 0);
        layout.Controls.Add(_subtitle, 0, 1);
        layout.Controls.Add(_summary, 0, 2);
        layout.Controls.Add(_grid, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        layout.Controls.Add(progressPanel, 0, 5);
        layout.Controls.Add(_showDetails, 0, 6);
        layout.Controls.Add(_details, 0, 7);
        Controls.Add(layout);

        _details.Tag = layout;
    }

    private void UpdateDetailsHeight()
    {
        if (_details.Tag is not TableLayoutPanel layout) return;
        layout.RowStyles[7].SizeType = SizeType.Absolute;
        layout.RowStyles[7].Height = _showDetails.Checked ? 150 : 0;
    }

    private async Task InitializeAsync()
    {
        try
        {
            SetBusy(true, "Carregando perfil e verificando o ambiente...");

            var loaded = ProfileLoader.Load(
                _packageRoot,
                _config.Profile,
                _config.Baseline,
                _config.PatchManifest);

            _discoveryProfile = DiscoveryProfile.Load(
                _config.Resolve(_packageRoot, _config.Profile));
            _inspector = new StateInspector(loaded.Profile, loaded.Baseline);

            var xdelta = _config.Resolve(_packageRoot, _config.XdeltaPath);
            var xz = _config.Resolve(_packageRoot, _config.XzPath);
            var vcdiff = new Xdelta3VcdiffCodec(xdelta);
            var transformers = new TransformerRegistry([
                new VcdiffDirectTransformer(vcdiff),
                new UnityFsSingleLzmaTransformer(vcdiff, new XzRawLzmaCodec(xz))
            ]);
            var patchEngine = new PatchEngine(
                _packageRoot,
                loaded.Profile,
                loaded.Baseline,
                loaded.PatchSet,
                transformers);

            _gameProfile = loaded.Profile;
            _baseline = loaded.Baseline;

            _operationService = new InstallerOperationService(
                new PatchEngineModificationAdapter(patchEngine),
                _inspector,
                _discoveryProfile,
                loaded.Profile,
                loaded.Baseline);

            var patchesRoot = Path.Combine(_packageRoot, "patches");
            _patchPayloadBytes = Directory.Exists(patchesRoot)
                ? Directory.EnumerateFiles(patchesRoot, "*", SearchOption.AllDirectories)
                    .Sum(path => new FileInfo(path).Length)
                : 0L;

            if (!string.IsNullOrWhiteSpace(_startupManualPath))
            {
                var manual = InstallationDiscoveryService.TryManual(_discoveryProfile, _startupManualPath!);
                if (manual is not null)
                    _manualCandidates.Add(manual);
            }

            SetBusy(false, "");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetBusy(false, "Falha ao inicializar.");
            ShowFailure("O instalador não pôde ser inicializado.", ex);
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy || _discoveryProfile is null || _inspector is null) return;

        try
        {
            SetBusy(true, "Procurando instalações e verificando arquivos...");

            var candidates = InstallationDiscoveryService.Discover(_discoveryProfile)
                .Concat(_manualCandidates)
                .ToArray();

            _assessments = await InstallationSelectionService.AssessAsync(_inspector, candidates);
            var decision = InstallationSelectionService.Decide(_assessments);

            PopulateGrid(_assessments);

            if (decision.RequiresUserChoice)
            {
                _grid.ClearSelection();
                _selected = null;
                _summary.Text = decision.Reason;
            }
            else if (decision.Selected is not null)
            {
                SelectAssessment(decision.Selected);
                _summary.Text = decision.Selected.Message;
            }
            else
            {
                _grid.ClearSelection();
                _selected = null;
                _summary.Text = decision.Reason;
            }

            UpdateActionButton();
            AppendDetail($"Discovery concluído. Candidatos: {_assessments.Count}. {decision.Reason}");
        }
        catch (Exception ex)
        {
            ShowFailure("Não foi possível verificar as instalações.", ex);
        }
        finally
        {
            SetBusy(false, "");
        }
    }

    private void PopulateGrid(IEnumerable<InstallationCandidateAssessment> assessments)
    {
        _grid.Rows.Clear();
        foreach (var assessment in assessments)
        {
            var row = _grid.Rows[_grid.Rows.Add(
                GuiPresentation.StoreText(assessment.Candidate.Store),
                assessment.StatusText,
                GuiPresentation.ActionText(assessment),
                assessment.Candidate.RootPath)];
            row.Tag = assessment;
        }
    }

    private void SelectAssessment(InstallationCandidateAssessment assessment)
    {
        _grid.ClearSelection();
        _selected = null;

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is InstallationCandidateAssessment candidate &&
                string.Equals(candidate.Candidate.RootPath, assessment.Candidate.RootPath, StringComparison.OrdinalIgnoreCase))
            {
                // SelectionChanged may fire while CurrentCell/Selected is changing.
                // Make the programmatic selection authoritative afterwards.
                _grid.CurrentCell = row.Cells[0];
                row.Selected = true;

                _selected = candidate;
                _summary.Text = candidate.Message;
                UpdateActionButton();
                return;
            }
        }

        UpdateActionButton();
    }
    private void SelectionChanged()
    {
        if (_grid.SelectedRows.Count != 1 || _grid.SelectedRows[0].Tag is not InstallationCandidateAssessment assessment)
        {
            _selected = null;
            UpdateActionButton();
            return;
        }

        _selected = assessment;
        _summary.Text = assessment.Message;
        UpdateActionButton();
    }

    private void UpdateActionButton()
    {
        _action.Text = GuiPresentation.ActionText(_selected);
        _action.Enabled = !_busy && _selected is { IsBlocked: false } &&
            _selected.PrimaryAction is CandidateAction.Install or CandidateAction.Restore;
    }

    private async Task BrowseAsync()
    {
        if (_busy || _discoveryProfile is null) return;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Escolha a pasta principal do jogo, que contém o executável e a pasta *_Data.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var candidate = InstallationDiscoveryService.TryManual(_discoveryProfile, dialog.SelectedPath);
        if (candidate is null)
        {
            MessageBox.Show(
                this,
                "A pasta escolhida não possui a estrutura mínima esperada para este perfil de jogo.",
                "Pasta não reconhecida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _manualCandidates.RemoveAll(x =>
            string.Equals(x.RootPath, candidate.RootPath, StringComparison.OrdinalIgnoreCase));
        _manualCandidates.Add(candidate);
        await RefreshAsync();
    }

    private async Task ExecuteSelectedAsync()
    {
        if (_busy || _selected is null || _operationService is null ||
            _inspector is null || _discoveryProfile is null || _gameProfile is null || _baseline is null) return;
        if (_selected.IsBlocked) return;

        var action = _selected.PrimaryAction;
        var verb = action == CandidateAction.Install ? "instalar a localização PT-BR" : "restaurar os arquivos originais";
        var confirmation = MessageBox.Show(
            this,
            $"Confirma que deseja {verb} nesta instalação?\n\n{_selected.Candidate.RootPath}\n\nOs arquivos serão revalidados antes de qualquer modificação.",
            "Confirmar operação",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (confirmation != DialogResult.Yes) return;

        try
        {
            SetBusy(true, "Revalidando instalação e permissões...");
            _details.Clear();

            // Read-only preflight happens before the write-access probe. This
            // preserves the rule that an unknown/changed build is refused
            // without even creating a temporary permission-test file.
            var privilege = await InstallerPrivilegeService.CheckAsync(
                _inspector,
                _discoveryProfile,
                _gameProfile,
                _baseline,
                _selected,
                action == CandidateAction.Install ? _patchPayloadBytes : 0L);

            AppendDetail($"Privilege decision: {privilege.Decision}. {privilege.Message}");

            if (privilege.Decision == PrivilegeDecision.Blocked)
            {
                MessageBox.Show(
                    this,
                    privilege.Message + "\n\nNenhum arquivo do jogo foi modificado.",
                    "Operação bloqueada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (privilege.Decision == PrivilegeDecision.ElevationRequired)
            {
                var elevate = MessageBox.Show(
                    this,
                    "Esta pasta exige privilégios administrativos para ser modificada.\n\n" +
                    "O Windows exibirá a confirmação do Controle de Conta de Usuário (UAC). " +
                    "A elevação será usada apenas por um processo auxiliar temporário para esta operação.\n\nContinuar?",
                    "Privilégios necessários",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2);

                if (elevate != DialogResult.Yes) return;
                await ExecuteElevatedAsync(_selected, action);
            }
            else
            {
                await ExecuteInProcessAsync(_selected, action);
            }
        }
        catch (Exception ex)
        {
            ShowFailure("A operação não pôde ser concluída.", ex);
        }
        finally
        {
            SetBusy(false, "");
            await RefreshAsync();
        }
    }

    private async Task ExecuteInProcessAsync(InstallationCandidateAssessment assessment, CandidateAction action)
    {
        if (_operationService is null) return;
        SetBusy(true, "Verificando...");

        var progress = new Progress<InstallerOperationProgress>(p =>
        {
            _progressText.Text = p.Message;
            AppendDetail($"{DateTime.Now:HH:mm:ss} {p.Stage}: {p.Message}");
        });

        var result = await _operationService.ExecuteAsync(
            assessment,
            action == CandidateAction.Install ? _patchPayloadBytes : 0L,
            progress);

        PresentOperationResult(result.Success, result.Message, result.TechnicalDetail);
    }

    private async Task ExecuteElevatedAsync(InstallationCandidateAssessment assessment, CandidateAction action)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("A elevação automática é suportada somente no Windows.");

        var worker = _config.Resolve(_packageRoot, _config.WorkerPath);
        if (!File.Exists(worker))
            throw new FileNotFoundException("Worker de operação elevada não encontrado.", worker);

        var ipcRoot = Path.Combine(Path.GetTempPath(), "WolfPatcher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ipcRoot);
        var progressPath = Path.Combine(ipcRoot, "progress.json");
        var resultPath = Path.Combine(ipcRoot, "result.json");

        try
        {
            var start = new ProcessStartInfo(worker)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = _packageRoot
            };
            start.ArgumentList.Add("--package");
            start.ArgumentList.Add(_packageRoot);
            start.ArgumentList.Add("--config");
            start.ArgumentList.Add("installer.gui.json");
            start.ArgumentList.Add("--game-root");
            start.ArgumentList.Add(assessment.Candidate.RootPath);
            start.ArgumentList.Add("--action");
            start.ArgumentList.Add(action == CandidateAction.Install ? "install" : "restore");
            start.ArgumentList.Add("--progress");
            start.ArgumentList.Add(progressPath);
            start.ArgumentList.Add("--result");
            start.ArgumentList.Add(resultPath);

            Process process;
            try
            {
                process = Process.Start(start)
                    ?? throw new InvalidOperationException("O processo elevado não pôde ser iniciado.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                AppendDetail("Elevação cancelada pelo usuário.");
                MessageBox.Show(
                    this,
                    "A solicitação de privilégios foi cancelada. Nenhuma operação foi iniciada.",
                    "Operação cancelada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (process)
            {
                SetBusy(true, "Aguardando operação com privilégios...");
                long lastSequence = -1;
                while (!process.HasExited)
                {
                    var update = ElevatedOperationChannel.TryReadProgress(progressPath);
                    if (update is not null && update.Sequence != lastSequence)
                    {
                        lastSequence = update.Sequence;
                        _progressText.Text = update.Message;
                        AppendDetail($"{DateTime.Now:HH:mm:ss} {update.Stage}: {update.Message} [worker elevado]");
                    }
                    await Task.Delay(150);
                }
                await process.WaitForExitAsync();
            }

            var result = ElevatedOperationChannel.TryReadResult(resultPath);
            if (result is null)
                throw new InvalidDataException("O worker elevado terminou sem produzir um resultado verificável.");

            PresentOperationResult(result.Success, result.Message, result.TechnicalDetail);
        }
        finally
        {
            try { Directory.Delete(ipcRoot, recursive: true); } catch { }
        }
    }

    private void PresentOperationResult(bool success, string message, string? technicalDetail)
    {
        if (success)
        {
            MessageBox.Show(
                this,
                message,
                "Operação concluída",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(technicalDetail))
            AppendDetail(technicalDetail!);

        MessageBox.Show(
            this,
            message + "\n\nNenhum sucesso foi declarado sem a validação final.",
            "Operação não concluída",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        _showDetails.Checked = true;
    }

    private void SetBusy(bool busy, string message)
    {
        _busy = busy;
        _progress.Visible = busy;
        _progressText.Text = message;
        _refresh.Enabled = !busy;
        _browse.Enabled = !busy;
        _close.Enabled = !busy;
        _grid.Enabled = !busy;
        UpdateActionButton();
    }

    private void ShowFailure(string message, Exception ex)
    {
        AppendDetail(ex.ToString());
        _showDetails.Checked = true;
        MessageBox.Show(
            this,
            message + "\n\n" + ex.Message,
            "WolfPatcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void AppendDetail(string text)
    {
        if (_details.TextLength > 0)
            _details.AppendText(Environment.NewLine);
        _details.AppendText(text);
    }
}
