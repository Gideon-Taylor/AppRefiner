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
            splitContainer1 = new SplitContainer();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            btnDebugLog = new Button();
            grpEditorSettings = new GroupBox();
            chkAutoPairing = new CheckBox();
            btnPlugins = new Button();
            btnGitInit = new Button();
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
            dataGridView1 = new DataGridView();
            colActive = new DataGridViewCheckBoxColumn();
            colDescr = new DataGridViewTextBoxColumn();
            colConfigure = new DataGridViewButtonColumn();
            splitContainer2 = new SplitContainer();
            btnConnectDB = new Button();
            btnLintCode = new Button();
            btnClearLint = new Button();
            chkLintAnnotate = new CheckBox();
            dataGridView2 = new DataGridView();
            colResultType = new DataGridViewTextBoxColumn();
            colResultMessage = new DataGridViewTextBoxColumn();
            colResultLine = new DataGridViewTextBoxColumn();
            label1 = new Label();
            tabPage5 = new TabPage();
            splitContainer3 = new SplitContainer();
            pnlTemplateParams = new Panel();
            btnApplyTemplate = new Button();
            cmbTemplates = new ComboBox();
            progressBar1 = new ProgressBar();
            lblStatus = new Label();
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
            splitContainerLint.Panel2.SuspendLayout();
            splitContainerLint.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            tabPage5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer3).BeginInit();
            splitContainer3.Panel1.SuspendLayout();
            splitContainer3.Panel2.SuspendLayout();
            splitContainer3.SuspendLayout();
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
            grpEditorSettings.Controls.Add(chkAutoPairing);
            grpEditorSettings.Controls.Add(btnPlugins);
            grpEditorSettings.Controls.Add(btnGitInit);
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
            // chkAutoPairing
            // 
            chkAutoPairing.AutoSize = true;
            chkAutoPairing.ForeColor = Color.Red;
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
            // btnGitInit
            // 
            btnGitInit.Location = new Point(432, 56);
            btnGitInit.Name = "btnGitInit";
            btnGitInit.Size = new Size(110, 23);
            btnGitInit.TabIndex = 27;
            btnGitInit.Text = "Git Repository...";
            btnGitInit.UseVisualStyleBackColor = true;
            btnGitInit.Click += btnGitInit_Click;
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
            splitContainerLint.Panel1.Controls.Add(dataGridView1);
            splitContainerLint.Panel1.Controls.Add(splitContainer2);
            // 
            // splitContainerLint.Panel2
            // 
            splitContainerLint.Panel2.Controls.Add(dataGridView2);
            splitContainerLint.Panel2.Controls.Add(label1);
            splitContainerLint.Size = new Size(556, 459);
            splitContainerLint.SplitterDistance = 230;
            splitContainerLint.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeColumns = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colActive, colDescr, colConfigure });
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 40);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.Size = new Size(556, 190);
            dataGridView1.TabIndex = 5;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
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
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Top;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(btnConnectDB);
            splitContainer2.Panel1.Controls.Add(btnLintCode);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(btnClearLint);
            splitContainer2.Panel2.Controls.Add(chkLintAnnotate);
            splitContainer2.Size = new Size(556, 40);
            splitContainer2.SplitterDistance = 378;
            splitContainer2.TabIndex = 0;
            // 
            // btnConnectDB
            // 
            btnConnectDB.Dock = DockStyle.Right;
            btnConnectDB.Location = new Point(271, 0);
            btnConnectDB.Name = "btnConnectDB";
            btnConnectDB.Size = new Size(107, 40);
            btnConnectDB.TabIndex = 10;
            btnConnectDB.Text = "Connect DB...";
            btnConnectDB.UseVisualStyleBackColor = true;
            btnConnectDB.Click += btnConnectDB_Click;
            // 
            // btnLintCode
            // 
            btnLintCode.Dock = DockStyle.Left;
            btnLintCode.Enabled = false;
            btnLintCode.Location = new Point(0, 0);
            btnLintCode.Name = "btnLintCode";
            btnLintCode.Size = new Size(239, 40);
            btnLintCode.TabIndex = 9;
            btnLintCode.Text = "Lint Code";
            btnLintCode.UseVisualStyleBackColor = true;
            btnLintCode.Click += btnLintCode_Click;
            // 
            // btnClearLint
            // 
            btnClearLint.Dock = DockStyle.Right;
            btnClearLint.Enabled = false;
            btnClearLint.Location = new Point(99, 0);
            btnClearLint.Name = "btnClearLint";
            btnClearLint.Size = new Size(75, 40);
            btnClearLint.TabIndex = 7;
            btnClearLint.Text = "Clear";
            btnClearLint.UseVisualStyleBackColor = true;
            btnClearLint.Click += btnClearLint_Click;
            // 
            // chkLintAnnotate
            // 
            chkLintAnnotate.AutoSize = true;
            chkLintAnnotate.Checked = true;
            chkLintAnnotate.CheckState = CheckState.Checked;
            chkLintAnnotate.Location = new Point(5, 11);
            chkLintAnnotate.Name = "chkLintAnnotate";
            chkLintAnnotate.Size = new Size(75, 19);
            chkLintAnnotate.TabIndex = 6;
            chkLintAnnotate.Text = "Annotate";
            chkLintAnnotate.UseVisualStyleBackColor = true;
            // 
            // dataGridView2
            // 
            dataGridView2.AllowUserToAddRows = false;
            dataGridView2.AllowUserToDeleteRows = false;
            dataGridView2.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Columns.AddRange(new DataGridViewColumn[] { colResultType, colResultMessage, colResultLine });
            dataGridView2.Dock = DockStyle.Fill;
            dataGridView2.Location = new Point(0, 25);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowHeadersVisible = false;
            dataGridView2.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView2.Size = new Size(556, 200);
            dataGridView2.TabIndex = 17;
            dataGridView2.CellClick += dataGridView2_CellClick;
            // 
            // colResultType
            // 
            colResultType.HeaderText = "Level";
            colResultType.Name = "colResultType";
            colResultType.ReadOnly = true;
            colResultType.Width = 75;
            // 
            // colResultMessage
            // 
            colResultMessage.HeaderText = "Message";
            colResultMessage.Name = "colResultMessage";
            colResultMessage.ReadOnly = true;
            colResultMessage.Width = 400;
            // 
            // colResultLine
            // 
            colResultLine.HeaderText = "Line";
            colResultLine.Name = "colResultLine";
            colResultLine.ReadOnly = true;
            colResultLine.Width = 60;
            // 
            // label1
            // 
            label1.Dock = DockStyle.Top;
            label1.Location = new Point(0, 0);
            label1.Name = "label1";
            label1.Size = new Size(556, 25);
            label1.TabIndex = 16;
            label1.Text = "Results";
            label1.TextAlign = ContentAlignment.MiddleCenter;
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
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(570, 561);
            Controls.Add(splitContainer1);
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
            splitContainerLint.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerLint).EndInit();
            splitContainerLint.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            tabPage5.ResumeLayout(false);
            splitContainer3.Panel1.ResumeLayout(false);
            splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer3).EndInit();
            splitContainer3.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer1;
        private ProgressBar progressBar1;
        private Label lblStatus;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private GroupBox grpEditorSettings;
        private Button btnPlugins;
        private Button btnGitInit;
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
        private DataGridView dataGridView1;
        private DataGridViewCheckBoxColumn colActive;
        private DataGridViewTextBoxColumn colDescr;
        private DataGridViewButtonColumn colConfigure;
        private SplitContainer splitContainer2;
        private Button btnConnectDB;
        private Button btnLintCode;
        private Button btnClearLint;
        private CheckBox chkLintAnnotate;
        private DataGridView dataGridView2;
        private DataGridViewTextBoxColumn colResultType;
        private DataGridViewTextBoxColumn colResultMessage;
        private DataGridViewTextBoxColumn colResultLine;
        private Label label1;
        private TabPage tabPage5;
        private SplitContainer splitContainer3;
        private Panel pnlTemplateParams;
        private Button btnApplyTemplate;
        private ComboBox cmbTemplates;
        private Button btnDebugLog;
        private CheckBox chkAutoPairing;
    }
}
