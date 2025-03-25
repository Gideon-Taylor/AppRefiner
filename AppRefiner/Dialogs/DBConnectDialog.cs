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
        public DBConnectDialog(IntPtr owner = default)
        {
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.dbTypeLabel = new Label();
            this.dbTypeComboBox = new ComboBox();
            this.dbNameLabel = new Label();
            this.dbNameComboBox = new ComboBox();
            this.usernameLabel = new Label();
            this.usernameTextBox = new TextBox();
            this.passwordLabel = new Label();
            this.passwordTextBox = new TextBox();
            this.connectButton = new Button();
            this.cancelButton = new Button();
            this.owner = owner;

            InitializeComponent();
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

            // usernameLabel
            this.usernameLabel.Text = "Username:";
            this.usernameLabel.Location = new Point(20, 110);
            this.usernameLabel.Size = new Size(100, 23);
            this.usernameLabel.TabIndex = 5;
            this.usernameLabel.TextAlign = ContentAlignment.MiddleLeft;

            // usernameTextBox
            this.usernameTextBox.Location = new Point(130, 110);
            this.usernameTextBox.Size = new Size(250, 23);
            this.usernameTextBox.TabIndex = 6;

            // passwordLabel
            this.passwordLabel.Text = "Password:";
            this.passwordLabel.Location = new Point(20, 140);
            this.passwordLabel.Size = new Size(100, 23);
            this.passwordLabel.TabIndex = 7;
            this.passwordLabel.TextAlign = ContentAlignment.MiddleLeft;

            // passwordTextBox
            this.passwordTextBox.Location = new Point(130, 140);
            this.passwordTextBox.Size = new Size(250, 23);
            this.passwordTextBox.TabIndex = 8;
            this.passwordTextBox.PasswordChar = '*';

            // connectButton
            this.connectButton.Text = "Connect";
            this.connectButton.Size = new Size(100, 30);
            this.connectButton.Location = new Point(130, 180);
            this.connectButton.TabIndex = 9;
            this.connectButton.BackColor = Color.FromArgb(0, 122, 204);
            this.connectButton.ForeColor = Color.White;
            this.connectButton.FlatStyle = FlatStyle.Flat;
            this.connectButton.FlatAppearance.BorderSize = 0;
            this.connectButton.Click += ConnectButton_Click;

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(100, 30);
            this.cancelButton.Location = new Point(280, 180);
            this.cancelButton.TabIndex = 10;
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
            this.ClientSize = new Size(400, 230);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.dbTypeLabel);
            this.Controls.Add(this.dbTypeComboBox);
            this.Controls.Add(this.dbNameLabel);
            this.Controls.Add(this.dbNameComboBox);
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
                
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter username and password", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var connectionString = $"Data Source={dbName};User Id={username};Password={password};";
                
                try
                {
                    switch (dbType)
                    {
                        case "Oracle":
                            DataManager = new OraclePeopleSoftDataManager(connectionString);
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
    }
}
