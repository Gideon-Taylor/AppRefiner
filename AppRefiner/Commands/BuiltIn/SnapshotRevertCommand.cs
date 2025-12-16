using AppRefiner.Dialogs;

namespace AppRefiner.Commands.BuiltIn
{
    /// <summary>
    /// Command to view file history and revert to a previous snapshot
    /// </summary>
    public class SnapshotRevertCommand : BaseCommand
    {
        public override string CommandName => "Snapshot: Revert to Previous Version";

        public override string CommandDescription => "View file history and revert to a previous snapshot";

        public override bool RequiresActiveEditor => true;

        public override Func<bool>? DynamicEnabledCheck => () => true; // Will check in Execute

        public override void Execute(CommandContext context)
        {
            try
            {
                if (context.ActiveEditor == null)
                {
                    MessageBox.Show("No active editor found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrEmpty(context.ActiveEditor.RelativePath))
                {
                    MessageBox.Show("This editor is not associated with a file in the Snapshot database.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (context.SnapshotManager == null)
                {
                    MessageBox.Show("Snapshot manager is not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Get process handle for dialog ownership
                var mainHandle = context.ActiveEditor.AppDesignerProcess.MainWindowHandle;

                // Run on UI thread to show dialog
                context.MainForm?.Invoke(() =>
                {
                    using var historyDialog = new SnapshotHistoryDialog(context.SnapshotManager, context.ActiveEditor, mainHandle);
                    historyDialog.ShowDialog(new WindowWrapper(mainHandle));
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"Error while reverting: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Revert Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
