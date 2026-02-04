namespace AppRefiner.Plugins
{
    partial class PluginManagerDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            txtPluginDirectory = new TextBox();
            btnBrowse = new Button();
            lstPlugins = new ListView();
            colName = new ColumnHeader();
            colVersion = new ColumnHeader();
            colLinters = new ColumnHeader();
            colStylers = new ColumnHeader();
            colCommands = new ColumnHeader();
            colRefactors = new ColumnHeader();
            colExtensions = new ColumnHeader();
            colPath = new ColumnHeader();
            label2 = new Label();
            btnRefresh = new Button();
            btnSave = new Button();
            lblStatus = new Label();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 15);
            label1.Name = "label1";
            label1.Size = new Size(95, 15);
            label1.TabIndex = 0;
            label1.Text = "Plugin Directory:";
            // 
            // txtPluginDirectory
            // 
            txtPluginDirectory.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtPluginDirectory.Location = new Point(111, 12);
            txtPluginDirectory.Name = "txtPluginDirectory";
            txtPluginDirectory.Size = new Size(594, 23);
            txtPluginDirectory.TabIndex = 1;
            // 
            // btnBrowse
            // 
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Location = new Point(711, 11);
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new Size(75, 23);
            btnBrowse.TabIndex = 2;
            btnBrowse.Text = "Browse...";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Click += btnBrowse_Click;
            // 
            // lstPlugins
            // 
            lstPlugins.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstPlugins.Columns.AddRange(new ColumnHeader[] { colName, colVersion, colLinters, colStylers, colCommands, colRefactors, colExtensions, colPath });
            lstPlugins.FullRowSelect = true;
            lstPlugins.GridLines = true;
            lstPlugins.Location = new Point(12, 69);
            lstPlugins.Name = "lstPlugins";
            lstPlugins.Size = new Size(774, 321);
            lstPlugins.TabIndex = 3;
            lstPlugins.UseCompatibleStateImageBehavior = false;
            lstPlugins.View = View.Details;
            // 
            // colName
            // 
            colName.Text = "Name";
            colName.Width = 80;
            // 
            // colVersion
            // 
            colVersion.Text = "Version";
            // 
            // colLinters
            // 
            colLinters.Text = "Linters";
            colLinters.Width = 50;
            // 
            // colStylers
            // 
            colStylers.Text = "Stylers";
            colStylers.Width = 50;
            // 
            // colCommands
            // 
            colCommands.Text = "Commands";
            colCommands.Width = 75;
            // 
            // colRefactors
            // 
            colRefactors.Text = "Refactors";
            colRefactors.Width = 65;
            // 
            // colExtensions
            // 
            colExtensions.Text = "Extensions";
            colExtensions.Width = 70;
            // 
            // colPath
            // 
            colPath.Text = "Path";
            colPath.Width = 400;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 51);
            label2.Name = "label2";
            label2.Size = new Size(91, 15);
            label2.TabIndex = 4;
            label2.Text = "Loaded Plugins:";
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Location = new Point(711, 40);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(75, 23);
            btnRefresh.TabIndex = 5;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Location = new Point(711, 411);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(75, 23);
            btnSave.TabIndex = 6;
            btnSave.Text = "OK";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(12, 419);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(100, 15);
            lblStatus.TabIndex = 8;
            lblStatus.Text = "No plugins found";
            // 
            // PluginManagerDialog
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(798, 450);
            Controls.Add(lblStatus);
            Controls.Add(btnSave);
            Controls.Add(btnRefresh);
            Controls.Add(label2);
            Controls.Add(lstPlugins);
            Controls.Add(btnBrowse);
            Controls.Add(txtPluginDirectory);
            Controls.Add(label1);
            MinimizeBox = false;
            Name = "PluginManagerDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Plugin Manager";
            Load += PluginManagerDialog_Load;
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private Label label1;
        private TextBox txtPluginDirectory;
        private Button btnBrowse;
        private ListView lstPlugins;
        private ColumnHeader colName;
        private ColumnHeader colVersion;
        private ColumnHeader colLinters;
        private ColumnHeader colStylers;
        private ColumnHeader colCommands;
        private ColumnHeader colRefactors;
        private ColumnHeader colExtensions;
        private ColumnHeader colPath;
        private Label label2;
        private Button btnRefresh;
        private Button btnSave;
        private Label lblStatus;
    }
}
