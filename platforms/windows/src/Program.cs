using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
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
        private const string RecommendedNodeVersion = ComponentVersions.NodeJsDistribution;
        private const string NodeX64Sha256 = ComponentVersions.NodeX64Sha256;
        private const string NodeArm64Sha256 = ComponentVersions.NodeArm64Sha256;
        private readonly Label status = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly TextBox log = new TextBox();
        private readonly Button install = new Button();
        private readonly Button close = new Button();
        private readonly Button refresh = new Button();
        private readonly LinkLabel source = new LinkLabel();
        private readonly Label nodeStatus = new Label();
        private readonly Label openClawStatus = new Label();
        private bool busy;
        private bool nodeDetected;
        private bool nodeReady;
        private string detectedNodeVersion = "";
        private bool openClawInstalled;
        private bool openClawConfigured;
        private Process gatewayProcess;

        public InstallerForm()
        {
            Text = "NeonX Agent Hub";
            ClientSize = new Size(820, 620);
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
            card.Size = new Size(760, 150);
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

            source.Text = "Release details";
            source.LinkColor = Color.FromArgb(34, 211, 238);
            source.AutoSize = true;
            source.Location = new Point(480, 126);
            source.LinkClicked += delegate { OpenUrl("https://github.com/openclaw/openclaw/releases/tag/v" + Version); };
            card.Controls.Add(source);

            ConfigureButton(install, "Checking...", 0, Color.FromArgb(8, 145, 178));
            install.Location = new Point(620, 53);
            install.Size = new Size(112, 42);
            install.Enabled = false;
            install.Click += async delegate { await HandleOpenClawActionAsync(); };
            card.Controls.Add(install);

            status.Text = "Ready to install";
            status.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            status.AutoSize = true;
            status.Location = new Point(32, 294);
            Controls.Add(status);
            progress.Location = new Point(32, 322);
            progress.Size = new Size(756, 12);
            Controls.Add(progress);

            log.Location = new Point(32, 350);
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
            refresh.Location = new Point(580, 552);
            refresh.Click += async delegate { await RefreshAgentsAsync(); };
            Controls.Add(refresh);
            ConfigureButton(close, "Close", 695, Color.FromArgb(51, 65, 85));
            close.Location = new Point(695, 552);
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
                SetStatus("Step 1/3 - Checking system Node.js and npm");
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

                SetStatus("Step 2/3 - Installing OpenClaw with system npm");
                Write("Running npm install -g openclaw@" + Version);
                int code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(RefreshPath() + "; npm install -g openclaw@" + Version), logPath);
                if (code != 0) throw new InvalidOperationException("npm returned error code " + code + ".");

                SetStatus("Step 3/3 - Checking openclaw --version");
                string verify = RefreshPath() + "; $c=Get-Command openclaw -ErrorAction SilentlyContinue; if(!$c){exit 127}; & $c.Source --version";
                code = await RunAsync("-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(verify), logPath);
                if (code != 0) throw new InvalidOperationException("Could not verify openclaw --version.");

                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = 100;
                openClawInstalled = true;
                openClawConfigured = IsOpenClawConfigured();
                openClawStatus.Text = openClawConfigured
                    ? "OpenClaw: installed - version " + Version
                    : "OpenClaw: installed - onboarding required";
                openClawStatus.ForeColor = Color.FromArgb(74, 222, 128);
                install.Text = openClawConfigured ? "Open" : "Onboard";
                SetStatus("OpenClaw " + Version + " installed successfully");
                Write("[COMPLETE] OpenClaw is ready.");
                MessageBox.Show("OpenClaw " + Version + " was installed successfully.", "NeonX Agent Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            SetStatus("Checking installed agents");

            Task<CommandResult> nodeCheck = RunCaptureAsync(RefreshPath() + "; node --version");
            Task<CommandResult> openClawCheck = RunCaptureAsync(
                RefreshPath() + "; $c=Get-Command openclaw -ErrorAction SilentlyContinue; if(!$c){exit 127}; & $c.Source --version");
            await Task.WhenAll(nodeCheck, openClawCheck);
            CommandResult node = nodeCheck.Result;
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
            openClawInstalled = result.ExitCode == 0;
            if (openClawInstalled)
            {
                string installedVersion = LastNonEmptyLine(result.Output);
                openClawConfigured = IsOpenClawConfigured();
                openClawStatus.Text = openClawConfigured
                    ? "OpenClaw: installed" + (installedVersion.Length > 0 ? " - version " + installedVersion : "")
                    : "OpenClaw: installed - onboarding required";
                openClawStatus.ForeColor = Color.FromArgb(74, 222, 128);
                install.Text = openClawConfigured ? "Open" : "Onboard";
                SetStatus(openClawConfigured ? "OpenClaw is installed and ready" : "Complete OpenClaw onboarding before opening the dashboard");
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
            SetStatus("Preparing the OpenClaw gateway");
            Write("Checking the OpenClaw local gateway configuration...");

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

            SetStatus("Starting the OpenClaw gateway");
            CommandResult result = await RunOpenClawCaptureAsync("dashboard --json");
            if (result.ExitCode != 0)
            {
                string gatewayError;
                if (StartOpenClawGateway(out gatewayError))
                {
                    Write("OpenClaw gateway started in the background. Waiting for the Control UI...");
                    for (int attempt = 0; attempt < 3 && result.ExitCode != 0; attempt++)
                    {
                        await Task.Delay(2000);
                        result = await RunOpenClawCaptureAsync("dashboard --json");
                    }
                }
                else
                {
                    result = new CommandResult(1, result.Output, result.Error + Environment.NewLine + gatewayError);
                }
            }
            if (result.ExitCode != 0)
            {
                ShowDashboardError(result);
            }
            else
            {
                string browserUrl = ReadDashboardBrowserUrl(result.Output);
                if (string.IsNullOrWhiteSpace(browserUrl))
                {
                    Write("[ERROR] OpenClaw did not return a secure dashboard URL.");
                    MessageBox.Show("OpenClaw did not return a secure dashboard URL.", "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (!OpenUrl(browserUrl))
                {
                    Write("[ERROR] Windows could not launch the default browser.");
                    MessageBox.Show("Windows could not launch the default browser.", "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    SetStatus("OpenClaw Control UI opened with secure one-time authentication");
                    Write("OpenClaw dashboard opened in your browser with a one-time pairing token.");
                }
            }
            install.Text = "Open";
            install.Enabled = true;
        }

        private void ShowDashboardError(CommandResult result)
        {
            string details = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            details = StripPowerShellClixml(details);
            if (string.IsNullOrWhiteSpace(details)) details = "OpenClaw did not provide an error message.";
            Write("[ERROR] " + details);
            MessageBox.Show("Could not open the OpenClaw dashboard.\r\n\r\n" + details, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private bool StartOpenClawGateway(out string error)
        {
            error = "";
            try
            {
                if (gatewayProcess != null && !gatewayProcess.HasExited) return true;
                if (gatewayProcess != null) gatewayProcess.Dispose();

                string nodePath = FindNodeExecutablePath();
                string openClawEntry = FindOpenClawEntryPath();
                if (nodePath.Length == 0) { error = "System node.exe was not found."; return false; }
                if (openClawEntry.Length == 0) { error = "The OpenClaw package entry point was not found."; return false; }

                ProcessStartInfo info = new ProcessStartInfo(nodePath, Quote(openClawEntry) + " gateway run --ws-log compact");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                gatewayProcess = Process.Start(info);
                return gatewayProcess != null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
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

        private static string ReadDashboardBrowserUrl(string output)
        {
            try
            {
                string value = (output ?? "").Trim();
                int start = value.IndexOf('{');
                int end = value.LastIndexOf('}');
                if (start < 0 || end < start) return "";
                Dictionary<string, object> result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(value.Substring(start, end - start + 1));
                object ok;
                object browserUrl;
                if (!result.TryGetValue("ok", out ok) || !(ok is bool) || !(bool)ok) return "";
                return result.TryGetValue("browserUrl", out browserUrl) ? Convert.ToString(browserUrl) : "";
            }
            catch
            {
                return "";
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
            if (!busy) return;
            e.Cancel = true;
            MessageBox.Show("OpenClaw is being installed. Please wait for the process to complete.", "NeonX Agent Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
