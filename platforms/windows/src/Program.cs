using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("NeonX Agent Hub")]
[assembly: System.Reflection.AssemblyProduct("NeonX Agent Hub")]
[assembly: System.Reflection.AssemblyCompany("NeonX")]
[assembly: System.Reflection.AssemblyDescription("NeonX desktop catalog for local AI agents")]
[assembly: System.Reflection.AssemblyVersion(NeonX.OpenClawInstaller.ComponentVersions.AssemblyVersion)]
[assembly: System.Reflection.AssemblyFileVersion(NeonX.OpenClawInstaller.ComponentVersions.AssemblyVersion)]

namespace NeonX.OpenClawInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Environment.SetEnvironmentVariable("NEONX_OPENCLAW_VERSION", ComponentVersions.OpenClaw, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("NEONX_CODEX_PLUGIN_VERSION", ComponentVersions.CodexPlugin, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("NEONX_NODEJS_VERSION", ComponentVersions.NodeJs, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("NEONX_NODEJS_X64_SHA256", ComponentVersions.NodeX64Sha256, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("NEONX_NODEJS_ARM64_SHA256", ComponentVersions.NodeArm64Sha256, EnvironmentVariableTarget.Process);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private const string Version = ComponentVersions.OpenClaw;
        private const string CodexPluginVersion = ComponentVersions.CodexPlugin;
        private const string RecommendedNodeVersion = ComponentVersions.NodeJsDistribution;
        private const string NodeX64Sha256 = ComponentVersions.NodeX64Sha256;
        private const string NodeArm64Sha256 = ComponentVersions.NodeArm64Sha256;
        private readonly Label status = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly TextBox log = new TextBox();
        private readonly Button install = new Button();
        private readonly Button stop = new Button();
        private readonly Button close = new Button();
        private readonly Button refresh = new Button();
        private readonly LinkLabel source = new LinkLabel();
        private readonly Label nodeStatus = new Label();
        private readonly Label openClawStatus = new Label();
        private readonly Label codexStatus = new Label();
        private bool busy;
        private bool nodeDetected;
        private bool nodeReady;
        private string detectedNodeVersion = "";
        private bool openClawInstalled;
        private bool openClawConfigured;
        private bool codexInstalled;
        private Process gatewayProcess;
        private string gatewayToken = "";
        private int gatewayPort = 18789;

        public InstallerForm()
        {
            Text = "NeonX Agent Hub";
            ClientSize = new Size(820, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            try { Icon = Icon.ExtractAssociatedIcon(typeof(InstallerForm).Assembly.Location); }
            catch { }

            RoundedLogo logoImage = new RoundedLogo();
            logoImage.Image = LoadLogo();
            logoImage.Location = new Point(30, 24);
            logoImage.Size = new Size(72, 72);
            logoImage.CornerRadius = 15;
            Controls.Add(logoImage);
            Controls.Add(MakeLabel("NeonX Agent Hub", 122, 30, 22F, Color.White));
            Controls.Add(MakeLabel("Install, launch, and manage your local AI agents", 124, 70, 9F, Color.FromArgb(148, 163, 184)));
            Controls.Add(MakeLabel("1 agent available", 650, 51, 9F, Color.FromArgb(34, 211, 238)));

            RoundedPanel card = new RoundedPanel();
            card.Location = new Point(30, 120);
            card.Size = new Size(760, 176);
            card.BackColor = Color.FromArgb(30, 41, 59);
            card.BorderColor = Color.FromArgb(51, 65, 85);
            card.CornerRadius = 18;
            Controls.Add(card);

            RoundedLogo agentIcon = new RoundedLogo();
            agentIcon.Image = LoadEmbeddedImage("OpenClawLogo");
            agentIcon.BackColor = card.BackColor;
            agentIcon.CornerRadius = 12;
            agentIcon.Location = new Point(24, 32);
            agentIcon.Size = new Size(58, 58);
            card.Controls.Add(agentIcon);
            card.Controls.Add(MakeLabel("OpenClaw", 104, 20, 16F, Color.White));
            card.Controls.Add(MakeLabel("Local AI agent gateway and control dashboard", 106, 55, 9F, Color.FromArgb(148, 163, 184)));
            card.Controls.Add(MakeLabel("Target version " + Version, 106, 79, 8.5F, Color.FromArgb(100, 116, 139)));

            nodeStatus.Text = "Node.js: checking...";
            nodeStatus.ForeColor = Color.FromArgb(251, 191, 36);
            nodeStatus.AutoSize = true;
            nodeStatus.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            nodeStatus.Location = new Point(106, 101);
            card.Controls.Add(nodeStatus);

            openClawStatus.Text = "OpenClaw: checking...";
            openClawStatus.ForeColor = Color.FromArgb(251, 191, 36);
            openClawStatus.AutoSize = true;
            openClawStatus.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            openClawStatus.Location = new Point(106, 124);
            card.Controls.Add(openClawStatus);

            codexStatus.Text = "Codex plugin: checking...";
            codexStatus.ForeColor = Color.FromArgb(251, 191, 36);
            codexStatus.AutoSize = true;
            codexStatus.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            codexStatus.Location = new Point(106, 147);
            card.Controls.Add(codexStatus);

            source.Text = "Release details";
            source.LinkColor = Color.FromArgb(34, 211, 238);
            source.AutoSize = true;
            source.Location = new Point(480, 150);
            source.LinkClicked += delegate { OpenUrl("https://github.com/openclaw/openclaw/releases/tag/v" + Version); };
            card.Controls.Add(source);

            ConfigureButton(install, "Checking...", 0, Color.FromArgb(8, 145, 178));
            install.Location = new Point(620, 53);
            install.Size = new Size(112, 42);
            install.Enabled = false;
            install.Click += async delegate { await HandleOpenClawActionAsync(); };
            card.Controls.Add(install);

            ConfigureButton(stop, "Stop", 0, Color.FromArgb(185, 28, 28));
            stop.Location = new Point(500, 53);
            stop.Size = new Size(110, 42);
            stop.Enabled = false;
            stop.Visible = false;
            stop.Click += async delegate { await StopOpenClawAsync(); };
            card.Controls.Add(stop);

            status.Text = "Ready to install";
            status.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            status.AutoSize = true;
            status.Location = new Point(32, 320);
            Controls.Add(status);
            progress.Location = new Point(32, 348);
            progress.Size = new Size(756, 12);
            Controls.Add(progress);

            log.Location = new Point(32, 376);
            log.Size = new Size(756, 180);
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BackColor = Color.FromArgb(2, 6, 23);
            log.ForeColor = Color.FromArgb(226, 232, 240);
            log.BorderStyle = BorderStyle.FixedSingle;
            log.Font = new Font("Consolas", 9F);
            log.Text = "NeonX Agent Hub is ready." + Environment.NewLine;
            Controls.Add(log);

            ConfigureButton(refresh, "Refresh", 572, Color.FromArgb(51, 65, 85));
            refresh.Location = new Point(580, 578);
            refresh.Click += async delegate { await RefreshAgentsAsync(); };
            Controls.Add(refresh);
            ConfigureButton(close, "Close", 695, Color.FromArgb(51, 65, 85));
            close.Location = new Point(695, 578);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            FormClosing += OnFormClosing;
            Shown += async delegate { await RefreshAgentsAsync(); };
        }

        private async Task InstallAsync()
        {
            if (busy) return;
            busy = true;
            SetEnabled(false);
            install.Text = "Installing...";
            progress.Style = ProgressBarStyle.Marquee;
            log.Clear();
            string directory = Path.Combine(Path.GetTempPath(), "NeonX", "OpenClaw-" + Version);
            string logPath = Path.Combine(directory, "NeonX-OpenClaw-install.log");

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(logPath, "NeonX Agent Hub " + DateTime.Now + Environment.NewLine, Encoding.UTF8);
                SetStatus("Step 1/4 - Checking system Node.js and npm");
                CommandResult node = await RunCaptureAsync(RefreshPath() + "; node --version");
                if (node.ExitCode != 0)
                    throw new InvalidOperationException("System Node.js was not found. Return to the agent card and use Install Node.");
                string nodeVersion = LastNonEmptyLine(node.Output);
                if (!IsSupportedNodeVersion(nodeVersion))
                    throw new InvalidOperationException("Node.js " + nodeVersion + " is not supported by OpenClaw " + Version + ". Required: >=22.22.3 <23, >=24.15.0 <25, or >=25.9.0.");
                CommandResult npm = await RunCaptureAsync(RefreshPath() + "; npm --version");
                if (npm.ExitCode != 0)
                    throw new InvalidOperationException("npm was not found in the system PATH. Install Node.js with npm, then click Refresh.");
                Write("[OK] node --version: " + nodeVersion);
                Write("[OK] npm --version: " + LastNonEmptyLine(npm.Output));

                SetStatus("Step 2/4 - Installing OpenClaw with system npm");
                Write("Running npm install -g openclaw@" + Version);
                int code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(RefreshPath() + "; npm install -g openclaw@" + Version), logPath);
                if (code != 0) throw new InvalidOperationException("npm returned error code " + code + ".");

                SetStatus("Step 3/4 - Installing the OpenClaw Codex plugin");
                Write("Running openclaw plugins install @openclaw/codex@" + CodexPluginVersion);
                string installCodexPlugin = RefreshPath() + "; openclaw plugins install @openclaw/codex@" + CodexPluginVersion + " --pin --accept-capabilities";
                code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(installCodexPlugin), logPath);
                if (code != 0) throw new InvalidOperationException("Codex plugin installation returned error code " + code + ".");

                SetStatus("Step 4/4 - Checking OpenClaw and the Codex plugin");
                string verify = RefreshPath() + "; $c=Get-Command openclaw -ErrorAction SilentlyContinue; if(!$c){exit 127}; & $c.Source --version";
                code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(verify), logPath);
                if (code != 0) throw new InvalidOperationException("Could not verify openclaw --version.");
                string verifyCodex = RefreshPath() + "; openclaw plugins inspect codex --json";
                code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(verifyCodex), logPath);
                if (code != 0) throw new InvalidOperationException("Could not verify the OpenClaw Codex plugin.");
                await ConfigureCodexPluginAsync();

                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = 100;
                openClawInstalled = true;
                codexInstalled = true;
                openClawConfigured = IsOpenClawConfigured();
                openClawStatus.Text = openClawConfigured
                    ? "OpenClaw: installed - version " + Version
                    : "OpenClaw: installed - onboarding required";
                openClawStatus.ForeColor = Color.FromArgb(74, 222, 128);
                codexStatus.Text = "Codex plugin: installed - version " + CodexPluginVersion;
                codexStatus.ForeColor = Color.FromArgb(74, 222, 128);
                install.Text = openClawConfigured ? "Open" : "Onboard";
                SetStatus("OpenClaw and the Codex plugin installed successfully");
                Write("[COMPLETE] OpenClaw and the Codex plugin are ready.");
                MessageBox.Show("OpenClaw " + Version + " and @openclaw/codex " + CodexPluginVersion + " were installed successfully.", "NeonX Agent Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LaunchOnboarding();
            }
            catch (Exception error)
            {
                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = 0;
                openClawInstalled = false;
                openClawStatus.Text = "OpenClaw: not installed";
                openClawStatus.ForeColor = Color.FromArgb(248, 113, 113);
                install.Text = nodeReady ? "Install" : (nodeDetected ? "Upgrade Node" : "Install Node");
                SetStatus("Installation did not complete");
                Write("[ERROR] " + error.Message);
                AppendFile(logPath, "[ERROR] " + error);
                MessageBox.Show(error.Message + "\r\n\r\nInstallation log:\r\n" + logPath, "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                SetEnabled(true);
                install.Enabled = true;
            }
        }

        private async Task HandleOpenClawActionAsync()
        {
            if (openClawInstalled)
            {
                if (!codexInstalled)
                {
                    await InstallCodexAsync();
                    return;
                }
                if (!openClawConfigured)
                {
                    LaunchOnboarding();
                    return;
                }
                await OpenDashboardAsync();
                return;
            }
            if (!nodeReady)
            {
                await InstallNodeAsync();
                return;
            }
            await InstallAsync();
        }

        private async Task InstallCodexAsync()
        {
            if (busy) return;
            busy = true;
            SetEnabled(false);
            install.Text = "Installing...";
            progress.Style = ProgressBarStyle.Marquee;
            string directory = Path.Combine(Path.GetTempPath(), "NeonX", "CodexPlugin-" + CodexPluginVersion);
            string logPath = Path.Combine(directory, "NeonX-CodexPlugin-install.log");
            bool installed = false;

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(logPath, "NeonX Agent Hub " + DateTime.Now + Environment.NewLine, Encoding.UTF8);
                SetStatus("Installing @openclaw/codex " + CodexPluginVersion);
                Write("Running openclaw plugins install @openclaw/codex@" + CodexPluginVersion);
                string installPlugin = RefreshPath() + "; openclaw plugins install @openclaw/codex@" + CodexPluginVersion + " --pin --accept-capabilities";
                int code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(installPlugin), logPath);
                if (code != 0) throw new InvalidOperationException("Codex plugin installation returned error code " + code + ".");

                string verify = RefreshPath() + "; openclaw plugins inspect codex --json";
                code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(verify), logPath);
                if (code != 0) throw new InvalidOperationException("Could not verify the OpenClaw Codex plugin.");
                await ConfigureCodexPluginAsync();

                installed = true;
                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = 100;
                SetStatus("Codex plugin " + CodexPluginVersion + " installed successfully");
                Write("[COMPLETE] The OpenClaw Codex plugin is ready.");
                MessageBox.Show("@openclaw/codex " + CodexPluginVersion + " was installed successfully.", "NeonX Agent Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = 0;
                SetStatus("Codex plugin installation did not complete");
                Write("[ERROR] " + error.Message);
                AppendFile(logPath, "[ERROR] " + error);
                MessageBox.Show(error.Message + "\r\n\r\nInstallation log:\r\n" + logPath, "Codex plugin installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                SetEnabled(true);
            }

            await RefreshAgentsAsync();
            if (installed && openClawInstalled && openClawConfigured) await OpenDashboardAsync();
        }

        private async Task InstallNodeAsync()
        {
            string reason = nodeDetected
                ? "The installed Node.js " + detectedNodeVersion + " is not compatible with OpenClaw " + Version + "."
                : "Node.js was not found in the system PATH.";
            string explanation = reason + "\r\n\r\nOpenClaw requires Node 22.22.3+, Node 24.15+, or Node 25.9+. Node 26 is recommended for the WAL-reset-safe linked SQLite runtime.\r\n\r\nNeonX will download Node.js " + RecommendedNodeVersion + " from nodejs.org and install it system-wide with Administrator permission. Continue?";
            if (MessageBox.Show(explanation, "Install Node.js 26", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

            string architecture = GetNodeInstallerArchitecture();
            if (architecture == null)
            {
                MessageBox.Show("Automatic Node.js 26 installation supports 64-bit x64 and ARM64 Windows. Please install Node.js manually from nodejs.org.", "Unsupported architecture", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                OpenUrl("https://nodejs.org/en/download");
                return;
            }

            busy = true;
            SetEnabled(false);
            install.Text = "Downloading...";
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 0;
            string directory = Path.Combine(Path.GetTempPath(), "NeonX", "Node-" + RecommendedNodeVersion);
            string fileName = "node-" + RecommendedNodeVersion + "-" + architecture + ".msi";
            string installerPath = Path.Combine(directory, fileName);
            string url = "https://nodejs.org/dist/" + RecommendedNodeVersion + "/" + fileName;
            bool installed = false;

            try
            {
                Directory.CreateDirectory(directory);
                SetStatus("Downloading Node.js " + RecommendedNodeVersion + " system installer");
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "NeonX-Agent-Hub/" + Version);
                    client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
                    {
                        UpdateNodeDownloadProgress(e.BytesReceived, e.TotalBytesToReceive);
                    };
                    await client.DownloadFileTaskAsync(new Uri(url), installerPath);
                }

                string expectedHash = architecture == "arm64" ? NodeArm64Sha256 : NodeX64Sha256;
                string actualHash = Sha256(installerPath);
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Node.js installer SHA-256 verification failed.");
                Write("[OK] Node.js installer SHA-256 verified.");

                install.Text = "Installing...";
                progress.Style = ProgressBarStyle.Marquee;
                SetStatus("Installing Node.js " + RecommendedNodeVersion + " system-wide - Administrator permission required");
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
                info.Arguments = "/i " + Quote(installerPath) + " /passive /norestart";
                info.UseShellExecute = true;
                info.Verb = "runas";
                using (Process process = Process.Start(info))
                {
                    await Task.Run(delegate { process.WaitForExit(); });
                    if (process.ExitCode != 0 && process.ExitCode != 3010)
                        throw new InvalidOperationException("Node.js installer returned error code " + process.ExitCode + ".");
                }
                installed = true;
                SetStatus("Node.js " + RecommendedNodeVersion + " installed successfully - checking system");
                Write("[OK] Node.js " + RecommendedNodeVersion + " installed system-wide.");
            }
            catch (Win32Exception error)
            {
                string message = error.NativeErrorCode == 1223 ? "Administrator permission was cancelled. Node.js was not installed." : error.Message;
                SetStatus("Node.js installation did not complete");
                Write("[ERROR] " + message);
                MessageBox.Show(message, "Node.js installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception error)
            {
                SetStatus("Node.js installation did not complete");
                Write("[ERROR] " + error.Message);
                MessageBox.Show(error.Message, "Node.js installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                SetEnabled(true);
                progress.Style = ProgressBarStyle.Blocks;
                if (!installed) install.Text = nodeDetected ? "Upgrade Node" : "Install Node";
            }

            if (installed) await RefreshAgentsAsync();
        }

        private async Task RefreshAgentsAsync()
        {
            if (busy) return;
            refresh.Enabled = false;
            install.Enabled = false;
            install.Text = "Checking...";
            nodeStatus.Text = "Node.js: checking node --version...";
            nodeStatus.ForeColor = Color.FromArgb(251, 191, 36);
            openClawStatus.Text = "OpenClaw: checking openclaw --version...";
            openClawStatus.ForeColor = Color.FromArgb(251, 191, 36);
            codexStatus.Text = "Codex plugin: checking package...";
            codexStatus.ForeColor = Color.FromArgb(251, 191, 36);
            SetStatus("Checking installed agents");

            Task<CommandResult> nodeCheck = RunNodeCaptureAsync("--version");
            Task<CommandResult> openClawCheck = RunOpenClawCaptureAsync("--version");
            bool codexOnDisk = IsCodexPluginInstalled();
            Task<CommandResult> codexCheck = codexOnDisk ? null : RunOpenClawCaptureAsync("plugins inspect codex --json");
            await Task.WhenAll(nodeCheck, openClawCheck);
            if (codexCheck != null) await codexCheck;
            CommandResult node = nodeCheck.Result;
            CommandResult codexResult = codexCheck != null ? codexCheck.Result : null;
            codexInstalled = codexOnDisk || (codexResult != null && codexResult.ExitCode == 0);
            string nodeVersion = node.ExitCode == 0 ? LastNonEmptyLine(node.Output) : "";
            nodeDetected = node.ExitCode == 0;
            detectedNodeVersion = nodeVersion;
            nodeReady = nodeDetected && IsSupportedNodeVersion(nodeVersion);
            if (nodeReady)
            {
                nodeStatus.Text = "Node.js: " + nodeVersion + " - system runtime ready";
                nodeStatus.ForeColor = Color.FromArgb(74, 222, 128);
            }
            else if (node.ExitCode == 0)
            {
                nodeStatus.Text = "Node.js: " + nodeVersion + " - needs 24.15+; Node 26 recommended";
                nodeStatus.ForeColor = Color.FromArgb(248, 113, 113);
            }
            else
            {
                nodeStatus.Text = "Node.js: not found in system PATH";
                nodeStatus.ForeColor = Color.FromArgb(248, 113, 113);
            }

            CommandResult result = openClawCheck.Result;
            if (codexInstalled)
            {
                codexStatus.Text = "Codex plugin: installed - version " + CodexPluginVersion;
                codexStatus.ForeColor = Color.FromArgb(74, 222, 128);
            }
            else
            {
                codexStatus.Text = "Codex plugin: not installed - included by default";
                codexStatus.ForeColor = Color.FromArgb(248, 113, 113);
            }

            openClawInstalled = result.ExitCode == 0;
            if (openClawInstalled)
            {
                string installedVersion = LastNonEmptyLine(result.Output);
                openClawConfigured = IsOpenClawConfigured();
                openClawStatus.Text = openClawConfigured
                    ? "OpenClaw: installed" + (installedVersion.Length > 0 ? " - version " + installedVersion : "")
                    : "OpenClaw: installed - onboarding required";
                openClawStatus.ForeColor = Color.FromArgb(74, 222, 128);
                install.Text = !codexInstalled ? "Install Codex" : (openClawConfigured ? "Open" : "Onboard");
                SetStatus(!codexInstalled
                    ? "The OpenClaw Codex plugin is included by default and still needs to be installed"
                    : (openClawConfigured ? "OpenClaw is installed and ready" : "Complete OpenClaw onboarding before opening the dashboard"));
            }
            else
            {
                openClawConfigured = false;
                openClawStatus.Text = "OpenClaw: not installed";
                openClawStatus.ForeColor = Color.FromArgb(248, 113, 113);
                install.Text = nodeReady ? "Install" : (nodeDetected ? "Upgrade Node" : "Install Node");
                SetStatus(nodeReady
                    ? "OpenClaw is available to install"
                    : (nodeDetected
                        ? "Node.js " + nodeVersion + " is incompatible; upgrade to Node 26 for OpenClaw"
                        : "Node.js is missing; install Node 26 for OpenClaw"));
            }
            install.Enabled = true;
            refresh.Enabled = true;
            stop.Enabled = openClawInstalled;
        }

        private async Task OpenDashboardAsync()
        {
            if (!IsOpenClawConfigured())
            {
                openClawConfigured = false;
                install.Text = "Onboard";
                LaunchOnboarding();
                return;
            }

            install.Enabled = false;
            install.Text = "Opening...";
            stop.Visible = false;
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 5;
            log.Clear();
            SetStatus("Open 1/5 - Checking installed components");
            Write("[OPEN 1/5] OpenClaw and the Codex plugin are installed.");
            Write("  - Codex configuration is preserved; no plugin settings are rewritten during Open.");

            progress.Value = 25;
            SetStatus("Open 2/5 - Checking local gateway configuration");
            Write("[OPEN 2/5] Checking gateway.mode...");
            CommandResult mode = await RunOpenClawCaptureAsync("config get gateway.mode");
            if (mode.ExitCode != 0 && ((mode.Output + mode.Error).IndexOf("unset", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Write("Setting gateway.mode=local for desktop use...");
                mode = await RunOpenClawCaptureAsync("config set gateway.mode local");
            }
            if (mode.ExitCode != 0)
            {
                ShowDashboardError(mode);
                install.Text = "Open";
                install.Enabled = true;
                return;
            }

            progress.Value = 45;
            SetStatus("Open 3/5 - Resolving the gateway port");
            Write("[OPEN 3/5] Reading the configured gateway port...");
            CommandResult portResult = await RunOpenClawCaptureAsync("config get gateway.port");
            int configuredPort;
            if (portResult.ExitCode == 0 && int.TryParse(LastNonEmptyLine(portResult.Output), out configuredPort) && configuredPort > 0 && configuredPort <= 65535)
                gatewayPort = configuredPort;
            Write("Gateway port: " + gatewayPort);

            progress.Value = 65;
            SetStatus("Open 4/5 - Starting the OpenClaw gateway");
            Write("[OPEN 4/5] Starting the local gateway process...");
            string gatewayError;
            if (!StartOpenClawGateway(gatewayPort, out gatewayError))
            {
                ShowDashboardError(new CommandResult(1, "", gatewayError));
                install.Text = "Open";
                install.Enabled = true;
                return;
            }

            progress.Value = 80;
            SetStatus("Open 5/5 - Waiting for the Control UI");
            Write("[OPEN 5/5] Gateway process started. Waiting for the Control UI...");
            if (!await WaitForGatewayAsync(gatewayPort))
            {
                ShowDashboardError(new CommandResult(1, "", "The OpenClaw gateway did not become ready. Check the OpenClaw gateway log for details."));
            }
            else
            {
                string browserUrl = "http://127.0.0.1:" + gatewayPort + "/#token=" + Uri.EscapeDataString(gatewayToken);
                if (!OpenUrl(browserUrl))
                {
                    Write("[ERROR] Windows could not launch the default browser.");
                    MessageBox.Show("Windows could not launch the default browser.", "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    progress.Value = 100;
                    SetStatus("OpenClaw Control UI opened with token authentication");
                    Write("[READY] OpenClaw dashboard opened in your browser with secure token authentication.");
                    stop.Visible = true;
                    stop.Enabled = true;
                }
            }
            install.Text = "Open";
            install.Enabled = true;
        }

        private async Task StopOpenClawAsync()
        {
            if (busy) return;
            busy = true;
            SetEnabled(false);
            stop.Text = "Stopping...";
            SetStatus("Stopping OpenClaw and its background gateway");
            Write("Stopping the OpenClaw gateway...");

            try
            {
                CommandResult result = await RunOpenClawCaptureAsync("gateway stop");
                StopOwnedGatewayProcess();
                if (result.ExitCode != 0)
                {
                    string details = StripPowerShellClixml(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
                    if (!string.IsNullOrWhiteSpace(details)) Write("[gateway stop] " + details);
                }
                SetStatus("OpenClaw stopped - no NeonX gateway is running in the background");
                Write("[STOPPED] OpenClaw background gateway has been terminated.");
            }
            finally
            {
                gatewayToken = "";
                busy = false;
                SetEnabled(true);
                stop.Text = "Stop";
                stop.Enabled = false;
                stop.Visible = false;
            }
        }

        private void ShowDashboardError(CommandResult result)
        {
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 0;
            stop.Enabled = false;
            stop.Visible = false;
            string details = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            details = StripPowerShellClixml(details);
            if (string.IsNullOrWhiteSpace(details)) details = "OpenClaw did not provide an error message.";
            Write("[ERROR] " + details);
            MessageBox.Show("Could not open the OpenClaw dashboard.\r\n\r\n" + details, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private async Task ConfigureCodexPluginAsync()
        {
            string[] configCommands =
            {
                "config set plugins.entries.codex.config.sessionCatalog.enabled true",
                "config set plugins.entries.codex.config.supervision.enabled true"
            };
            string[] configDescriptions = { "session catalog enabled", "supervision enabled" };
            for (int index = 0; index < configCommands.Length; index++)
            {
                Write("  - Applying Codex setting: " + configDescriptions[index] + "...");
                CommandResult result = await RunOpenClawCaptureAsync(configCommands[index]);
                if (result.ExitCode != 0)
                {
                    string details = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                    throw new InvalidOperationException("Could not configure the Codex plugin: " + StripPowerShellClixml(details));
                }
                Write("  - Codex setting applied: " + configDescriptions[index] + ".");
            }

            Write("  - Applying Codex setting: plugin enabled...");
            CommandResult enable = await RunOpenClawCaptureAsync("plugins enable codex --accept-capabilities");
            if (enable.ExitCode != 0)
            {
                string details = string.IsNullOrWhiteSpace(enable.Error) ? enable.Output : enable.Error;
                throw new InvalidOperationException("Could not enable the Codex plugin: " + StripPowerShellClixml(details));
            }
            Write("  - Codex setting applied: plugin enabled.");

            Write("  - Applying default provider: neonx...");
            string neonxProviderJson = "{\"api\":\"openai-completions\",\"baseUrl\":\"https://api.neonx.ai/v1\",\"models\":[{\"id\":\"gpt-5.6-terra\",\"name\":\"gpt-5.6-terra\"}]}";
            string neonxProviderCommand = "config set models.providers.neonx \"" + neonxProviderJson.Replace("\"", "\\\"") + "\" --strict-json --expect-current-absent";
            CommandResult provider = await RunOpenClawCaptureAsync(neonxProviderCommand);
            if (provider.ExitCode != 0)
            {
                string providerDetails = string.IsNullOrWhiteSpace(provider.Error) ? provider.Error : provider.Output;
                providerDetails = StripPowerShellClixml(providerDetails);
                Write("  - The default neonx provider is already configured or was rejected: " + providerDetails);
            }
            else
            {
                Write("  - Default provider neonx registered with model gpt-5.6-terra.");
            }

            Write("  - Applying default model: neonx/gpt-5.6-terra...");
            string defaultModelCommand = "config set agents.defaults.model.primary \"neonx/gpt-5.6-terra\" --expect-current-absent";
            CommandResult defaultModel = await RunOpenClawCaptureAsync(defaultModelCommand);
            if (defaultModel.ExitCode != 0)
            {
                string modelDetails = string.IsNullOrWhiteSpace(defaultModel.Error) ? defaultModel.Error : defaultModel.Output;
                modelDetails = StripPowerShellClixml(modelDetails);
                Write("  - The default model is already set or was rejected: " + modelDetails);
            }
            else
            {
                Write("  - Default model neonx/gpt-5.6-terra selected.");
            }
            Write("Codex plugin, session catalog, supervision, and the neonx provider are configured.");
        }

        private async Task<CommandResult> RunCaptureAsync(string command)
        {
            ProcessStartInfo info = new ProcessStartInfo(PowerShellPath(), "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(command));
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                await Task.Run(delegate { process.WaitForExit(); });
                return new CommandResult(process.ExitCode, await output, await error);
            }
        }

        private async Task<CommandResult> RunOpenClawCaptureAsync(string arguments)
        {
            string nodePath = FindNodeExecutablePath();
            string openClawEntry = FindOpenClawEntryPath();
            if (nodePath.Length == 0) return new CommandResult(127, "", "System node.exe was not found.");
            if (openClawEntry.Length == 0) return new CommandResult(127, "", "The OpenClaw package entry point was not found.");

            ProcessStartInfo info = new ProcessStartInfo(nodePath, Quote(openClawEntry) + " " + arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.UTF8;
            info.StandardErrorEncoding = Encoding.UTF8;
            using (Process process = Process.Start(info))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                await Task.Run(delegate { process.WaitForExit(); });
                return new CommandResult(process.ExitCode, await output, await error);
            }
        }

        private async Task<CommandResult> RunNodeCaptureAsync(string arguments)
        {
            string nodePath = FindNodeExecutablePath();
            if (nodePath.Length == 0) return new CommandResult(127, "", "System node.exe was not found.");
            ProcessStartInfo info = new ProcessStartInfo(nodePath, arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                await Task.Run(delegate { process.WaitForExit(); });
                return new CommandResult(process.ExitCode, await output, await error);
            }
        }

        private static bool IsCodexPluginInstalled()
        {
            try
            {
                string stateDirectory = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR");
                if (string.IsNullOrWhiteSpace(stateDirectory))
                    stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw");
                string projectsDirectory = Path.Combine(stateDirectory, "npm", "projects");
                if (!Directory.Exists(projectsDirectory)) return false;
                foreach (string projectDirectory in Directory.GetDirectories(projectsDirectory))
                {
                    string packagePath = Path.Combine(projectDirectory, "node_modules", "@openclaw", "codex", "package.json");
                    if (File.Exists(packagePath)) return true;
                }
            }
            catch { }
            return false;
        }

        private bool StartOpenClawGateway(int port, out string error)
        {
            error = "";
            try
            {
                if (gatewayProcess != null && !gatewayProcess.HasExited && gatewayToken.Length > 0 && gatewayPort == port) return true;
                if (gatewayProcess != null) gatewayProcess.Dispose();

                string nodePath = FindNodeExecutablePath();
                string openClawEntry = FindOpenClawEntryPath();
                if (nodePath.Length == 0) { error = "System node.exe was not found."; return false; }
                if (openClawEntry.Length == 0) { error = "The OpenClaw package entry point was not found."; return false; }

                gatewayToken = CreateGatewayToken();
                gatewayPort = port;
                ProcessStartInfo info = new ProcessStartInfo(nodePath, Quote(openClawEntry) + " gateway run --force --auth token --port " + port + " --ws-log compact");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                info.EnvironmentVariables["OPENCLAW_GATEWAY_TOKEN"] = gatewayToken;
                gatewayProcess = Process.Start(info);
                return gatewayProcess != null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void StopOwnedGatewayProcess()
        {
            if (gatewayProcess == null) return;
            try
            {
                if (!gatewayProcess.HasExited)
                {
                    gatewayProcess.Kill();
                    gatewayProcess.WaitForExit(5000);
                }
            }
            catch { }
            finally
            {
                gatewayProcess.Dispose();
                gatewayProcess = null;
                gatewayToken = "";
            }
        }

        private void StopOpenClawBeforeExit()
        {
            StopOwnedGatewayProcess();
            try
            {
                string nodePath = FindNodeExecutablePath();
                string openClawEntry = FindOpenClawEntryPath();
                if (nodePath.Length == 0 || openClawEntry.Length == 0) return;

                ProcessStartInfo info = new ProcessStartInfo(nodePath, Quote(openClawEntry) + " gateway stop");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                using (Process process = Process.Start(info))
                {
                    if (!process.WaitForExit(5000)) process.Kill();
                }
            }
            catch { }
        }

        private async Task<bool> WaitForGatewayAsync(int port)
        {
            string url = "http://127.0.0.1:" + port + "/";
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (gatewayProcess == null || gatewayProcess.HasExited) return false;
                if (attempt % 4 == 0)
                {
                    int elapsedSeconds = attempt / 2;
                    SetStatus("Open 5/5 - Waiting for Control UI (" + elapsedSeconds + "s)");
                    Write("  - Waiting for Control UI: " + elapsedSeconds + " seconds elapsed...");
                    progress.Value = Math.Min(98, 80 + attempt / 2);
                }
                bool ready = await Task.Run(delegate
                {
                    try
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        request.Method = "GET";
                        request.Timeout = 1000;
                        request.ReadWriteTimeout = 1000;
                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                            return response.StatusCode == HttpStatusCode.OK;
                    }
                    catch
                    {
                        return false;
                    }
                });
                if (ready)
                {
                    Write("  - Control UI responded successfully on port " + port + ".");
                    return true;
                }
                await Task.Delay(500);
            }
            return false;
        }

        private static string CreateGatewayToken()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static string LastNonEmptyLine(string value)
        {
            string[] lines = (value ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length == 0 ? "" : lines[lines.Length - 1].Trim().TrimStart('v');
        }

        private static bool IsSupportedNodeVersion(string value)
        {
            try
            {
                Version version = new Version((value ?? "").Trim().TrimStart('v'));
                if (version.Major == 22) return version.CompareTo(new Version(22, 22, 3)) >= 0;
                if (version.Major == 24) return version.CompareTo(new Version(24, 15, 0)) >= 0;
                return version.CompareTo(new Version(25, 9, 0)) >= 0;
            }
            catch { return false; }
        }

        private static string GetNodeInstallerArchitecture()
        {
            string architecture = (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? "") + " " +
                (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "");
            if (architecture.IndexOf("ARM64", StringComparison.OrdinalIgnoreCase) >= 0) return "arm64";
            if (Environment.Is64BitOperatingSystem) return "x64";
            return null;
        }

        private void UpdateNodeDownloadProgress(long downloadedBytes, long totalBytes)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<long, long>(UpdateNodeDownloadProgress), downloadedBytes, totalBytes);
                return;
            }
            if (totalBytes <= 0)
            {
                progress.Style = ProgressBarStyle.Marquee;
                status.Text = "Downloading Node.js " + RecommendedNodeVersion + " - " + FormatMegabytes(downloadedBytes);
                return;
            }
            int percent = (int)Math.Min(100, downloadedBytes * 100L / totalBytes);
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = percent;
            status.Text = "Downloading Node.js " + RecommendedNodeVersion + " - " + percent + "% (" + FormatMegabytes(downloadedBytes) + " / " + FormatMegabytes(totalBytes) + ")";
        }

        private static string FormatMegabytes(long bytes)
        {
            return (bytes / 1024D / 1024D).ToString("0.0") + " MB";
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                StringBuilder result = new StringBuilder();
                foreach (byte value in sha.ComputeHash(stream)) result.Append(value.ToString("X2"));
                return result.ToString();
            }
        }

        private async Task<int> RunAsync(string arguments, string logPath)
        {
            ProcessStartInfo info = new ProcessStartInfo(PowerShellPath(), arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.UTF8;
            info.StandardErrorEncoding = Encoding.UTF8;
            using (Process process = new Process())
            {
                process.StartInfo = info;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Write(e.Data);
                        AppendFile(logPath, e.Data);
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) { Write("[stderr] " + e.Data); AppendFile(logPath, "[stderr] " + e.Data); } };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await Task.Run(delegate { process.WaitForExit(); });
                return process.ExitCode;
            }
        }

        private void LaunchOnboarding()
        {
            try
            {
                string command = RefreshPath() + "; $c=Get-Command openclaw -ErrorAction SilentlyContinue; if(!$c){Write-Error 'OpenClaw was not found in PATH.'; exit 127}; & $c.Source onboard --mode local --suppress-gateway-token-output";
                ProcessStartInfo info = new ProcessStartInfo(PowerShellPath(), "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command " + Quote(command));
                info.UseShellExecute = true;
                info.WindowStyle = ProcessWindowStyle.Normal;
                Process process = new Process();
                process.StartInfo = info;
                process.EnableRaisingEvents = true;
                process.Exited += delegate
                {
                    process.Dispose();
                    try
                    {
                        BeginInvoke(new Action(async delegate
                        {
                            await RefreshAgentsAsync();
                            if (openClawInstalled && openClawConfigured) await OpenDashboardAsync();
                        }));
                    }
                    catch (InvalidOperationException) { }
                };
                process.Start();
                SetStatus("Complete onboarding in the OpenClaw terminal window");
                Write("Opening the interactive OpenClaw onboarding wizard...");
            }
            catch (Exception error)
            {
                Write("[ERROR] Could not start onboarding: " + error.Message);
                MessageBox.Show("Could not start OpenClaw onboarding.\r\n\r\n" + error.Message, "Onboarding failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FindNodeExecutablePath()
        {
            RefreshProcessEnvironmentPath();
            foreach (string directory in (Environment.GetEnvironmentVariable("Path") ?? "").Split(';'))
            {
                string cleanDirectory = directory.Trim().Trim('"');
                if (cleanDirectory.Length == 0) continue;
                string candidate = Path.Combine(cleanDirectory, "node.exe");
                if (File.Exists(candidate)) return candidate;
            }
            return "";
        }

        private static string FindOpenClawEntryPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string candidate = Path.Combine(appData, "npm", "node_modules", "openclaw", "openclaw.mjs");
            if (File.Exists(candidate)) return candidate;

            RefreshProcessEnvironmentPath();
            foreach (string directory in (Environment.GetEnvironmentVariable("Path") ?? "").Split(';'))
            {
                string cleanDirectory = directory.Trim().Trim('"');
                if (cleanDirectory.Length == 0) continue;
                candidate = Path.Combine(cleanDirectory, "node_modules", "openclaw", "openclaw.mjs");
                if (File.Exists(candidate)) return candidate;
            }
            return "";
        }

        private static void RefreshProcessEnvironmentPath()
        {
            string machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
            string userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
            Environment.SetEnvironmentVariable("Path", machinePath + ";" + userPath, EnvironmentVariableTarget.Process);
        }

        private static string StripPowerShellClixml(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            int marker = value.IndexOf("#< CLIXML", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return value.Trim();
            int end = value.IndexOf("</Objs>", marker, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return value.Substring(0, marker).Trim();
            return (value.Substring(0, marker) + value.Substring(end + 7)).Trim();
        }

        private static bool IsOpenClawConfigured()
        {
            string configuredPath = Environment.GetEnvironmentVariable("OPENCLAW_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
                return File.Exists(Environment.ExpandEnvironmentVariables(configuredPath.Trim().Trim('"')));

            string stateDirectory = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR");
            if (string.IsNullOrWhiteSpace(stateDirectory))
                stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw");
            else
                stateDirectory = Environment.ExpandEnvironmentVariables(stateDirectory.Trim().Trim('"'));
            return File.Exists(Path.Combine(stateDirectory, "openclaw.json"));
        }

        private static string PowerShellPath()
        {
            return Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        }

        private static string RefreshPath()
        {
            return "$ProgressPreference='SilentlyContinue'; $env:Path=[Environment]::GetEnvironmentVariable('Path','Machine')+';'+[Environment]::GetEnvironmentVariable('Path','User')";
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static Label MakeLabel(string text, int x, int y, float size, Color color)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI Semibold", size, FontStyle.Bold), ForeColor = color };
        }

        private static Image LoadLogo()
        {
            return LoadEmbeddedImage("NeonXLogo");
        }

        private static Image LoadEmbeddedImage(string resourceName)
        {
            using (Stream stream = typeof(InstallerForm).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new InvalidOperationException("Embedded image not found: " + resourceName);
                using (Image image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }

        private static void ConfigureButton(Button button, string text, int x, Color color)
        {
            button.Text = text;
            button.Location = new Point(x, 482);
            button.Size = new Size(95, 36);
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }

        private void SetEnabled(bool enabled)
        {
            install.Enabled = enabled;
            stop.Enabled = enabled && openClawInstalled;
            close.Enabled = enabled;
            refresh.Enabled = enabled;
            source.Enabled = enabled;
            ControlBox = enabled;
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatus), text);
                return;
            }
            status.Text = text;
        }

        private void Write(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Write), text); return; }
            log.AppendText(text + Environment.NewLine);
            log.SelectionStart = log.TextLength;
            log.ScrollToCaret();
        }

        private static void AppendFile(string path, string text)
        {
            try { File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }

        private static bool OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                MessageBox.Show("NeonX components are being installed. Please wait for the process to complete.", "NeonX Agent Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StopOpenClawBeforeExit();
        }
    }

    internal sealed class CommandResult
    {
        public int ExitCode { get; private set; }
        public string Output { get; private set; }
        public string Error { get; private set; }

        public CommandResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output ?? "";
            Error = error ?? "";
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public Color BorderColor { get; set; }
        public int CornerRadius { get; set; }

        public RoundedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedLogo.CreateRoundedRectangle(bounds, CornerRadius))
            using (SolidBrush fill = new SolidBrush(BackColor))
            using (Pen border = new Pen(BorderColor, 1F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    internal sealed class RoundedLogo : Control
    {
        public Image Image { get; set; }
        public int CornerRadius { get; set; }

        public RoundedLogo()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Image == null) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = CreateRoundedRectangle(bounds, CornerRadius))
            {
                GraphicsState state = e.Graphics.Save();
                e.Graphics.SetClip(path);
                e.Graphics.DrawImage(Image, bounds);
                e.Graphics.Restore(state);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Image != null) Image.Dispose();
            base.Dispose(disposing);
        }

        internal static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
