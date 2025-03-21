using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AppRefiner.Templates;

namespace AppRefiner
{
    public class TemplateParameterDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly Panel contentPanel;
        private readonly Button applyButton;
        private readonly Button cancelButton;
        private readonly Template template;
        private readonly Dictionary<string, Control> inputControls = new();
        private readonly Dictionary<string, Label> inputLabels = new();
        private readonly Dictionary<string, DisplayCondition> inputsDisplayConditions = new();
        private readonly IntPtr owner;
        
        private const int RightMargin = 20; // Add a margin from the right edge

        public Dictionary<string, string> ParameterValues { get; private set; } = new();

        public TemplateParameterDialog(Template template, IntPtr owner)
        {
            this.template = template;
            this.owner = owner;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.contentPanel = new Panel();
            this.applyButton = new Button();
            this.cancelButton = new Button();
            
            InitializeComponent();
            GenerateParameterControls();
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
            this.headerLabel.Text = $"Template Parameters: {template.TemplateName}";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // contentPanel
            this.contentPanel.AutoScroll = true;
            this.contentPanel.Dock = DockStyle.None;
            this.contentPanel.Padding = new Padding(10);
            this.contentPanel.Location = new Point(0, this.headerPanel.Height);
            this.contentPanel.Width = 500;

            // applyButton
            this.applyButton.Text = "Apply Template";
            this.applyButton.Size = new Size(120, 30);
            this.applyButton.BackColor = Color.FromArgb(0, 122, 204);
            this.applyButton.ForeColor = Color.White;
            this.applyButton.FlatStyle = FlatStyle.Flat;
            this.applyButton.Click += ApplyButton_Click;

            // cancelButton
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Size = new Size(100, 30);
            this.cancelButton.FlatStyle = FlatStyle.Flat;
            this.cancelButton.Click += CancelButton_Click;
            this.cancelButton.BackColor = Color.FromArgb(150, 150, 150);
            this.cancelButton.ForeColor = Color.White;

            // TemplateParameterDialog
            this.Text = "Template Parameters";
            this.ClientSize = new Size(500, 400); // Initial size, will be adjusted after controls are generated
            this.Controls.Add(this.contentPanel);
            this.Controls.Add(this.applyButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;
            this.AcceptButton = this.applyButton;

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private void GenerateParameterControls()
        {
            // Clear existing controls and dictionaries
            contentPanel.Controls.Clear();
            inputControls.Clear();
            inputLabels.Clear();
            inputsDisplayConditions.Clear();

            if (template == null || template.Inputs == null || template.Inputs.Count == 0)
            {
                // Set minimum height for dialog with no parameters
                const int emptyButtonPadding = 20;
                int emptyButtonsY = this.headerPanel.Height + emptyButtonPadding;
                
                // Position buttons
                this.cancelButton.Location = new Point(this.ClientSize.Width - 230 - RightMargin, emptyButtonsY);
                this.applyButton.Location = new Point(this.ClientSize.Width - 120 - RightMargin, emptyButtonsY);
                
                // Adjust dialog height
                this.ClientSize = new Size(this.ClientSize.Width, emptyButtonsY + this.applyButton.Height + emptyButtonPadding);
                return;
            }

            const int labelWidth = 150;
            const int controlWidth = 200;
            const int verticalSpacing = 30;
            const int horizontalPadding = 10;
            int currentY = 10;

            foreach (var input in template.Inputs)
            {
                // Create label for parameter
                Label label = new()
                {
                    Text = input.Label + ":",
                    Location = new Point(horizontalPadding, currentY + 3),
                    Size = new Size(labelWidth, 20),
                    AutoSize = false,
                    Tag = input.Id // Store input ID in Tag for easier reference
                };
                contentPanel.Controls.Add(label);
                inputLabels[input.Id] = label;

                // Create input control based on parameter type
                Control inputControl;
                switch (input.Type.ToLower())
                {
                    case "boolean":
                        inputControl = new CheckBox
                        {
                            Checked = !string.IsNullOrEmpty(input.DefaultValue) &&
                                      (input.DefaultValue.ToLower() == "true" || input.DefaultValue == "1" ||
                                       input.DefaultValue.ToLower() == "yes"),
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Text = "", // No text needed since we have the label
                            Tag = input.Id // Store input ID in Tag for easier reference
                        };
                        break;

                    // Could add more types here (dropdown, etc.) in the future

                    default: // Default to TextBox for string, number, etc.
                        inputControl = new TextBox
                        {
                            Text = input.DefaultValue ?? string.Empty,
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Tag = input.Id // Store input ID in Tag for easier reference
                        };
                        break;
                }

                // Add tooltip if description is available
                if (!string.IsNullOrEmpty(input.Description))
                {
                    ToolTip tooltip = new();
                    tooltip.SetToolTip(inputControl, input.Description);
                    tooltip.SetToolTip(label, input.Description);
                }

                // Store display condition if present
                if (input.DisplayCondition != null)
                {
                    inputsDisplayConditions[input.Id] = input.DisplayCondition;
                }

                contentPanel.Controls.Add(inputControl);
                inputControls[input.Id] = inputControl;

                currentY += verticalSpacing;
            }

            // Add event handlers for controls that affect display conditions
            foreach (var kvp in inputControls)
            {
                if (kvp.Value is CheckBox checkBox)
                {
                    checkBox.CheckedChanged += (s, e) => UpdateDisplayConditions();
                }
                else if (kvp.Value is TextBox textBox)
                {
                    textBox.TextChanged += (s, e) => UpdateDisplayConditions();
                }
                // Add handlers for other control types as needed
            }

            // Initial update of display conditions
            UpdateDisplayConditions();
            
            // Set content panel height to fit all controls
            contentPanel.Height = currentY + 10;
            
            // Position buttons below the last control
            const int buttonPadding = 20;
            int buttonsY = this.headerPanel.Height + contentPanel.Height + buttonPadding;
            
            this.cancelButton.Location = new Point(this.ClientSize.Width - 230 - RightMargin, buttonsY);
            this.applyButton.Location = new Point(this.ClientSize.Width - 120 - RightMargin, buttonsY);
            
            // Adjust dialog height to fit content and buttons
            this.ClientSize = new Size(this.ClientSize.Width, buttonsY + this.applyButton.Height + buttonPadding);
        }

        private void UpdateDisplayConditions()
        {
            var currentValues = GetParameterValues();

            foreach (var condition in inputsDisplayConditions)
            {
                string inputId = condition.Key;
                bool shouldDisplay = Template.IsDisplayConditionMet(condition.Value, currentValues);

                if (inputControls.TryGetValue(inputId, out Control? control))
                {
                    control.Visible = shouldDisplay;
                }

                if (inputLabels.TryGetValue(inputId, out Label? label))
                {
                    label.Visible = shouldDisplay;
                }
            }
        }

        private Dictionary<string, string> GetParameterValues()
        {
            var values = new Dictionary<string, string>();

            foreach (var kvp in inputControls)
            {
                string value = string.Empty;

                if (kvp.Value is TextBox textBox)
                {
                    value = textBox.Text;
                }
                else if (kvp.Value is CheckBox checkBox)
                {
                    value = checkBox.Checked ? "true" : "false";
                }
                // Add other control types as needed

                values[kvp.Key] = value;
            }

            return values;
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            ParameterValues = GetParameterValues();

            if (!template.ValidateInputs(ParameterValues))
            {
                MessageBox.Show("Please fill in all required fields.", "Required Fields Missing",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            // Center on owner window
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }
            
            // Focus the first input control if available
            if (inputControls.Count > 0)
            {
                inputControls.Values.First().Focus();
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }
}
