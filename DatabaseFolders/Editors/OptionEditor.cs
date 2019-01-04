using DatabaseFolders.Models;
using DatabaseFolders.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace DatabaseFolders.Editors
{
    public partial class OptionEditor : Form
    {
        private List<DbFolderSetting> _cachedFolderSettings;

        public OptionEditor()
        {
            InitializeComponent();
        }

        private void OptionEditor_Load(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = SettingService.GetSettings();

            this._cachedFolderSettings = settings;

            var dt = new DataTable("SettingTable");
            dt.Columns.Add("Name");
            dt.Columns.Add("Patterns");

            settings.ForEach(s => dt.LoadDataRow(new string[] { s.FolderName, string.Join("\n", s.Patterns ?? new string[0]) }, LoadOption.OverwriteChanges));

            gridData.DataSource = dt;
            gridData.ResetBindings();
        }

        private void btnAddFolder_Click(object sender, EventArgs e)
        {
            var addDialog = new AddFolder();
            addDialog.ShowDialog(this);

            if (addDialog.IsSaved)
            {
                LoadSettings();
            }
        }

        private void btnDeleteFolder_Click(object sender, EventArgs e)
        {
            if (gridData.SelectedRows.Count > 0)
            {
                if (MessageBox.Show("Are you sure to delete this folder?", "Database Folder Deletion",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    var currentRow = gridData.SelectedRows[0];
                    SettingService.DeleteSetting(currentRow.Cells[0].Value as string);

                    LoadSettings();
                }
            }
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            if (gridData.SelectedRows.Count > 0)
            {
                var currentRow = gridData.SelectedRows[0];
                var currentRowIndex = currentRow.Index;

                SettingService.MoveUp(currentRow.Cells[0].Value as string);
                LoadSettings();

                if (currentRowIndex > 0)
                {
                    gridData.Rows[currentRowIndex - 1].Selected = true;
                }
            }
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            if (gridData.SelectedRows.Count > 0)
            {
                var currentRow = gridData.SelectedRows[0];
                var currentRowIndex = currentRow.Index;

                SettingService.MoveDown(currentRow.Cells[0].Value as string);
                LoadSettings();

                if (currentRowIndex < gridData.RowCount - 1)
                    gridData.Rows[currentRowIndex + 1].Selected = true;
                else if (currentRowIndex == gridData.RowCount - 1)
                    gridData.Rows[currentRowIndex].Selected = true;
            }
        }

        private void gridData_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (gridData.SelectedRows.Count > 0)
            {
                var currentRow = gridData.SelectedRows[0];
                var currentSetting = this._cachedFolderSettings.SingleOrDefault(s => s.FolderName.Equals(currentRow.Cells[0].Value));
                if (currentSetting != null)
                {
                    var editDialog = new AddFolder(currentSetting);
                    editDialog.ShowDialog(this);

                    if (editDialog.IsSaved)
                    {
                        LoadSettings();
                    }
                }
            }
        }
    }
}
