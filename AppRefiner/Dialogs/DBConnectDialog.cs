using AppRefiner.Database;
using AppRefiner.Properties;
using System.DirectoryServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PeopleCodeParser.SelfHosted;

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
            public bool IsLdap { get; set; }
            public string LdapServer { get; set; } = string.Empty;
            public string Context { get; set; } = string.Empty;
            public string DbService { get; set; } = string.Empty;
            public string LdapDescriptor { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string? EncryptedPassword { get; set; }

            public DbConnectionSettings() { }

            public DbConnectionSettings(bool isReadOnly, bool isLdap, string ldapServer, string context, string dbService, string ldapDescriptor, string username, string @namespace, string? encryptedPassword = null)
            {
                IsReadOnly = isReadOnly;
                IsLdap = isLdap;
                LdapServer = ldapServer;
                Context = context;
                DbService = dbService;
                LdapDescriptor = ldapDescriptor;
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
        private readonly Dictionary<string, string> ldapServiceDescriptors = new(StringComparer.OrdinalIgnoreCase);

        // UI Controls
        // UI Controls
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly Label dbTypeLabel;
        private readonly ComboBox dbTypeComboBox;
        private readonly Label dbNameLabel;
        private readonly ComboBox dbNameComboBox;
        private readonly Label dbNameHintLabel;
        private readonly RadioButton bootstrapRadioButton;
        private readonly RadioButton readOnlyRadioButton;
        private readonly Label connectionTypeLabel;
        private readonly ComboBox connectionTypeComboBox;
        private readonly Label ldapServerLabel;
        private readonly TextBox ldapServerTextBox;
        private readonly Label contextLabel;
        private readonly TextBox contextTextBox;
        private readonly Label dbServiceLabel;
        private readonly ComboBox dbServiceComboBox;
        private readonly Button loadButton;
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
            this.dbNameHintLabel = new Label();
            this.bootstrapRadioButton = new RadioButton();
            this.readOnlyRadioButton = new RadioButton();
            this.connectionTypeLabel = new Label();
            this.connectionTypeComboBox = new ComboBox();
            this.ldapServerLabel = new Label();
            this.ldapServerTextBox = new TextBox();
            this.contextLabel = new Label();
            this.contextTextBox = new TextBox();
            this.dbServiceLabel = new Label();
            this.dbServiceComboBox = new ComboBox();
            this.loadButton = new Button();
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
            this.dbNameComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            this.dbNameComboBox.Location = new Point(130, 80);
            this.dbNameComboBox.Size = new Size(250, 23);
            this.dbNameComboBox.TabIndex = 4;
            this.dbNameComboBox.SelectedIndexChanged += DbNameComboBox_SelectedIndexChanged;

            // dbNameHintLabel
            this.dbNameHintLabel.Text = "For LDAP, set TNS_ADMIN and enter an LDAP service name or full connect descriptor.";
            this.dbNameHintLabel.Location = new Point(130, 105);
            this.dbNameHintLabel.Size = new Size(250, 20);
            this.dbNameHintLabel.TabIndex = 5;
            this.dbNameHintLabel.ForeColor = Color.Gray;
            this.dbNameHintLabel.Font = new Font("Segoe UI", 8F, FontStyle.Italic, GraphicsUnit.Point);
            this.dbNameHintLabel.Visible = false;

            // bootstrapRadioButton
            this.bootstrapRadioButton.Text = "Bootstrap";
            this.bootstrapRadioButton.Location = new Point(138, 128);
            this.bootstrapRadioButton.Size = new Size(86, 23);
            this.bootstrapRadioButton.TabIndex = 6;
            this.bootstrapRadioButton.Checked = true;
            this.bootstrapRadioButton.CheckedChanged += ConnectionTypeRadioButton_CheckedChanged;

            // readOnlyRadioButton
            this.readOnlyRadioButton.Text = "Read Only";
            this.readOnlyRadioButton.Location = new Point(228, 128);
            this.readOnlyRadioButton.Size = new Size(92, 23);
            this.readOnlyRadioButton.TabIndex = 7;
            this.readOnlyRadioButton.CheckedChanged += ConnectionTypeRadioButton_CheckedChanged;

            // connectionTypeLabel
            this.connectionTypeLabel.Text = "Connection Type:";
            this.connectionTypeLabel.Location = new Point(20, 160);
            this.connectionTypeLabel.Size = new Size(100, 23);
            this.connectionTypeLabel.TabIndex = 8;
            this.connectionTypeLabel.TextAlign = ContentAlignment.MiddleLeft;

            // connectionTypeComboBox
            this.connectionTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.connectionTypeComboBox.Location = new Point(130, 160);
            this.connectionTypeComboBox.Size = new Size(250, 23);
            this.connectionTypeComboBox.TabIndex = 9;
            this.connectionTypeComboBox.Items.Add("Standard");
            this.connectionTypeComboBox.Items.Add("LDAP");
            this.connectionTypeComboBox.SelectedIndex = 0;
            this.connectionTypeComboBox.SelectedIndexChanged += ConnectionTypeComboBox_SelectedIndexChanged;

            // ldapServerLabel
            this.ldapServerLabel.Text = "LDAP Server:";
            this.ldapServerLabel.Location = new Point(20, 190);
            this.ldapServerLabel.Size = new Size(100, 23);
            this.ldapServerLabel.TabIndex = 10;
            this.ldapServerLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.ldapServerLabel.Visible = false;

            // ldapServerTextBox
            this.ldapServerTextBox.Location = new Point(130, 190);
            this.ldapServerTextBox.Size = new Size(250, 23);
            this.ldapServerTextBox.TabIndex = 11;
            this.ldapServerTextBox.Visible = false;

            // contextLabel
            this.contextLabel.Text = "Context:";
            this.contextLabel.Location = new Point(20, 220);
            this.contextLabel.Size = new Size(100, 23);
            this.contextLabel.TabIndex = 12;
            this.contextLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.contextLabel.Visible = false;

            // contextTextBox
            this.contextTextBox.Location = new Point(130, 220);
            this.contextTextBox.Size = new Size(250, 23);
            this.contextTextBox.TabIndex = 13;
            this.contextTextBox.Visible = false;

            // dbServiceLabel
            this.dbServiceLabel.Text = "DB Service:";
            this.dbServiceLabel.Location = new Point(20, 250);
            this.dbServiceLabel.Size = new Size(100, 23);
            this.dbServiceLabel.TabIndex = 14;
            this.dbServiceLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.dbServiceLabel.Visible = false;

            // dbServiceComboBox
            this.dbServiceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.dbServiceComboBox.Location = new Point(130, 250);
            this.dbServiceComboBox.Size = new Size(170, 23);
            this.dbServiceComboBox.TabIndex = 15;
            this.dbServiceComboBox.Visible = false;
            this.dbServiceComboBox.SelectedIndexChanged += DbServiceComboBox_SelectedIndexChanged;

            // loadButton
            this.loadButton.Text = "Load Services";
            this.loadButton.Location = new Point(305, 250);
            this.loadButton.Size = new Size(75, 23);
            this.loadButton.TabIndex = 16;
            this.loadButton.BackColor = Color.FromArgb(0, 122, 204);
            this.loadButton.ForeColor = Color.White;
            this.loadButton.FlatStyle = FlatStyle.Flat;
            this.loadButton.FlatAppearance.BorderSize = 0;
            this.loadButton.Visible = false;
            this.loadButton.Click += LoadButton_Click;

            // namespaceLabel
            this.namespaceLabel.Text = "Namespace:";
            this.namespaceLabel.Location = new Point(20, 280);
            this.namespaceLabel.Size = new Size(100, 23);
            this.namespaceLabel.TabIndex = 17;
            this.namespaceLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.namespaceLabel.Visible = false;

            // namespaceTextBox
            this.namespaceTextBox.Location = new Point(130, 280);
            this.namespaceTextBox.Size = new Size(250, 23);
            this.namespaceTextBox.TabIndex = 18;
            this.namespaceTextBox.Visible = false;

            // usernameLabel
            this.usernameLabel.Text = "Username:";
            this.usernameLabel.Location = new Point(20, 310);
            this.usernameLabel.Size = new Size(100, 23);
            this.usernameLabel.TabIndex = 19;
            this.usernameLabel.TextAlign = ContentAlignment.MiddleLeft;

            // usernameTextBox
            this.usernameTextBox.Location = new Point(130, 310);
            this.usernameTextBox.Size = new Size(250, 23);
            this.usernameTextBox.TabIndex = 20;

            // passwordLabel
            this.passwordLabel.Text = "Password:";
            this.passwordLabel.Location = new Point(20, 340);
            this.passwordLabel.Size = new Size(100, 23);
            this.passwordLabel.TabIndex = 21;
            this.passwordLabel.TextAlign = ContentAlignment.MiddleLeft;

            // passwordTextBox
            this.passwordTextBox.Location = new Point(130, 340);
            this.passwordTextBox.Size = new Size(250, 23);
            this.passwordTextBox.TabIndex = 22;
            this.passwordTextBox.PasswordChar = '*';

            // savePasswordCheckBox
            this.savePasswordCheckBox.Text = "Save Password";
            this.savePasswordCheckBox.Location = new Point(130, 370);
            this.savePasswordCheckBox.Size = new Size(250, 23);
            this.savePasswordCheckBox.TabIndex = 23;
            this.savePasswordCheckBox.CheckAlign = ContentAlignment.MiddleLeft;

            // connectButton
            this.connectButton.Text = "Connect";
            this.connectButton.Size = new Size(100, 30);
            this.connectButton.Location = new Point(130, 400);
            this.connectButton.TabIndex = 24;
            this.connectButton.BackColor = Color.FromArgb(0, 122, 204);
            this.connectButton.ForeColor = Color.White;
            this.connectButton.FlatStyle = FlatStyle.Flat;
            this.connectButton.FlatAppearance.BorderSize = 0;
            this.connectButton.Click += ConnectButton_Click;

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(100, 30);
            this.cancelButton.Location = new Point(280, 400);
            this.cancelButton.TabIndex = 25;
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
            this.loadingLabel.Location = new Point(130, 400);
            this.loadingLabel.Size = new Size(250, 23);
            this.loadingLabel.TabIndex = 26;
            this.loadingLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.loadingLabel.Visible = false;

            // loadingProgressBar
            this.loadingProgressBar.Location = new Point(130, 430);
            this.loadingProgressBar.Size = new Size(250, 23);
            this.loadingProgressBar.TabIndex = 27;
            this.loadingProgressBar.Style = ProgressBarStyle.Marquee;
            this.loadingProgressBar.Visible = false;

            // DBConnectDialog
            this.Text = "Connect to Database";
            this.ClientSize = new Size(400, 470);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.dbTypeLabel);
            this.Controls.Add(this.dbTypeComboBox);
            this.Controls.Add(this.dbNameLabel);
            this.Controls.Add(this.dbNameComboBox);
            this.Controls.Add(this.dbNameHintLabel);
            this.Controls.Add(this.bootstrapRadioButton);
            this.Controls.Add(this.readOnlyRadioButton);
            this.Controls.Add(this.connectionTypeLabel);
            this.Controls.Add(this.connectionTypeComboBox);
            this.Controls.Add(this.ldapServerLabel);
            this.Controls.Add(this.ldapServerTextBox);
            this.Controls.Add(this.contextLabel);
            this.Controls.Add(this.contextTextBox);
            this.Controls.Add(this.dbServiceLabel);
            this.Controls.Add(this.dbServiceComboBox);
            this.Controls.Add(this.loadButton);
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
            if (sender is RadioButton rb && rb.Checked)
            {
                UpdateUIForConnectionType();
            }
        }

        private void ConnectionTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateUIForConnectionType();
        }

        private void DbServiceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // When DB Service is selected, sync the Database Name field (if visible)
            if (connectionTypeComboBox.SelectedItem?.ToString() == "LDAP" && dbServiceComboBox.SelectedIndex >= 0)
            {
                dbNameComboBox.Text = dbServiceComboBox.SelectedItem?.ToString() ?? "";
            }
        }

        private void LoadButton_Click(object? sender, EventArgs e)
        {
            // Load available DB services from LDAP directory
            if (string.IsNullOrEmpty(ldapServerTextBox.Text))
            {
                MessageBox.Show("Please enter LDAP Server address first", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(contextTextBox.Text))
            {
                MessageBox.Show("Please enter LDAP Context first", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                dbServiceComboBox.Items.Clear();
                ldapServiceDescriptors.Clear();
                loadButton.Enabled = false;
                loadButton.Text = "Loading...";
                this.Refresh();

                var services = QueryLdapServices(ldapServerTextBox.Text, contextTextBox.Text);

                if (services.Count > 0)
                {
                    foreach (var service in services)
                    {
                        dbServiceComboBox.Items.Add(service.Name);
                        if (!string.IsNullOrWhiteSpace(service.Descriptor))
                        {
                            ldapServiceDescriptors[service.Name] = service.Descriptor;
                        }
                    }
                    dbServiceComboBox.SelectedIndex = 0;
                    MessageBox.Show($"Found {services.Count} database services in LDAP.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No database services found in LDAP directory. Please enter DB Service manually.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading LDAP services:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                loadButton.Enabled = true;
                loadButton.Text = "Load Services";
                this.Refresh();
            }
        }

        private List<LdapServiceInfo> QueryLdapServices(string ldapServers, string context)
        {
            Dictionary<string, string?> services = new(StringComparer.OrdinalIgnoreCase);
            List<string> errors = new();
            List<LdapEndpoint> endpoints = ParseLdapEndpoints(ldapServers);

            if (endpoints.Count == 0)
            {
                throw new InvalidOperationException("No valid LDAP server endpoints were found. Use host, host:port, or host:port:sslPort.");
            }

            foreach (var endpoint in endpoints)
            {
                try
                {
                    foreach (var service in QueryLdapServices(endpoint, context))
                    {
                        services[service.Name] = service.Descriptor;
                    }
                }
                catch (Exception ex)
                {
                    string endpointLabel = endpoint.UseSsl
                        ? $"{endpoint.Host}:{endpoint.Port} (SSL)"
                        : $"{endpoint.Host}:{endpoint.Port}";
                    string error = $"{endpointLabel}: {ex.Message}";
                    errors.Add(error);
                    Debug.Log($"LDAP query failed for {endpointLabel}: {ex}");
                }
            }

            if (services.Count == 0 && errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            return services
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new LdapServiceInfo(kvp.Key, kvp.Value))
                .ToList();
        }

        private List<LdapServiceInfo> QueryLdapServices(LdapEndpoint endpoint, string context)
        {
            List<LdapServiceInfo> services = new();
            string ldapUrl = $"LDAP://{endpoint.Host}:{endpoint.Port}/{context}";

            using DirectoryEntry entry = new(ldapUrl);
            entry.AuthenticationType = endpoint.UseSsl
                ? AuthenticationTypes.Anonymous | AuthenticationTypes.SecureSocketsLayer
                : AuthenticationTypes.Anonymous;

            using DirectorySearcher searcher = new(entry);
            searcher.Filter = "(objectClass=orclNetService)";
            searcher.SearchScope = SearchScope.Subtree;
            searcher.PageSize = 1000;
            searcher.PropertiesToLoad.Add("cn");
            searcher.PropertiesToLoad.Add("orclNetDescString");

            using SearchResultCollection results = searcher.FindAll();

            foreach (SearchResult result in results)
            {
                if (!result.Properties.Contains("cn"))
                {
                    continue;
                }

                foreach (var value in result.Properties["cn"])
                {
                    string? name = GetLdapPropertyString(value);
                    if (!string.IsNullOrWhiteSpace(name) && !name.Contains("_", StringComparison.Ordinal))
                    {
                        string? descriptor = result.Properties.Contains("orclNetDescString") && result.Properties["orclNetDescString"].Count > 0
                            ? GetLdapPropertyString(result.Properties["orclNetDescString"][0])
                            : null;
                        services.Add(new LdapServiceInfo(name, descriptor));
                    }
                }
            }

            return services;
        }

        private static string? GetLdapPropertyString(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string stringValue)
            {
                return stringValue.Trim();
            }

            if (value is byte[] bytes)
            {
                string decodedValue = Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim();
                if (!string.IsNullOrWhiteSpace(decodedValue))
                {
                    return decodedValue;
                }

                decodedValue = Encoding.ASCII.GetString(bytes).TrimEnd('\0').Trim();
                return string.IsNullOrWhiteSpace(decodedValue) ? null : decodedValue;
            }

            return value.ToString()?.Trim();
        }

        private List<LdapEndpoint> ParseLdapEndpoints(string ldapServers)
        {
            List<LdapEndpoint> endpoints = new();

            foreach (string rawEntry in ldapServers.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string entry = rawEntry;
                bool useSsl = false;

                if (entry.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase))
                {
                    entry = entry.Substring("LDAP://".Length);
                }
                else if (entry.StartsWith("LDAPS://", StringComparison.OrdinalIgnoreCase))
                {
                    entry = entry.Substring("LDAPS://".Length);
                    useSsl = true;
                }

                entry = entry.Trim('/');
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                string[] parts = entry.Split(':', StringSplitOptions.TrimEntries);
                string host = parts[0];
                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                if (parts.Length == 1)
                {
                    endpoints.Add(new LdapEndpoint(host, useSsl ? 636 : 389, useSsl));
                    continue;
                }

                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    endpoints.Add(new LdapEndpoint(host, port, useSsl));
                    continue;
                }

                if (parts.Length >= 3)
                {
                    bool added = false;

                    if (int.TryParse(parts[1], out int ldapPort) && ldapPort > 0)
                    {
                        endpoints.Add(new LdapEndpoint(host, ldapPort, false));
                        added = true;
                    }

                    if (int.TryParse(parts[2], out int sslPort) && sslPort > 0)
                    {
                        endpoints.Add(new LdapEndpoint(host, sslPort, true));
                        added = true;
                    }

                    if (added)
                    {
                        continue;
                    }
                }

                if (!useSsl)
                {
                    endpoints.Add(new LdapEndpoint(host, 389, false));
                }
                else
                {
                    endpoints.Add(new LdapEndpoint(host, 636, true));
                }
            }

            return endpoints;
        }

        private readonly record struct LdapEndpoint(string Host, int Port, bool UseSsl);
        private readonly record struct LdapServiceInfo(string Name, string? Descriptor);

        private void UpdateUIForConnectionType()
        {
            // Get connection mode from radio buttons
            bool isReadOnly = readOnlyRadioButton.Checked;
            bool isOracle = dbTypeComboBox.SelectedItem?.ToString() == "Oracle";
            
            // Get connection type from dropdown
            string? connectionType = connectionTypeComboBox.SelectedItem?.ToString();
            bool isLdap = isOracle && connectionType == "LDAP";

            dbNameHintLabel.Visible = false;

            // Hide all optional sections first
            connectionTypeLabel.Visible = isOracle;
            connectionTypeComboBox.Visible = isOracle;
            namespaceLabel.Visible = false;
            namespaceTextBox.Visible = false;
            ldapServerLabel.Visible = false;
            ldapServerTextBox.Visible = false;
            contextLabel.Visible = false;
            contextTextBox.Visible = false;
            dbServiceLabel.Visible = false;
            dbServiceComboBox.Visible = false;
            loadButton.Visible = false;
            dbNameLabel.Visible = true;
            dbNameComboBox.Visible = true;
            dbNameComboBox.Enabled = true;

            if (!isOracle && connectionTypeComboBox.SelectedItem?.ToString() != "Standard")
            {
                connectionTypeComboBox.SelectedItem = "Standard";
            }

            // Show namespace field for Read Only mode (both Standard and LDAP)
            if (isReadOnly)
            {
                namespaceLabel.Visible = true;
                namespaceTextBox.Visible = true;
                UpdateNamespaceForSqlServer();
            }

            // Show LDAP-specific controls when LDAP connection type is selected
            if (isLdap)
            {
                ldapServerLabel.Visible = true;
                ldapServerTextBox.Visible = true;
                contextLabel.Visible = true;
                contextTextBox.Visible = true;
                dbServiceLabel.Visible = true;
                dbServiceComboBox.Visible = true;
                loadButton.Visible = true;

                // For LDAP, make Database Name read-only and sync with DB Service
                dbNameComboBox.Enabled = false;
                if (dbServiceComboBox.SelectedIndex >= 0)
                {
                    dbNameComboBox.Text = dbServiceComboBox.SelectedItem?.ToString() ?? "";
                }
            }

            ApplyFormLayout(isLdap, isReadOnly);
        }

        private void ApplyFormLayout(bool isLdap, bool isReadOnly)
        {
            const int labelX = 20;
            const int fieldX = 130;
            const int rowHeight = 30;
            const int buttonYGap = 18;
            const int progressYGap = 34;
            const int bottomPadding = 62;

            int nextRowY = isLdap ? 280 : 190;

            if (isReadOnly)
            {
                namespaceLabel.Location = new Point(labelX, nextRowY);
                namespaceTextBox.Location = new Point(fieldX, nextRowY);
                nextRowY += rowHeight;
            }

            usernameLabel.Location = new Point(labelX, nextRowY);
            usernameTextBox.Location = new Point(fieldX, nextRowY);
            nextRowY += rowHeight;

            passwordLabel.Location = new Point(labelX, nextRowY);
            passwordTextBox.Location = new Point(fieldX, nextRowY);
            nextRowY += rowHeight;

            savePasswordCheckBox.Location = new Point(fieldX, nextRowY);

            int buttonY = nextRowY + buttonYGap;
            connectButton.Location = new Point(130, buttonY);
            cancelButton.Location = new Point(280, buttonY);

            loadingLabel.Location = new Point(fieldX, buttonY);
            loadingProgressBar.Location = new Point(fieldX, buttonY + progressYGap);

            this.ClientSize = new Size(400, buttonY + bottomPadding);
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

        }

        private void DbTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (dbTypeComboBox.SelectedItem is string dbType)
            {
                connectionTypeComboBox.SelectedIndexChanged -= ConnectionTypeComboBox_SelectedIndexChanged;
                connectionTypeComboBox.Items.Clear();

                switch (dbType)
                {
                    case "Oracle":
                        connectionTypeComboBox.Items.Add("Standard");
                        connectionTypeComboBox.Items.Add("LDAP");
                        connectionTypeComboBox.SelectedItem = "Standard";
                        LoadOracleTnsNames();
                        break;
                    case "SQL Server":
                        connectionTypeComboBox.Items.Add("Standard");
                        connectionTypeComboBox.SelectedItem = "Standard";
                        LoadSqlServerDsns();
                        break;
                }

                connectionTypeComboBox.SelectedIndexChanged += ConnectionTypeComboBox_SelectedIndexChanged;

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
                    if (connectionTypeComboBox.SelectedItem?.ToString() == "LDAP" && (string.IsNullOrEmpty(ldapServerTextBox.Text) || string.IsNullOrEmpty(contextTextBox.Text)))
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
            connectionTypeComboBox.Enabled = !isConnecting;
            ldapServerTextBox.Enabled = !isConnecting;
            contextTextBox.Enabled = !isConnecting;
            dbServiceComboBox.Enabled = !isConnecting;
            loadButton.Enabled = !isConnecting;
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

            string? dbType = dbTypeComboBox.SelectedItem?.ToString();
            
            // Get connection mode from radio buttons
            bool isReadOnly = readOnlyRadioButton.Checked;
            
            // Get connection type from dropdown
            string? connType = connectionTypeComboBox.SelectedItem?.ToString();
            bool isLdap = connType == "LDAP";

            if (string.IsNullOrEmpty(dbType))
            {
                MessageBox.Show("Please select a database type", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string username = usernameTextBox.Text;
            string password = passwordTextBox.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter username and password", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Validate connection-type-specific requirements
            string? connectionString = null;
            string? namespaceForConnection = null;
            string saveKey = dbNameComboBox.Text;

            try
            {
                if (isLdap)
                {
                    // LDAP-specific validation
                    if (string.IsNullOrEmpty(ldapServerTextBox.Text) || string.IsNullOrEmpty(contextTextBox.Text) || string.IsNullOrEmpty(dbServiceComboBox.Text))
                    {
                        MessageBox.Show("For LDAP connection, please enter LDAP Server, Context, and select DB Service", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string dataSource = dbServiceComboBox.Text;
                    if (ldapServiceDescriptors.TryGetValue(dbServiceComboBox.Text, out string? descriptor) && !string.IsNullOrWhiteSpace(descriptor))
                    {
                        dataSource = descriptor;
                    }

                    // Connect with the resolved descriptor when available so ODP.NET does not
                    // need separate ldap.ora / LDAPsettings configuration at runtime.
                    connectionString = $"Data Source={dataSource};User Id={username};Password={password};";
                    
                    // For Read Only + LDAP, also set the namespace
                    if (isReadOnly && !string.IsNullOrEmpty(namespaceTextBox.Text))
                    {
                        namespaceForConnection = namespaceTextBox.Text;
                    }

                    Debug.Log($"Using LDAP Oracle connection for service '{dbServiceComboBox.Text}' with {(dataSource == dbServiceComboBox.Text ? "alias" : "resolved descriptor")} data source.");
                }
                else if (isReadOnly)
                {
                    // Standard connection with Read Only User
                    string @namespace = namespaceTextBox.Text;
                    if (string.IsNullOrEmpty(@namespace))
                    {
                        MessageBox.Show("Please enter a namespace for Read Only User", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (dbType == "Oracle")
                    {
                        connectionString = $"Data Source={dbNameComboBox.Text};User Id={username};Password={password};";
                        namespaceForConnection = @namespace;
                    }
                    else if (dbType == "SQL Server")
                    {
                        connectionString = $"DSN={dbNameComboBox.Text};UID={username};PWD={password};";
                        namespaceForConnection = @namespace;
                    }
                }
                else
                {
                    // Bootstrap connection
                    if (string.IsNullOrEmpty(dbNameComboBox.Text))
                    {
                        MessageBox.Show("Please select a database name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (dbType == "Oracle")
                    {
                        connectionString = $"Data Source={dbNameComboBox.Text};User Id={username};Password={password};";
                    }
                    else if (dbType == "SQL Server")
                    {
                        connectionString = $"DSN={dbNameComboBox.Text};UID={username};PWD={password};";
                    }
                }

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new NotSupportedException($"Database type '{dbType}' is not supported.");
                }

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
                        string? detailedError = DataManager switch
                        {
                            OraclePeopleSoftDataManager oracleManager => oracleManager.LastConnectionError,
                            SqlServerPeopleSoftDataManager sqlServerManager => sqlServerManager.LastConnectionError,
                            _ => null
                        };

                        throw new Exception(string.IsNullOrWhiteSpace(detailedError)
                            ? "Failed to connect to database"
                            : detailedError);
                    }

                    PeopleCodeParser.SelfHosted.PeopleCodeParser.ToolsRelease = new ToolsVersion(DataManager.GetToolsVersion());
                });

                string? encryptedPassword = savePasswordCheckBox.Checked ? EncryptPassword(password, saveKey) : null;
                SaveSettingsForDatabase(
                    saveKey,
                    isReadOnly,
                    isLdap,
                    ldapServerTextBox.Text,
                    contextTextBox.Text,
                    dbServiceComboBox.Text,
                    isLdap && ldapServiceDescriptors.TryGetValue(dbServiceComboBox.Text, out string? savedDescriptor) ? savedDescriptor : string.Empty,
                    username,
                    namespaceForConnection ?? "",
                    encryptedPassword);

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
        private void SaveSettingsForDatabase(string dbName, bool isReadOnly, bool isLdap, string ldapServer, string context, string dbService, string ldapDescriptor, string username, string @namespace, string? encryptedPassword = null)
        {
            if (savedSettings == null)
                savedSettings = new Dictionary<string, DbConnectionSettings>();

            savedSettings[dbName] = new DbConnectionSettings(isReadOnly, isLdap, ldapServer, context, dbService, ldapDescriptor, username, @namespace, encryptedPassword);
            SaveAllSettings();
        }

        /// <summary>
        /// Applies saved settings for a specific database
        /// </summary>
        /// <returns>True if settings were successfully applied, false otherwise</returns>
        private bool ApplySettingsForDatabase(string dbName)
        {
            if (savedSettings == null)
                return false;

            string settingsKey = dbName;
            string legacyLdapKey = $"{dbName} (LDAP)";
            bool hasExactSettings = savedSettings.TryGetValue(settingsKey, out var settings);
            bool hasLegacyLdapSettings = savedSettings.TryGetValue(legacyLdapKey, out var legacyLdapSettings);

            if (hasLegacyLdapSettings && (!hasExactSettings || (settings != null && !settings.IsLdap)))
            {
                settingsKey = legacyLdapKey;
                settings = legacyLdapSettings;
            }
            else if (!hasExactSettings || settings == null)
            {
                return false;
            }

            if (settingsKey == legacyLdapKey && !settings.IsLdap)
            {
                settings.IsLdap = true;
                if (string.IsNullOrWhiteSpace(settings.DbService))
                {
                    settings.DbService = dbName;
                }
            }

            // Apply connection type based on settings
            if (settings.IsReadOnly)
            {
                readOnlyRadioButton.Checked = true; // Read Only User
            }
            else
            {
                bootstrapRadioButton.Checked = true; // Bootstrap
            }

            // Set username
            usernameTextBox.Text = settings.Username;

            // Set namespace
            namespaceTextBox.Text = settings.Namespace;

            // Restore LDAP-specific settings
            connectionTypeComboBox.SelectedItem = settings.IsLdap ? "LDAP" : "Standard";
            ldapServerTextBox.Text = settings.LdapServer;
            contextTextBox.Text = settings.Context;

            dbServiceComboBox.Items.Clear();
            ldapServiceDescriptors.Clear();
            if (settings.IsLdap && !string.IsNullOrWhiteSpace(settings.DbService))
            {
                dbServiceComboBox.Items.Add(settings.DbService);
                dbServiceComboBox.SelectedItem = settings.DbService;
                if (!string.IsNullOrWhiteSpace(settings.LdapDescriptor))
                {
                    ldapServiceDescriptors[settings.DbService] = settings.LdapDescriptor;
                }
            }

            // Set password if it was saved
            if (!string.IsNullOrEmpty(settings.EncryptedPassword))
            {
                try
                {
                    passwordTextBox.Text = DecryptPassword(settings.EncryptedPassword, settingsKey);
                    savePasswordCheckBox.Checked = true;
                }
                catch
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
