namespace AppRefiner.Dialogs
{
    // Define delegate for command actions that can receive progress dialog
    public delegate void CommandAction(CommandProgressDialog progressDialog);

    public class Command
    {
        public string Title { get; set; }
        private string _description = "";
        private Func<string>? _dynamicDescription;
        public Action? LegacyExecute { get; set; }
        public CommandAction? ExecuteWithProgress { get; set; }

        // New properties for enabled state
        private bool _isEnabled = true;
        private Func<bool>? _dynamicEnabled;

        public string Description
        {
            get
            {
                return _dynamicDescription != null ? _dynamicDescription() : _description;
            }
            set
            {
                _description = value;
                _dynamicDescription = null;
            }
        }

        public bool Enabled
        {
            get
            {
                return _dynamicEnabled != null ? _dynamicEnabled() : _isEnabled;
            }
            set
            {
                _isEnabled = value;
                _dynamicEnabled = null;
            }
        }

        // Legacy constructors using Action
        public Command(string title, string description, Action execute)
        {
            Title = title;
            _description = description;
            LegacyExecute = execute;
        }

        public Command(string title, Func<string> dynamicDescription, Action execute)
        {
            Title = title;
            _dynamicDescription = dynamicDescription;
            LegacyExecute = execute;
        }

        public Command(string title, string description, Action execute, bool enabled)
        {
            Title = title;
            _description = description;
            LegacyExecute = execute;
            _isEnabled = enabled;
        }

        public Command(string title, string description, Action execute, Func<bool> dynamicEnabled)
        {
            Title = title;
            _description = description;
            LegacyExecute = execute;
            _dynamicEnabled = dynamicEnabled;
        }

        public Command(string title, Func<string> dynamicDescription, Action execute, Func<bool> dynamicEnabled)
        {
            Title = title;
            _dynamicDescription = dynamicDescription;
            LegacyExecute = execute;
            _dynamicEnabled = dynamicEnabled;
        }

        // New constructors using CommandAction for progress reporting
        public Command(string title, string description, CommandAction execute)
        {
            Title = title;
            _description = description;
            ExecuteWithProgress = execute;
        }

        public Command(string title, Func<string> dynamicDescription, CommandAction execute)
        {
            Title = title;
            _dynamicDescription = dynamicDescription;
            ExecuteWithProgress = execute;
        }

        public Command(string title, string description, CommandAction execute, bool enabled)
        {
            Title = title;
            _description = description;
            ExecuteWithProgress = execute;
            _isEnabled = enabled;
        }

        public Command(string title, string description, CommandAction execute, Func<bool> dynamicEnabled)
        {
            Title = title;
            _description = description;
            ExecuteWithProgress = execute;
            _dynamicEnabled = dynamicEnabled;
        }

        public Command(string title, Func<string> dynamicDescription, CommandAction execute, Func<bool> dynamicEnabled)
        {
            Title = title;
            _dynamicDescription = dynamicDescription;
            ExecuteWithProgress = execute;
            _dynamicEnabled = dynamicEnabled;
        }
    }

    public partial class CommandPaletteDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly TextBox searchBox;
        private readonly ListView commandListView;
        private readonly List<Command> allCommands;
        private List<Command> filteredCommands;
        private CommandAction? selectedAction;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private IntPtr owner;

        public CommandPaletteDialog(List<Command> commands, IntPtr owner)
        {
            allCommands = commands;
            filteredCommands = new List<Command>(allCommands);
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchBox = new TextBox();
            this.commandListView = new ListView();
            this.owner = owner;
            InitializeComponent();
            ConfigureForm();
            PopulateCommandList();
        }

        public CommandAction? GetSelectedAction()
        {
            return selectedAction;
        }

        private void InitializeComponent()
        {   
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "AppRefiner - Command Palette";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // searchBox
            this.searchBox.BorderStyle = BorderStyle.FixedSingle;
            this.searchBox.Dock = DockStyle.Top;
            this.searchBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            this.searchBox.Location = new Point(0, 0);
            this.searchBox.Margin = new Padding(0);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new Size(550, 32);
            this.searchBox.TabIndex = 0;
            this.searchBox.TextChanged += new EventHandler(this.SearchBox_TextChanged);
            this.searchBox.KeyDown += new KeyEventHandler(this.SearchBox_KeyDown);

            // commandListView
            this.commandListView.BorderStyle = BorderStyle.None;
            this.commandListView.Dock = DockStyle.Fill;
            this.commandListView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.commandListView.FullRowSelect = true;
            this.commandListView.HeaderStyle = ColumnHeaderStyle.None;
            this.commandListView.HideSelection = false;
            this.commandListView.Location = new Point(0, 32);
            this.commandListView.Name = "commandListView";
            this.commandListView.Size = new Size(550, 318);
            this.commandListView.TabIndex = 1;
            this.commandListView.UseCompatibleStateImageBehavior = false;
            this.commandListView.View = View.Tile;
            this.commandListView.MouseDoubleClick += new MouseEventHandler(this.CommandListView_MouseDoubleClick);
            this.commandListView.KeyDown += new KeyEventHandler(this.CommandListView_KeyDown);

            // CommandPalette
            this.ClientSize = new Size(550, 380); // Made slightly taller to accommodate header
            this.Controls.Add(this.commandListView);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Command Palette";
            this.ShowInTaskbar = false;
            
            // Add background color to make dialog stand out
            this.BackColor = Color.FromArgb(240, 240, 245);
            
            // Add a 1-pixel border to make the dialog visually distinct
            this.Padding = new Padding(1);
            
            // Add resize event handler to update tile size when form is resized
            this.Resize += new EventHandler(this.CommandPalette_Resize);
            
            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Change the view to TileView to show both title and description
            commandListView.View = View.Tile;
            commandListView.TileSize = new Size(commandListView.Width - 25, 50);  // Reduced width to prevent horizontal scrolling

            // Configure the list view for title and description
            commandListView.Columns.Add("Title", 250);
            commandListView.Columns.Add("Description", 250);

            // Show item tooltips
            commandListView.ShowItemToolTips = true;
        }

        private void PopulateCommandList()
        {
            commandListView.Items.Clear();

            foreach (var command in filteredCommands)
            {
                var item = new ListViewItem(command.Title);
                item.SubItems.Add(command.Description);
                item.Tag = command;

                // Add tooltip with description
                item.ToolTipText = command.Description;

                // Apply visual style for disabled commands
                if (!command.Enabled)
                {
                    item.ForeColor = SystemColors.GrayText;
                }

                commandListView.Items.Add(item);
            }

            if (commandListView.Items.Count > 0)
            {
                commandListView.Items[0].Selected = true;
                // Removed commandListView.Select() to prevent focus stealing
            }
        }

        private void FilterCommands(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredCommands = new List<Command>(allCommands);
            }
            else
            {
                filter = filter.ToLower();
                filteredCommands = allCommands
                    .Where(c => c.Title.ToLower().Contains(filter) ||
                                c.Description.ToLower().Contains(filter))
                    .ToList();
            }

            PopulateCommandList();

            if (commandListView.Items.Count > 0)
            {
                commandListView.Items[0].Selected = true;
                commandListView.EnsureVisible(0);
            }

            // Ensure focus stays in the search box
            searchBox.Focus();
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            FilterCommands(searchBox.Text);
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (commandListView.Items.Count > 0)
                {
                    int index = 0;
                    if (commandListView.SelectedIndices.Count > 0)
                    {
                        index = commandListView.SelectedIndices[0] + 1;
                        if (index >= commandListView.Items.Count)
                            index = 0;
                    }
                    commandListView.SelectedIndices.Clear();
                    commandListView.Items[index].Selected = true;
                    commandListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (commandListView.Items.Count > 0)
                {
                    int index = commandListView.Items.Count - 1;
                    if (commandListView.SelectedIndices.Count > 0)
                    {
                        index = commandListView.SelectedIndices[0] - 1;
                        if (index < 0)
                            index = commandListView.Items.Count - 1;
                    }
                    commandListView.SelectedIndices.Clear();
                    commandListView.Items[index].Selected = true;
                    commandListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                SelectCommand();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private void CommandListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            SelectCommand();
        }

        private void CommandListView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SelectCommand();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
            else if (e.KeyData == Keys.Tab || e.KeyData == (Keys.Tab | Keys.Shift))
            {
                // Send focus back to search box
                searchBox.Focus();
                e.Handled = true;
            }
        }

        private void SelectCommand()
        {
            if (commandListView.SelectedItems.Count > 0)
            {
                var command = (Command?)commandListView.SelectedItems[0].Tag;

                // Only select if the command is enabled
                if (command?.Enabled ?? false)
                {
                    // Store the command to execute, prioritizing the progress-aware action
                    if (command?.ExecuteWithProgress != null)
                    {
                        selectedAction = command.ExecuteWithProgress;
                    }
                    else if (command?.LegacyExecute != null)
                    {
                        // Wrap the legacy action in a CommandAction delegate
                        selectedAction = (progressDialog) => command.LegacyExecute?.Invoke();
                    }

                    // Close the form with DialogResult.OK
                    this.DialogResult = DialogResult.OK;
                    this.Hide();
                    this.Close();
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            searchBox.Focus();

            // Add drop shadow effect to the form
            const int CS_DROPSHADOW = 0x00020000;
            CreateParams cp = this.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
                        
            // Create the mouse handler if this is a modal dialog
            if (this.Modal && owner != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Add drop shadow effect to the form
                const int CS_DROPSHADOW = 0x00020000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw a border around the form
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, 
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Left
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Top
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Right
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid);   // Bottom
        }

        private void CommandPalette_Resize(object? sender, EventArgs e)
        {
            // Update tile size when form is resized to prevent horizontal scrolling
            commandListView.TileSize = new Size(commandListView.Width - 25, 50);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            
            // Dispose the mouse handler
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}
