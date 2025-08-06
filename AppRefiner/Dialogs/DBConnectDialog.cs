using System;
using System.Drawing;
using System.Windows.Forms;
using AppRefiner.Database;
using System.Collections.Generic;
using System.Text.Json;
using AppRefiner.Properties;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for connecting to a database
    /// </summary>
    public class DBConnectDialog : Form
    {
        // Data manager that will be created when connection is successful
        public IDataManager? DataManager { get; private set; }

        // Class to store database connection settings
        private class DbConnectionSettings
        {
            public bool IsReadOnly { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string? EncryptedPassword { get; set; }

            public DbConnectionSettings() { }

            public DbConnectionSettings(bool isReadOnly, string username, string @namespace, string? encryptedPassword = null)
            {
                IsReadOnly = isReadOnly;
                Username = username;
                Namespace = @namespace;
                EncryptedPassword = encryptedPassword;
            }
        }

        // Dictionary to store connection settings for each database
        private static Dictionary<string, DbConnectionSettings>? savedSettings;
        
        // Track if settings were loaded successfully
        private bool settingsLoaded = false;
        
        // Cached database connection lists for smart detection
        private List<string> oracleNames = new();
        private List<string> sqlServerDsns = new();

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
        private readonly CheckBox savePasswordCheckBox;
        private readonly Button connectButton;
        private readonly Button cancelButton;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private readonly Label loadingLabel;
        private readonly ProgressBar loadingProgressBar;
        private bool isConnecting = false;
        private bool isInitialLoad = true;

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
            this.savePasswordCheckBox = new CheckBox();
            this.connectButton = new Button();
            this.cancelButton = new Button();
            this.loadingLabel = new Label();
            this.loadingProgressBar = new ProgressBar();
            this.owner = owner;

            // Load saved settings
            LoadAllSettings();

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
            this.dbTypeComboBox.Items.Add("SQL Server");
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
            this.dbNameComboBox.SelectedIndexChanged += DbNameComboBox_SelectedIndexChanged;

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

            // savePasswordCheckBox
            this.savePasswordCheckBox.Text = "Save Password";
            this.savePasswordCheckBox.Location = new Point(130, 200);
            this.savePasswordCheckBox.Size = new Size(250, 23);
            this.savePasswordCheckBox.TabIndex = 13;
            this.savePasswordCheckBox.CheckAlign = ContentAlignment.MiddleLeft;

            // connectButton
            this.connectButton.Text = "Connect";
            this.connectButton.Size = new Size(100, 30);
            this.connectButton.Location = new Point(130, 230);
            this.connectButton.TabIndex = 14;
            this.connectButton.BackColor = Color.FromArgb(0, 122, 204);
            this.connectButton.ForeColor = Color.White;
            this.connectButton.FlatStyle = FlatStyle.Flat;
            this.connectButton.FlatAppearance.BorderSize = 0;
            this.connectButton.Click += ConnectButton_Click;

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(100, 30);
            this.cancelButton.Location = new Point(280, 230);
            this.cancelButton.TabIndex = 15;
            this.cancelButton.BackColor = Color.FromArgb(100, 100, 100);
            this.cancelButton.ForeColor = Color.White;
            this.cancelButton.FlatStyle = FlatStyle.Flat;
            this.cancelButton.FlatAppearance.BorderSize = 0;
            this.cancelButton.Click += (s, e) => 
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // loadingLabel
            this.loadingLabel.Text = "Connecting...";
            this.loadingLabel.Location = new Point(130, 230);
            this.loadingLabel.Size = new Size(250, 23);
            this.loadingLabel.TabIndex = 16;
            this.loadingLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.loadingLabel.Visible = false;

            // loadingProgressBar
            this.loadingProgressBar.Location = new Point(130, 260);
            this.loadingProgressBar.Size = new Size(250, 23);
            this.loadingProgressBar.TabIndex = 17;
            this.loadingProgressBar.Style = ProgressBarStyle.Marquee;
            this.loadingProgressBar.Visible = false;

            // DBConnectDialog
            this.Text = "Connect to Database";
            this.ClientSize = new Size(400, 280);
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
            this.Controls.Add(this.savePasswordCheckBox);
            this.Controls.Add(this.connectButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.loadingLabel);
            this.Controls.Add(this.loadingProgressBar);
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

            // Load all database connections for smart detection
            LoadAllDatabaseConnections();
            
            // Load database names based on initially selected type
            string? dbType = this.dbTypeComboBox.SelectedItem?.ToString();
            if (dbType == "Oracle")
            {
                LoadOracleTnsNames();
            }
            else if (dbType == "SQL Server")
            {
                LoadSqlServerDsns();
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
                
                // Handle SQL Server-specific namespace behavior
                UpdateNamespaceForSqlServer();
                
                // Adjust positions of username and password controls
                usernameLabel.Location = new Point(20, 170);
                usernameTextBox.Location = new Point(130, 170);
                passwordLabel.Location = new Point(20, 200);
                passwordTextBox.Location = new Point(130, 200);
                savePasswordCheckBox.Location = new Point(130, 230);
                
                // Adjust positions of loading controls
                loadingLabel.Location = new Point(130, 260);
                loadingProgressBar.Location = new Point(130, 290);
                
                // Adjust positions of action buttons
                connectButton.Location = new Point(130, 320);
                cancelButton.Location = new Point(280, 320);
                
                // Adjust form height
                this.ClientSize = new Size(400, 360);
            }
            else
            {
                // Hide namespace controls
                namespaceLabel.Visible = false;
                namespaceTextBox.Visible = false;
                
                // Reset namespace to editable when not in read-only mode
                namespaceTextBox.ReadOnly = false;
                namespaceTextBox.Text = string.Empty;
                // Reset positions of username and password controls
                usernameLabel.Location = new Point(20, 140);
                usernameTextBox.Location = new Point(130, 140);
                passwordLabel.Location = new Point(20, 170);
                passwordTextBox.Location = new Point(130, 170);
                savePasswordCheckBox.Location = new Point(130, 200);
                
                // Reset positions of loading controls
                loadingLabel.Location = new Point(130, 230);
                loadingProgressBar.Location = new Point(130, 260);
                
                // Reset positions of action buttons
                connectButton.Location = new Point(130, 290);
                cancelButton.Location = new Point(280, 290);
                
                // Reset form height
                this.ClientSize = new Size(400, 330);
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
            
            // Set focus to password field if settings were loaded
            if (settingsLoaded && !string.IsNullOrEmpty(usernameTextBox.Text))
            {
                this.BeginInvoke(new Action(() => passwordTextBox.Focus()));
            }
            
            // Auto-connect if database is selected and password is saved
            if (settingsLoaded && !string.IsNullOrEmpty(passwordTextBox.Text) && 
                !string.IsNullOrEmpty(dbNameComboBox.Text) && !string.IsNullOrEmpty(usernameTextBox.Text))
            {
                // Slight delay to ensure UI is fully loaded
                this.BeginInvoke(new Action(() => {
                    // Check if namespace is required but missing
                    if (readOnlyRadioButton.Checked && string.IsNullOrEmpty(namespaceTextBox.Text))
                        return;
                        
                    ConnectButton_Click(null, EventArgs.Empty);
                }));
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
                    case "SQL Server":
                        LoadSqlServerDsns();
                        break;
                }
                
                // Update UI based on the new database type selection
                UpdateUIForConnectionType();
            }
        }

        private void LoadOracleTnsNames()
        {
            dbNameComboBox.Items.Clear();
            dbNameComboBox.Items.AddRange(oracleNames.ToArray());
            
            if (dbNameComboBox.Items.Count > 0)
            {
                dbNameComboBox.SelectedIndex = 0;
            }
        }

        private void LoadSqlServerDsns()
        {
            dbNameComboBox.Items.Clear();
            dbNameComboBox.Items.AddRange(sqlServerDsns.ToArray());
            
            if (dbNameComboBox.Items.Count > 0)
            {
                dbNameComboBox.SelectedIndex = 0;
            }
        }
        
        /// <summary>
        /// Loads all available database connections for smart detection
        /// </summary>
        private void LoadAllDatabaseConnections()
        {
            try
            {
                // Load Oracle TNS names
                oracleNames = OracleDbConnection.GetAllTnsNames();
                
                // Load SQL Server DSNs (both System and User)
                sqlServerDsns = SqlServerDbConnection.GetAvailableDsns();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading database connections: {ex.Message}");
                
                // Fallback to empty lists
                oracleNames = new List<string>();
                sqlServerDsns = new List<string>();
            }
        }

        /// <summary>
        /// Updates namespace behavior for SQL Server read-only connections
        /// </summary>
        private void UpdateNamespaceForSqlServer()
        {
            string? dbType = dbTypeComboBox.SelectedItem?.ToString();
            
            if (dbType == "SQL Server" && readOnlyRadioButton.Checked)
            {
                // For SQL Server read-only connections, set namespace to database name and make it read-only
                namespaceTextBox.ReadOnly = true;
                namespaceTextBox.Text = dbNameComboBox.Text ?? "";
                namespaceTextBox.BackColor = SystemColors.Control; // Visual indication it's read-only
            }
            else
            {
                // For Oracle or non-read-only connections, allow namespace editing
                namespaceTextBox.ReadOnly = false;
                namespaceTextBox.BackColor = SystemColors.Window; // Normal editable appearance
            }
        }

        private void DbNameComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Apply saved settings for the selected database
            string dbName = dbNameComboBox.Text;
            if (!string.IsNullOrEmpty(dbName))
            {
                settingsLoaded = ApplySettingsForDatabase(dbName);
                
                // Update namespace for SQL Server read-only connections when DB name changes
                UpdateNamespaceForSqlServer();
                
                // Auto-connect on initial load if password is saved
                if (isInitialLoad && settingsLoaded && !string.IsNullOrEmpty(passwordTextBox.Text) && 
                    !string.IsNullOrEmpty(usernameTextBox.Text))
                {
                    // Check if namespace is required but missing
                    if (readOnlyRadioButton.Checked && string.IsNullOrEmpty(namespaceTextBox.Text))
                        return;
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        // Invoke the ConnectButton_Click method on the UI thread    
                        //this.BeginInvoke(new Action(() => ConnectButton_Click(null, EventArgs.Empty)));
                        isInitialLoad = false;
                    });
                }
            }
        }

        private void SetConnectingState(bool isConnecting)
        {
            this.isConnecting = isConnecting;
            
            // Update UI controls
            dbTypeComboBox.Enabled = !isConnecting;
            dbNameComboBox.Enabled = !isConnecting;
            bootstrapRadioButton.Enabled = !isConnecting;
            readOnlyRadioButton.Enabled = !isConnecting;
            namespaceTextBox.Enabled = !isConnecting;
            usernameTextBox.Enabled = !isConnecting;
            passwordTextBox.Enabled = !isConnecting;
            savePasswordCheckBox.Enabled = !isConnecting;
            connectButton.Enabled = !isConnecting;
            cancelButton.Enabled = !isConnecting;
            
            // Show/hide loading indicator
            loadingLabel.Visible = isConnecting;
            loadingProgressBar.Visible = isConnecting;
            
            // Force UI update
            this.Update();
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (isConnecting)
                return;

            if (dbTypeComboBox.SelectedItem is string dbType && !string.IsNullOrEmpty(dbNameComboBox.Text))
            {
                string dbName = dbNameComboBox.Text;
                string username = usernameTextBox.Text;
                string password = passwordTextBox.Text;
                string @namespace = readOnlyRadioButton.Checked ? namespaceTextBox.Text : string.Empty;
                
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

                string connectionString;
                if (dbType == "Oracle")
                {
                    connectionString = $"Data Source={dbName};User Id={username};Password={password};";
                }
                else if (dbType == "SQL Server")
                {
                    connectionString = $"DSN={dbName};UID={username};PWD={password};";
                }
                else
                {
                    throw new NotSupportedException($"Database type '{dbType}' is not supported.");
                }
                string? namespaceForConnection = readOnlyRadioButton.Checked ? @namespace : null;
                
                try
                {
                    SetConnectingState(true);
                    
                    // Run connection in background to keep UI responsive
                    await Task.Run(() =>
                    {
                        switch (dbType)
                        {
                            case "Oracle":
                                DataManager = new OraclePeopleSoftDataManager(connectionString, namespaceForConnection);
                                break;
                            case "SQL Server":
                                DataManager = new SqlServerPeopleSoftDataManager(connectionString, namespaceForConnection);
                                break;
                        }

                        if (DataManager == null)
                        {
                            throw new Exception("Failed to create data manager");
                        }

                        if (!DataManager.Connect())
                        {
                            throw new Exception("Failed to connect to database");
                        }
                    });

                    // Save the connection settings
                    string? encryptedPassword = savePasswordCheckBox.Checked ? EncryptPassword(password, dbName) : null;
                    SaveSettingsForDatabase(dbName, readOnlyRadioButton.Checked, username, @namespace, encryptedPassword);
                    
                    // Close the dialog without showing a success message
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error connecting to database: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetConnectingState(false);
                }
            }
            else
            {
                MessageBox.Show("Please select a database type and name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Selects a database in the combo box by name with smart type detection
        /// </summary>
        /// <param name="dbName">The database name to select</param>
        private void SelectDatabaseByName(string dbName)
        {
            if (string.IsNullOrEmpty(dbName))
                return;

            // Smart detection: Check which database type contains this name
            bool foundInOracle = oracleNames.Any(name => name.Equals(dbName, StringComparison.OrdinalIgnoreCase));
            bool foundInSqlServer = sqlServerDsns.Any(dsn => dsn.Equals(dbName, StringComparison.OrdinalIgnoreCase));

            // Auto-select database type based on where the name was found
            if (foundInOracle && !foundInSqlServer)
            {
                // Found only in Oracle - select Oracle type
                dbTypeComboBox.SelectedIndex = 0; // Oracle
                LoadOracleTnsNames();
                
                // Select the specific database name
                for (int i = 0; i < dbNameComboBox.Items.Count; i++)
                {
                    if (dbNameComboBox.Items[i].ToString()?.Equals(dbName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        dbNameComboBox.SelectedIndex = i;
                        return;
                    }
                }
            }
            else if (foundInSqlServer && !foundInOracle)
            {
                // Found only in SQL Server - select SQL Server type
                dbTypeComboBox.SelectedIndex = 1; // SQL Server
                LoadSqlServerDsns();
                
                // Select the specific database name
                for (int i = 0; i < dbNameComboBox.Items.Count; i++)
                {
                    if (dbNameComboBox.Items[i].ToString()?.Equals(dbName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        dbNameComboBox.SelectedIndex = i;
                        return;
                    }
                }
            }
            else if (foundInOracle && foundInSqlServer)
            {
                // Found in both - prefer Oracle (existing behavior)
                dbTypeComboBox.SelectedIndex = 0; // Oracle
                LoadOracleTnsNames();
                
                // Select the specific database name
                for (int i = 0; i < dbNameComboBox.Items.Count; i++)
                {
                    if (dbNameComboBox.Items[i].ToString()?.Equals(dbName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        dbNameComboBox.SelectedIndex = i;
                        return;
                    }
                }
            }
            else
            {
                // Not found in either - use fallback behavior (existing logic)
                // Search current combo box contents first
                for (int i = 0; i < dbNameComboBox.Items.Count; i++)
                {
                    if (dbNameComboBox.Items[i].ToString()?.Contains(dbName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        dbNameComboBox.SelectedIndex = i;
                        return;
                    }
                }
                
                // If not found, add it to the current combo box
                if (!dbNameComboBox.Items.Contains(dbName))
                {
                    dbNameComboBox.Items.Add(dbName);
                    dbNameComboBox.SelectedItem = dbName;
                }
            }
        }

        #region Settings Management

        /// <summary>
        /// Loads all saved database connection settings
        /// </summary>
        private void LoadAllSettings()
        {
            if (savedSettings != null)
                return;

            savedSettings = new Dictionary<string, DbConnectionSettings>();
            
            string settingsJson = Settings.Default.DbConnectionSettings;
            if (!string.IsNullOrEmpty(settingsJson))
            {
                try
                {
                    savedSettings = JsonSerializer.Deserialize<Dictionary<string, DbConnectionSettings>>(settingsJson);
                }
                catch
                {
                    // If settings are corrupt, use empty dictionary
                    savedSettings = new Dictionary<string, DbConnectionSettings>();
                }
            }
        }

        /// <summary>
        /// Saves all database connection settings
        /// </summary>
        private void SaveAllSettings()
        {
            if (savedSettings == null)
                return;

            string settingsJson = JsonSerializer.Serialize(savedSettings);
            Settings.Default.DbConnectionSettings = settingsJson;
            Settings.Default.Save();
        }

        /// <summary>
        /// Saves connection settings for a specific database
        /// </summary>
        private void SaveSettingsForDatabase(string dbName, bool isReadOnly, string username, string @namespace, string? encryptedPassword = null)
        {
            if (savedSettings == null)
                savedSettings = new Dictionary<string, DbConnectionSettings>();

            savedSettings[dbName] = new DbConnectionSettings(isReadOnly, username, @namespace, encryptedPassword);
            SaveAllSettings();
        }

        /// <summary>
        /// Applies saved settings for a specific database
        /// </summary>
        /// <returns>True if settings were successfully applied, false otherwise</returns>
        private bool ApplySettingsForDatabase(string dbName)
        {
            if (savedSettings == null || !savedSettings.TryGetValue(dbName, out var settings))
                return false;

            // Apply connection type
            readOnlyRadioButton.Checked = settings.IsReadOnly;
            bootstrapRadioButton.Checked = !settings.IsReadOnly;
            
            // Set username
            usernameTextBox.Text = settings.Username;
            
            // Set namespace
            namespaceTextBox.Text = settings.Namespace;
            
            // Set password if it was saved
            if (!string.IsNullOrEmpty(settings.EncryptedPassword))
            {
                try
                {
                    passwordTextBox.Text = DecryptPassword(settings.EncryptedPassword, dbName);
                    savePasswordCheckBox.Checked = true;
                }
                catch
                {
                    // If decryption fails, clear the password field
                    passwordTextBox.Text = string.Empty;
                    savePasswordCheckBox.Checked = false;
                }
            }
            else
            {
                passwordTextBox.Text = string.Empty;
                savePasswordCheckBox.Checked = false;
            }
            
            // Update UI based on connection type
            UpdateUIForConnectionType();
            
            return true;
        }

        #endregion

        #region Password Encryption

        /// <summary>
        /// Encrypts a password using Windows Data Protection API with database name as entropy
        /// </summary>
        /// <param name="password">The password to encrypt</param>
        /// <param name="dbName">Database name to use as entropy</param>
        /// <returns>Base64 encoded encrypted password</returns>
        private string EncryptPassword(string password, string dbName)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            try
            {
                // Convert the password to bytes
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                
                // Use database name as entropy
                byte[] entropyBytes = Encoding.UTF8.GetBytes(dbName);
                
                // Encrypt the password using DPAPI (Windows Data Protection API)
                byte[] encryptedBytes = ProtectedData.Protect(
                    passwordBytes, 
                    entropyBytes, // Use DB name as entropy
                    DataProtectionScope.CurrentUser); // Scope: only current Windows user can decrypt
                
                // Convert to Base64 for storage
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to encrypt password", ex);
            }
        }

        /// <summary>
        /// Decrypts a password using Windows Data Protection API with database name as entropy
        /// </summary>
        /// <param name="encryptedPassword">Base64 encoded encrypted password</param>
        /// <param name="dbName">Database name used as entropy during encryption</param>
        /// <returns>The decrypted password</returns>
        private string DecryptPassword(string encryptedPassword, string dbName)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return string.Empty;

            try
            {
                // Convert from Base64
                byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
                
                // Use same database name as entropy
                byte[] entropyBytes = Encoding.UTF8.GetBytes(dbName);
                
                // Decrypt the password using DPAPI
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    entropyBytes, // Use same database name as entropy
                    DataProtectionScope.CurrentUser); // Same scope used for encryption
                
                // Convert back to string
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to decrypt password", ex);
            }
        }

        #endregion
    }
}
