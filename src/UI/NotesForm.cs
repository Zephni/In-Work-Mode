using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using WorkMode.Configuration;
using WorkMode.Models;

namespace WorkMode.UI
{
    public sealed class NotesForm : Form
    {
        private const string ChangeMessage = "change\n";
        private const string LoadMessage = "load\n";
        private readonly Workspace _workspace;
        private readonly AppConfig _config;
        private readonly WebView2 _editor;
        private readonly Timer _saveTimer;
        private string _pendingMarkdown;

        public event EventHandler NotesChanged;

        public NotesForm(Workspace workspace, AppConfig config)
        {
            _workspace = workspace;
            _config = config;
            _pendingMarkdown = workspace.Notes ?? string.Empty;

            Text = workspace.Title + " - Notes";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(520, 380);
            Size = new Size(
                _config.NotesWindowWidth > 0 ? _config.NotesWindowWidth : 680,
                _config.NotesWindowHeight > 0 ? _config.NotesWindowHeight : 520);
            MaximizeBox = false;
            MinimizeBox = true;
            Theme.ApplyForm(this);

            _editor = new WebView2
            {
                BackColor = Theme.SurfaceAlt,
                Location = new Point(12, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            Controls.Add(_editor);

            _saveTimer = new Timer { Interval = 350 };
            _saveTimer.Tick += (s, e) => SaveMarkdown();
            FormClosing += (s, e) =>
            {
                SaveMarkdown();
                SaveWindowSettings();
            };
            Shown += async (s, e) => await InitializeEditorAsync();
            Resize += (s, e) => LayoutEditor();
            ResizeEnd += (s, e) => SaveWindowSettings();
            LayoutEditor();
        }

        private async System.Threading.Tasks.Task InitializeEditorAsync()
        {
            try
            {
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WorkMode", "WebView2");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
                await _editor.EnsureCoreWebView2Async(environment);
                _editor.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _editor.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                if (_config.NotesZoomFactor > 0)
                    _editor.ZoomFactor = _config.NotesZoomFactor;
                _editor.ZoomFactorChanged += (s, e) => SaveZoomFactor();

                string editorPath = Path.Combine(EmbeddedRuntime.EditorDirectory, "index.html");
                if (!File.Exists(editorPath))
                    throw new FileNotFoundException("The Markdown editor files are missing.", editorPath);

                _editor.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "workmode.editor",
                    Path.GetDirectoryName(editorPath),
                    CoreWebView2HostResourceAccessKind.DenyCors);
                _editor.Source = new Uri("https://workmode.editor/index.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The Markdown editor could not be started.\r\n\r\n" + ex.Message,
                    "Editor unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message;
            try { message = e.TryGetWebMessageAsString(); }
            catch { return; }

            if (message == "ready")
            {
                _editor.CoreWebView2.PostWebMessageAsString(LoadMessage + _pendingMarkdown);
                return;
            }

            if (!message.StartsWith(ChangeMessage, StringComparison.Ordinal)) return;
            _pendingMarkdown = message.Substring(ChangeMessage.Length);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveMarkdown()
        {
            _saveTimer.Stop();
            if (_workspace.Notes == _pendingMarkdown) return;
            _workspace.Notes = _pendingMarkdown;
            var handler = NotesChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void SaveWindowSettings()
        {
            if (WindowState != FormWindowState.Normal) return;
            _config.NotesWindowWidth = Width;
            _config.NotesWindowHeight = Height;
            _config.Save();
        }

        private void SaveZoomFactor()
        {
            _config.NotesZoomFactor = _editor.ZoomFactor;
            _config.Save();
        }

        private void LayoutEditor()
        {
            const int margin = 12;
            _editor.SetBounds(
                margin,
                margin,
                ClientSize.Width - margin * 2,
                ClientSize.Height - margin * 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _saveTimer.Dispose();
                _editor.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
