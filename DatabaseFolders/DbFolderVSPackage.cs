using DatabaseFolders.Services;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DatabaseFolders
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(DbFolderVSPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution)]
    public sealed class DbFolderVSPackage : Package
    {
        /// <summary>
        /// DatabaseFolderVSPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "abf4426e-91da-4791-8ca8-f01b694095d8";

        public const string DbFoldersTagString = "DbFolders";
        public const string DbFolderIconKey = "DbFolderIcon";
        private ObjectExplorerService _objectExplorerService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbFolderVSPackage"/> class.
        /// </summary>
        public DbFolderVSPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override void Initialize()
        {
            base.Initialize();
            DbFolderCommand.Initialize(this);

            this._objectExplorerService = new ObjectExplorerService();
            AttachTreeViewEvents();

            AddSkipLoading();
        }
        #endregion

        #region Private methods
        private void AddSkipLoading()
        {
            var timer = new Timer();
            timer.Interval = 2000;
            timer.Tick += (sender, args) =>
            {
                timer.Stop();

                var myPackage = UserRegistryRoot.CreateSubKey(@"Packages\{" + PackageGuidString + "}");
                myPackage?.SetValue("SkipLoading", 1);
            };
            timer.Start();
        }

        private void AttachTreeViewEvents()
        {
            var runCount = 1;
            var timer = new Timer();
            timer.Interval = 500;
            timer.Tick += (sender, args) =>
            {
                try
                {
                    var treeView = GetObjectExplorerTreeView();
                    if (treeView != null)
                    {
                        // attach event to the treeview
                        treeView.BeforeExpand += new TreeViewCancelEventHandler(ObjectExplorerTreeViewBeforeExpandCallback);
                        treeView.AfterExpand += new TreeViewEventHandler(ObjectExplorerTreeViewAfterExpandCallback);

                        // add icon
                        if (treeView.ImageList.Images[DbFolderIconKey] == null)
                        {
                            treeView.ImageList.Images.Add(DbFolderIconKey, PackageResources.folder_database);
                        }

                        // stop timer, we only need to run once
                        timer.Stop();
                    }
                }
                catch (Exception ex)
                {
                    // sometime the time to wait for ssms initialize is long, hence exception can be thrown
                    // all we need is to wait here
                    // maximum wait time is 10 seconds
                    runCount++;

                    if (runCount == 20)
                    {
                        timer.Stop();
                    }
                }
                
            };
            timer.Start();
        }

        private TreeView GetObjectExplorerTreeView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var objectExplorerService = (IObjectExplorerService)this.GetService(typeof(IObjectExplorerService));
            if (objectExplorerService != null)
            {
                var oesTreeProperty = objectExplorerService.GetType().GetProperty("Tree", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (oesTreeProperty != null)
                    return (TreeView)oesTreeProperty.GetValue(objectExplorerService, null);
            }

            return null;
        }

        private void ObjectExplorerTreeViewAfterExpandCallback(object sender, TreeViewEventArgs e)
        {
            // Wait for the async node expand to finish or we could miss nodes
            try
            {
                if (this._objectExplorerService.GetNodeExpanding(e.Node))
                {
                    e.Node.TreeView.Cursor = Cursors.AppStarting;

                    var nodeExpanding = new Timer();
                    nodeExpanding.Interval = 10;
                    EventHandler nodeExpandingEvent = null;
                    nodeExpandingEvent = (object o, EventArgs e2) =>
                    {
                        if (!this._objectExplorerService.GetNodeExpanding(e.Node))
                        {
                            nodeExpanding.Tick -= nodeExpandingEvent;
                            nodeExpanding.Stop();
                            nodeExpanding.Dispose();

                            ReorganizeFolders(e.Node, true);

                            e.Node.TreeView.Cursor = Cursors.Default;
                        }
                    };
                    nodeExpanding.Tick += nodeExpandingEvent;
                    nodeExpanding.Start();

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void ObjectExplorerTreeViewBeforeExpandCallback(object sender, TreeViewCancelEventArgs e)
        {
            try
            {
                if (e.Node.GetNodeCount(false) == 1)
                    return;

                if (this._objectExplorerService.GetNodeExpanding(e.Node))
                {
                    ReorganizeFolders(e.Node);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void ReorganizeFolders(TreeNode node, bool expand = false)
        {
            try
            {
                // uses node.Tag to prevent this running again on already orgainsed db folder
                if (node?.Parent != null && (node.Tag == null || node.Tag.ToString() != DbFoldersTagString))
                {
                    var urnPath = this._objectExplorerService.GetNodeUrnPath(node);
                    if (!string.IsNullOrEmpty(urnPath))
                    {
                        switch (urnPath)
                        {
                            case "Server/DatabasesFolder":
                                var settings = SettingService.GetSettings();

                                var dbFolderCount = this._objectExplorerService.ReorganizeDatabaseNodes(node, DbFoldersTagString, settings);
                                if (expand && dbFolderCount == 1)
                                {
                                    node.LastNode.Expand();
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        
        #endregion
    }
}
