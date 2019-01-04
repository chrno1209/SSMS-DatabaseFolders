using DatabaseFolders.Models;
using DatabaseFolders.Services;
using System;
using System.Windows.Forms;

namespace DatabaseFolders.Editors
{
    public partial class AddFolder : Form
    {
        public DbFolderSetting FolderSettingModel { get; set; }
        public bool IsSaved { get; set; }

        public AddFolder()
        {
            InitializeComponent();
        }

        public AddFolder(DbFolderSetting model)
        {
            InitializeComponent();

            this.FolderSettingModel = model;
        }

        private void AddFolder_Load(object sender, EventArgs e)
        {
            if (FolderSettingModel != null)
            {
                txtFolderName.Text = FolderSettingModel.FolderName;
                txtPatterns.Text = string.Join("\n", FolderSettingModel.Patterns ?? new string[0]);

                this.Text = "Edit Folder";
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            errorProvider1.Clear();

            if (string.IsNullOrEmpty(txtFolderName.Text))
            {
                errorProvider1.SetError(txtFolderName, "Folder name cannot be empty");
                return;
            }

            if (string.IsNullOrEmpty(txtPatterns.Text))
            {
                errorProvider1.SetError(txtPatterns, "Folder patterns cannot be empty");
                return;
            }

            var isEdit = this.FolderSettingModel != null;
            var originName = isEdit ? this.FolderSettingModel.FolderName : null;

            this.FolderSettingModel = new DbFolderSetting()
            {
                FolderName = txtFolderName.Text.Trim(),
                Patterns = txtPatterns.Text.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
            };

            if (isEdit)
                SettingService.EditSetting(originName, FolderSettingModel);
            else
                SettingService.AddSetting(this.FolderSettingModel);

            this.IsSaved = true;
            this.Close();
        }
    }
}
