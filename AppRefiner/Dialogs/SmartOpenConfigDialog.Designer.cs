namespace AppRefiner.Dialogs
{
    partial class SmartOpenConfigDialog
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
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.contentPanel = new Panel();
            this.definitionTypesGroupBox = new GroupBox();
            this.definitionTypesPanel = new Panel();
            this.selectAllButton = new Button();
            this.selectNoneButton = new Button();
            this.settingsGroupBox = new GroupBox();
            this.maxResultsLabel = new Label();
            this.maxResultsNumericUpDown = new NumericUpDown();
            this.sortByLastUpdateCheckBox = new CheckBox();
            this.buttonPanel = new Panel();
            this.okButton = new Button();
            this.cancelButton = new Button();
            this.headerPanel.SuspendLayout();
            this.contentPanel.SuspendLayout();
            this.definitionTypesGroupBox.SuspendLayout();
            this.settingsGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxResultsNumericUpDown)).BeginInit();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // headerPanel
            // 
            this.headerPanel.BackColor = Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(60)))));
            this.headerPanel.Controls.Add(this.headerLabel);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Location = new Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new Size(600, 35);
            this.headerPanel.TabIndex = 0;
            // 
            // headerLabel
            // 
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Location = new Point(0, 0);
            this.headerLabel.Name = "headerLabel";
            this.headerLabel.Size = new Size(600, 35);
            this.headerLabel.TabIndex = 0;
            this.headerLabel.Text = "AppRefiner - Smart Open Configuration";
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // contentPanel
            // 
            this.contentPanel.BackColor = Color.White;
            this.contentPanel.Controls.Add(this.definitionTypesGroupBox);
            this.contentPanel.Controls.Add(this.settingsGroupBox);
            this.contentPanel.Dock = DockStyle.Fill;
            this.contentPanel.Location = new Point(0, 35);
            this.contentPanel.Name = "contentPanel";
            this.contentPanel.Padding = new Padding(12);
            this.contentPanel.Size = new Size(600, 415);
            this.contentPanel.TabIndex = 1;
            // 
            // definitionTypesGroupBox
            // 
            this.definitionTypesGroupBox.Anchor = ((AnchorStyles)((((AnchorStyles.Top | AnchorStyles.Bottom) 
            | AnchorStyles.Left) 
            | AnchorStyles.Right)));
            this.definitionTypesGroupBox.Controls.Add(this.definitionTypesPanel);
            this.definitionTypesGroupBox.Controls.Add(this.selectAllButton);
            this.definitionTypesGroupBox.Controls.Add(this.selectNoneButton);
            this.definitionTypesGroupBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.definitionTypesGroupBox.Location = new Point(15, 15);
            this.definitionTypesGroupBox.Name = "definitionTypesGroupBox";
            this.definitionTypesGroupBox.Size = new Size(570, 280);
            this.definitionTypesGroupBox.TabIndex = 0;
            this.definitionTypesGroupBox.TabStop = false;
            this.definitionTypesGroupBox.Text = "Definition Types to Include in Search";
            // 
            // definitionTypesPanel
            // 
            this.definitionTypesPanel.Anchor = ((AnchorStyles)((((AnchorStyles.Top | AnchorStyles.Bottom) 
            | AnchorStyles.Left) 
            | AnchorStyles.Right)));
            this.definitionTypesPanel.AutoScroll = true;
            this.definitionTypesPanel.BorderStyle = BorderStyle.FixedSingle;
            this.definitionTypesPanel.Location = new Point(6, 22);
            this.definitionTypesPanel.Name = "definitionTypesPanel";
            this.definitionTypesPanel.Size = new Size(558, 220);
            this.definitionTypesPanel.TabIndex = 0;
            // 
            // selectAllButton
            // 
            this.selectAllButton.Anchor = ((AnchorStyles)((AnchorStyles.Bottom | AnchorStyles.Left)));
            this.selectAllButton.Location = new Point(6, 248);
            this.selectAllButton.Name = "selectAllButton";
            this.selectAllButton.Size = new Size(80, 26);
            this.selectAllButton.TabIndex = 1;
            this.selectAllButton.Text = "Select All";
            this.selectAllButton.UseVisualStyleBackColor = true;
            this.selectAllButton.Click += new EventHandler(this.SelectAllButton_Click);
            // 
            // selectNoneButton
            // 
            this.selectNoneButton.Anchor = ((AnchorStyles)((AnchorStyles.Bottom | AnchorStyles.Left)));
            this.selectNoneButton.Location = new Point(92, 248);
            this.selectNoneButton.Name = "selectNoneButton";
            this.selectNoneButton.Size = new Size(80, 26);
            this.selectNoneButton.TabIndex = 2;
            this.selectNoneButton.Text = "Select None";
            this.selectNoneButton.UseVisualStyleBackColor = true;
            this.selectNoneButton.Click += new EventHandler(this.SelectNoneButton_Click);
            // 
            // settingsGroupBox
            // 
            this.settingsGroupBox.Anchor = ((AnchorStyles)(((AnchorStyles.Bottom | AnchorStyles.Left) 
            | AnchorStyles.Right)));
            this.settingsGroupBox.Controls.Add(this.maxResultsLabel);
            this.settingsGroupBox.Controls.Add(this.maxResultsNumericUpDown);
            this.settingsGroupBox.Controls.Add(this.sortByLastUpdateCheckBox);
            this.settingsGroupBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.settingsGroupBox.Location = new Point(15, 301);
            this.settingsGroupBox.Name = "settingsGroupBox";
            this.settingsGroupBox.Size = new Size(570, 80);
            this.settingsGroupBox.TabIndex = 1;
            this.settingsGroupBox.TabStop = false;
            this.settingsGroupBox.Text = "Search Settings";
            // 
            // maxResultsLabel
            // 
            this.maxResultsLabel.AutoSize = true;
            this.maxResultsLabel.Location = new Point(6, 25);
            this.maxResultsLabel.Name = "maxResultsLabel";
            this.maxResultsLabel.Size = new Size(122, 15);
            this.maxResultsLabel.TabIndex = 0;
            this.maxResultsLabel.Text = "Max Results Per Type:";
            // 
            // maxResultsNumericUpDown
            // 
            this.maxResultsNumericUpDown.Location = new Point(134, 23);
            this.maxResultsNumericUpDown.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.maxResultsNumericUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.maxResultsNumericUpDown.Name = "maxResultsNumericUpDown";
            this.maxResultsNumericUpDown.Size = new Size(80, 23);
            this.maxResultsNumericUpDown.TabIndex = 1;
            this.maxResultsNumericUpDown.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // sortByLastUpdateCheckBox
            // 
            this.sortByLastUpdateCheckBox.AutoSize = true;
            this.sortByLastUpdateCheckBox.Location = new Point(6, 52);
            this.sortByLastUpdateCheckBox.Name = "sortByLastUpdateCheckBox";
            this.sortByLastUpdateCheckBox.Size = new Size(125, 19);
            this.sortByLastUpdateCheckBox.TabIndex = 2;
            this.sortByLastUpdateCheckBox.Text = "Sort by Last Update";
            this.sortByLastUpdateCheckBox.UseVisualStyleBackColor = true;
            // 
            // buttonPanel
            // 
            this.buttonPanel.BackColor = Color.White;
            this.buttonPanel.Controls.Add(this.okButton);
            this.buttonPanel.Controls.Add(this.cancelButton);
            this.buttonPanel.Dock = DockStyle.Bottom;
            this.buttonPanel.Location = new Point(0, 450);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.Size = new Size(600, 50);
            this.buttonPanel.TabIndex = 2;
            // 
            // okButton
            // 
            this.okButton.Anchor = ((AnchorStyles)((AnchorStyles.Top | AnchorStyles.Right)));
            this.okButton.DialogResult = DialogResult.OK;
            this.okButton.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.okButton.Location = new Point(431, 12);
            this.okButton.Name = "okButton";
            this.okButton.Size = new Size(75, 26);
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new EventHandler(this.OkButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((AnchorStyles)((AnchorStyles.Top | AnchorStyles.Right)));
            this.cancelButton.DialogResult = DialogResult.Cancel;
            this.cancelButton.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.cancelButton.Location = new Point(512, 12);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new Size(75, 26);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // SmartOpenConfigDialog
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new Size(600, 500);
            this.Controls.Add(this.contentPanel);
            this.Controls.Add(this.buttonPanel);
            this.Controls.Add(this.headerPanel);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SmartOpenConfigDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Smart Open Configuration";
            this.headerPanel.ResumeLayout(false);
            this.contentPanel.ResumeLayout(false);
            this.definitionTypesGroupBox.ResumeLayout(false);
            this.settingsGroupBox.ResumeLayout(false);
            this.settingsGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxResultsNumericUpDown)).EndInit();
            this.buttonPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private Panel headerPanel;
        private Label headerLabel;
        private Panel contentPanel;
        private GroupBox definitionTypesGroupBox;
        private Panel definitionTypesPanel;
        private Button selectAllButton;
        private Button selectNoneButton;
        private GroupBox settingsGroupBox;
        private Label maxResultsLabel;
        private NumericUpDown maxResultsNumericUpDown;
        private CheckBox sortByLastUpdateCheckBox;
        private Panel buttonPanel;
        private Button okButton;
        private Button cancelButton;
    }
}