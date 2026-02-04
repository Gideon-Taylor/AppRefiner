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
            grpCodeFolding = new GroupBox();
            chkCodeFolding = new CheckBox();
            chkInitCollapsed = new CheckBox();
            chkRememberFolds = new CheckBox();
            grpEditorFeatures = new GroupBox();
            chkMultiSelection = new CheckBox();
            chkAutoPairing = new CheckBox();
            chkDocMinimap = new CheckBox();
            chkInlineParameterHints = new CheckBox();
            chkLineSelectionFix = new CheckBox();
            groupBox3 = new GroupBox();
            chkVariableSuggestions = new CheckBox();
            chkObjectMembers = new CheckBox();
            chkFunctionSignatures = new CheckBox();
            chkSystemVariables = new CheckBox();
            label1 = new Label();
            cmbTheme = new ComboBox();
            chkFilled = new CheckBox();
            grpAppearance = new GroupBox();
            chkAutoDark = new CheckBox();
            chkAutoCenterDialogs = new CheckBox();
            grpFeatureOverrides = new GroupBox();
            chkOverrideFindReplace = new CheckBox();
            chkOverrideOpen = new CheckBox();
            btnConfigSmartOpen = new Button();
            chkBetterSQL = new CheckBox();
            groupBox2 = new GroupBox();
            chkEventMapping = new CheckBox();
            chkEventMapXrefs = new CheckBox();
            groupBox4 = new GroupBox();
            optClassPath = new RadioButton();
            optClassText = new RadioButton();
            grpApplication = new GroupBox();
            chkOnlyPPC = new CheckBox();
            chkPromptForDB = new CheckBox();
            btnPlugins = new Button();
            lblLintReportDir = new Label();
            txtLintReportDir = new TextBox();
            btnBrowseLintReport = new Button();
            lblTnsAdminDir = new Label();
            txtTnsAdminDir = new TextBox();
            btnBrowseTnsAdmin = new Button();
            lnkWhatsNew = new LinkLabel();
            linkDocs = new LinkLabel();
            btnDebugLog = new Button();
            tabPage4 = new TabPage();
            dataGridView3 = new DataGridView();
            dataGridViewCheckBoxColumn1 = new DataGridViewCheckBoxColumn();
            dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            tabPage3 = new TabPage();
            splitContainer4 = new SplitContainer();
            btnConnectDB = new Button();
            btnClearLint = new Button();
            dataGridView1 = new DataGridView();
            colActive = new DataGridViewCheckBoxColumn();
            colDescr = new DataGridViewTextBoxColumn();
            colConfigure = new DataGridViewButtonColumn();
            tabPageTooltips = new TabPage();
            dataGridViewTooltips = new DataGridView();
            dataGridViewCheckBoxColumnTooltips = new DataGridViewCheckBoxColumn();
            dataGridViewTextBoxColumnTooltips = new DataGridViewTextBoxColumn();
            tabPage2 = new TabPage();
            gridRefactors = new DataGridView();
            dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
            Column1 = new DataGridViewTextBoxColumn();
            dataGridViewButtonColumn1 = new DataGridViewButtonColumn();
            tabPage6 = new TabPage();
            gridExtensions = new DataGridView();
            colExtActive = new DataGridViewCheckBoxColumn();
            colExtTarget = new DataGridViewTextBoxColumn();
            colExtContents = new DataGridViewTextBoxColumn();
            colExtConfigure = new DataGridViewButtonColumn();
            tabPage5 = new TabPage();
            splitContainer3 = new SplitContainer();
            pnlTemplateParams = new Panel();
            btnApplyTemplate = new Button();
            cmbTemplates = new ComboBox();
            lnkNewVersion = new LinkLabel();
            chkUseEnhancedEditor = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            grpCodeFolding.SuspendLayout();
            grpEditorFeatures.SuspendLayout();
            groupBox3.SuspendLayout();
            grpAppearance.SuspendLayout();
            grpFeatureOverrides.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox4.SuspendLayout();
            grpApplication.SuspendLayout();
            tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).BeginInit();
            tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer4).BeginInit();
            splitContainer4.Panel1.SuspendLayout();
            splitContainer4.Panel2.SuspendLayout();
            splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tabPageTooltips.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewTooltips).BeginInit();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridRefactors).BeginInit();
            tabPage6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridExtensions).BeginInit();
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
            splitContainer1.Panel2.BackColor = SystemColors.Info;
            splitContainer1.Panel2.Controls.Add(lnkNewVersion);
            splitContainer1.Size = new Size(570, 637);
            splitContainer1.SplitterDistance = 591;
            splitContainer1.TabIndex = 0;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Controls.Add(tabPageTooltips);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage6);
            tabControl1.Controls.Add(tabPage5);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(570, 591);
            tabControl1.TabIndex = 3;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(grpCodeFolding);
            tabPage1.Controls.Add(grpEditorFeatures);
            tabPage1.Controls.Add(groupBox3);
            tabPage1.Controls.Add(grpAppearance);
            tabPage1.Controls.Add(grpFeatureOverrides);
            tabPage1.Controls.Add(groupBox2);
            tabPage1.Controls.Add(grpApplication);
            tabPage1.Controls.Add(lnkWhatsNew);
            tabPage1.Controls.Add(linkDocs);
            tabPage1.Controls.Add(btnDebugLog);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(562, 563);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Editor Tweaks";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // grpCodeFolding
            // 
            grpCodeFolding.Controls.Add(chkCodeFolding);
            grpCodeFolding.Controls.Add(chkInitCollapsed);
            grpCodeFolding.Controls.Add(chkRememberFolds);
            grpCodeFolding.Location = new Point(8, 6);
            grpCodeFolding.Name = "grpCodeFolding";
            grpCodeFolding.Size = new Size(270, 95);
            grpCodeFolding.TabIndex = 0;
            grpCodeFolding.TabStop = false;
            grpCodeFolding.Text = "Code Folding";
            // 
            // chkCodeFolding
            // 
            chkCodeFolding.AutoSize = true;
            chkCodeFolding.Location = new Point(10, 22);
            chkCodeFolding.Name = "chkCodeFolding";
            chkCodeFolding.Size = new Size(135, 19);
            chkCodeFolding.TabIndex = 0;
            chkCodeFolding.Text = "Enable Code Folding";
            chkCodeFolding.UseVisualStyleBackColor = true;
            // 
            // chkInitCollapsed
            // 
            chkInitCollapsed.AutoSize = true;
            chkInitCollapsed.Location = new Point(10, 47);
            chkInitCollapsed.Name = "chkInitCollapsed";
            chkInitCollapsed.Size = new Size(149, 19);
            chkInitCollapsed.TabIndex = 1;
            chkInitCollapsed.Text = "Auto Collapse on Open";
            chkInitCollapsed.UseVisualStyleBackColor = true;
            // 
            // chkRememberFolds
            // 
            chkRememberFolds.AutoSize = true;
            chkRememberFolds.Location = new Point(10, 72);
            chkRememberFolds.Name = "chkRememberFolds";
            chkRememberFolds.Size = new Size(144, 19);
            chkRememberFolds.TabIndex = 2;
            chkRememberFolds.Text = "Remember Fold States";
            chkRememberFolds.UseVisualStyleBackColor = true;
            // 
            // grpEditorFeatures
            // 
            grpEditorFeatures.Controls.Add(chkMultiSelection);
            grpEditorFeatures.Controls.Add(chkAutoPairing);
            grpEditorFeatures.Controls.Add(chkDocMinimap);
            grpEditorFeatures.Controls.Add(chkInlineParameterHints);
            grpEditorFeatures.Controls.Add(chkLineSelectionFix);
            grpEditorFeatures.Location = new Point(8, 107);
            grpEditorFeatures.Name = "grpEditorFeatures";
            grpEditorFeatures.Size = new Size(270, 145);
            grpEditorFeatures.TabIndex = 1;
            grpEditorFeatures.TabStop = false;
            grpEditorFeatures.Text = "Editor Features";
            // 
            // chkMultiSelection
            // 
            chkMultiSelection.AutoSize = true;
            chkMultiSelection.Location = new Point(10, 22);
            chkMultiSelection.Name = "chkMultiSelection";
            chkMultiSelection.Size = new Size(121, 19);
            chkMultiSelection.TabIndex = 0;
            chkMultiSelection.Text = "Multiple Selection";
            chkMultiSelection.UseVisualStyleBackColor = true;
            // 
            // chkAutoPairing
            // 
            chkAutoPairing.AutoSize = true;
            chkAutoPairing.Location = new Point(10, 47);
            chkAutoPairing.Name = "chkAutoPairing";
            chkAutoPairing.Size = new Size(148, 19);
            chkAutoPairing.TabIndex = 1;
            chkAutoPairing.Text = "Pair Quotes and Parens";
            chkAutoPairing.UseVisualStyleBackColor = true;
            // 
            // chkDocMinimap
            // 
            chkDocMinimap.AutoSize = true;
            chkDocMinimap.Location = new Point(10, 72);
            chkDocMinimap.Name = "chkDocMinimap";
            chkDocMinimap.Size = new Size(133, 19);
            chkDocMinimap.TabIndex = 2;
            chkDocMinimap.Text = "Document Minimap";
            chkDocMinimap.UseVisualStyleBackColor = true;
            // 
            // chkInlineParameterHints
            // 
            chkInlineParameterHints.AutoSize = true;
            chkInlineParameterHints.Location = new Point(10, 97);
            chkInlineParameterHints.Name = "chkInlineParameterHints";
            chkInlineParameterHints.Size = new Size(143, 19);
            chkInlineParameterHints.TabIndex = 3;
            chkInlineParameterHints.Text = "Inline Parameter Hints";
            chkInlineParameterHints.UseVisualStyleBackColor = true;
            // 
            // chkLineSelectionFix
            // 
            chkLineSelectionFix.AutoSize = true;
            chkLineSelectionFix.Location = new Point(10, 122);
            chkLineSelectionFix.Name = "chkLineSelectionFix";
            chkLineSelectionFix.Size = new Size(116, 19);
            chkLineSelectionFix.TabIndex = 4;
            chkLineSelectionFix.Text = "Line Selection Fix";
            chkLineSelectionFix.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(chkVariableSuggestions);
            groupBox3.Controls.Add(chkObjectMembers);
            groupBox3.Controls.Add(chkFunctionSignatures);
            groupBox3.Controls.Add(chkSystemVariables);
            groupBox3.Controls.Add(label1);
            groupBox3.Controls.Add(cmbTheme);
            groupBox3.Controls.Add(chkFilled);
            groupBox3.Location = new Point(8, 258);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(270, 147);
            groupBox3.TabIndex = 2;
            groupBox3.TabStop = false;
            groupBox3.Text = "Auto Suggest";
            // 
            // chkVariableSuggestions
            // 
            chkVariableSuggestions.AutoSize = true;
            chkVariableSuggestions.Location = new Point(10, 22);
            chkVariableSuggestions.Name = "chkVariableSuggestions";
            chkVariableSuggestions.Size = new Size(72, 19);
            chkVariableSuggestions.TabIndex = 0;
            chkVariableSuggestions.Text = "Variables";
            chkVariableSuggestions.UseVisualStyleBackColor = true;
            // 
            // chkObjectMembers
            // 
            chkObjectMembers.AutoSize = true;
            chkObjectMembers.Location = new Point(10, 47);
            chkObjectMembers.Name = "chkObjectMembers";
            chkObjectMembers.Size = new Size(131, 19);
            chkObjectMembers.TabIndex = 1;
            chkObjectMembers.Text = "Methods/Properties";
            chkObjectMembers.UseVisualStyleBackColor = true;
            // 
            // chkFunctionSignatures
            // 
            chkFunctionSignatures.AutoSize = true;
            chkFunctionSignatures.Location = new Point(10, 72);
            chkFunctionSignatures.Name = "chkFunctionSignatures";
            chkFunctionSignatures.Size = new Size(104, 19);
            chkFunctionSignatures.TabIndex = 2;
            chkFunctionSignatures.Text = "Call Signatures";
            chkFunctionSignatures.UseVisualStyleBackColor = true;
            // 
            // chkSystemVariables
            // 
            chkSystemVariables.AutoSize = true;
            chkSystemVariables.Location = new Point(10, 97);
            chkSystemVariables.Name = "chkSystemVariables";
            chkSystemVariables.Size = new Size(113, 19);
            chkSystemVariables.TabIndex = 3;
            chkSystemVariables.Text = "System Variables";
            chkSystemVariables.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(10, 122);
            label1.Name = "label1";
            label1.Size = new Size(47, 15);
            label1.TabIndex = 4;
            label1.Text = "Theme:";
            // 
            // cmbTheme
            // 
            cmbTheme.FormattingEnabled = true;
            cmbTheme.Location = new Point(60, 119);
            cmbTheme.Name = "cmbTheme";
            cmbTheme.Size = new Size(100, 23);
            cmbTheme.TabIndex = 5;
            cmbTheme.SelectedIndexChanged += ThemeSetting_Changed;
            // 
            // chkFilled
            // 
            chkFilled.AutoSize = true;
            chkFilled.Location = new Point(170, 121);
            chkFilled.Name = "chkFilled";
            chkFilled.Size = new Size(54, 19);
            chkFilled.TabIndex = 6;
            chkFilled.Text = "Filled";
            chkFilled.UseVisualStyleBackColor = true;
            chkFilled.CheckedChanged += ThemeSetting_Changed;
            // 
            // grpAppearance
            // 
            grpAppearance.Controls.Add(chkAutoDark);
            grpAppearance.Controls.Add(chkAutoCenterDialogs);
            grpAppearance.Location = new Point(284, 6);
            grpAppearance.Name = "grpAppearance";
            grpAppearance.Size = new Size(270, 72);
            grpAppearance.TabIndex = 3;
            grpAppearance.TabStop = false;
            grpAppearance.Text = "Appearance";
            // 
            // chkAutoDark
            // 
            chkAutoDark.AutoSize = true;
            chkAutoDark.Location = new Point(10, 22);
            chkAutoDark.Name = "chkAutoDark";
            chkAutoDark.Size = new Size(113, 19);
            chkAutoDark.TabIndex = 0;
            chkAutoDark.Text = "Auto Dark Mode";
            chkAutoDark.UseVisualStyleBackColor = true;
            // 
            // chkAutoCenterDialogs
            // 
            chkAutoCenterDialogs.AutoSize = true;
            chkAutoCenterDialogs.Location = new Point(10, 47);
            chkAutoCenterDialogs.Name = "chkAutoCenterDialogs";
            chkAutoCenterDialogs.Size = new Size(103, 19);
            chkAutoCenterDialogs.TabIndex = 1;
            chkAutoCenterDialogs.Text = "Center Dialogs";
            chkAutoCenterDialogs.UseVisualStyleBackColor = true;
            // 
            // grpFeatureOverrides
            // 
            grpFeatureOverrides.Controls.Add(chkUseEnhancedEditor);
            grpFeatureOverrides.Controls.Add(chkOverrideFindReplace);
            grpFeatureOverrides.Controls.Add(chkOverrideOpen);
            grpFeatureOverrides.Controls.Add(btnConfigSmartOpen);
            grpFeatureOverrides.Controls.Add(chkBetterSQL);
            grpFeatureOverrides.Location = new Point(284, 84);
            grpFeatureOverrides.Name = "grpFeatureOverrides";
            grpFeatureOverrides.Size = new Size(270, 129);
            grpFeatureOverrides.TabIndex = 4;
            grpFeatureOverrides.TabStop = false;
            grpFeatureOverrides.Text = "Feature Overrides";
            // 
            // chkOverrideFindReplace
            // 
            chkOverrideFindReplace.AutoSize = true;
            chkOverrideFindReplace.Location = new Point(10, 22);
            chkOverrideFindReplace.Name = "chkOverrideFindReplace";
            chkOverrideFindReplace.Size = new Size(143, 19);
            chkOverrideFindReplace.TabIndex = 0;
            chkOverrideFindReplace.Text = "Override Find/Replace";
            chkOverrideFindReplace.UseVisualStyleBackColor = true;
            // 
            // chkOverrideOpen
            // 
            chkOverrideOpen.AutoSize = true;
            chkOverrideOpen.Location = new Point(10, 47);
            chkOverrideOpen.Name = "chkOverrideOpen";
            chkOverrideOpen.Size = new Size(140, 19);
            chkOverrideOpen.TabIndex = 1;
            chkOverrideOpen.Text = "Override Open Dialog";
            chkOverrideOpen.UseVisualStyleBackColor = true;
            // 
            // btnConfigSmartOpen
            // 
            btnConfigSmartOpen.Location = new Point(180, 44);
            btnConfigSmartOpen.Name = "btnConfigSmartOpen";
            btnConfigSmartOpen.Size = new Size(80, 23);
            btnConfigSmartOpen.TabIndex = 2;
            btnConfigSmartOpen.Text = "Config...";
            btnConfigSmartOpen.UseVisualStyleBackColor = true;
            btnConfigSmartOpen.Click += btnConfigSmartOpen_Click;
            // 
            // chkBetterSQL
            // 
            chkBetterSQL.AutoSize = true;
            chkBetterSQL.Location = new Point(10, 75);
            chkBetterSQL.Name = "chkBetterSQL";
            chkBetterSQL.Size = new Size(88, 19);
            chkBetterSQL.TabIndex = 3;
            chkBetterSQL.Text = "Format SQL";
            chkBetterSQL.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(chkEventMapping);
            groupBox2.Controls.Add(chkEventMapXrefs);
            groupBox2.Controls.Add(groupBox4);
            groupBox2.Location = new Point(284, 219);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(270, 115);
            groupBox2.TabIndex = 5;
            groupBox2.TabStop = false;
            groupBox2.Text = "Event Mapping";
            // 
            // chkEventMapping
            // 
            chkEventMapping.AutoSize = true;
            chkEventMapping.Location = new Point(10, 22);
            chkEventMapping.Name = "chkEventMapping";
            chkEventMapping.Size = new Size(143, 19);
            chkEventMapping.TabIndex = 0;
            chkEventMapping.Text = "Detect Event Mapping";
            chkEventMapping.UseVisualStyleBackColor = true;
            // 
            // chkEventMapXrefs
            // 
            chkEventMapXrefs.AutoSize = true;
            chkEventMapXrefs.Location = new Point(10, 47);
            chkEventMapXrefs.Name = "chkEventMapXrefs";
            chkEventMapXrefs.Size = new Size(194, 19);
            chkEventMapXrefs.TabIndex = 1;
            chkEventMapXrefs.Text = "Show Event Mapped References";
            chkEventMapXrefs.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(optClassPath);
            groupBox4.Controls.Add(optClassText);
            groupBox4.Location = new Point(10, 72);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(250, 38);
            groupBox4.TabIndex = 2;
            groupBox4.TabStop = false;
            groupBox4.Text = "Show";
            // 
            // optClassPath
            // 
            optClassPath.AutoSize = true;
            optClassPath.Location = new Point(10, 15);
            optClassPath.Name = "optClassPath";
            optClassPath.Size = new Size(79, 19);
            optClassPath.TabIndex = 0;
            optClassPath.TabStop = true;
            optClassPath.Text = "Class Path";
            optClassPath.UseVisualStyleBackColor = true;
            // 
            // optClassText
            // 
            optClassText.AutoSize = true;
            optClassText.Location = new Point(100, 15);
            optClassText.Name = "optClassText";
            optClassText.Size = new Size(76, 19);
            optClassText.TabIndex = 1;
            optClassText.TabStop = true;
            optClassText.Text = "Class Text";
            optClassText.UseVisualStyleBackColor = true;
            // 
            // grpApplication
            // 
            grpApplication.Controls.Add(chkOnlyPPC);
            grpApplication.Controls.Add(chkPromptForDB);
            grpApplication.Controls.Add(btnPlugins);
            grpApplication.Controls.Add(lblLintReportDir);
            grpApplication.Controls.Add(txtLintReportDir);
            grpApplication.Controls.Add(btnBrowseLintReport);
            grpApplication.Controls.Add(lblTnsAdminDir);
            grpApplication.Controls.Add(txtTnsAdminDir);
            grpApplication.Controls.Add(btnBrowseTnsAdmin);
            grpApplication.Location = new Point(8, 411);
            grpApplication.Name = "grpApplication";
            grpApplication.Size = new Size(546, 105);
            grpApplication.TabIndex = 6;
            grpApplication.TabStop = false;
            grpApplication.Text = "Application";
            // 
            // chkOnlyPPC
            // 
            chkOnlyPPC.AutoSize = true;
            chkOnlyPPC.Location = new Point(10, 22);
            chkOnlyPPC.Name = "chkOnlyPPC";
            chkOnlyPPC.Size = new Size(200, 19);
            chkOnlyPPC.TabIndex = 0;
            chkOnlyPPC.Text = "Only Process PeopleCode Editors";
            chkOnlyPPC.UseVisualStyleBackColor = true;
            // 
            // chkPromptForDB
            // 
            chkPromptForDB.AutoSize = true;
            chkPromptForDB.Location = new Point(220, 22);
            chkPromptForDB.Name = "chkPromptForDB";
            chkPromptForDB.Size = new Size(222, 19);
            chkPromptForDB.TabIndex = 1;
            chkPromptForDB.Text = "Prompt for DB Connection on Attach";
            chkPromptForDB.UseVisualStyleBackColor = true;
            // 
            // btnPlugins
            // 
            btnPlugins.Location = new Point(456, 18);
            btnPlugins.Name = "btnPlugins";
            btnPlugins.Size = new Size(80, 23);
            btnPlugins.TabIndex = 2;
            btnPlugins.Text = "Plugins...";
            btnPlugins.UseVisualStyleBackColor = true;
            btnPlugins.Click += btnPlugins_Click;
            // 
            // lblLintReportDir
            // 
            lblLintReportDir.AutoSize = true;
            lblLintReportDir.Location = new Point(6, 52);
            lblLintReportDir.Name = "lblLintReportDir";
            lblLintReportDir.Size = new Size(119, 15);
            lblLintReportDir.TabIndex = 3;
            lblLintReportDir.Text = "Lint Report Directory:";
            // 
            // txtLintReportDir
            // 
            txtLintReportDir.Location = new Point(130, 49);
            txtLintReportDir.Name = "txtLintReportDir";
            txtLintReportDir.Size = new Size(375, 23);
            txtLintReportDir.TabIndex = 4;
            // 
            // btnBrowseLintReport
            // 
            btnBrowseLintReport.Location = new Point(511, 48);
            btnBrowseLintReport.Name = "btnBrowseLintReport";
            btnBrowseLintReport.Size = new Size(25, 23);
            btnBrowseLintReport.TabIndex = 5;
            btnBrowseLintReport.Text = "...";
            btnBrowseLintReport.UseVisualStyleBackColor = true;
            btnBrowseLintReport.Click += btnReportDirectory_Click;
            // 
            // lblTnsAdminDir
            // 
            lblTnsAdminDir.AutoSize = true;
            lblTnsAdminDir.Location = new Point(2, 79);
            lblTnsAdminDir.Name = "lblTnsAdminDir";
            lblTnsAdminDir.Size = new Size(127, 15);
            lblTnsAdminDir.TabIndex = 6;
            lblTnsAdminDir.Text = "TNS_ADMIN Directory:";
            // 
            // txtTnsAdminDir
            // 
            txtTnsAdminDir.Location = new Point(130, 76);
            txtTnsAdminDir.Name = "txtTnsAdminDir";
            txtTnsAdminDir.Size = new Size(375, 23);
            txtTnsAdminDir.TabIndex = 7;
            // 
            // btnBrowseTnsAdmin
            // 
            btnBrowseTnsAdmin.Location = new Point(511, 75);
            btnBrowseTnsAdmin.Name = "btnBrowseTnsAdmin";
            btnBrowseTnsAdmin.Size = new Size(25, 23);
            btnBrowseTnsAdmin.TabIndex = 8;
            btnBrowseTnsAdmin.Text = "...";
            btnBrowseTnsAdmin.UseVisualStyleBackColor = true;
            btnBrowseTnsAdmin.Click += btnTNSADMIN_Click;
            // 
            // lnkWhatsNew
            // 
            lnkWhatsNew.AutoSize = true;
            lnkWhatsNew.Location = new Point(14, 528);
            lnkWhatsNew.Name = "lnkWhatsNew";
            lnkWhatsNew.Size = new Size(79, 15);
            lnkWhatsNew.TabIndex = 7;
            lnkWhatsNew.TabStop = true;
            lnkWhatsNew.Text = "What's New...";
            lnkWhatsNew.LinkClicked += lnkWhatsNew_LinkClicked;
            // 
            // linkDocs
            // 
            linkDocs.AutoSize = true;
            linkDocs.Location = new Point(334, 528);
            linkDocs.Name = "linkDocs";
            linkDocs.Size = new Size(127, 15);
            linkDocs.TabIndex = 8;
            linkDocs.TabStop = true;
            linkDocs.Text = "View Documentation...";
            linkDocs.LinkClicked += linkDocs_LinkClicked;
            // 
            // btnDebugLog
            // 
            btnDebugLog.Location = new Point(467, 524);
            btnDebugLog.Name = "btnDebugLog";
            btnDebugLog.Size = new Size(85, 23);
            btnDebugLog.TabIndex = 9;
            btnDebugLog.Text = "Debug Log...";
            btnDebugLog.UseVisualStyleBackColor = true;
            btnDebugLog.Click += btnDebugLog_Click;
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(dataGridView3);
            tabPage4.Location = new Point(4, 24);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(3);
            tabPage4.Size = new Size(562, 563);
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
            dataGridView3.Size = new Size(556, 557);
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
            // tabPage3
            // 
            tabPage3.Controls.Add(splitContainer4);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(562, 563);
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
            // 
            // splitContainer4.Panel2
            // 
            splitContainer4.Panel2.Controls.Add(dataGridView1);
            splitContainer4.Size = new Size(556, 557);
            splitContainer4.SplitterDistance = 58;
            splitContainer4.TabIndex = 0;
            // 
            // btnConnectDB
            // 
            btnConnectDB.Dock = DockStyle.Right;
            btnConnectDB.Location = new Point(449, 0);
            btnConnectDB.Name = "btnConnectDB";
            btnConnectDB.Size = new Size(107, 58);
            btnConnectDB.TabIndex = 14;
            btnConnectDB.Text = "Connect DB...";
            btnConnectDB.UseVisualStyleBackColor = true;
            btnConnectDB.Click += btnConnectDB_Click;
            // 
            // btnClearLint
            // 
            btnClearLint.Dock = DockStyle.Left;
            btnClearLint.Enabled = false;
            btnClearLint.Location = new Point(0, 0);
            btnClearLint.Name = "btnClearLint";
            btnClearLint.Size = new Size(123, 58);
            btnClearLint.TabIndex = 13;
            btnClearLint.Text = "Clear Annotations";
            btnClearLint.UseVisualStyleBackColor = true;
            btnClearLint.Click += btnClearLint_Click;
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
            dataGridView1.Size = new Size(556, 495);
            dataGridView1.TabIndex = 6;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            dataGridView1.CellPainting += dataGridView1_CellPainting;
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
            colDescr.Width = 420;
            // 
            // colConfigure
            // 
            colConfigure.FillWeight = 114.213196F;
            colConfigure.HeaderText = "Configure";
            colConfigure.Name = "colConfigure";
            colConfigure.Text = "Configure...";
            colConfigure.UseColumnTextForButtonValue = true;
            colConfigure.Width = 80;
            // 
            // tabPageTooltips
            // 
            tabPageTooltips.Controls.Add(dataGridViewTooltips);
            tabPageTooltips.Location = new Point(4, 24);
            tabPageTooltips.Name = "tabPageTooltips";
            tabPageTooltips.Padding = new Padding(3);
            tabPageTooltips.Size = new Size(562, 563);
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
            dataGridViewTooltips.Size = new Size(556, 557);
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
            tabPage2.Controls.Add(gridRefactors);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(562, 563);
            tabPage2.TabIndex = 8;
            tabPage2.Text = "Refactors";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // gridRefactors
            // 
            gridRefactors.AllowUserToAddRows = false;
            gridRefactors.AllowUserToDeleteRows = false;
            gridRefactors.AllowUserToResizeColumns = false;
            gridRefactors.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridRefactors.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn2, Column1, dataGridViewButtonColumn1 });
            gridRefactors.Dock = DockStyle.Fill;
            gridRefactors.Location = new Point(3, 3);
            gridRefactors.Name = "gridRefactors";
            gridRefactors.RowHeadersVisible = false;
            gridRefactors.Size = new Size(556, 557);
            gridRefactors.TabIndex = 7;
            gridRefactors.CellContentClick += gridRefactors_CellContentClick;
            gridRefactors.CellPainting += gridRefactors_CellPainting;
            // 
            // dataGridViewTextBoxColumn2
            // 
            dataGridViewTextBoxColumn2.FillWeight = 110.569466F;
            dataGridViewTextBoxColumn2.HeaderText = "Description";
            dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            dataGridViewTextBoxColumn2.ReadOnly = true;
            dataGridViewTextBoxColumn2.Width = 372;
            // 
            // Column1
            // 
            Column1.HeaderText = "Shortcut";
            Column1.Name = "Column1";
            Column1.ReadOnly = true;
            // 
            // dataGridViewButtonColumn1
            // 
            dataGridViewButtonColumn1.FillWeight = 114.213196F;
            dataGridViewButtonColumn1.HeaderText = "Configure";
            dataGridViewButtonColumn1.Name = "dataGridViewButtonColumn1";
            dataGridViewButtonColumn1.Text = "Configure...";
            dataGridViewButtonColumn1.UseColumnTextForButtonValue = true;
            dataGridViewButtonColumn1.Width = 80;
            // 
            // tabPage6
            // 
            tabPage6.Controls.Add(gridExtensions);
            tabPage6.Location = new Point(4, 24);
            tabPage6.Name = "tabPage6";
            tabPage6.Padding = new Padding(3);
            tabPage6.Size = new Size(562, 563);
            tabPage6.TabIndex = 9;
            tabPage6.Text = "Extensions";
            tabPage6.UseVisualStyleBackColor = true;
            // 
            // gridExtensions
            // 
            gridExtensions.AllowUserToAddRows = false;
            gridExtensions.AllowUserToDeleteRows = false;
            gridExtensions.AllowUserToResizeColumns = false;
            gridExtensions.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridExtensions.Columns.AddRange(new DataGridViewColumn[] { colExtActive, colExtTarget, colExtContents, colExtConfigure });
            gridExtensions.Dock = DockStyle.Fill;
            gridExtensions.Location = new Point(3, 3);
            gridExtensions.Name = "gridExtensions";
            gridExtensions.RowHeadersVisible = false;
            gridExtensions.Size = new Size(556, 557);
            gridExtensions.TabIndex = 8;
            gridExtensions.CellContentClick += gridExtensions_CellContentClick;
            gridExtensions.CellValueChanged += gridExtensions_CellValueChanged;
            // 
            // colExtActive
            // 
            colExtActive.HeaderText = "Active";
            colExtActive.Name = "colExtActive";
            colExtActive.Width = 50;
            // 
            // colExtTarget
            // 
            colExtTarget.FillWeight = 110.569466F;
            colExtTarget.HeaderText = "Target";
            colExtTarget.Name = "colExtTarget";
            colExtTarget.ReadOnly = true;
            // 
            // colExtContents
            // 
            colExtContents.HeaderText = "Contents";
            colExtContents.Name = "colExtContents";
            colExtContents.ReadOnly = true;
            colExtContents.Width = 300;
            // 
            // colExtConfigure
            // 
            colExtConfigure.HeaderText = "Inspect";
            colExtConfigure.Name = "colExtConfigure";
            // 
            // tabPage5
            // 
            tabPage5.Controls.Add(splitContainer3);
            tabPage5.Controls.Add(cmbTemplates);
            tabPage5.Location = new Point(4, 24);
            tabPage5.Name = "tabPage5";
            tabPage5.Padding = new Padding(3);
            tabPage5.Size = new Size(562, 563);
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
            splitContainer3.Size = new Size(556, 534);
            splitContainer3.SplitterDistance = 490;
            splitContainer3.TabIndex = 1;
            // 
            // pnlTemplateParams
            // 
            pnlTemplateParams.Dock = DockStyle.Fill;
            pnlTemplateParams.Location = new Point(0, 0);
            pnlTemplateParams.Name = "pnlTemplateParams";
            pnlTemplateParams.Size = new Size(556, 490);
            pnlTemplateParams.TabIndex = 3;
            // 
            // btnApplyTemplate
            // 
            btnApplyTemplate.Dock = DockStyle.Fill;
            btnApplyTemplate.Location = new Point(0, 0);
            btnApplyTemplate.Name = "btnApplyTemplate";
            btnApplyTemplate.Size = new Size(556, 40);
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
            // lnkNewVersion
            // 
            lnkNewVersion.Dock = DockStyle.Fill;
            lnkNewVersion.Location = new Point(0, 0);
            lnkNewVersion.Name = "lnkNewVersion";
            lnkNewVersion.Size = new Size(570, 42);
            lnkNewVersion.TabIndex = 37;
            lnkNewVersion.TabStop = true;
            lnkNewVersion.Text = "A newer version is available";
            lnkNewVersion.TextAlign = ContentAlignment.MiddleCenter;
            lnkNewVersion.LinkClicked += lnkNewVersion_LinkClicked;
            // 
            // chkUseEnhancedEditor
            // 
            chkUseEnhancedEditor.AutoSize = true;
            chkUseEnhancedEditor.Location = new Point(10, 100);
            chkUseEnhancedEditor.Name = "chkUseEnhancedEditor";
            chkUseEnhancedEditor.Size = new Size(139, 19);
            chkUseEnhancedEditor.TabIndex = 4;
            chkUseEnhancedEditor.Text = "Use Enhanced Editor*";
            chkUseEnhancedEditor.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(570, 637);
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
            tabPage1.PerformLayout();
            grpCodeFolding.ResumeLayout(false);
            grpCodeFolding.PerformLayout();
            grpEditorFeatures.ResumeLayout(false);
            grpEditorFeatures.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            grpAppearance.ResumeLayout(false);
            grpAppearance.PerformLayout();
            grpFeatureOverrides.ResumeLayout(false);
            grpFeatureOverrides.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            grpApplication.ResumeLayout(false);
            grpApplication.PerformLayout();
            tabPage4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView3).EndInit();
            tabPage3.ResumeLayout(false);
            splitContainer4.Panel1.ResumeLayout(false);
            splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer4).EndInit();
            splitContainer4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tabPageTooltips.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewTooltips).EndInit();
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridRefactors).EndInit();
            tabPage6.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridExtensions).EndInit();
            tabPage5.ResumeLayout(false);
            splitContainer3.Panel1.ResumeLayout(false);
            splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer3).EndInit();
            splitContainer3.ResumeLayout(false);
            ResumeLayout(false);
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            linterManager?.HandleLinterGridCellContentClick(sender, e);
        }

        #endregion

        private SplitContainer splitContainer1;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private Button btnDebugLog;
        // New GroupBoxes for reorganized Editor Tweaks tab
        private GroupBox grpCodeFolding;
        private GroupBox grpEditorFeatures;
        private GroupBox grpAppearance;
        private GroupBox grpFeatureOverrides;
        private GroupBox grpApplication;
        // Code Folding controls
        private CheckBox chkCodeFolding;
        private CheckBox chkInitCollapsed;
        private CheckBox chkRememberFolds;
        // Editor Features controls
        private CheckBox chkMultiSelection;
        private CheckBox chkAutoPairing;
        private CheckBox chkDocMinimap;
        private CheckBox chkInlineParameterHints;
        private CheckBox chkLineSelectionFix;
        // Appearance controls
        private CheckBox chkAutoDark;
        private CheckBox chkAutoCenterDialogs;
        // Feature Overrides controls
        private CheckBox chkOverrideFindReplace;
        private CheckBox chkOverrideOpen;
        private Button btnConfigSmartOpen;
        private CheckBox chkBetterSQL;
        // Application controls
        private CheckBox chkOnlyPPC;
        private CheckBox chkPromptForDB;
        private Button btnPlugins;
        private Label lblLintReportDir;
        private TextBox txtLintReportDir;
        private Button btnBrowseLintReport;
        private Label lblTnsAdminDir;
        private TextBox txtTnsAdminDir;
        private Button btnBrowseTnsAdmin;
        // Auto Suggest group
        private GroupBox groupBox3;
        private CheckBox chkFunctionSignatures;
        private CheckBox chkVariableSuggestions;
        private CheckBox chkObjectMembers;
        private CheckBox chkSystemVariables;
        private ComboBox cmbTheme;
        private Label label1;
        private CheckBox chkFilled;
        // Event Mapping group
        private GroupBox groupBox2;
        private CheckBox chkEventMapping;
        private GroupBox groupBox4;
        private RadioButton optClassText;
        private RadioButton optClassPath;
        private CheckBox chkEventMapXrefs;
        // Footer links
        private LinkLabel linkDocs;
        private LinkLabel lnkWhatsNew;
        private LinkLabel lnkNewVersion;
        // Other tabs
        private TabPage tabPage4;
        private DataGridView dataGridView3;
        private DataGridViewCheckBoxColumn dataGridViewCheckBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private TabPage tabPage3;
        private SplitContainer splitContainer4;
        private Button btnConnectDB;
        private Button btnClearLint;
        private DataGridView dataGridView1;
        private DataGridViewCheckBoxColumn colActive;
        private DataGridViewTextBoxColumn colDescr;
        private DataGridViewButtonColumn colConfigure;
        private TabPage tabPageTooltips;
        private DataGridView dataGridViewTooltips;
        private DataGridViewCheckBoxColumn dataGridViewCheckBoxColumnTooltips;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumnTooltips;
        private TabPage tabPage5;
        private SplitContainer splitContainer3;
        private Panel pnlTemplateParams;
        private Button btnApplyTemplate;
        private ComboBox cmbTemplates;
        private TabPage tabPage2;
        private DataGridView gridRefactors;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private DataGridViewTextBoxColumn Column1;
        private DataGridViewButtonColumn dataGridViewButtonColumn1;
        private TabPage tabPage6;
        private DataGridView gridExtensions;
        private DataGridViewTextBoxColumn colExtType;
        private DataGridViewCheckBoxColumn colExtActive;
        private DataGridViewTextBoxColumn colExtTarget;
        private DataGridViewTextBoxColumn colExtContents;
        private DataGridViewButtonColumn colExtConfigure;
        private CheckBox chkUseEnhancedEditor;
    }
}