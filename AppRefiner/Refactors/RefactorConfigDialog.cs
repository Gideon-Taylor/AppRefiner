using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace AppRefiner.Refactors
{
    public partial class RefactorConfigDialog : Form
    {
        private Type _refactorType;
        private Dictionary<string, Control> _propertyControls = new Dictionary<string, Control>();
        private Dictionary<string, object?> _currentConfig = new Dictionary<string, object?>();

        public RefactorConfigDialog(Type refactorType)
        {
            InitializeComponent();
            _refactorType = refactorType;
            
            // Get refactor name for dialog title
            string refactorName = _refactorType.Name;
            Text = $"Configure {refactorName} Refactor";
            
            // Load current configuration
            LoadCurrentConfiguration();
            
            // Set up the form with controls for each configurable property
            InitializePropertyControls();
        }

        private void LoadCurrentConfiguration()
        {
            try
            {
                var configJson = RefactorConfigManager.GetOrCreateRefactorConfig(_refactorType);
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson);
                
                if (config != null)
                {
                    foreach (var kvp in config)
                    {
                        _currentConfig[kvp.Key] = JsonElementToObject(kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error loading refactor configuration: {ex.Message}");
                // Continue with empty configuration
            }
        }

        private object? JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }

        private void InitializePropertyControls()
        {
            var properties = _refactorType.GetConfigurableProperties();
            
            if (properties.Count == 0)
            {
                return;
            }

            // Create a panel for the controls
            Panel panel = new Panel
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
                Label label = new Label
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
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 30),
                Location = new Point(buttonPanel.Width - 180, 10)
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
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
            StringBuilder result = new StringBuilder();
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
            // Get the current value from configuration
            _currentConfig.TryGetValue(property.Name, out object? value);
            
            if (property.PropertyType == typeof(bool))
            {
                CheckBox checkBox = new CheckBox
                {
                    Checked = value != null && (bool)value,
                    AutoSize = true
                };
                return checkBox;
            }
            else if (property.PropertyType == typeof(int))
            {
                NumericUpDown numericUpDown = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 10000,
                    Width = 200
                };
                
                // Set the value after setting Min/Max
                if (value != null)
                {
                    try
                    {
                        numericUpDown.Value = Convert.ToDecimal(value);
                    }
                    catch
                    {
                        numericUpDown.Value = 0;
                    }
                }
                
                return numericUpDown;
            }
            else if (property.PropertyType == typeof(string))
            {
                TextBox textBox = new TextBox
                {
                    Text = value?.ToString() ?? string.Empty,
                    Width = 200
                };
                return textBox;
            }
            else if (property.PropertyType.IsEnum)
            {
                ComboBox comboBox = new ComboBox
                {
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                
                foreach (var enumValue in Enum.GetValues(property.PropertyType))
                {
                    comboBox.Items.Add(enumValue);
                }
                
                // Set selected item
                if (value != null)
                {
                    try
                    {
                        var enumValue = Enum.Parse(property.PropertyType, value.ToString()!);
                        comboBox.SelectedItem = enumValue;
                    }
                    catch
                    {
                        if (comboBox.Items.Count > 0)
                            comboBox.SelectedIndex = 0;
                    }
                }
                else if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
                
                return comboBox;
            }
            
            // For unsupported types, return a disabled textbox with the string representation
            TextBox disabledTextBox = new TextBox
            {
                Text = value?.ToString() ?? string.Empty,
                Width = 200,
                Enabled = false
            };
            return disabledTextBox;
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Build configuration from controls
                var config = new Dictionary<string, object?>();
                var properties = _refactorType.GetConfigurableProperties();

                foreach (var property in properties)
                {
                    if (_propertyControls.TryGetValue(property.Name, out Control? control))
                    {
                        try
                        {
                            object? value = GetValueFromControl(control, property.PropertyType);
                            config[property.Name] = value;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error setting property {property.Name}: {ex.Message}", "Error", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }
                
                // Serialize and save the configuration
                string configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                RefactorConfigManager.UpdateRefactorConfig(_refactorType, configJson);
                
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            // RefactorConfigDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 300);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RefactorConfigDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Refactor Configuration";
            this.ResumeLayout(false);
        }
    }
}