namespace AppRefiner
{
    partial class MainForm
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
            if (disposing)
            {
                // Dispose of managed resources
                if (components != null)
                {
                    components.Dispose();
                }

                // Dispose of timers
                savepointDebounceTimer?.Dispose();

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            splitContainer1 = new SplitContainer();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            btnDebugLog = new Button();
            grpEditorSettings = new GroupBox();
            chkPromptForDB = new CheckBox();
            chkAutoPairing = new CheckBox();
            btnPlugins = new Button();
            chkBetterSQL = new CheckBox();
            chkOnlyPPC = new CheckBox();
            chkInitCollapsed = new CheckBox();
            chkAutoDark = new CheckBox();
            tabPage4 = new TabPage();
            dataGridView3 = new DataGridView();
            dataGridViewCheckBoxColumn1 = new DataGridViewCheckBoxColumn();
            dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            tabPageTooltips = new TabPage();
            dataGridViewTooltips = new DataGridView();
            dataGridViewCheckBoxColumnTooltips = new DataGridViewCheckBoxColumn();
            dataGridViewTextBoxColumnTooltips = new DataGridViewTextBoxColumn();
            tabPage2 = new TabPage();
            splitContainerLint = new SplitContainer();
            splitContainer2 = new SplitContainer();
            tabPage5 = new TabPage();
            splitContainer3 = new SplitContainer();
            pnlTemplateParams = new Panel();
            btnApplyTemplate = new Button();
            cmbTemplates = new ComboBox();
            progressBar1 = new ProgressBar();
            lblStatus = new Label();
            tabPage3 = new TabPage();
            splitContainer4 = new SplitContainer();
            dataGridView1 = new DataGridView();
            colActive = new DataGridViewCheckBoxColumn();
            colDescr = new DataGridViewTextBoxColumn();
            colConfigure = new DataGridViewButtonColumn();
            btnConnectDB = new Button();
            btnClearLint = new Button();
            btnLintCode = new Button();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            grpEditorSettings.SuspendLayout();
            tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).BeginInit();
            tabPageTooltips.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewTooltips).BeginInit();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerLint).BeginInit();
            splitContainerLint.Panel1.SuspendLayout();
            splitContainerLint.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.SuspendLayout();
            tabPage5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer3).BeginInit();
            splitContainer3.Panel1.SuspendLayout();
            splitContainer3.Panel2.SuspendLayout();
            splitContainer3.SuspendLayout();
            tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer4).BeginInit();
            splitContainer4.Panel1.SuspendLayout();
            splitContainer4.Panel2.SuspendLayout();
            splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(progressBar1);
            splitContainer1.Panel2.Controls.Add(lblStatus);
            splitContainer1.Size = new Size(570, 561);
            splitContainer1.SplitterDistance = 493;
            splitContainer1.TabIndex = 0;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Controls.Add(tabPageTooltips);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage5);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(570, 493);
            tabControl1.TabIndex = 3;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(btnDebugLog);
            tabPage1.Controls.Add(grpEditorSettings);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(562, 465);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Editor Tweaks";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // btnDebugLog
            // 
            btnDebugLog.Location = new Point(469, 436);
            btnDebugLog.Name = "btnDebugLog";
            btnDebugLog.Size = new Size(85, 23);
            btnDebugLog.TabIndex = 27;
            btnDebugLog.Text = "Debug Log...";
            btnDebugLog.UseVisualStyleBackColor = true;
            btnDebugLog.Click += btnDebugLog_Click;
            // 
            // grpEditorSettings
            // 
            grpEditorSettings.Controls.Add(chkPromptForDB);
            grpEditorSettings.Controls.Add(chkAutoPairing);
            grpEditorSettings.Controls.Add(btnPlugins);
            grpEditorSettings.Controls.Add(chkBetterSQL);
            grpEditorSettings.Controls.Add(chkOnlyPPC);
            grpEditorSettings.Controls.Add(chkInitCollapsed);
            grpEditorSettings.Controls.Add(chkAutoDark);
            grpEditorSettings.Location = new Point(6, 6);
            grpEditorSettings.Name = "grpEditorSettings";
            grpEditorSettings.Size = new Size(548, 94);
            grpEditorSettings.TabIndex = 20;
            grpEditorSettings.TabStop = false;
            grpEditorSettings.Text = "Settings";
            // 
            // chkPromptForDB
            // 
            chkPromptForDB.AutoSize = true;
            chkPromptForDB.Location = new Point(245, 47);
            chkPromptForDB.Name = "chkPromptForDB";
            chkPromptForDB.Size = new Size(167, 19);
            chkPromptForDB.TabIndex = 28;
            chkPromptForDB.Text = "Prompt for DB Connection";
            chkPromptForDB.UseVisualStyleBackColor = true;
            // 
            // chkAutoPairing
            // 
            chkAutoPairing.AutoSize = true;
            chkAutoPairing.ForeColor = SystemColors.ControlText;
            chkAutoPairing.Location = new Point(245, 22);
            chkAutoPairing.Name = "chkAutoPairing";
            chkAutoPairing.Size = new Size(146, 19);
            chkAutoPairing.TabIndex = 26;
            chkAutoPairing.Text = "Pair quotes and parens";
            chkAutoPairing.UseVisualStyleBackColor = true;
            // 
            // btnPlugins
            // 
            btnPlugins.Location = new Point(432, 18);
            btnPlugins.Name = "btnPlugins";
            btnPlugins.Size = new Size(110, 23);
            btnPlugins.TabIndex = 24;
            btnPlugins.Text = "Plugins...";
            btnPlugins.UseVisualStyleBackColor = true;
            btnPlugins.Click += btnPlugins_Click;
            // 
            // chkBetterSQL
            // 
            chkBetterSQL.AutoSize = true;
            chkBetterSQL.Location = new Point(116, 47);
            chkBetterSQL.Name = "chkBetterSQL";
            chkBetterSQL.Size = new Size(88, 19);
            chkBetterSQL.TabIndex = 17;
            chkBetterSQL.Text = "Format SQL";
            chkBetterSQL.UseVisualStyleBackColor = true;
            // 
            // chkOnlyPPC
            // 
            chkOnlyPPC.AutoSize = true;
            chkOnlyPPC.Location = new Point(10, 47);
            chkOnlyPPC.Name = "chkOnlyPPC";
            chkOnlyPPC.Size = new Size(76, 19);
            chkOnlyPPC.TabIndex = 16;
            chkOnlyPPC.Text = "Only PPC";
            chkOnlyPPC.UseVisualStyleBackColor = true;
            // 
            // chkInitCollapsed
            // 
            chkInitCollapsed.AutoSize = true;
            chkInitCollapsed.Location = new Point(10, 22);
            chkInitCollapsed.Name = "chkInitCollapsed";
            chkInitCollapsed.Size = new Size(100, 19);
            chkInitCollapsed.TabIndex = 15;
            chkInitCollapsed.Text = "Auto Collapse";
            chkInitCollapsed.UseVisualStyleBackColor = true;
            // 
            // chkAutoDark
            // 
            chkAutoDark.AutoSize = true;
            chkAutoDark.Location = new Point(116, 22);
            chkAutoDark.Name = "chkAutoDark";
            chkAutoDark.Size = new Size(113, 19);
            chkAutoDark.TabIndex = 14;
            chkAutoDark.Text = "Auto Dark Mode";
            chkAutoDark.UseVisualStyleBackColor = true;
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(dataGridView3);
            tabPage4.Location = new Point(4, 24);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(3);
            tabPage4.Size = new Size(562, 465);
            tabPage4.TabIndex = 3;
            tabPage4.Text = "Stylers";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // dataGridView3
            // 
            dataGridView3.AllowUserToAddRows = false;
            dataGridView3.AllowUserToDeleteRows = false;
            dataGridView3.AllowUserToResizeColumns = false;
            dataGridView3.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView3.Columns.AddRange(new DataGridViewColumn[] { dataGridViewCheckBoxColumn1, dataGridViewTextBoxColumn1 });
            dataGridView3.Dock = DockStyle.Fill;
            dataGridView3.Location = new Point(3, 3);
            dataGridView3.Name = "dataGridView3";
            dataGridView3.RowHeadersVisible = false;
            dataGridView3.Size = new Size(556, 459);
            dataGridView3.TabIndex = 3;
            dataGridView3.CellContentClick += dataGridView3_CellContentClick;
            dataGridView3.CellValueChanged += dataGridView3_CellValueChanged;
            // 
            // dataGridViewCheckBoxColumn1
            // 
            dataGridViewCheckBoxColumn1.FillWeight = 75.21733F;
            dataGridViewCheckBoxColumn1.HeaderText = "Active";
            dataGridViewCheckBoxColumn1.Name = "dataGridViewCheckBoxColumn1";
            dataGridViewCheckBoxColumn1.Width = 50;
            // 
            // dataGridViewTextBoxColumn1
            // 
            dataGridViewTextBoxColumn1.FillWeight = 110.569466F;
            dataGridViewTextBoxColumn1.HeaderText = "Description";
            dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            dataGridViewTextBoxColumn1.ReadOnly = true;
            dataGridViewTextBoxColumn1.Width = 500;
            // 
            // tabPageTooltips
            // 
            tabPageTooltips.Controls.Add(dataGridViewTooltips);
            tabPageTooltips.Location = new Point(4, 24);
            tabPageTooltips.Name = "tabPageTooltips";
            tabPageTooltips.Padding = new Padding(3);
            tabPageTooltips.Size = new Size(562, 465);
            tabPageTooltips.TabIndex = 6;
            tabPageTooltips.Text = "Tooltips";
            tabPageTooltips.UseVisualStyleBackColor = true;
            // 
            // dataGridViewTooltips
            // 
            dataGridViewTooltips.AllowUserToAddRows = false;
            dataGridViewTooltips.AllowUserToDeleteRows = false;
            dataGridViewTooltips.AllowUserToResizeColumns = false;
            dataGridViewTooltips.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewTooltips.Columns.AddRange(new DataGridViewColumn[] { dataGridViewCheckBoxColumnTooltips, dataGridViewTextBoxColumnTooltips });
            dataGridViewTooltips.Dock = DockStyle.Fill;
            dataGridViewTooltips.Location = new Point(3, 3);
            dataGridViewTooltips.Name = "dataGridViewTooltips";
            dataGridViewTooltips.RowHeadersVisible = false;
            dataGridViewTooltips.Size = new Size(556, 459);
            dataGridViewTooltips.TabIndex = 3;
            dataGridViewTooltips.CellContentClick += dataGridViewTooltips_CellContentClick;
            dataGridViewTooltips.CellValueChanged += dataGridViewTooltips_CellValueChanged;
            // 
            // dataGridViewCheckBoxColumnTooltips
            // 
            dataGridViewCheckBoxColumnTooltips.FillWeight = 75.21733F;
            dataGridViewCheckBoxColumnTooltips.HeaderText = "Active";
            dataGridViewCheckBoxColumnTooltips.Name = "dataGridViewCheckBoxColumnTooltips";
            dataGridViewCheckBoxColumnTooltips.Width = 50;
            // 
            // dataGridViewTextBoxColumnTooltips
            // 
            dataGridViewTextBoxColumnTooltips.FillWeight = 110.569466F;
            dataGridViewTextBoxColumnTooltips.HeaderText = "Description";
            dataGridViewTextBoxColumnTooltips.Name = "dataGridViewTextBoxColumnTooltips";
            dataGridViewTextBoxColumnTooltips.ReadOnly = true;
            dataGridViewTextBoxColumnTooltips.Width = 500;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(splitContainerLint);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(562, 465);
            tabPage2.TabIndex = 4;
            tabPage2.Text = "Linters";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // splitContainerLint
            // 
            splitContainerLint.Dock = DockStyle.Fill;
            splitContainerLint.Location = new Point(3, 3);
            splitContainerLint.Name = "splitContainerLint";
            splitContainerLint.Orientation = Orientation.Horizontal;
            // 
            // splitContainerLint.Panel1
            // 
            splitContainerLint.Panel1.Controls.Add(splitContainer2);
            splitContainerLint.Size = new Size(556, 459);
            splitContainerLint.SplitterDistance = 230;
            splitContainerLint.TabIndex = 0;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Top;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            splitContainer2.Size = new Size(556, 40);
            splitContainer2.SplitterDistance = 378;
            splitContainer2.TabIndex = 0;
            // 
            // tabPage5
            // 
            tabPage5.Controls.Add(splitContainer3);
            tabPage5.Controls.Add(cmbTemplates);
            tabPage5.Location = new Point(4, 24);
            tabPage5.Name = "tabPage5";
            tabPage5.Padding = new Padding(3);
            tabPage5.Size = new Size(562, 465);
            tabPage5.TabIndex = 5;
            tabPage5.Text = "Templates";
            tabPage5.UseVisualStyleBackColor = true;
            // 
            // splitContainer3
            // 
            splitContainer3.Dock = DockStyle.Fill;
            splitContainer3.Location = new Point(3, 26);
            splitContainer3.Name = "splitContainer3";
            splitContainer3.Orientation = Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            splitContainer3.Panel1.Controls.Add(pnlTemplateParams);
            // 
            // splitContainer3.Panel2
            // 
            splitContainer3.Panel2.Controls.Add(btnApplyTemplate);
            splitContainer3.Size = new Size(556, 436);
            splitContainer3.SplitterDistance = 403;
            splitContainer3.TabIndex = 1;
            // 
            // pnlTemplateParams
            // 
            pnlTemplateParams.Dock = DockStyle.Fill;
            pnlTemplateParams.Location = new Point(0, 0);
            pnlTemplateParams.Name = "pnlTemplateParams";
            pnlTemplateParams.Size = new Size(556, 403);
            pnlTemplateParams.TabIndex = 3;
            // 
            // btnApplyTemplate
            // 
            btnApplyTemplate.Dock = DockStyle.Fill;
            btnApplyTemplate.Location = new Point(0, 0);
            btnApplyTemplate.Name = "btnApplyTemplate";
            btnApplyTemplate.Size = new Size(556, 29);
            btnApplyTemplate.TabIndex = 1;
            btnApplyTemplate.Text = "Generate Template";
            btnApplyTemplate.UseVisualStyleBackColor = true;
            btnApplyTemplate.Click += btnApplyTemplate_Click;
            // 
            // cmbTemplates
            // 
            cmbTemplates.Dock = DockStyle.Top;
            cmbTemplates.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTemplates.FormattingEnabled = true;
            cmbTemplates.Location = new Point(3, 3);
            cmbTemplates.Name = "cmbTemplates";
            cmbTemplates.Size = new Size(556, 23);
            cmbTemplates.TabIndex = 0;
            // 
            // progressBar1
            // 
            progressBar1.Dock = DockStyle.Bottom;
            progressBar1.Location = new Point(0, 9);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(570, 25);
            progressBar1.TabIndex = 22;
            // 
            // lblStatus
            // 
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Location = new Point(0, 34);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(570, 30);
            lblStatus.TabIndex = 21;
            lblStatus.Text = "Stopped";
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(splitContainer4);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(562, 465);
            tabPage3.TabIndex = 7;
            tabPage3.Text = "Linters";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // splitContainer4
            // 
            splitContainer4.Dock = DockStyle.Fill;
            splitContainer4.Location = new Point(3, 3);
            splitContainer4.Name = "splitContainer4";
            splitContainer4.Orientation = Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            splitContainer4.Panel1.Controls.Add(btnConnectDB);
            splitContainer4.Panel1.Controls.Add(btnClearLint);
            splitContainer4.Panel1.Controls.Add(btnLintCode);
            // 
            // splitContainer4.Panel2
            // 
            splitContainer4.Panel2.Controls.Add(dataGridView1);
            splitContainer4.Size = new Size(556, 459);
            splitContainer4.SplitterDistance = 49;
            splitContainer4.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeColumns = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colActive, colDescr, colConfigure });
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.Size = new Size(556, 406);
            dataGridView1.TabIndex = 6;
            // 
            // colActive
            // 
            colActive.FillWeight = 75.21733F;
            colActive.HeaderText = "Active";
            colActive.Name = "colActive";
            colActive.Width = 50;
            // 
            // colDescr
            // 
            colDescr.FillWeight = 110.569466F;
            colDescr.HeaderText = "Description";
            colDescr.Name = "colDescr";
            colDescr.ReadOnly = true;
            colDescr.Width = 410;
            // 
            // colConfigure
            // 
            colConfigure.FillWeight = 114.213196F;
            colConfigure.HeaderText = "Configure";
            colConfigure.Name = "colConfigure";
            colConfigure.Text = "Configure...";
            colConfigure.UseColumnTextForButtonValue = true;
            colConfigure.Width = 75;
            // 
            // btnConnectDB
            // 
            btnConnectDB.Dock = DockStyle.Right;
            btnConnectDB.Location = new Point(449, 0);
            btnConnectDB.Name = "btnConnectDB";
            btnConnectDB.Size = new Size(107, 49);
            btnConnectDB.TabIndex = 14;
            btnConnectDB.Text = "Connect DB...";
            btnConnectDB.UseVisualStyleBackColor = true;
            btnConnectDB.Click += btnConnectDB_Click;
            // 
            // btnClearLint
            // 
            btnClearLint.Dock = DockStyle.Left;
            btnClearLint.Enabled = false;
            btnClearLint.Location = new Point(239, 0);
            btnClearLint.Name = "btnClearLint";
            btnClearLint.Size = new Size(123, 49);
            btnClearLint.TabIndex = 13;
            btnClearLint.Text = "Clear Annotations";
            btnClearLint.UseVisualStyleBackColor = true;
            btnClearLint.Click += btnClearLint_Click;
            // 
            // btnLintCode
            // 
            btnLintCode.Dock = DockStyle.Left;
            btnLintCode.Enabled = false;
            btnLintCode.Location = new Point(0, 0);
            btnLintCode.Name = "btnLintCode";
            btnLintCode.Size = new Size(239, 49);
            btnLintCode.TabIndex = 12;
            btnLintCode.Text = "Lint Code";
            btnLintCode.UseVisualStyleBackColor = true;
            btnLintCode.Click += btnLintCode_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(570, 561);
            Controls.Add(splitContainer1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "App Refiner";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            grpEditorSettings.ResumeLayout(false);
            grpEditorSettings.PerformLayout();
            tabPage4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView3).EndInit();
            tabPageTooltips.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewTooltips).EndInit();
            tabPage2.ResumeLayout(false);
            splitContainerLint.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerLint).EndInit();
            splitContainerLint.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            tabPage5.ResumeLayout(false);
            splitContainer3.Panel1.ResumeLayout(false);
            splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer3).EndInit();
            splitContainer3.ResumeLayout(false);
            tabPage3.ResumeLayout(false);
            splitContainer4.Panel1.ResumeLayout(false);
            splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer4).EndInit();
            splitContainer4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            linterManager?.HandleLinterGridCellContentClick(sender, e);
        }

        #endregion

        private SplitContainer splitContainer1;
        private ProgressBar progressBar1;
        private Label lblStatus;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private GroupBox grpEditorSettings;
        private Button btnPlugins;
        private CheckBox chkBetterSQL;
        private CheckBox chkOnlyPPC;
        private CheckBox chkInitCollapsed;
        private CheckBox chkAutoDark;
        private TabPage tabPage4;
        private DataGridView dataGridView3;
        private DataGridViewCheckBoxColumn dataGridViewCheckBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private TabPage tabPageTooltips;
        private DataGridView dataGridViewTooltips;
        private DataGridViewCheckBoxColumn dataGridViewCheckBoxColumnTooltips;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumnTooltips;
        private TabPage tabPage2;
        private SplitContainer splitContainerLint;
        private SplitContainer splitContainer2;
        private TabPage tabPage5;
        private SplitContainer splitContainer3;
        private Panel pnlTemplateParams;
        private Button btnApplyTemplate;
        private ComboBox cmbTemplates;
        private Button btnDebugLog;
        private CheckBox chkAutoPairing;
        private CheckBox chkPromptForDB;
        private TabPage tabPage3;
        private SplitContainer splitContainer4;
        private DataGridView dataGridView1;
        private DataGridViewCheckBoxColumn colActive;
        private DataGridViewTextBoxColumn colDescr;
        private DataGridViewButtonColumn colConfigure;
        private Button btnConnectDB;
        private Button btnClearLint;
        private Button btnLintCode;
    }
}
