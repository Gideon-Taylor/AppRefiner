using System;
using System.Drawing;
using System.Windows.Forms;
using AppRefiner.Git;
using System.Collections.Generic;
using System.Linq;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for displaying Git history and reverting to previous versions
    /// </summary>
    public class GitHistoryDialog : Form
    {
        // UI Controls
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly ListView historyListView;
        private readonly Button revertButton;
        private readonly Button viewButton;
        private readonly Button diffButton;
        private readonly Button cancelButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        
        // Data
        private readonly GitRepositoryManager gitManager;
        private readonly ScintillaEditor editor;
        private readonly List<CommitInfo> commitHistory;
        private CommitInfo? selectedCommit;
        
        /// <summary>
        /// Gets the selected commit to revert to
        /// </summary>
        public CommitInfo? SelectedCommit => selectedCommit;

        /// <summary>
        /// Initializes a new instance of the GitHistoryDialog class
        /// </summary>
        /// <param name="gitManager">The Git repository manager</param>
        /// <param name="editor">The editor to show history for</param>
        /// <param name="owner">The owner window handle</param>
        public GitHistoryDialog(GitRepositoryManager gitManager, ScintillaEditor editor, IntPtr owner = default)
        {
            this.gitManager = gitManager;
            this.editor = editor;
            this.owner = owner;
            
            // Initialize UI controls
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.historyListView = new ListView();
            this.revertButton = new Button();
            this.viewButton = new Button();
            this.diffButton = new Button();
            this.cancelButton = new Button();
            
            // Get commit history for the file
            if (!string.IsNullOrEmpty(editor.RelativePath))
            {
                commitHistory = gitManager.GetFileHistory(editor.RelativePath);
            }
            else
            {
                commitHistory = new List<CommitInfo>();
            }
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.TabIndex = 0;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "Git History";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TabIndex = 0;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // historyListView
            this.historyListView.FullRowSelect = true;
            this.historyListView.HideSelection = false;
            this.historyListView.Location = new Point(20, 50);
            this.historyListView.Size = new Size(560, 230);
            this.historyListView.TabIndex = 1;
            this.historyListView.UseCompatibleStateImageBehavior = false;
            this.historyListView.View = View.Details;
            this.historyListView.MultiSelect = false;
            this.historyListView.SelectedIndexChanged += HistoryListView_SelectedIndexChanged;
            
            // Configure columns
            this.historyListView.Columns.Add("Date", 120);
            this.historyListView.Columns.Add("Message", 420);
            
            // Populate with commit history
            PopulateHistoryList();

            // revertButton
            this.revertButton.Text = "Revert to Selected";
            this.revertButton.Size = new Size(130, 30);
            this.revertButton.Location = new Point(20, 300);
            this.revertButton.TabIndex = 2;
            this.revertButton.BackColor = Color.FromArgb(0, 122, 204);
            this.revertButton.ForeColor = Color.White;
            this.revertButton.FlatStyle = FlatStyle.Flat;
            this.revertButton.FlatAppearance.BorderSize = 0;
            this.revertButton.Enabled = false;
            this.revertButton.Click += RevertButton_Click;

            // viewButton
            this.viewButton.Text = "View Content";
            this.viewButton.Size = new Size(130, 30);
            this.viewButton.Location = new Point(160, 300);
            this.viewButton.TabIndex = 3;
            this.viewButton.BackColor = Color.FromArgb(0, 122, 204);
            this.viewButton.ForeColor = Color.White;
            this.viewButton.FlatStyle = FlatStyle.Flat;
            this.viewButton.FlatAppearance.BorderSize = 0;
            this.viewButton.Enabled = false;
            this.viewButton.Click += ViewButton_Click;

            // diffButton
            this.diffButton.Text = "View Diff";
            this.diffButton.Size = new Size(130, 30);
            this.diffButton.Location = new Point(300, 300);
            this.diffButton.TabIndex = 4;
            this.diffButton.BackColor = Color.FromArgb(0, 122, 204);
            this.diffButton.ForeColor = Color.White;
            this.diffButton.FlatStyle = FlatStyle.Flat;
            this.diffButton.FlatAppearance.BorderSize = 0;
            this.diffButton.Enabled = false;
            this.diffButton.Click += DiffButton_Click;

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(100, 30);
            this.cancelButton.Location = new Point(480, 300);
            this.cancelButton.TabIndex = 5;
            this.cancelButton.BackColor = Color.FromArgb(100, 100, 100);
            this.cancelButton.ForeColor = Color.White;
            this.cancelButton.FlatStyle = FlatStyle.Flat;
            this.cancelButton.FlatAppearance.BorderSize = 0;
            this.cancelButton.Click += (s, e) => 
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // GitHistoryDialog
            this.Text = "Git History";
            this.ClientSize = new Size(600, 350);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.historyListView);
            this.Controls.Add(this.revertButton);
            this.Controls.Add(this.viewButton);
            this.Controls.Add(this.diffButton);
            this.Controls.Add(this.cancelButton);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void PopulateHistoryList()
        {
            historyListView.Items.Clear();
            
            foreach (var commit in commitHistory)
            {
                var item = new ListViewItem(commit.Date.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(commit.Message);
                item.Tag = commit;
                
                historyListView.Items.Add(item);
            }
            
            // Select the first item if there are any
            if (historyListView.Items.Count > 0)
            {
                historyListView.Items[0].Selected = true;
            }
        }

        private void HistoryListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (historyListView.SelectedItems.Count > 0)
            {
                selectedCommit = (CommitInfo)historyListView.SelectedItems[0].Tag;
                revertButton.Enabled = true;
                viewButton.Enabled = true;
                
                // Enable diff button only if this is not the last commit in the history
                // (which means it's not the initial commit for this file)
                int selectedIndex = historyListView.SelectedItems[0].Index;
                diffButton.Enabled = selectedIndex < commitHistory.Count - 1;
            }
            else
            {
                selectedCommit = null;
                revertButton.Enabled = false;
                viewButton.Enabled = false;
                diffButton.Enabled = false;
            }
        }

        private void RevertButton_Click(object? sender, EventArgs e)
        {
            if (selectedCommit != null)
            {
                // Confirm revert
                var caption = editor.Caption ?? "file";
                var result = MessageBox.Show(
                    $"Are you sure you want to revert {caption} to the version from {selectedCommit.Date.ToString("yyyy-MM-dd HH:mm:ss")}?\n\nThis will replace the current content in the editor.",
                    "Confirm Revert",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    if (gitManager.RevertEditorToCommit(editor, selectedCommit.CommitId))
                    {
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to revert to the selected version. See debug log for details.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ViewButton_Click(object? sender, EventArgs e)
        {
            if (selectedCommit != null && !string.IsNullOrEmpty(editor.RelativePath))
            {
                var content = gitManager.GetFileContentFromCommit(editor.RelativePath, selectedCommit.CommitId);
                if (content != null)
                {
                    using var viewDialog = new TextViewDialog(
                        content, 
                        $"{editor.Caption} - {selectedCommit.Date.ToString("yyyy-MM-dd HH:mm:ss")}",
                        owner);
                    
                    viewDialog.ShowDialog();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to retrieve file content from the selected commit.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void DiffButton_Click(object? sender, EventArgs e)
        {
            if (selectedCommit != null && !string.IsNullOrEmpty(editor.RelativePath))
            {
                // Get the selected index
                int selectedIndex = historyListView.SelectedItems[0].Index;
                
                // Check if this is not the last commit (which would be the initial commit)
                if (selectedIndex < commitHistory.Count - 1)
                {
                    // Get the previous commit to compare with
                    string? previousCommitId = commitHistory[selectedIndex + 1].CommitId;
                    
                    // Get the diff
                    var diff = gitManager.GetFileDiff(editor.RelativePath, selectedCommit.CommitId, previousCommitId);
                    
                    if (diff != null)
                    {
                        using var diffDialog = new DiffViewDialog(
                            diff,
                            $"Diff: {editor.Caption} - {selectedCommit.Date.ToString("yyyy-MM-dd HH:mm:ss")}",
                            owner);
                        
                        diffDialog.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to generate diff for the selected commit.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "No previous version available to compare with. This appears to be the initial commit of the file.",
                        "Information",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            // Center on owner window
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }

            // Create the mouse handler if this is a modal dialog
            if (this.Modal && owner != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw a border around the form
            using (var pen = new Pen(Color.FromArgb(100, 100, 120)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            
            // Dispose the mouse handler
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
} 