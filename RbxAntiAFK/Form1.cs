using Guna.UI2.WinForms;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Shell32;
using DiscordRPC;
using DiscordRPC.Logging;

namespace RbxAntiAfk
{
    public partial class RbxAntiAFK : Form
    {
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        const int SW_RESTORE = 9;
        const int SW_MINIMIZE = 6;
        const int SW_SHOWNA = 8;
        const int MOUSEEVENTF_MOVE = 0x0001;

        private Timer macroTimer = new Timer();
        private bool isSilentMode = false;
        private bool sendScreenshots = false;
        private string webhookUrl = "";
        private string logPath = @"C:\RbxAntiAFK";

        // Discord RPC
        public DiscordRpcClient client;

        public RbxAntiAFK()
        {
            InitializeComponent();
            InitRPC(); // Запуск діскорд статусу
            macroTimer.Tick += MacroTimer_Tick;
            if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            this.FormClosing += RbxAntiAFK_FormClosing;
        }

        private void InitRPC()
        {
            client = new DiscordRpcClient("1498376089729372180");
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            client.Initialize();

            client.SetPresence(new RichPresence()
            {
                Details = "In Menu",
                State = "Waiting for start...",
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    LargeImageText = "RbxAntiAFK v1.0",
                }
            });
        }

        private async void btnMacro_Click(object sender, EventArgs e)
        {
            if (!btnMacro.Checked)
            {
                string timeInput = Microsoft.VisualBasic.Interaction.InputBox("Interval (min):", "Config", "15");
                if (!int.TryParse(timeInput, out int minutes)) return;

                isSilentMode = (MessageBox.Show("Silent Mode?", "Config", MessageBoxButtons.YesNo) == DialogResult.Yes);
                webhookUrl = Microsoft.VisualBasic.Interaction.InputBox("Webhook URL:", "Config", "");
                if (!string.IsNullOrEmpty(webhookUrl))
                    sendScreenshots = (MessageBox.Show("Send Screenshots?", "Config", MessageBoxButtons.YesNo) == DialogResult.Yes);

                macroTimer.Interval = minutes * 60 * 1000;
                macroTimer.Start();
                btnMacro.Checked = true;
                UpdateUI();

                client.SetPresence(new RichPresence()
                {
                    Details = "Grinding...",
                    State = $"Interval: {minutes}m",
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "logo",
                        LargeImageText = "Working hard!"
                    }
                });

                await SendDiscordWebhook("🚀 **Macro Started!**");
                CheckAndRunMacro();
            }
            else
            {
                macroTimer.Stop();
                btnMacro.Checked = false;
                UpdateUI();

                client.SetPresence(new RichPresence() { Details = "In Menu", State = "Idling" });

                await SendDiscordWebhook("🛑 **Macro Stopped by user**");
            }
        }

        private void RbxAntiAFK_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
        }

        private async System.Threading.Tasks.Task SendDiscordWebhook(string message, string imagePath = null)
        {
            if (string.IsNullOrEmpty(webhookUrl)) return;
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    using (var multipartContent = new MultipartFormDataContent())
                    {
                        string json = "{\"content\":\"" + message.Replace("\n", "\\n") + "\"}";
                        multipartContent.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");
                        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                        {
                            var fileContent = new ByteArrayContent(File.ReadAllBytes(imagePath));
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                            multipartContent.Add(fileContent, "file", "screenshot.png");
                        }
                        await httpClient.PostAsync(webhookUrl, multipartContent);
                    }
                }
            }
            catch { }
            finally { if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath)) File.Delete(imagePath); }
        }

        private void CheckAndRunMacro()
        {
            Process roblox = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault() ?? Process.GetProcessesByName("RobloxPlayer").FirstOrDefault();
            if (roblox == null) return;
            IntPtr hWnd = roblox.MainWindowHandle;
            Shell shell = new Shell();
            if (!isSilentMode) shell.MinimizeAll();
            System.Threading.Thread.Sleep(1000);
            if (isSilentMode) ShowWindow(hWnd, SW_SHOWNA);
            else { ShowWindow(hWnd, SW_RESTORE); SetForegroundWindow(hWnd); }
            System.Threading.Thread.Sleep(2000);
            string path = sendScreenshots ? Path.Combine(logPath, "ss.png") : null;
            if (sendScreenshots) CaptureScreen(path);
            mouse_event(MOUSEEVENTF_MOVE, 100, 0, 0, 0);
            System.Threading.Thread.Sleep(500);
            mouse_event(MOUSEEVENTF_MOVE, -100, 0, 0, 0);
            ShowWindow(hWnd, SW_MINIMIZE);
            if (!isSilentMode) shell.UndoMinimizeALL();
            _ = SendDiscordWebhook("📸 **Anti-AFK Cycle Complete**", path);
        }

        private void CaptureScreen(string path)
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap)) g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    bitmap.Save(path, ImageFormat.Png);
                }
            }
            catch { }
        }

        private void UpdateUI() { btnMacro.Text = btnMacro.Checked ? "ON" : "OFF"; btnMacro.FillColor = btnMacro.Checked ? Color.Green : Color.Red; }
        private void MacroTimer_Tick(object sender, EventArgs e) => CheckAndRunMacro();
        private void btnExit_Click(object sender, EventArgs e) { client.Dispose(); Application.Exit(); }
        private void btnMinimize_Click(object sender, EventArgs e) => this.WindowState = FormWindowState.Minimized;

        private void RbxAntiAFK_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (client != null) client.Dispose();
            if (!string.IsNullOrEmpty(webhookUrl)) { _ = SendDiscordWebhook("🛑 **Application Closed**"); System.Threading.Thread.Sleep(500); }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/nevqq", UseShellExecute = true });
        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => Process.Start(new ProcessStartInfo { FileName = "https://www.youtube.com/@NevqSoftWare", UseShellExecute = true });
    }
}
