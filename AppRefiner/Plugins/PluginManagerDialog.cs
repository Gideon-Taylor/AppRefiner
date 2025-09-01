namespace AppRefiner.Plugins
{
    public partial class PluginManagerDialog : Form
    {
        private readonly string _pluginDirectory;

        // Public property to access the plugin directory
        public string PluginDirectory => txtPluginDirectory.Text;

        public PluginManagerDialog(string pluginDirectory)
        {
            InitializeComponent();
            _pluginDirectory = pluginDirectory;
        }

        private void PluginManagerDialog_Load(object sender, EventArgs e)
        {
            txtPluginDirectory.Text = _pluginDirectory;
            RefreshPluginList();
        }

        private void RefreshPluginList()
        {
            lstPlugins.Items.Clear();

            var plugins = PluginManager.GetLoadedPluginMetadata();
            foreach (var plugin in plugins)
            {
                var item = new ListViewItem(plugin.AssemblyName);
                item.SubItems.Add(plugin.Version);
                item.SubItems.Add(plugin.LinterCount.ToString());
                item.SubItems.Add(plugin.StylerCount.ToString());
                item.SubItems.Add(plugin.FilePath);
                item.Tag = plugin;

                lstPlugins.Items.Add(item);
            }

            // Update status
            lblStatus.Text = $"Loaded {plugins.Count} plugins with " +
                             $"{plugins.Sum(p => p.LinterCount)} linters and " +
                             $"{plugins.Sum(p => p.StylerCount)} stylers";
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Plugin Directory",
                UseDescriptionForTitle = true,
                SelectedPath = txtPluginDirectory.Text
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtPluginDirectory.Text = dialog.SelectedPath;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.PluginDirectory = txtPluginDirectory.Text;
            Properties.Settings.Default.Save();

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                int count = PluginManager.LoadPlugins(txtPluginDirectory.Text);
                RefreshPluginList();
                MessageBox.Show($"Successfully loaded {count} plugin assemblies.",
                    "Plugins Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading plugins: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
