using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppRefiner.Dialogs
{
    public partial class InitGitRepositoryDialog : Form
    {
        public string RepositoryPath { get; private set; }

        public InitGitRepositoryDialog()
        {
            InitializeComponent();

            // Use default path from settings if available
            if (!string.IsNullOrEmpty(Properties.Settings.Default.GitRepositoryPath))
            {
                txtRepositoryPath.Text = Properties.Settings.Default.GitRepositoryPath;
            }
        }

        private void InitializeComponent()
        {
            this.txtRepositoryPath = new TextBox();
            this.btnBrowse = new Button();
            this.label1 = new Label();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.SuspendLayout();
            
            // 
            // txtRepositoryPath
            // 
            this.txtRepositoryPath.Anchor = ((AnchorStyles)(((AnchorStyles.Top | AnchorStyles.Left) | AnchorStyles.Right)));
            this.txtRepositoryPath.Location = new Point(12, 32);
            this.txtRepositoryPath.Name = "txtRepositoryPath";
            this.txtRepositoryPath.Size = new Size(351, 23);
            this.txtRepositoryPath.TabIndex = 0;
            
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((AnchorStyles)((AnchorStyles.Top | AnchorStyles.Right)));
            this.btnBrowse.Location = new Point(369, 31);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new Size(75, 23);
            this.btnBrowse.TabIndex = 1;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new EventHandler(this.btnBrowse_Click);
            
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new Size(211, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Select folder to initialize Git repository:";
            
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((AnchorStyles)((AnchorStyles.Bottom | AnchorStyles.Right)));
            this.btnOK.DialogResult = DialogResult.OK;
            this.btnOK.Location = new Point(288, 76);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new EventHandler(this.btnOK_Click);
            
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((AnchorStyles)((AnchorStyles.Bottom | AnchorStyles.Right)));
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new Point(369, 76);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            
            // 
            // InitGitRepositoryDialog
            // 
            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new Size(456, 111);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.txtRepositoryPath);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InitGitRepositoryDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Initialize Git Repository";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            // Create the folder browser dialog but don't show it yet
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select folder to initialize Git repository",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(txtRepositoryPath.Text) && Directory.Exists(txtRepositoryPath.Text))
            {
                folderDialog.InitialDirectory = txtRepositoryPath.Text;
            }

            // Make sure we're on the UI thread and dialog is properly parented
            if (InvokeRequired)
            {
                Invoke(() => ShowFolderBrowser(folderDialog));
            }
            else
            {
                ShowFolderBrowser(folderDialog);
            }
        }

        private void ShowFolderBrowser(FolderBrowserDialog folderDialog)
        {
            // Temporarily hide this dialog to prevent focus issues
            this.Visible = false;
            
            try
            {
                // Show the folder browser dialog
                if (folderDialog.ShowDialog(this.Owner) == DialogResult.OK)
                {
                    txtRepositoryPath.Text = folderDialog.SelectedPath;
                }
            }
            finally
            {
                // Make sure our dialog becomes visible again
                this.Visible = true;
                this.BringToFront();
                this.Activate();
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRepositoryPath.Text))
            {
                MessageBox.Show("Please select a folder for the Git repository.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
                return;
            }

            if (!Directory.Exists(txtRepositoryPath.Text))
            {
                var result = MessageBox.Show("The selected folder does not exist. Would you like to create it?", 
                    "Folder does not exist", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.No)
                {
                    DialogResult = DialogResult.None;
                    return;
                }

                try
                {
                    Directory.CreateDirectory(txtRepositoryPath.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create directory: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
                }
            }

            // Check if directory already contains a Git repository
            if (Directory.Exists(Path.Combine(txtRepositoryPath.Text, ".git")))
            {
                var result = MessageBox.Show("This folder already appears to contain a Git repository. " +
                    "Do you want to use this existing repository?", 
                    "Repository exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.No)
                {
                    DialogResult = DialogResult.None;
                    return;
                }
            }

            RepositoryPath = txtRepositoryPath.Text;
            
            // Save path to settings
            // Properties.Settings.Default.GitRepositoryPath = RepositoryPath;
            // Properties.Settings.Default.Save();
        }

        private TextBox txtRepositoryPath;
        private Button btnBrowse;
        private Label label1;
        private Button btnOK;
        private Button btnCancel;
    }
} 