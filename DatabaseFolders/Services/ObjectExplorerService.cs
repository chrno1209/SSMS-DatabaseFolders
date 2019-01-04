using DatabaseFolders.Helpers;
using DatabaseFolders.Models;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DatabaseFolders.Services
{
    public class ObjectExplorerService
    {
        private readonly string[] SystemDatabaseFolders = new[] { "System Databases", "Database Snapshots" };
        private readonly string[] IgnorePatterns = new[] { "*" };

        public bool GetNodeExpanding(TreeNode node)
        {
            if (node is ILazyLoadingNode lazyNode)
                return lazyNode.Expanding;
            else
                return false;
        }

        public string GetNodeUrnPath(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            return ni?.UrnPath;
        }

        public INodeInformation GetNodeInformation(TreeNode node)
        {
            INodeInformation result = null;
            if (node is IServiceProvider serviceProvider)
            {
                result = (serviceProvider.GetService(typeof(INodeInformation)) as INodeInformation);
            }
            return result;
        }

        public string GetDatabaseFolderName(TreeNode node, List<DbFolderSetting> settings)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
            {
                if (!SystemDatabaseFolders.Contains(node.Text))
                {
                    var folder = string.Empty;
                    foreach (var setting in settings)
                    {
                        foreach (var pattern in setting.Patterns)
                        {
                            if (string.IsNullOrEmpty(folder) && 
                                !string.IsNullOrEmpty(pattern) && !IgnorePatterns.Contains(pattern) && 
                                //Regex.IsMatch(node.Text, pattern, RegexOptions.Singleline & RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5))
                                node.Text.Like(pattern)
                            )
                            {
                                folder = setting.FolderName;
                                break;
                            }
                        }
                    }

                    return folder;
                }
            }

            return null;
        }


        public int ReorganizeDatabaseNodes(TreeNode node, string nodeTag, List<DbFolderSetting> settings)
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
                var folder = GetDatabaseFolderName(childNode, settings);

                if (string.IsNullOrEmpty(folder))
                    continue;

                //create folder node
                if (folderTreeNodeToRender.All(s => s.Name != folder) && !node.Nodes.ContainsKey(folder))
                {
                    TreeNode folderNode = new DbFolderTreeNode(node);
                    folderNode.Name = folder;
                    folderNode.Text = folder;
                    folderNode.Tag = nodeTag;

                    folderNode.ImageKey = DbFolderVSPackage.DbFolderIconKey;
                    folderNode.SelectedImageKey = DbFolderVSPackage.DbFolderIconKey;

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

            //render db folder
            foreach (var folderNode in folderTreeNodeToRender.OrderBy(s => s.Text))
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
    }
}
