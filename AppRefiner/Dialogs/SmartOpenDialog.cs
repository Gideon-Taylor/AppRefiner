using AppRefiner.Database.Models;
using System.Runtime.InteropServices;

namespace AppRefiner.Dialogs
{
    public partial class SmartOpenDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly TextBox searchBox;
        private readonly ListView targetListView;
        private readonly Func<string, int, List<OpenTarget>> searchFunction;
        private readonly Action? bypassCallback;
        private List<OpenTarget> currentTargets;
        private OpenTarget? selectedTarget;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private IntPtr owner;
        private System.Threading.Timer? searchTimer;

        private const int SEARCH_DELAY_MS = 300;
        private const int DEFAULT_MAX_RESULTS = 50;

        // P/Invoke declarations for enabling collapsible groups
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int LVM_ENABLEGROUPVIEW = 0x109D;
        private const int LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1036;
        private const int LVS_EX_DOUBLEBUFFER = 0x00010000;
        private const int LVM_SETGROUPINFO = 0x1093;
        private const int LVGF_STATE = 0x4;
        private const int LVGS_COLLAPSIBLE = 0x8;
        private const int LVGS_COLLAPSED = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LVGROUP
        {
            public int cbSize;
            public int mask;
            public IntPtr pszHeader;
            public int cchHeader;
            public IntPtr pszFooter;
            public int cchFooter;
            public int iGroupId;
            public int stateMask;
            public int state;
            public int uAlign;
        }

        public SmartOpenDialog(Func<string, int, List<OpenTarget>> searchFunction, IntPtr owner, Action? bypassCallback = null)
        {
            this.searchFunction = searchFunction;
            this.bypassCallback = bypassCallback;
            this.currentTargets = new List<OpenTarget>();
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchBox = new TextBox();
            this.targetListView = new ListView();
            this.owner = owner;
            InitializeComponent();
            ConfigureForm();
        }

        public OpenTarget? GetSelectedTarget()
        {
            return selectedTarget;
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
            this.headerLabel.Text = "AppRefiner - Smart Open";
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

            // targetListView
            this.targetListView.BorderStyle = BorderStyle.None;
            this.targetListView.Dock = DockStyle.Fill;
            this.targetListView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.targetListView.FullRowSelect = true;
            this.targetListView.HeaderStyle = ColumnHeaderStyle.None;
            this.targetListView.HideSelection = false;
            this.targetListView.Location = new Point(0, 32);
            this.targetListView.Name = "targetListView";
            this.targetListView.Size = new Size(550, 318);
            this.targetListView.TabIndex = 1;
            this.targetListView.UseCompatibleStateImageBehavior = false;
            this.targetListView.View = View.Tile;
            this.targetListView.MouseDoubleClick += new MouseEventHandler(this.TargetListView_MouseDoubleClick);
            this.targetListView.KeyDown += new KeyEventHandler(this.TargetListView_KeyDown);

            // SmartOpenDialog
            this.ClientSize = new Size(550, 380);
            this.Controls.Add(this.targetListView);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Smart Open";
            this.ShowInTaskbar = false;

            // Add background color to make dialog stand out
            this.BackColor = Color.FromArgb(240, 240, 245);

            // Add a 1-pixel border to make the dialog visually distinct
            this.Padding = new Padding(1);

            // Add resize event handler to update tile size when form is resized
            this.Resize += new EventHandler(this.SmartOpenDialog_Resize);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Change the view to TileView to show both title and description
            targetListView.View = View.Tile;
            targetListView.TileSize = new Size(targetListView.Width - 25, 50);

            // Configure the list view for title and description
            targetListView.Columns.Add("Name", 300);
            targetListView.Columns.Add("Description", 200);

            // Show item tooltips
            targetListView.ShowItemToolTips = true;

            // Enable grouping
            targetListView.ShowGroups = true;
            targetListView.GroupImageList = null; // No group images needed

            // Enable collapsible groups and double buffering for better performance
            EnableCollapsibleGroups();
        }

        private void EnableCollapsibleGroups()
        {
            if (targetListView.Handle != IntPtr.Zero)
            {
                // Enable group view
                SendMessage(targetListView.Handle, LVM_ENABLEGROUPVIEW, new IntPtr(1), IntPtr.Zero);
                
                // Enable double buffering for smoother rendering
                SendMessage(targetListView.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, new IntPtr(LVS_EX_DOUBLEBUFFER), new IntPtr(LVS_EX_DOUBLEBUFFER));
            }
        }

        private void CreateGroupsFromTargets()
        {
            targetListView.Groups.Clear();

            // Get distinct types from current targets
            var distinctTypes = currentTargets
                .Select(t => t.Type)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Create a group for each type
            int groupId = 0;
            foreach (var type in distinctTypes)
            {
                var group = new ListViewGroup(type.ToString(), type.ToString())
                {
                    HeaderAlignment = HorizontalAlignment.Left,
                    CollapsedState = ListViewGroupCollapsedState.Expanded // Start expanded
                };
                
                // Set a unique group ID - this is important for Win32 API calls
                group.Tag = groupId;
                targetListView.Groups.Add(group);
                groupId++;
            }
        }

        private void PopulateTargetList()
        {
            targetListView.Items.Clear();

            // Create groups first
            CreateGroupsFromTargets();

            foreach (var target in currentTargets)
            {
                var item = new ListViewItem(target.ToString());
                item.SubItems.Add(target.Description);
                item.Tag = target;

                // Add tooltip with full information
                item.ToolTipText = $"{target.Type}: {target.Name}\nPath: {target.Path}\n{target.Description}";

                // Find the appropriate group for this target's type
                var group = targetListView.Groups.Cast<ListViewGroup>()
                    .FirstOrDefault(g => g.Name == target.Type.ToString());

                if (group != null)
                {
                    item.Group = group;
                }

                targetListView.Items.Add(item);
            }

            if (targetListView.Items.Count > 0)
            {
                targetListView.Items[0].Selected = true;
            }
        }

        private void PerformSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                currentTargets.Clear();
            }
            else
            {
                try
                {
                    currentTargets = searchFunction(searchTerm, DEFAULT_MAX_RESULTS);
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error performing search: {ex.Message}");
                    currentTargets = new List<OpenTarget>();
                }
            }

            PopulateTargetList();

            if (targetListView.Items.Count > 0)
            {
                targetListView.Items[0].Selected = true;
                targetListView.EnsureVisible(0);
            }

            // Ensure focus stays in the search box
            searchBox.Focus();
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            // Cancel any existing timer
            searchTimer?.Dispose();

            // Start a new timer for delayed search
            searchTimer = new System.Threading.Timer(_ =>
            {
                // Execute search on UI thread
                this.Invoke(() => PerformSearch(searchBox.Text));
            }, null, SEARCH_DELAY_MS, Timeout.Infinite);
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (targetListView.Items.Count > 0)
                {
                    int index = 0;
                    if (targetListView.SelectedIndices.Count > 0)
                    {
                        index = targetListView.SelectedIndices[0] + 1;
                        if (index >= targetListView.Items.Count)
                            index = 0;
                    }
                    targetListView.SelectedIndices.Clear();
                    targetListView.Items[index].Selected = true;
                    targetListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (targetListView.Items.Count > 0)
                {
                    int index = targetListView.Items.Count - 1;
                    if (targetListView.SelectedIndices.Count > 0)
                    {
                        index = targetListView.SelectedIndices[0] - 1;
                        if (index < 0)
                            index = targetListView.Items.Count - 1;
                    }
                    targetListView.SelectedIndices.Clear();
                    targetListView.Items[index].Selected = true;
                    targetListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                SelectTarget();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                // Collapse the group of the currently selected item
                CollapseSelectedItemGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                // Expand the group of the currently selected item
                ExpandSelectedItemGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private void TargetListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            SelectTarget();
        }

        private void TargetListView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SelectTarget();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                // Collapse the group of the currently selected item
                CollapseSelectedItemGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                // Expand the group of the currently selected item
                ExpandSelectedItemGroup();
                e.Handled = true;
            }
            else if (e.KeyData == Keys.Tab || e.KeyData == (Keys.Tab | Keys.Shift))
            {
                // Send focus back to search box
                searchBox.Focus();
                e.Handled = true;
            }
        }

        private void SelectTarget()
        {
            if (targetListView.SelectedItems.Count > 0)
            {
                var target = (OpenTarget?)targetListView.SelectedItems[0].Tag;
                if (target != null)
                {
                    selectedTarget = target;
                    this.DialogResult = DialogResult.OK;
                    this.Hide();
                    this.Close();
                }
            }
        }

        private void CollapseSelectedItemGroup()
        {
            if (targetListView.SelectedItems.Count > 0)
            {
                var selectedItem = targetListView.SelectedItems[0];
                Debug.Log($"CollapseSelectedItemGroup: Selected item = {selectedItem.Text}");
                
                if (selectedItem.Group != null)
                {
                    Debug.Log($"CollapseSelectedItemGroup: Group = {selectedItem.Group.Name}");
                    
                    // Store the current group index before collapsing
                    int currentGroupIndex = targetListView.Groups.IndexOf(selectedItem.Group);
                    Debug.Log($"CollapseSelectedItemGroup: Group index = {currentGroupIndex}");
                    
                    // Collapse the group
                    SetGroupCollapsedState(selectedItem.Group, true);
                    
                    // Find the next visible item to select after collapsing
                    MoveSelectionToNextVisibleItem(currentGroupIndex);
                }
                else
                {
                    Debug.Log("CollapseSelectedItemGroup: Selected item has no group");
                }
            }
            else
            {
                Debug.Log("CollapseSelectedItemGroup: No selected items");
            }
        }

        private void ExpandSelectedItemGroup()
        {
            if (targetListView.SelectedItems.Count > 0)
            {
                var selectedItem = targetListView.SelectedItems[0];
                if (selectedItem.Group != null)
                {
                    SetGroupCollapsedState(selectedItem.Group, false);
                }
            }
        }

        private void SetGroupCollapsedState(ListViewGroup group, bool collapsed)
        {
            Debug.Log($"Setting group ({group.Name}) collapsed state to: {collapsed}");

            try
            {
                // Try using the .NET CollapsedState property first
                group.CollapsedState = collapsed ? ListViewGroupCollapsedState.Collapsed : ListViewGroupCollapsedState.Expanded;
                Debug.Log($"Successfully set .NET CollapsedState for group {group.Name}");
            }
            catch (Exception ex)
            {
                Debug.Log($"Failed to set .NET CollapsedState: {ex.Message}");
                
                // Fallback to Win32 API if .NET property fails
                SetGroupCollapsedStateWin32(group, collapsed);
            }
        }

        private void SetGroupCollapsedStateWin32(ListViewGroup group, bool collapsed)
        {
            if (targetListView.Handle == IntPtr.Zero)
                return;

            // Find the group index
            int groupIndex = targetListView.Groups.IndexOf(group);
            if (groupIndex < 0)
            {
                Debug.Log($"Group not found in collection: {group.Name}");
                return;
            }

            Debug.Log($"Using Win32 API for group {groupIndex} ({group.Name}) collapsed state: {collapsed}");

            // The group ID needs to be set properly for the Win32 API
            var lvGroup = new LVGROUP
            {
                cbSize = Marshal.SizeOf<LVGROUP>(),
                mask = LVGF_STATE,
                stateMask = LVGS_COLLAPSIBLE | LVGS_COLLAPSED,
                state = LVGS_COLLAPSIBLE | (collapsed ? LVGS_COLLAPSED : 0),
                iGroupId = groupIndex
            };

            // Send the message to update the group state
            var ptr = Marshal.AllocHGlobal(lvGroup.cbSize);
            try
            {
                Marshal.StructureToPtr(lvGroup, ptr, false);
                var result = SendMessage(targetListView.Handle, LVM_SETGROUPINFO, new IntPtr(groupIndex), ptr);
                Debug.Log($"LVM_SETGROUPINFO result for group {groupIndex}: {result}");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private void MoveSelectionToNextVisibleItem(int collapsedGroupIndex)
        {
            // Clear current selection
            targetListView.SelectedItems.Clear();

            // Look for the next expanded group after the collapsed one
            for (int groupIndex = collapsedGroupIndex + 1; groupIndex < targetListView.Groups.Count; groupIndex++)
            {
                var group = targetListView.Groups[groupIndex];
                
                // Check if this group has visible items (expanded groups will have items)
                var firstItemInGroup = group.Items.Cast<ListViewItem>().FirstOrDefault();
                if (firstItemInGroup != null)
                {
                    // Select the first item in this group
                    firstItemInGroup.Selected = true;
                    firstItemInGroup.Focused = true;
                    targetListView.EnsureVisible(firstItemInGroup.Index);
                    return;
                }
            }

            // If no group found after the collapsed one, look before it
            for (int groupIndex = collapsedGroupIndex - 1; groupIndex >= 0; groupIndex--)
            {
                var group = targetListView.Groups[groupIndex];
                
                // Check if this group has visible items
                var firstItemInGroup = group.Items.Cast<ListViewItem>().FirstOrDefault();
                if (firstItemInGroup != null)
                {
                    // Select the first item in this group
                    firstItemInGroup.Selected = true;
                    firstItemInGroup.Focused = true;
                    targetListView.EnsureVisible(firstItemInGroup.Index);
                    return;
                }
            }

            // If no visible items found at all, focus back to search box
            searchBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            else if (keyData == (Keys.Control | Keys.O))
            {
                // Ctrl+O pressed within SmartOpen - bypass to native App Designer open
                if (bypassCallback != null)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Hide(); // Hide immediately for responsiveness
                    
                    // Execute the bypass callback
                    bypassCallback();
                    
                    this.Close();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            
            // Enable collapsible groups now that the handle exists
            EnableCollapsibleGroups();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            searchBox.Focus();

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

        private void SmartOpenDialog_Resize(object? sender, EventArgs e)
        {
            // Update tile size when form is resized to prevent horizontal scrolling
            targetListView.TileSize = new Size(targetListView.Width - 25, 50);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // Dispose resources
            searchTimer?.Dispose();
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}