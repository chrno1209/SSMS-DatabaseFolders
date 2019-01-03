using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using IServiceProvider = System.IServiceProvider;
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
                        treeView.BeforeExpand += new TreeViewCancelEventHandler(ObjectExplorerTreeViewBeforeExpandCallback);
                        treeView.AfterExpand += new TreeViewEventHandler(ObjectExplorerTreeViewAfterExpandCallback);
                        timer.Stop();
                    }
                }
                catch (Exception ex)
                {
                    runCount++;

                    if (runCount == 10)
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
                if (GetNodeExpanding(e.Node))
                {
                    e.Node.TreeView.Cursor = Cursors.AppStarting;

                    var nodeExpanding = new Timer();
                    nodeExpanding.Interval = 10;
                    EventHandler nodeExpandingEvent = null;
                    nodeExpandingEvent = (object o, EventArgs e2) =>
                    {
                        if (!GetNodeExpanding(e.Node))
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

                if (GetNodeExpanding(e.Node))
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
                if (node != null && node.Parent != null && (node.Tag == null || node.Tag.ToString() != DbFoldersTagString))
                {
                    var urnPath = GetNodeUrnPath(node);
                    if (!string.IsNullOrEmpty(urnPath))
                    {
                        switch (urnPath)
                        {
                            case "Server/DatabasesFolder":
                                var settings = GetSettings();

                                var dbFolderCount = ReorganizeDatabaseNodes(node, DbFoldersTagString, PackageResources.folder_database, settings);
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

        private Dictionary<string, List<string>> GetSettings()
        {
            var settings = new Dictionary<string, List<string>>();
            var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var path = Path.Combine(Path.GetDirectoryName(location), "Settings.json");

            if (!File.Exists(path))
            {
                using (var writer = File.CreateText(path))
                {
                    var newSettings = JObject.Parse("{ \"folders\": {} }");
                    writer.Write(newSettings.ToString(Newtonsoft.Json.Formatting.Indented));
                }

                return settings;
            }

            var json = JObject.Load(
                new JsonTextReader(File.OpenText(path)));

            if (json["folders"] != null)
            {
                var folders = (JObject)json["folders"];
                foreach (JProperty folder in folders.Properties())
                {
                    var data = new List<string>();
                    if (folder.Value is JArray)
                    {
                        data = ((JArray)folder.Value).Values<string>().ToList();
                    }

                    settings.Add(folder.Name, data);
                }
            }

            return settings;
        }

        private bool GetNodeExpanding(TreeNode node)
        {
            var lazyNode = node as ILazyLoadingNode;
            if (lazyNode != null)
                return lazyNode.Expanding;
            else
                return false;
        }

        private string GetNodeUrnPath(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
                return ni.UrnPath;
            else
                return null;
        }

        private INodeInformation GetNodeInformation(TreeNode node)
        {
            INodeInformation result = null;
            IServiceProvider serviceProvider = node as IServiceProvider;
            if (serviceProvider != null)
            {
                result = (serviceProvider.GetService(typeof(INodeInformation)) as INodeInformation);
            }
            return result;
        }

        private readonly string[] SystemDatabaseFolders = new[] { "System Databases", "Database Snapshots" };
        private readonly string[] IgnorePatterns = new[] { "*" };
        private string GetDatabaseFolder(TreeNode node, Dictionary<string, List<string>> settings)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
            {
                if (!SystemDatabaseFolders.Contains(node.Text))
                {
                    var folder = string.Empty;
                    foreach (var setting in settings)
                    {
                        foreach (var pattern in setting.Value)
                        {
                            if (string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(pattern) && !IgnorePatterns.Contains(pattern) && Regex.IsMatch(node.Text, pattern, RegexOptions.Singleline & RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5)))
                            {
                                folder = setting.Key;
                                break;
                            }
                        }
                    }

                    return folder;
                }
            }
            return null;
        }

        public int ReorganizeDatabaseNodes(TreeNode node, string nodeTag, Icon icon, Dictionary<string, List<string>> settings)
        {
            if (node.Nodes.Count <= 1)
                return 0;

            node.TreeView.BeginUpdate();

            //can't move nodes while iterating forward over them
            //create list of nodes to move then perform the update

            var dbFolders = new Dictionary<string, List<TreeNode>>();
            var folderTreeNodeToRender = new List<TreeNode>();

            foreach (TreeNode childNode in node.Nodes)
            {
                //skip folder node folders but make sure they are in folders list
                if (childNode.Tag != null && childNode.Tag.ToString() == nodeTag)
                {
                    if (!dbFolders.ContainsKey(childNode.Name))
                        dbFolders.Add(childNode.Name, new List<TreeNode>());

                    continue;
                }
                var folder = GetDatabaseFolder(childNode, settings);

                if (string.IsNullOrEmpty(folder))
                    continue;

                //create folder node
                if (folderTreeNodeToRender.All(s => s.Name != folder) && !node.Nodes.ContainsKey(folder))
                {
                    TreeNode folderNode = new DbFolderTreeNode(node, icon);
                    folderNode.Name = folder;
                    folderNode.Text = folder;
                    folderNode.Tag = nodeTag;

                    if (icon == null)
                    {
                        folderNode.ImageIndex = node.ImageIndex;
                        folderNode.SelectedImageIndex = node.ImageIndex;
                    }

                    folderTreeNodeToRender.Add(folderNode);
                }

                //add node to folder list
                if (!dbFolders.TryGetValue(folder, out var folderNodeList))
                {
                    folderNodeList = new List<TreeNode>();
                    dbFolders.Add(folder, folderNodeList);
                }
                folderNodeList.Add(childNode);
            }

            foreach (var folderNode in folderTreeNodeToRender)
            {
                node.Nodes.Add(folderNode);
            }

            //move nodes to folder node
            foreach (string folder in dbFolders.Keys)
            {
                var folderNode = node.Nodes[folder];
                foreach (TreeNode childNode in dbFolders[folder])
                {
                    node.Nodes.Remove(childNode);
                    folderNode.Nodes.Add(childNode);
                }
            }

            node.TreeView.EndUpdate();

            return dbFolders.Count;
        }
        #endregion
    }
}
