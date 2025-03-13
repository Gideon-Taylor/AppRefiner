using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace AppRefiner
{
    public class Command
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public Action Execute { get; set; }

        public Command(string title, string description, Action execute)
        {
            Title = title;
            Description = description;
            Execute = execute;
        }
    }

    public partial class CommandPalette : Form
    {
        private TextBox searchBox;
        private ListView commandListView;
        private List<Command> allCommands;
        private List<Command> filteredCommands;

        public CommandPalette(List<Command> commands)
        {
            allCommands = commands;
            filteredCommands = new List<Command>(allCommands);
            InitializeComponent();
            ConfigureForm();
            PopulateCommandList();
        }

        private void InitializeComponent()
        {
            this.searchBox = new TextBox();
            this.commandListView = new ListView();
            this.SuspendLayout();

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
            this.commandListView.View = View.Details;
            this.commandListView.MouseDoubleClick += new MouseEventHandler(this.CommandListView_MouseDoubleClick);
            
            // CommandPalette
            this.ClientSize = new Size(550, 350);
            this.Controls.Add(this.commandListView);
            this.Controls.Add(this.searchBox);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Name = "CommandPalette";
            this.Text = "Command Palette";
            this.Deactivate += new EventHandler(this.CommandPalette_Deactivate);
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Add a column to the ListView that fills the width
            commandListView.Columns.Add("Command", -1, HorizontalAlignment.Left);
            commandListView.Columns[0].Width = commandListView.Width - 4;

            // Set up list view with groups for title and description
            commandListView.ShowGroups = false;
            commandListView.View = View.Details;
            commandListView.FullRowSelect = true;
        }

        private void PopulateCommandList()
        {
            commandListView.Items.Clear();
            
            foreach (var command in filteredCommands)
            {
                var item = new ListViewItem(command.Title);
                item.SubItems.Add(command.Description);
                item.Tag = command;
                commandListView.Items.Add(item);
            }

            if (commandListView.Items.Count > 0)
            {
                commandListView.Items[0].Selected = true;
                commandListView.Select();
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
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            FilterCommands(searchBox.Text);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
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
                    commandListView.Items[index].Selected = true;
                    commandListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelectedCommand();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private void CommandListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ExecuteSelectedCommand();
        }

        private void ExecuteSelectedCommand()
        {
            if (commandListView.SelectedItems.Count > 0)
            {
                var command = (Command)commandListView.SelectedItems[0].Tag;
                this.Close();
                command.Execute();
            }
        }

        private void CommandPalette_Deactivate(object sender, EventArgs e)
        {
            this.Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            searchBox.Focus();
        }
    }
}
