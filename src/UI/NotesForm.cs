using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    public sealed class NotesForm : Form
    {
        private const int ListIndentPixels = 24;
        private const int MarkdownIndentSpaces = 2;
        private readonly Workspace _workspace;
        private readonly RichTextBox _notesBox;
        private readonly FlowLayoutPanel _markdownToolbar;
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly Timer _saveTimer;
        private readonly Dictionary<string, string> _linkTargets = new Dictionary<string, string>();
        private bool _updatingEditor;

        public event EventHandler NotesChanged;

        public NotesForm(Workspace workspace)
        {
            _workspace = workspace;

            Text = workspace.Title + " - Notes";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(420, 320);
            ClientSize = new Size(520, 400);
            MaximizeBox = false;
            MinimizeBox = false;
            Theme.ApplyForm(this);

            _notesBox = new RichTextBox
            {
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                WordWrap = true,
                AcceptsTab = true,
                DetectUrls = true,
                Location = new Point(12, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            LoadMarkdown(workspace.Notes ?? string.Empty);
            _notesBox.TextChanged += OnNotesChanged;
            _notesBox.KeyDown += OnNotesKeyDown;
            _notesBox.HandleCreated += (s, e) => ApplyNotesPadding();
            _notesBox.LinkClicked += OnLinkClicked;

            _saveTimer = new Timer { Interval = 350 };
            _saveTimer.Tick += (s, e) => SaveMarkdown();
            FormClosing += (s, e) => SaveMarkdown();

            _markdownToolbar = BuildMarkdownToolbar();

            var insertTimeButton = new Button
            {
                Text = "Insert time",
                Image = Glyphs.Clock(18, Color.White),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 0, 10, 0),
                Size = new Size(128, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            Theme.StyleButton(insertTimeButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            insertTimeButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular);
            insertTimeButton.Click += OnInsertTime;

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(84, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Theme.StyleSurfaceButton(closeButton);
            closeButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular);

            Controls.Add(_notesBox);
            Controls.Add(_markdownToolbar);
            Controls.Add(insertTimeButton);
            Controls.Add(closeButton);
            CancelButton = closeButton;

            Resize += (s, e) => LayoutControls(insertTimeButton, closeButton);
            LayoutControls(insertTimeButton, closeButton);
            Shown += (s, e) =>
            {
                _notesBox.SelectionStart = _notesBox.TextLength;
                _notesBox.SelectionLength = 0;
                _notesBox.Focus();
                _notesBox.ScrollToCaret();
            };
        }

        private FlowLayoutPanel BuildMarkdownToolbar()
        {
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Height = 40,
                Location = new Point(12, 12),
                Padding = new Padding(2, 3, 2, 3),
                BackColor = Theme.Surface,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Color iconColor = Theme.Text;
            panel.Controls.Add(CreateMarkdownButton(Glyphs.MarkdownText(26, iconColor, "H1", FontStyle.Bold), "Heading", (s, e) => FormatHeading()));
            panel.Controls.Add(CreateMarkdownButton(Glyphs.BulletedList(26, iconColor), "Bulleted list", (s, e) => FormatBulletedList()));
            panel.Controls.Add(CreateMarkdownButton(Glyphs.MarkdownText(26, iconColor, "1.", FontStyle.Bold), "Numbered list", (s, e) => FormatNumberedList()));
            panel.Controls.Add(CreateMarkdownButton(Glyphs.MarkdownText(26, iconColor, "B", FontStyle.Bold), "Bold", (s, e) => FormatSelection(FontStyle.Bold)));
            panel.Controls.Add(CreateMarkdownButton(Glyphs.MarkdownText(26, iconColor, "I", FontStyle.Italic), "Italic", (s, e) => FormatSelection(FontStyle.Italic)));
            panel.Controls.Add(CreateMarkdownButton(Glyphs.Code(26, iconColor), "Inline code", (s, e) => FormatCode()));
            panel.Controls.Add(CreateMarkdownButton(Glyphs.Link(26, iconColor), "Link", (s, e) => FormatLink()));

            return panel;
        }

        private Button CreateMarkdownButton(Image image, string tooltip, EventHandler onClick)
        {
            var button = new Button
            {
                Image = image,
                Size = new Size(38, 34),
                Margin = new Padding(0, 0, 2, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
            button.FlatAppearance.MouseDownBackColor = Theme.Border;
            button.Click += onClick;
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private void EnsureSelection()
        {
            if (_notesBox.SelectionLength > 0) return;
            int start = _notesBox.SelectionStart;
            _notesBox.SelectedText = "text";
            _notesBox.Select(start, 4);
        }

        private void FormatSelection(FontStyle style)
        {
            EnsureSelection();
            Font current = _notesBox.SelectionFont ?? _notesBox.Font;
            FontStyle nextStyle = (current.Style & style) == style
                ? current.Style & ~style
                : current.Style | style;
            _notesBox.SelectionFont = new Font(current.FontFamily, current.Size, nextStyle);
            _notesBox.Focus();
            ScheduleSave();
        }

        private void FormatHeading()
        {
            SelectCurrentParagraphs();
            _notesBox.SelectionFont = new Font(_notesBox.Font.FontFamily, 16f, FontStyle.Bold);
            _notesBox.Focus();
            ScheduleSave();
        }

        private void FormatBulletedList()
        {
            SelectCurrentParagraphs();
            _notesBox.SelectionBullet = !_notesBox.SelectionBullet;
            _notesBox.Focus();
            ScheduleSave();
        }

        private void FormatNumberedList()
        {
            int startLine = _notesBox.GetLineFromCharIndex(_notesBox.SelectionStart);
            int endLine = _notesBox.GetLineFromCharIndex(_notesBox.SelectionStart + _notesBox.SelectionLength);
            for (int line = endLine; line >= startLine; line--)
            {
                int start = _notesBox.GetFirstCharIndexFromLine(line);
                string lineText = _notesBox.Lines[line];
                if (Regex.IsMatch(lineText, @"^\d+\.\s")) continue;
                _notesBox.Select(start, 0);
                _notesBox.SelectedText = (line - startLine + 1) + ". ";
            }
            _notesBox.Focus();
            ScheduleSave();
        }

        private void FormatCode()
        {
            EnsureSelection();
            _notesBox.SelectionFont = new Font("Consolas", _notesBox.Font.Size, FontStyle.Regular);
            _notesBox.SelectionBackColor = Theme.Border;
            _notesBox.Focus();
            ScheduleSave();
        }

        private void FormatLink()
        {
            int start = _notesBox.SelectionStart;
            string label = _notesBox.SelectedText;
            if (string.IsNullOrWhiteSpace(label) || !Uri.IsWellFormedUriString(label, UriKind.Absolute))
            {
                label = "https://";
                _notesBox.SelectedText = label;
                _notesBox.Select(start, label.Length);
            }
            _linkTargets[label] = label;
            _notesBox.SelectionFont = new Font(_notesBox.Font, FontStyle.Underline);
            _notesBox.SelectionColor = Theme.AccentHover;
            _notesBox.Focus();
            ScheduleSave();
        }

        private void SelectCurrentParagraphs()
        {
            int startLine = _notesBox.GetLineFromCharIndex(_notesBox.SelectionStart);
            int endLine = _notesBox.GetLineFromCharIndex(_notesBox.SelectionStart + _notesBox.SelectionLength);
            int start = _notesBox.GetFirstCharIndexFromLine(startLine);
            int end = endLine + 1 < _notesBox.Lines.Length
                ? _notesBox.GetFirstCharIndexFromLine(endLine + 1) - 1
                : _notesBox.TextLength;
            _notesBox.Select(start, Math.Max(0, end - start));
        }

        private void OnNotesKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back && _notesBox.SelectionLength == 0)
            {
                int line = _notesBox.GetLineFromCharIndex(_notesBox.SelectionStart);
                string[] lines = _notesBox.Lines;
                int lineLength = line < lines.Length ? lines[line].Length : 0;
                bool emptyBullet = _notesBox.SelectionBullet && lineLength == 0;
                if (emptyBullet)
                {
                    _notesBox.SelectionBullet = false;
                    _notesBox.SelectionIndent = 0;
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    ScheduleSave();
                }
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                int line = _notesBox.GetLineFromCharIndex(_notesBox.SelectionStart);
                string[] lines = _notesBox.Lines;
                int lineLength = line < lines.Length ? lines[line].Length : 0;
                bool emptyBullet = _notesBox.SelectionBullet && lineLength == 0;
                if (emptyBullet)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _notesBox.SelectionIndent = 0;
                        ScheduleSave();
                    }));
                }
                return;
            }

            if (e.KeyCode != Keys.Tab) return;

            int selectionStart = _notesBox.SelectionStart;
            int selectionLength = _notesBox.SelectionLength;
            int startLine = _notesBox.GetLineFromCharIndex(selectionStart);
            int selectionEnd = selectionLength > 0
                ? selectionStart + selectionLength - 1
                : selectionStart;
            int endLine = _notesBox.GetLineFromCharIndex(selectionEnd);
            bool changed = false;

            for (int line = startLine; line <= endLine; line++)
            {
                int lineStart = _notesBox.GetFirstCharIndexFromLine(line);
                _notesBox.Select(lineStart, _notesBox.Lines[line].Length);
                if (!_notesBox.SelectionBullet) continue;

                int indent = _notesBox.SelectionIndent + (e.Shift ? -ListIndentPixels : ListIndentPixels);
                _notesBox.SelectionIndent = Math.Max(0, indent);
                changed = true;
            }

            _notesBox.Select(selectionStart, selectionLength);
            if (!changed) return;

            e.SuppressKeyPress = true;
            e.Handled = true;
            ScheduleSave();
        }

        private void LayoutControls(Button insertTimeButton, Button closeButton)
        {
            const int margin = 12;
            const int gap = 10;
            int buttonTop = ClientSize.Height - margin - insertTimeButton.Height;
            _markdownToolbar.SetBounds(
                margin,
                margin,
                ClientSize.Width - margin * 2,
                _markdownToolbar.Height);
            int notesTop = _markdownToolbar.Bottom + gap;
            _notesBox.SetBounds(
                margin,
                notesTop,
                ClientSize.Width - margin * 2,
                buttonTop - gap - notesTop);
            insertTimeButton.Location = new Point(margin, buttonTop);
            closeButton.Location = new Point(ClientSize.Width - margin - closeButton.Width, buttonTop);
            ApplyNotesPadding();
        }

        private void ApplyNotesPadding()
        {
            if (!_notesBox.IsHandleCreated) return;
            Interop.NativeMethods.SetTextBoxPadding(
                _notesBox.Handle, 7, 6, _notesBox.Width, _notesBox.Height);
        }

        private void OnNotesChanged(object sender, EventArgs e)
        {
            if (!_updatingEditor) ScheduleSave();
        }

        private void ScheduleSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveMarkdown()
        {
            if (_updatingEditor || !_notesBox.IsHandleCreated) return;
            _saveTimer.Stop();
            int selectionStart = _notesBox.SelectionStart;
            int selectionLength = _notesBox.SelectionLength;
            Interop.NativeMethods.SendMessage(_notesBox.Handle, Interop.NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            string markdown = SerializeMarkdown();
            _notesBox.Select(selectionStart, selectionLength);
            Interop.NativeMethods.SendMessage(_notesBox.Handle, Interop.NativeMethods.WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            _notesBox.Invalidate();

            if (_workspace.Notes == markdown) return;
            _workspace.Notes = markdown;
            var handler = NotesChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private string SerializeMarkdown()
        {
            var markdown = new StringBuilder();
            for (int lineIndex = 0; lineIndex < _notesBox.Lines.Length; lineIndex++)
            {
                int lineStart = _notesBox.GetFirstCharIndexFromLine(lineIndex);
                string line = _notesBox.Lines[lineIndex];
                bool heading = line.Length > 0 && IsHeadingAt(lineStart);
                _notesBox.Select(lineStart, line.Length > 0 ? 1 : 0);
                bool bullet = _notesBox.SelectionBullet;
                if (heading) markdown.Append("# ");
                else if (bullet)
                {
                    int indentLevel = Math.Max(0, _notesBox.SelectionIndent / ListIndentPixels);
                    markdown.Append(' ', indentLevel * MarkdownIndentSpaces).Append("- ");
                }
                markdown.Append(SerializeInline(lineStart, line.Length, heading));
                if (lineIndex < _notesBox.Lines.Length - 1) markdown.AppendLine();
            }
            return markdown.ToString();
        }

        private string SerializeInline(int start, int length, bool suppressBold)
        {
            var result = new StringBuilder();
            int offset = 0;
            while (offset < length)
            {
                CharacterFormat format = GetCharacterFormat(start + offset);
                int runLength = 1;
                while (offset + runLength < length && format.Equals(GetCharacterFormat(start + offset + runLength)))
                    runLength++;

                string text = _notesBox.Text.Substring(start + offset, runLength);
                if (format.IsLink)
                {
                    string url;
                    if (!_linkTargets.TryGetValue(text, out url)) url = text;
                    result.Append('[').Append(text).Append("](").Append(url).Append(')');
                }
                else
                {
                    if (format.Bold && !suppressBold) result.Append("**");
                    if (format.Italic) result.Append('*');
                    if (format.Code) result.Append('`');
                    result.Append(text);
                    if (format.Code) result.Append('`');
                    if (format.Italic) result.Append('*');
                    if (format.Bold && !suppressBold) result.Append("**");
                }
                offset += runLength;
            }
            return result.ToString();
        }

        private CharacterFormat GetCharacterFormat(int index)
        {
            _notesBox.Select(index, 1);
            Font font = _notesBox.SelectionFont ?? _notesBox.Font;
            return new CharacterFormat(
                font.Bold,
                font.Italic,
                font.Name.Equals("Consolas", StringComparison.OrdinalIgnoreCase),
                font.Underline && _notesBox.SelectionColor.ToArgb() == Theme.AccentHover.ToArgb());
        }

        private bool IsHeadingAt(int index)
        {
            _notesBox.Select(index, 1);
            Font font = _notesBox.SelectionFont ?? _notesBox.Font;
            return font.Bold && font.Size >= 15f;
        }

        private void OnInsertTime(object sender, EventArgs e)
        {
            _workspace.Sync();
            string line = DateTime.Now.ToString("yyyy-MM-dd") + " hours: " +
                          Workspace.Format(_workspace.ElapsedSeconds);
            _notesBox.Select(0, 0);
            _notesBox.SelectionFont = _notesBox.Font;
            _notesBox.SelectionColor = Theme.Text;
            _notesBox.SelectionBackColor = Theme.SurfaceAlt;
            _notesBox.SelectedText = line + (_notesBox.TextLength > 0 ? Environment.NewLine : string.Empty);
            _notesBox.SelectionStart = line.Length;
            _notesBox.Focus();
        }

        private void LoadMarkdown(string markdown)
        {
            _updatingEditor = true;
            _notesBox.Text = markdown;
            _notesBox.SelectAll();
            _notesBox.SelectionFont = _notesBox.Font;
            _notesBox.SelectionColor = Theme.Text;
            _notesBox.SelectionBackColor = Theme.SurfaceAlt;

            ReplaceMarkdown(@"(?m)^#{1,3}[ \t]+(.+)$", 1, (start, length, match) =>
                _notesBox.SelectionFont = new Font(_notesBox.Font.FontFamily, 16f, FontStyle.Bold));
            ReplaceMarkdown(@"(?m)^([ \t]*)[-*][ \t]+(.+)$", 2, (start, length, match) =>
            {
                int spaces = match.Groups[1].Value.Replace("\t", "    ").Length;
                _notesBox.SelectionBullet = true;
                _notesBox.SelectionIndent = (spaces / MarkdownIndentSpaces) * ListIndentPixels;
            });
            ReplaceMarkdown(@"\[([^\]]+)\]\(([^)]+)\)", 1, (start, length, match) =>
            {
                _linkTargets[match.Groups[1].Value] = match.Groups[2].Value;
                _notesBox.SelectionFont = new Font(_notesBox.Font, FontStyle.Underline);
                _notesBox.SelectionColor = Theme.AccentHover;
            });
            ReplaceMarkdown(@"\*\*(.+?)\*\*", 1, (start, length, match) => AddSelectionStyle(FontStyle.Bold));
            ReplaceMarkdown(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", 1, (start, length, match) => AddSelectionStyle(FontStyle.Italic));
            ReplaceMarkdown(@"`([^`]+)`", 1, (start, length, match) =>
            {
                _notesBox.SelectionFont = new Font("Consolas", _notesBox.Font.Size, FontStyle.Regular);
                _notesBox.SelectionBackColor = Theme.Border;
            });

            _notesBox.Select(_notesBox.TextLength, 0);
            _updatingEditor = false;
        }

        private void ReplaceMarkdown(string pattern, int contentGroup, Action<int, int, Match> applyFormatting)
        {
            MatchCollection matches = Regex.Matches(_notesBox.Text, pattern);
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Match match = matches[i];
                string content = match.Groups[contentGroup].Value;
                _notesBox.Select(match.Index, match.Length);
                _notesBox.SelectedText = content;
                _notesBox.Select(match.Index, content.Length);
                applyFormatting(match.Index, content.Length, match);
            }
        }

        private void AddSelectionStyle(FontStyle style)
        {
            Font current = _notesBox.SelectionFont ?? _notesBox.Font;
            _notesBox.SelectionFont = new Font(current.FontFamily, current.Size, current.Style | style);
        }

        private void OnLinkClicked(object sender, LinkClickedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(e.LinkText); }
            catch { }
        }

        private struct CharacterFormat : IEquatable<CharacterFormat>
        {
            public readonly bool Bold;
            public readonly bool Italic;
            public readonly bool Code;
            public readonly bool IsLink;

            public CharacterFormat(bool bold, bool italic, bool code, bool isLink)
            {
                Bold = bold;
                Italic = italic;
                Code = code;
                IsLink = isLink;
            }

            public bool Equals(CharacterFormat other)
            {
                return Bold == other.Bold && Italic == other.Italic &&
                       Code == other.Code && IsLink == other.IsLink;
            }
        }
    }
}