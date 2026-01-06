namespace AppRefiner.Dialogs
{
    partial class TypeExtensionInspectDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.methodsGroupBox = new System.Windows.Forms.GroupBox();
            this.methodsDataGridView = new System.Windows.Forms.DataGridView();
            this.propertiesGroupBox = new System.Windows.Forms.GroupBox();
            this.propertiesDataGridView = new System.Windows.Forms.DataGridView();
            this.closeButton = new System.Windows.Forms.Button();
            this.methodsGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.methodsDataGridView)).BeginInit();
            this.propertiesGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.propertiesDataGridView)).BeginInit();
            this.SuspendLayout();
            //
            // methodsGroupBox
            //
            this.methodsGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.methodsGroupBox.Controls.Add(this.methodsDataGridView);
            this.methodsGroupBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.methodsGroupBox.Location = new System.Drawing.Point(12, 12);
            this.methodsGroupBox.Name = "methodsGroupBox";
            this.methodsGroupBox.Size = new System.Drawing.Size(760, 250);
            this.methodsGroupBox.TabIndex = 0;
            this.methodsGroupBox.TabStop = false;
            this.methodsGroupBox.Text = "Methods";
            //
            // methodsDataGridView
            //
            this.methodsDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.methodsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.methodsDataGridView.Location = new System.Drawing.Point(6, 22);
            this.methodsDataGridView.Name = "methodsDataGridView";
            this.methodsDataGridView.RowTemplate.Height = 25;
            this.methodsDataGridView.Size = new System.Drawing.Size(748, 222);
            this.methodsDataGridView.TabIndex = 0;
            //
            // propertiesGroupBox
            //
            this.propertiesGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertiesGroupBox.Controls.Add(this.propertiesDataGridView);
            this.propertiesGroupBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.propertiesGroupBox.Location = new System.Drawing.Point(12, 268);
            this.propertiesGroupBox.Name = "propertiesGroupBox";
            this.propertiesGroupBox.Size = new System.Drawing.Size(760, 250);
            this.propertiesGroupBox.TabIndex = 1;
            this.propertiesGroupBox.TabStop = false;
            this.propertiesGroupBox.Text = "Properties";
            //
            // propertiesDataGridView
            //
            this.propertiesDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertiesDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.propertiesDataGridView.Location = new System.Drawing.Point(6, 22);
            this.propertiesDataGridView.Name = "propertiesDataGridView";
            this.propertiesDataGridView.RowTemplate.Height = 25;
            this.propertiesDataGridView.Size = new System.Drawing.Size(748, 222);
            this.propertiesDataGridView.TabIndex = 0;
            //
            // closeButton
            //
            this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.closeButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.closeButton.Location = new System.Drawing.Point(697, 524);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(75, 28);
            this.closeButton.TabIndex = 2;
            this.closeButton.Text = "Close";
            this.closeButton.UseVisualStyleBackColor = true;
            this.closeButton.Click += new System.EventHandler(this.CloseButton_Click);
            //
            // TypeExtensionInspectDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.propertiesGroupBox);
            this.Controls.Add(this.methodsGroupBox);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "TypeExtensionInspectDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Inspect Type Extension";
            this.methodsGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.methodsDataGridView)).EndInit();
            this.propertiesGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.propertiesDataGridView)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox methodsGroupBox;
        private System.Windows.Forms.DataGridView methodsDataGridView;
        private System.Windows.Forms.GroupBox propertiesGroupBox;
        private System.Windows.Forms.DataGridView propertiesDataGridView;
        private System.Windows.Forms.Button closeButton;
    }
}
