using System;
using System.Drawing;
using System.Windows.Forms;
using AppRefiner.Database;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for connecting to a database
    /// </summary>
    public class DBConnectDialog : Form
    {
        // Data manager that will be created when connection is successful
        public IDataManager? DataManager { get; private set; }

        // UI Controls
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly Label dbTypeLabel;
        private readonly ComboBox dbTypeComboBox;
        private readonly Label dbNameLabel;
        private readonly ComboBox dbNameComboBox;
        private readonly RadioButton bootstrapRadioButton;
        private readonly RadioButton readOnlyRadioButton;
        private readonly Label namespaceLabel;
        private readonly TextBox namespaceTextBox;
        private readonly Label usernameLabel;
        private readonly TextBox usernameTextBox;
        private readonly Label passwordLabel;
        private readonly TextBox passwordTextBox;
        private readonly Button connectButton;
        private readonly Button cancelButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        /// <summary>
        /// Initializes a new instance of the DBConnectDialog class
        /// </summary>
        /// <param name="owner">The owner window handle</param>
        /// <param name="defaultDbName">Optional default database name to select</param>
        public DBConnectDialog(IntPtr owner = default, string? defaultDbName = null)
        {
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.dbTypeLabel = new Label();
            this.dbTypeComboBox = new ComboBox();
            this.dbNameLabel = new Label();
            this.dbNameComboBox = new ComboBox();
            this.bootstrapRadioButton = new RadioButton();
            this.readOnlyRadioButton = new RadioButton();
            this.namespaceLabel = new Label();
            this.namespaceTextBox = new TextBox();
            this.usernameLabel = new Label();
            this.usernameTextBox = new TextBox();
            this.passwordLabel = new Label();
            this.passwordTextBox = new TextBox();
            this.connectButton = new Button();
            this.cancelButton = new Button();
            this.owner = owner;

            InitializeComponent();
            
            // Set default DB name if provided
            if (!string.IsNullOrEmpty(defaultDbName))
            {
                SelectDatabaseByName(defaultDbName);
            }
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
            this.headerLabel.Text = "Database Connection";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TabIndex = 0;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // dbTypeLabel
            this.dbTypeLabel.Text = "Database Type:";
            this.dbTypeLabel.Location = new Point(20, 50);
            this.dbTypeLabel.Size = new Size(100, 23);
            this.dbTypeLabel.TabIndex = 1;
            this.dbTypeLabel.TextAlign = ContentAlignment.MiddleLeft;

            // dbTypeComboBox
            this.dbTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.dbTypeComboBox.Location = new Point(130, 50);
            this.dbTypeComboBox.Size = new Size(250, 23);
            this.dbTypeComboBox.TabIndex = 2;
            this.dbTypeComboBox.Items.Add("Oracle");
            this.dbTypeComboBox.SelectedIndex = 0;
            this.dbTypeComboBox.SelectedIndexChanged += DbTypeComboBox_SelectedIndexChanged;

            // dbNameLabel
            this.dbNameLabel.Text = "Database Name:";
            this.dbNameLabel.Location = new Point(20, 80);
            this.dbNameLabel.Size = new Size(100, 23);
            this.dbNameLabel.TabIndex = 3;
            this.dbNameLabel.TextAlign = ContentAlignment.MiddleLeft;

            // dbNameComboBox
            this.dbNameComboBox.Location = new Point(130, 80);
            this.dbNameComboBox.Size = new Size(250, 23);
            this.dbNameComboBox.TabIndex = 4;

            // Radio buttons for connection type
            this.bootstrapRadioButton.Text = "Bootstrap";
            this.bootstrapRadioButton.Location = new Point(130, 110);
            this.bootstrapRadioButton.Size = new Size(110, 23);
            this.bootstrapRadioButton.TabIndex = 5;
            this.bootstrapRadioButton.Checked = true;
            this.bootstrapRadioButton.CheckedChanged += ConnectionTypeRadioButton_CheckedChanged;

            this.readOnlyRadioButton.Text = "Read Only User";
            this.readOnlyRadioButton.Location = new Point(250, 110);
            this.readOnlyRadioButton.Size = new Size(130, 23);
            this.readOnlyRadioButton.TabIndex = 6;
            this.readOnlyRadioButton.CheckedChanged += ConnectionTypeRadioButton_CheckedChanged;

            // namespaceLabel
            this.namespaceLabel.Text = "Namespace:";
            this.namespaceLabel.Location = new Point(20, 140);
            this.namespaceLabel.Size = new Size(100, 23);
            this.namespaceLabel.TabIndex = 7;
            this.namespaceLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.namespaceLabel.Visible = false;

            // namespaceTextBox
            this.namespaceTextBox.Location = new Point(130, 140);
            this.namespaceTextBox.Size = new Size(250, 23);
            this.namespaceTextBox.TabIndex = 8;
            this.namespaceTextBox.Visible = false;

            // usernameLabel
            this.usernameLabel.Text = "Username:";
            this.usernameLabel.Location = new Point(20, 140);
            this.usernameLabel.Size = new Size(100, 23);
            this.usernameLabel.TabIndex = 9;
            this.usernameLabel.TextAlign = ContentAlignment.MiddleLeft;

            // usernameTextBox
            this.usernameTextBox.Location = new Point(130, 140);
            this.usernameTextBox.Size = new Size(250, 23);
            this.usernameTextBox.TabIndex = 10;

            // passwordLabel
            this.passwordLabel.Text = "Password:";
            this.passwordLabel.Location = new Point(20, 170);
            this.passwordLabel.Size = new Size(100, 23);
            this.passwordLabel.TabIndex = 11;
            this.passwordLabel.TextAlign = ContentAlignment.MiddleLeft;

            // passwordTextBox
            this.passwordTextBox.Location = new Point(130, 170);
            this.passwordTextBox.Size = new Size(250, 23);
            this.passwordTextBox.TabIndex = 12;
            this.passwordTextBox.PasswordChar = '*';

            // connectButton
            this.connectButton.Text = "Connect";
            this.connectButton.Size = new Size(100, 30);
            this.connectButton.Location = new Point(130, 210);
            this.connectButton.TabIndex = 13;
            this.connectButton.BackColor = Color.FromArgb(0, 122, 204);
            this.connectButton.ForeColor = Color.White;
            this.connectButton.FlatStyle = FlatStyle.Flat;
            this.connectButton.FlatAppearance.BorderSize = 0;
            this.connectButton.Click += ConnectButton_Click;

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(100, 30);
            this.cancelButton.Location = new Point(280, 210);
            this.cancelButton.TabIndex = 14;
            this.cancelButton.BackColor = Color.FromArgb(100, 100, 100);
            this.cancelButton.ForeColor = Color.White;
            this.cancelButton.FlatStyle = FlatStyle.Flat;
            this.cancelButton.FlatAppearance.BorderSize = 0;
            this.cancelButton.Click += (s, e) => 
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // DBConnectDialog
            this.Text = "Connect to Database";
            this.ClientSize = new Size(400, 260);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.dbTypeLabel);
            this.Controls.Add(this.dbTypeComboBox);
            this.Controls.Add(this.dbNameLabel);
            this.Controls.Add(this.dbNameComboBox);
            this.Controls.Add(this.bootstrapRadioButton);
            this.Controls.Add(this.readOnlyRadioButton);
            this.Controls.Add(this.namespaceLabel);
            this.Controls.Add(this.namespaceTextBox);
            this.Controls.Add(this.usernameLabel);
            this.Controls.Add(this.usernameTextBox);
            this.Controls.Add(this.passwordLabel);
            this.Controls.Add(this.passwordTextBox);
            this.Controls.Add(this.connectButton);
            this.Controls.Add(this.cancelButton);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.AcceptButton = this.connectButton;
            this.CancelButton = this.cancelButton;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

            // Load TNS names for Oracle
            if (this.dbTypeComboBox.SelectedItem?.ToString() == "Oracle")
            {
                LoadOracleTnsNames();
            }
            
            // Update UI based on initial radio button selection
            UpdateUIForConnectionType();
        }

        private void ConnectionTypeRadioButton_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateUIForConnectionType();
        }

        private void UpdateUIForConnectionType()
        {
            if (readOnlyRadioButton.Checked)
            {
                // Show namespace controls
                namespaceLabel.Visible = true;
                namespaceTextBox.Visible = true;
                
                // Adjust positions of username and password controls
                usernameLabel.Location = new Point(20, 170);
                usernameTextBox.Location = new Point(130, 170);
                passwordLabel.Location = new Point(20, 200);
                passwordTextBox.Location = new Point(130, 200);
                
                // Adjust positions of action buttons
                connectButton.Location = new Point(130, 240);
                cancelButton.Location = new Point(280, 240);
                
                // Adjust form height
                this.ClientSize = new Size(400, 290);
            }
            else
            {
                // Hide namespace controls
                namespaceLabel.Visible = false;
                namespaceTextBox.Visible = false;
                
                // Reset positions of username and password controls
                usernameLabel.Location = new Point(20, 140);
                usernameTextBox.Location = new Point(130, 140);
                passwordLabel.Location = new Point(20, 170);
                passwordTextBox.Location = new Point(130, 170);
                
                // Reset positions of action buttons
                connectButton.Location = new Point(130, 210);
                cancelButton.Location = new Point(280, 210);
                
                // Reset form height
                this.ClientSize = new Size(400, 260);
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

        private void DbTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (dbTypeComboBox.SelectedItem is string dbType)
            {
                switch (dbType)
                {
                    case "Oracle":
                        LoadOracleTnsNames();
                        break;
                }
            }
        }

        private void LoadOracleTnsNames()
        {
            var tnsNames = OracleDbConnection.GetAllTnsNames();
            dbNameComboBox.Items.Clear();
            dbNameComboBox.Items.AddRange(tnsNames.ToArray());
            
            if (dbNameComboBox.Items.Count > 0)
            {
                dbNameComboBox.SelectedIndex = 0;
            }
        }

        private void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (dbTypeComboBox.SelectedItem is string dbType && !string.IsNullOrEmpty(dbNameComboBox.Text))
            {
                string dbName = dbNameComboBox.Text;
                string username = usernameTextBox.Text;
                string password = passwordTextBox.Text;
                string? @namespace = readOnlyRadioButton.Checked ? namespaceTextBox.Text : null;
                
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter username and password", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (readOnlyRadioButton.Checked && string.IsNullOrEmpty(@namespace))
                {
                    MessageBox.Show("Please enter a namespace for Read Only User", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var connectionString = $"Data Source={dbName};User Id={username};Password={password};";
                
                try
                {
                    switch (dbType)
                    {
                        case "Oracle":
                            DataManager = new OraclePeopleSoftDataManager(connectionString, @namespace);
                            break;
                    }

                    if (DataManager == null)
                    {
                        MessageBox.Show("Failed to create data manager", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (DataManager.Connect())
                    {
                        MessageBox.Show("Connected to database", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("Failed to connect to database", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error connecting to database: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a database type and name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Selects a database in the combo box by name
        /// </summary>
        /// <param name="dbName">The database name to select</param>
        private void SelectDatabaseByName(string dbName)
        {
            // Find the database in the combo box
            for (int i = 0; i < dbNameComboBox.Items.Count; i++)
            {
                if (dbNameComboBox.Items[i].ToString()?.Contains(dbName) == true)
                {
                    dbNameComboBox.SelectedIndex = i;
                    return;
                }
            }
            
            // If not found, add it to the combo box
            if (!string.IsNullOrEmpty(dbName) && !dbNameComboBox.Items.Contains(dbName))
            {
                dbNameComboBox.Items.Add(dbName);
                dbNameComboBox.SelectedItem = dbName;
            }
        }
    }
}
