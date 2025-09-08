using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using AppRefiner.Services;
using System.Drawing;
using System.Windows.Forms;

namespace AppRefiner.Refactors
{
    public enum SuppressReportMode
    {
        LINE, NEAREST_BLOCK, METHOD_OR_FUNC, GLOBAL
    }

    /// <summary>
    /// Creates a new instance of the SuppressReportRefactor class using the self-hosted parser
    /// </summary>
    public class SuppressReportRefactor : BaseRefactor
    {
        /// <summary>
        /// Gets the display name of this refactoring operation
        /// </summary>
        public new static string RefactorName => "Suppress Report";

        /// <summary>
        /// Gets the description of this refactoring operation
        /// </summary>
        public new static string RefactorDescription => "Suppress linting reports for a specific scope";

        private SuppressReportMode type = SuppressReportMode.LINE;
        private AstNode? globalNode;

        /// <summary>
        /// Indicates that this refactor requires a user input dialog
        /// </summary>
        public override bool RequiresUserInputDialog => true;

        public SuppressReportRefactor(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

        /// <summary>
        /// Dialog form for selecting suppress report mode
        /// </summary>
        private class SuppressReportDialog : Form
        {
            private RadioButton rbLine = new();
            private RadioButton rbNearestBlock = new();
            private RadioButton rbMethodOrFunc = new();
            private RadioButton rbGlobal = new();
            private Button btnOk = new();
            private Button btnCancel = new();
            private Label lblPrompt = new();
            private Panel headerPanel = new();
            private Label headerLabel = new();

            public SuppressReportMode SelectedMode { get; private set; }

            public SuppressReportDialog(SuppressReportMode initialMode = SuppressReportMode.LINE)
            {
                SelectedMode = initialMode;
                InitializeComponent();
                
                // Set the initial selected radio button
                switch (initialMode)
                {
                    case SuppressReportMode.LINE:
                        rbLine.Checked = true;
                        break;
                    case SuppressReportMode.NEAREST_BLOCK:
                        rbNearestBlock.Checked = true;
                        break;
                    case SuppressReportMode.METHOD_OR_FUNC:
                        rbMethodOrFunc.Checked = true;
                        break;
                    case SuppressReportMode.GLOBAL:
                        rbGlobal.Checked = true;
                        break;
                }
            }

            private void InitializeComponent()
            {
                this.SuspendLayout();
                
                // headerPanel
                this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                this.headerPanel.Dock = DockStyle.Top;
                this.headerPanel.Height = 30;
                this.headerPanel.Controls.Add(this.headerLabel);
                
                // headerLabel
                this.headerLabel.Text = "Suppress Report";
                this.headerLabel.ForeColor = Color.White;
                this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                this.headerLabel.Dock = DockStyle.Fill;
                this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
                
                // lblPrompt
                this.lblPrompt.AutoSize = true;
                this.lblPrompt.Location = new System.Drawing.Point(12, 40);
                this.lblPrompt.Name = "lblPrompt";
                this.lblPrompt.Size = new System.Drawing.Size(116, 15);
                this.lblPrompt.TabIndex = 0;
                this.lblPrompt.Text = "Select suppression scope:";
                
                // rbLine
                this.rbLine.AutoSize = true;
                this.rbLine.Location = new System.Drawing.Point(20, 65);
                this.rbLine.Name = "rbLine";
                this.rbLine.Size = new System.Drawing.Size(150, 19);
                this.rbLine.TabIndex = 1;
                this.rbLine.Text = "Current &Line";
                this.rbLine.UseVisualStyleBackColor = true;
                
                // rbNearestBlock
                this.rbNearestBlock.AutoSize = true;
                this.rbNearestBlock.Location = new System.Drawing.Point(20, 90);
                this.rbNearestBlock.Name = "rbNearestBlock";
                this.rbNearestBlock.Size = new System.Drawing.Size(150, 19);
                this.rbNearestBlock.TabIndex = 2;
                this.rbNearestBlock.Text = "Nearest &Block";
                this.rbNearestBlock.UseVisualStyleBackColor = true;
                
                // rbMethodOrFunc
                this.rbMethodOrFunc.AutoSize = true;
                this.rbMethodOrFunc.Location = new System.Drawing.Point(20, 115);
                this.rbMethodOrFunc.Name = "rbMethodOrFunc";
                this.rbMethodOrFunc.Size = new System.Drawing.Size(150, 19);
                this.rbMethodOrFunc.TabIndex = 3;
                this.rbMethodOrFunc.Text = "&Method or Function";
                this.rbMethodOrFunc.UseVisualStyleBackColor = true;
                
                // rbGlobal
                this.rbGlobal.AutoSize = true;
                this.rbGlobal.Location = new System.Drawing.Point(20, 140);
                this.rbGlobal.Name = "rbGlobal";
                this.rbGlobal.Size = new System.Drawing.Size(150, 19);
                this.rbGlobal.TabIndex = 4;
                this.rbGlobal.Text = "&Global";
                this.rbGlobal.UseVisualStyleBackColor = true;
                
                // btnOk
                this.btnOk.DialogResult = DialogResult.OK;
                this.btnOk.Location = new System.Drawing.Point(116, 175);
                this.btnOk.Name = "btnOk";
                this.btnOk.Size = new System.Drawing.Size(75, 28);
                this.btnOk.TabIndex = 5;
                this.btnOk.Text = "&OK";
                this.btnOk.UseVisualStyleBackColor = true;
                this.btnOk.Click += BtnOk_Click;
                
                // btnCancel
                this.btnCancel.DialogResult = DialogResult.Cancel;
                this.btnCancel.Location = new System.Drawing.Point(197, 175);
                this.btnCancel.Name = "btnCancel";
                this.btnCancel.Size = new System.Drawing.Size(75, 28);
                this.btnCancel.TabIndex = 6;
                this.btnCancel.Text = "&Cancel";
                this.btnCancel.UseVisualStyleBackColor = true;
                
                // SuppressReportDialog
                this.AcceptButton = this.btnOk;
                this.CancelButton = this.btnCancel;
                this.ClientSize = new System.Drawing.Size(284, 215);
                this.Controls.Add(this.btnCancel);
                this.Controls.Add(this.btnOk);
                this.Controls.Add(this.rbGlobal);
                this.Controls.Add(this.rbMethodOrFunc);
                this.Controls.Add(this.rbNearestBlock);
                this.Controls.Add(this.rbLine);
                this.Controls.Add(this.lblPrompt);
                this.Controls.Add(this.headerPanel);
                this.FormBorderStyle = FormBorderStyle.None;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.Name = "SuppressReportDialog";
                this.StartPosition = FormStartPosition.CenterParent;
                this.Text = "Suppress Report";
                this.ShowInTaskbar = false;
                this.ResumeLayout(false);
                this.PerformLayout();
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                // Determine which radio button is selected
                if (rbLine.Checked)
                {
                    SelectedMode = SuppressReportMode.LINE;
                }
                else if (rbNearestBlock.Checked)
                {
                    SelectedMode = SuppressReportMode.NEAREST_BLOCK;
                }
                else if (rbMethodOrFunc.Checked)
                {
                    SelectedMode = SuppressReportMode.METHOD_OR_FUNC;
                }
                else if (rbGlobal.Checked)
                {
                    SelectedMode = SuppressReportMode.GLOBAL;
                }
                
                DialogResult = DialogResult.OK;
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
        }

        /// <summary>
        /// Shows the dialog to get the suppression mode from the user
        /// </summary>
        public override bool ShowRefactorDialog()
        {
            using var dialog = new SuppressReportDialog(type);
            
            // Show dialog with the specified owner
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            DialogResult result = dialog.ShowDialog(wrapper);

            // If user confirmed, update the suppression mode
            if (result == DialogResult.OK)
            {
                type = dialog.SelectedMode;
                return true;
            }

            return false;
        }

        public override void VisitBlock(BlockNode node)
        {
            base.VisitBlock(node);

            if (node.SourceSpan.ContainsLine(LineNumber))
            {
                var statement = node.Statements.Where(s => s.SourceSpan.ContainsLine(LineNumber)).FirstOrDefault();
                if (statement is not null)
                {
                    GenerateChange(node);
                }


            }


        }

        protected override void OnEnterGlobalScope(ScopeContext scope, ProgramNode node)
        {
            base.OnEnterGlobalScope(scope, node);
            if (node.Imports.Count > 0)
            {
                globalNode = node.Imports.First();
            }
            else if (node.MainBlock != null)
            {
                globalNode = node.MainBlock;
            }
            else if (node.AppClass != null)
            {
                globalNode = node.AppClass;
            }
            else if (node.Interface != null)
            {
                globalNode = node.Interface;
            }
        }

        private void GenerateChange(BlockNode node)
        {
            if (Editor.LineToReports.TryGetValue(LineNumber, out var reports))
            {
                var newSuppressLine = $"/* #AppRefiner suppress ({string.Join(",", reports.Select(r => r.GetFullId()))}) */\r\n";

                if (type == SuppressReportMode.LINE)
                {
                    var startIndex = ScintillaManager.GetLineStartIndex(Editor, LineNumber);
                    if (startIndex == -1)
                    {
                        startIndex = 0;
                    }
                    InsertText(startIndex, newSuppressLine, "Add suppression comment");
                    return;
                }
                else if (type == SuppressReportMode.NEAREST_BLOCK)
                {
                    InsertText(node.SourceSpan.Start.ByteIndex, newSuppressLine, "Add suppression comment");
                    return;
				}
                else if (type == SuppressReportMode.METHOD_OR_FUNC)
                {
                    var parentNode = node.Parent;
                    /* TODO: we need to support property get/set but don't have access to their implementation node */
                    while (parentNode is not MethodNode && parentNode is not FunctionNode && parentNode is not null)
                    {
                        parentNode = parentNode?.Parent;
                    }

                    if (parentNode is null)
                    {
                        SetFailure("Unable to locate method or function start.");
                        return;
                    }
                    if (parentNode is MethodNode method && method.Implementation is not null)
                    {
                        InsertText(method.Implementation.SourceSpan.Start.ByteIndex, newSuppressLine, "Add suppression comment");
                    } else if (parentNode is FunctionNode func)
                    {
                        InsertText(func.SourceSpan.Start.ByteIndex, newSuppressLine, "Add suppression comment");
                    }

                    return;
				}
                else if (type == SuppressReportMode.GLOBAL)
                {
                    if (globalNode is not null)
                    {
						InsertText(globalNode.SourceSpan.Start.ByteIndex, newSuppressLine, "Add suppression comment");
						return;
					} else
                    {
                        SetFailure("Unable to find the start of the global scope.");
                    }
                }
                else
                {
                    SetFailure("Unable to find line to insert suppress report.");
                }
            }
            else
            {
                SetFailure("No report found at cursor position. Please place cursor on a line with a report.");
            }
        }
    }
}