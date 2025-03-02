using AppRefiner.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppRefiner
{
    public partial class DBConnectDialog : Form
    {
        public IDataManager? DataManager;

        public DBConnectDialog()
        {
            InitializeComponent();
        }

        private void cmbDBType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbDBType.SelectedItem is string dbType)
            {
                switch (dbType)
                {
                    case "Oracle":
                        var tnsNames = OracleDbConnection.GetAllTnsNames();
                        cmbDBName.Items.Clear();
                        cmbDBName.Items.AddRange(tnsNames.ToArray());
                        break;

                }
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (cmbDBType.SelectedItem is string dbType && cmbDBName.SelectedItem is string dbName)
            {
                var connectionString = $"Data Source={dbName};User Id={txtUserName.Text};Password={txtPassword.Text};";
                switch (dbType)
                {
                    case "Oracle":
                        DataManager = new OraclePeopleSoftDataManager(connectionString);
                        break;
                }
                if (DataManager.Connect())
                {
                    MessageBox.Show("Connected to database", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to connect to database", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
