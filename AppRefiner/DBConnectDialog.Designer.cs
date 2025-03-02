namespace AppRefiner
{
    partial class DBConnectDialog
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
            cmbDBType = new ComboBox();
            cmbDBName = new ComboBox();
            txtUserName = new TextBox();
            txtPassword = new MaskedTextBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            btnConnect = new Button();
            SuspendLayout();
            // 
            // cmbDBType
            // 
            cmbDBType.FormattingEnabled = true;
            cmbDBType.Items.AddRange(new object[] { "Oracle" });
            cmbDBType.Location = new Point(72, 12);
            cmbDBType.Name = "cmbDBType";
            cmbDBType.Size = new Size(171, 23);
            cmbDBType.TabIndex = 0;
            cmbDBType.SelectedIndexChanged += cmbDBType_SelectedIndexChanged;
            // 
            // cmbDBName
            // 
            cmbDBName.FormattingEnabled = true;
            cmbDBName.Location = new Point(72, 41);
            cmbDBName.Name = "cmbDBName";
            cmbDBName.Size = new Size(171, 23);
            cmbDBName.TabIndex = 1;
            // 
            // txtUserName
            // 
            txtUserName.Location = new Point(72, 70);
            txtUserName.Name = "txtUserName";
            txtUserName.Size = new Size(171, 23);
            txtUserName.TabIndex = 2;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(72, 99);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(171, 23);
            txtPassword.TabIndex = 3;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 15);
            label1.Name = "label1";
            label1.Size = new Size(49, 15);
            label1.TabIndex = 4;
            label1.Text = "DB Type";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(6, 44);
            label2.Name = "label2";
            label2.Size = new Size(57, 15);
            label2.TabIndex = 5;
            label2.Text = "DB Name";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 76);
            label3.Name = "label3";
            label3.Size = new Size(33, 15);
            label3.TabIndex = 6;
            label3.Text = "User:";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 102);
            label4.Name = "label4";
            label4.Size = new Size(60, 15);
            label4.TabIndex = 7;
            label4.Text = "Password:";
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(92, 141);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(75, 23);
            btnConnect.TabIndex = 8;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // DBConnectDialog
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(272, 176);
            Controls.Add(btnConnect);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(txtPassword);
            Controls.Add(txtUserName);
            Controls.Add(cmbDBName);
            Controls.Add(cmbDBType);
            Name = "DBConnectDialog";
            Text = "Connect to DB";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox cmbDBType;
        private ComboBox cmbDBName;
        private TextBox txtUserName;
        private MaskedTextBox txtPassword;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Button btnConnect;
    }
}