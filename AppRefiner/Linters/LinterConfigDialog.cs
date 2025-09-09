using System.Reflection;
using System.Text;

namespace AppRefiner.Linters
{
    public partial class LinterConfigDialog : Form
    {
        private BaseLintRule _linter;
        private Dictionary<string, Control> _propertyControls = new();

        public LinterConfigDialog(BaseLintRule linter)
        {
            InitializeComponent();
            _linter = linter;
            Text = $"Configure {_linter.LINTER_ID} Linter";

            // Set up the form with controls for each configurable property
            InitializePropertyControls();
        }

        private void InitializePropertyControls()
        {
            var properties = _linter.GetConfigurableProperties();

            if (properties.Count == 0)
            {
                return;
            }

            // Create a panel for the controls
            Panel panel = new()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            const int labelWidth = 150;
            const int controlWidth = 200;
            const int verticalSpacing = 30;
            const int horizontalPadding = 10;
            int currentY = 10;

            foreach (var property in properties)
            {
                // Create a label for the property
                Label label = new()
                {
                    Text = FormatPropertyName(property.Name) + ":",
                    Location = new Point(horizontalPadding, currentY + 3),
                    Size = new Size(labelWidth, 20),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                // Create an appropriate control based on the property type
                Control control = CreateControlForProperty(property);
                if (control != null)
                {
                    control.Location = new Point(horizontalPadding + labelWidth + 10, currentY);
                    control.Size = new Size(controlWidth, control is ComboBox ? 21 : 20);

                    panel.Controls.Add(label);
                    panel.Controls.Add(control);
                    _propertyControls[property.Name] = control;

                    currentY += verticalSpacing;
                }
            }

            // Add buttons at the bottom
            Panel buttonPanel = new()
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            Button okButton = new()
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 30),
                Location = new Point(buttonPanel.Width - 180, 10)
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(80, 30),
                Location = new Point(buttonPanel.Width - 90, 10)
            };

            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            // Add panels to the form
            Controls.Add(panel);
            Controls.Add(buttonPanel);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            // Set form size based on content
            int formHeight = Math.Min(500, (properties.Count * verticalSpacing) + 100);
            ClientSize = new Size(400, formHeight);

            // Adjust button positions after form size is set
            okButton.Location = new Point(ClientSize.Width - 180, 10);
            cancelButton.Location = new Point(ClientSize.Width - 90, 10);
        }

        private string FormatPropertyName(string propertyName)
        {
            // Add spaces before capital letters and capitalize the first letter
            StringBuilder result = new();
            for (int i = 0; i < propertyName.Length; i++)
            {
                if (i > 0 && char.IsUpper(propertyName[i]))
                {
                    result.Append(' ');
                }
                result.Append(i == 0 ? char.ToUpper(propertyName[i]) : propertyName[i]);
            }
            return result.ToString();
        }

        private Control CreateControlForProperty(PropertyInfo property)
        {
            object? value = property.GetValue(_linter);

            if (property.PropertyType == typeof(bool))
            {
                CheckBox checkBox = new()
                {
                    Checked = value != null && (bool)value,
                    AutoSize = true
                };
                return checkBox;
            }
            else if (property.PropertyType == typeof(int))
            {
                NumericUpDown numericUpDown = new()
                {
                    Minimum = 0,
                    Maximum = 10000, // Increased maximum to accommodate larger values
                    Width = 200
                };

                // Set the value after setting Min/Max
                if (value != null)
                {
                    numericUpDown.Value = (int)value;
                }

                return numericUpDown;
            }
            else if (property.PropertyType == typeof(string))
            {
                TextBox textBox = new()
                {
                    Text = value?.ToString() ?? string.Empty,
                    Width = 200
                };
                return textBox;
            }
            else if (property.PropertyType.IsEnum)
            {
                ComboBox comboBox = new()
                {
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };

                foreach (var enumValue in Enum.GetValues(property.PropertyType))
                {
                    comboBox.Items.Add(enumValue);
                }

                comboBox.SelectedItem = value;
                return comboBox;
            }

            // For unsupported types, return a disabled textbox with the string representation
            TextBox disabledTextBox = new()
            {
                Text = value?.ToString() ?? string.Empty,
                Width = 200,
                Enabled = false
            };
            return disabledTextBox;
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            // Update the linter properties from the controls
            foreach (var property in _linter.GetConfigurableProperties())
            {
                if (_propertyControls.TryGetValue(property.Name, out Control? control))
                {
                    try
                    {
                        object? value = GetValueFromControl(control, property.PropertyType);
                        property.SetValue(_linter, value);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error setting property {property.Name}: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            // Save the updated configuration
            LinterConfigManager.UpdateLinterConfig(_linter);

            DialogResult = DialogResult.OK;
            Close();
        }

        private object? GetValueFromControl(Control control, Type propertyType)
        {
            if (control is CheckBox checkBox)
            {
                return checkBox.Checked;
            }
            else if (control is NumericUpDown numericUpDown)
            {
                if (propertyType == typeof(int))
                    return (int)numericUpDown.Value;
                return numericUpDown.Value;
            }
            else if (control is TextBox textBox)
            {
                return textBox.Text;
            }
            else if (control is ComboBox comboBox && propertyType.IsEnum)
            {
                return comboBox.SelectedItem;
            }

            return null;
        }

        // Designer-generated code
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // LinterConfigDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 300);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LinterConfigDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Linter Configuration";
            this.ResumeLayout(false);
        }
    }
}
