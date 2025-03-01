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
            grpEditorActions = new GroupBox();
            btnRestoreSnapshot = new Button();
            btnTakeSnapshot = new Button();
            btnCollapseAll = new Button();
            btnExpand = new Button();
            btnDarkMode = new Button();
            btnStart = new Button();
            grpEditorSettings = new GroupBox();
            chkBetterSQL = new CheckBox();
            chkOnlyPPC = new CheckBox();
            chkInitCollapsed = new CheckBox();
            chkAutoDark = new CheckBox();
            tabPage4 = new TabPage();
            dataGridView3 = new DataGridView();
            dataGridViewCheckBoxColumn1 = new DataGridViewCheckBoxColumn();
            dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            tabPage2 = new TabPage();
            splitContainerLint = new SplitContainer();
            dataGridView1 = new DataGridView();
            colActive = new DataGridViewCheckBoxColumn();
            colDescr = new DataGridViewTextBoxColumn();
            colLevel = new DataGridViewComboBoxColumn();
            splitContainer2 = new SplitContainer();
            btnLintCode = new Button();
            btnClearLint = new Button();
            chkLintAnnotate = new CheckBox();
            dataGridView2 = new DataGridView();
            colResultType = new DataGridViewTextBoxColumn();
            colResultMessage = new DataGridViewTextBoxColumn();
            colResultLine = new DataGridViewTextBoxColumn();
            label1 = new Label();
            tabPage3 = new TabPage();
            groupBox1 = new GroupBox();
            btnAddFlowerBox = new Button();
            progressBar1 = new ProgressBar();
            lblStatus = new Label();
            btnOptimizeImports = new Button();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            grpEditorActions.SuspendLayout();
            grpEditorSettings.SuspendLayout();
            tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).BeginInit();
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
            tabPage3.SuspendLayout();
            groupBox1.SuspendLayout();
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
            splitContainer1.Size = new Size(531, 561);
            splitContainer1.SplitterDistance = 493;
            splitContainer1.TabIndex = 0;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(531, 493);
            tabControl1.TabIndex = 3;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(grpEditorActions);
            tabPage1.Controls.Add(btnStart);
            tabPage1.Controls.Add(grpEditorSettings);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(523, 465);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Editor Tweaks";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // grpEditorActions
            // 
            grpEditorActions.Controls.Add(btnRestoreSnapshot);
            grpEditorActions.Controls.Add(btnTakeSnapshot);
            grpEditorActions.Controls.Add(btnCollapseAll);
            grpEditorActions.Controls.Add(btnExpand);
            grpEditorActions.Controls.Add(btnDarkMode);
            grpEditorActions.Enabled = false;
            grpEditorActions.Location = new Point(8, 149);
            grpEditorActions.Name = "grpEditorActions";
            grpEditorActions.Size = new Size(528, 106);
            grpEditorActions.TabIndex = 0;
            grpEditorActions.TabStop = false;
            grpEditorActions.Text = "Actions";
            // 
            // btnRestoreSnapshot
            // 
            btnRestoreSnapshot.Enabled = false;
            btnRestoreSnapshot.Location = new Point(101, 51);
            btnRestoreSnapshot.Name = "btnRestoreSnapshot";
            btnRestoreSnapshot.Size = new Size(110, 23);
            btnRestoreSnapshot.TabIndex = 25;
            btnRestoreSnapshot.Text = "Restore Snapshot";
            btnRestoreSnapshot.UseVisualStyleBackColor = true;
            btnRestoreSnapshot.Click += btnRestoreSnapshot_Click;
            // 
            // btnTakeSnapshot
            // 
            btnTakeSnapshot.Location = new Point(101, 22);
            btnTakeSnapshot.Name = "btnTakeSnapshot";
            btnTakeSnapshot.Size = new Size(110, 23);
            btnTakeSnapshot.TabIndex = 24;
            btnTakeSnapshot.Text = "Take Snapshot";
            btnTakeSnapshot.UseVisualStyleBackColor = true;
            btnTakeSnapshot.Click += btnTakeSnapshot_Click;
            // 
            // btnCollapseAll
            // 
            btnCollapseAll.Location = new Point(10, 22);
            btnCollapseAll.Name = "btnCollapseAll";
            btnCollapseAll.Size = new Size(85, 23);
            btnCollapseAll.TabIndex = 21;
            btnCollapseAll.Text = "Collapse All";
            btnCollapseAll.UseVisualStyleBackColor = true;
            btnCollapseAll.Click += btnCollapseAll_Click;
            // 
            // btnExpand
            // 
            btnExpand.Location = new Point(10, 51);
            btnExpand.Name = "btnExpand";
            btnExpand.Size = new Size(85, 23);
            btnExpand.TabIndex = 22;
            btnExpand.Text = "Expand All";
            btnExpand.UseVisualStyleBackColor = true;
            btnExpand.Click += btnExpand_Click;
            // 
            // btnDarkMode
            // 
            btnDarkMode.Location = new Point(424, 22);
            btnDarkMode.Name = "btnDarkMode";
            btnDarkMode.Size = new Size(85, 23);
            btnDarkMode.TabIndex = 23;
            btnDarkMode.Text = "Dark Mode";
            btnDarkMode.UseVisualStyleBackColor = true;
            btnDarkMode.Click += btnDarkMode_Click;
            // 
            // btnStart
            // 
            btnStart.Dock = DockStyle.Top;
            btnStart.Font = new Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnStart.Location = new Point(3, 3);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(517, 45);
            btnStart.TabIndex = 24;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // grpEditorSettings
            // 
            grpEditorSettings.Controls.Add(chkBetterSQL);
            grpEditorSettings.Controls.Add(chkOnlyPPC);
            grpEditorSettings.Controls.Add(chkInitCollapsed);
            grpEditorSettings.Controls.Add(chkAutoDark);
            grpEditorSettings.Location = new Point(8, 54);
            grpEditorSettings.Name = "grpEditorSettings";
            grpEditorSettings.Size = new Size(528, 89);
            grpEditorSettings.TabIndex = 20;
            grpEditorSettings.TabStop = false;
            grpEditorSettings.Text = "Settings";
            // 
            // chkBetterSQL
            // 
            chkBetterSQL.AutoSize = true;
            chkBetterSQL.Location = new Point(161, 42);
            chkBetterSQL.Name = "chkBetterSQL";
            chkBetterSQL.Size = new Size(88, 19);
            chkBetterSQL.TabIndex = 17;
            chkBetterSQL.Text = "Format SQL";
            chkBetterSQL.UseVisualStyleBackColor = true;
            // 
            // chkOnlyPPC
            // 
            chkOnlyPPC.AutoSize = true;
            chkOnlyPPC.Location = new Point(10, 42);
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
            chkAutoDark.Location = new Point(161, 22);
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
            tabPage4.Size = new Size(523, 465);
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
            dataGridView3.Size = new Size(517, 459);
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
            // tabPage2
            // 
            tabPage2.Controls.Add(splitContainerLint);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(523, 465);
            tabPage2.TabIndex = 4;
            tabPage2.Text = "Linting";
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
            splitContainerLint.Size = new Size(517, 459);
            splitContainerLint.SplitterDistance = 230;
            splitContainerLint.TabIndex = 0;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeColumns = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { colActive, colDescr, colLevel });
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 40);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.Size = new Size(517, 190);
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
            // colLevel
            // 
            colLevel.FillWeight = 114.213196F;
            colLevel.HeaderText = "Level";
            colLevel.Items.AddRange(new object[] { "GrayOut", "Style", "Error", "Warning", "Info" });
            colLevel.Name = "colLevel";
            colLevel.Width = 75;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Top;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(btnLintCode);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(btnClearLint);
            splitContainer2.Panel2.Controls.Add(chkLintAnnotate);
            splitContainer2.Size = new Size(517, 40);
            splitContainer2.SplitterDistance = 352;
            splitContainer2.TabIndex = 0;
            // 
            // btnLintCode
            // 
            btnLintCode.Dock = DockStyle.Top;
            btnLintCode.Enabled = false;
            btnLintCode.Location = new Point(0, 0);
            btnLintCode.Name = "btnLintCode";
            btnLintCode.Size = new Size(352, 40);
            btnLintCode.TabIndex = 4;
            btnLintCode.Text = "Lint Code";
            btnLintCode.UseVisualStyleBackColor = true;
            btnLintCode.Click += btnLintCode_Click;
            // 
            // btnClearLint
            // 
            btnClearLint.Dock = DockStyle.Right;
            btnClearLint.Location = new Point(86, 0);
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
            dataGridView2.Size = new Size(517, 200);
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
            label1.Size = new Size(517, 25);
            label1.TabIndex = 16;
            label1.Text = "Results";
            label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(groupBox1);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(523, 465);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Refactor";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(btnOptimizeImports);
            groupBox1.Controls.Add(btnAddFlowerBox);
            groupBox1.Location = new Point(8, 8);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(528, 113);
            groupBox1.TabIndex = 2;
            groupBox1.TabStop = false;
            groupBox1.Text = "Quick Edits";
            // 
            // btnAddFlowerBox
            // 
            btnAddFlowerBox.Location = new Point(6, 22);
            btnAddFlowerBox.Name = "btnAddFlowerBox";
            btnAddFlowerBox.Size = new Size(109, 23);
            btnAddFlowerBox.TabIndex = 2;
            btnAddFlowerBox.Text = "Add Flowerbox";
            btnAddFlowerBox.UseVisualStyleBackColor = true;
            btnAddFlowerBox.Click += btnAddFlowerBox_Click;
            // 
            // progressBar1
            // 
            progressBar1.Dock = DockStyle.Bottom;
            progressBar1.Location = new Point(0, 9);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(531, 25);
            progressBar1.TabIndex = 22;
            // 
            // lblStatus
            // 
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Location = new Point(0, 34);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(531, 30);
            lblStatus.TabIndex = 21;
            lblStatus.Text = "Stopped";
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnOptimizeImports
            // 
            btnOptimizeImports.Location = new Point(121, 22);
            btnOptimizeImports.Name = "btnOptimizeImports";
            btnOptimizeImports.Size = new Size(109, 23);
            btnOptimizeImports.TabIndex = 3;
            btnOptimizeImports.Text = "Optimize Imports";
            btnOptimizeImports.UseVisualStyleBackColor = true;
            btnOptimizeImports.Click += btnOptimizeImports_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(531, 561);
            Controls.Add(splitContainer1);
            Name = "MainForm";
            Text = "App Refiner";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            grpEditorActions.ResumeLayout(false);
            grpEditorSettings.ResumeLayout(false);
            grpEditorSettings.PerformLayout();
            tabPage4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView3).EndInit();
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
            tabPage3.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer1;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private GroupBox grpEditorActions;
        private Button btnCollapseAll;
        private Button btnExpand;
        private Button btnDarkMode;
        private Button btnStart;
        private GroupBox grpEditorSettings;
        private CheckBox chkBetterSQL;
        private CheckBox chkOnlyPPC;
        private CheckBox chkInitCollapsed;
        private CheckBox chkAutoDark;
        private TabPage tabPage4;
        private DataGridView dataGridView3;
        private DataGridViewCheckBoxColumn dataGridViewCheckBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private TabPage tabPage2;
        private SplitContainer splitContainerLint;
        private DataGridView dataGridView2;
        private DataGridViewTextBoxColumn colResultType;
        private DataGridViewTextBoxColumn colResultMessage;
        private DataGridViewTextBoxColumn colResultLine;
        private Label label1;
        private TabPage tabPage3;
        private ProgressBar progressBar1;
        private Label lblStatus;
        private SplitContainer splitContainer2;
        private Button btnLintCode;
        private CheckBox chkLintAnnotate;
        private DataGridView dataGridView1;
        private DataGridViewCheckBoxColumn colActive;
        private DataGridViewTextBoxColumn colDescr;
        private DataGridViewComboBoxColumn colLevel;
        private Button btnClearLint;
        private Button btnRestoreSnapshot;
        private Button btnTakeSnapshot;
        private GroupBox groupBox1;
        private Button btnAddFlowerBox;
        private Button btnOptimizeImports;
    }
}