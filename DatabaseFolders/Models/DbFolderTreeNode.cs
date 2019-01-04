using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using System;
using System.Drawing;

namespace DatabaseFolders.Models
{
    internal class DbFolderTreeNode : HierarchyTreeNode, INodeWithMenu, IServiceProvider
    {
        readonly object _parent;

        public DbFolderTreeNode(object o)
        {
            _parent = o;
        }

        public override Icon Icon => (_parent as INodeWithIcon)?.Icon;

        public override Icon SelectedIcon => (_parent as INodeWithIcon)?.SelectedIcon;

        public override bool ShowPolicyHealthState
        {
            get => false;
            set
            {
                //throw new NotImplementedException();
            }
        }

        public override int State => (_parent as INodeWithIcon)?.State ?? 0;


        public object GetService(Type serviceType)
        {
            return (_parent as IServiceProvider)?.GetService(serviceType);
        }


        public void DoDefaultAction()
        {
            (_parent as INodeWithMenu)?.DoDefaultAction();
        }

        public void ShowContextMenu(Point screenPos)
        {
            (_parent as INodeWithMenu)?.ShowContextMenu(screenPos);
        }

    }
}
