using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Shell;
using Sitecore.Shell.Applications.ContentEditor.Gutters;
using Sitecore.Shell.Applications.ContentManager;
using Sitecore.Shell.Applications.ContentManager.Sidebars;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;

namespace Sitecore.Support.Shell.Applications.ContentManager.Sidebars
{
    public class Tree : Sitecore.Shell.Applications.ContentManager.Sidebars.Sidebar
    { 
        private int _disableEvents;

        private Item _folderItem;

        private List<GutterRenderer> _gutterRenderers;

        private Item _rootItem;

        public DataContext DataContext
        {
            get;
            set;
        }

        public Item FolderItem
        {
            get
            {
                return this._folderItem;
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                this._folderItem = value;
            }
        }

        private List<GutterRenderer> GutterRenderers
        {
            get
            {
                List<GutterRenderer> arg_18_0;
                if ((arg_18_0 = this._gutterRenderers) == null)
                {
                    arg_18_0 = (this._gutterRenderers = GutterManager.GetRenderers());
                }
                return arg_18_0;
            }
        }

        public Item RootItem
        {
            get
            {
                return this._rootItem;
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                this._rootItem = value;
            }
        }

        private StringCollection UpdateQueue
        {
            get;
            set;
        }

        public override void ChangeRoot(Item root, Item folder)
        {
            Assert.ArgumentNotNull(root, "root");
            Assert.ArgumentNotNull(folder, "folder");
            this.RootItem = root;
            this.FolderItem = folder;
            string text = this.RenderTree();
            SheerResponse.SetInnerHtml("ContentTreeInnerPanel", text);
        }

        public void DisableEvents()
        {
            this._disableEvents++;
        }

        public void EnableEvents()
        {
            this._disableEvents--;
            if (this._disableEvents < 0)
            {
                this._disableEvents = 0;
            }
        }

        public IEnumerable<Item> FilterChildren(Item currentItem)
        {
            Assert.IsNotNull(currentItem, "currentItem is null");
            IDataView expr_16 = this.DataContext.DataView;
            Assert.IsNotNull(expr_16, "dataView is null");
            return expr_16.GetChildren(currentItem, string.Empty, true, 0, 0, string.Empty).Cast<Item>();
        }

        private static string GetClassName(Item item, bool active)
        {
            if (active)
            {
                return "scContentTreeNodeActive";
            }
            if (Tree.IsItemUIStatic(item))
            {
                return "scContentTreeNodeStatic";
            }
            return "scContentTreeNodeNormal";
        }

        private string GetNodeID(string shortID)
        {
            Assert.ArgumentNotNullOrEmpty(shortID, "shortID");
            return base.ID + "_Node_" + shortID;
        }

        private static string GetStyle(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            if (item.TemplateID == TemplateIDs.TemplateField)
            {
                return string.Empty;
            }
            string text = item.Appearance.Style;
            if (string.IsNullOrEmpty(text) && (item.Appearance.Hidden || item.RuntimeSettings.IsVirtual || item.IsItemClone))
            {
                text = "color:#666666";
            }
            if (!string.IsNullOrEmpty(text))
            {
                text = " style=\"" + text + "\"";
            }
            return text;
        }

        public override void Initialize(Sitecore.Shell.Applications.ContentManager.ContentEditorForm form, Item folder, Item root)
        {
            Assert.ArgumentNotNull(form, "form");
            Assert.ArgumentNotNull(folder, "folder");
            Assert.ArgumentNotNull(root, "root");
            this.RootItem = root;
            this.FolderItem = folder;
            SiteContext expr_34 = Client.Site;
            expr_34.Notifications.ItemCopied+=(new ItemCopiedDelegate(this.ItemCopiedNotification));
            expr_34.Notifications.ItemCreated += (new ItemCreatedDelegate(this.ItemCreatedNotification));
            expr_34.Notifications.ItemDeleted += (new ItemDeletedDelegate(this.ItemDeletedNotification));
            expr_34.Notifications.ItemMoved += (new ItemMovedDelegate(this.ItemMovedNotification));
            expr_34.Notifications.ItemSaved += (new ItemSavedDelegate(this.ItemSavedNotification));
            expr_34.Notifications.ItemSortorderChanged += (new ItemSortorderChangedDelegate(this.ItemSortorderChangedNotification));
            expr_34.Notifications.ItemRenamed += (new ItemRenamedDelegate(this.ItemRenamedNotification));
            expr_34.Notifications.ItemChildrenChanged += (new ItemChildrenChangedDelegate(this.ItemChildrenChangedNotification));
            if (!Context.ClientPage.IsEvent)
            {
                System.Web.UI.Control placeholder = base.GetPlaceholder();
                if (placeholder != null)
                {
                    string text = this.RenderTree();
                    string text2 = string.Empty;
                    if (!UserOptions.ContentEditor.ShowGutter)
                    {
                        text2 = " style=\"padding:0px;background:white\"";
                    }
                    text = string.Concat(new string[]
                    {
                        "<script type=\"text/JavaScript\" language=\"javascript\">scContent.initializeTree()</script><div id=\"ContentTreeInnerPanel\" class=\"scContentTree\"",
                        text2,
                        "     onclick=\"javascript:if (window.scGeckoActivate) window.scGeckoActivate(); return scContent.onTreeClick(this, event)\"     oncontextmenu=\"javascript:return scContent.onTreeContextMenu(this, event)\"     onkeydown=\"javascript:return scContent.onTreeKeyDown(this, event)\"><div id='ContentTreeActualSize'>",
                        text,
                        "</div></div>"
                    });
                    placeholder.Controls.Add(new LiteralControl(text));
                }
            }
        }

        private static bool IsItemUIStatic(Item item)
        {
            return item[FieldIDs.UIStaticItem] == "1";
        }

        private void ItemChildrenChangedNotification(object sender, ItemChildrenChangedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                this.QueueRefresh(args.Item.Paths.LongID);
            }
        }

        private void ItemCopiedNotification(object sender, ItemCopiedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item parent = args.Copy.Parent;
                if (parent != null)
                {
                    this.QueueRefresh(parent.Paths.LongID);
                }
            }
        }

        private void ItemCreatedNotification(object sender, ItemCreatedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item parent = args.Item.Parent;
                if (parent != null)
                {
                    this.QueueRefresh(parent.Paths.LongID);
                }
            }
        }

        private void ItemDeletedNotification(object sender, ItemDeletedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item item = args.Item.Database.Items[args.ParentID];
                if (item != null)
                {
                    this.QueueRefresh(item.Paths.LongID);
                }
            }
        }

        private void ItemMovedNotification(object sender, ItemMovedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item item = args.Item.Database.Items[args.OldParentID];
                if (item != null)
                {
                    this.QueueRefresh(item.Paths.LongID);
                }
                if (args.Item.Parent != null)
                {
                    this.QueueRefresh(args.Item.Parent.Paths.LongID);
                }
            }
        }

        private void ItemRenamedNotification(object sender, ItemRenamedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item item = args.Item.Database.GetItem(args.Item.ID, args.Item.Language, args.Item.Version);
                if (item != null)
                {
                    string shortID = item.ID.ToShortID().ToString();
                    string nodeID = this.GetNodeID(shortID);
                    string text = Tree.RenderIcon(item) + item.Appearance.DisplayName;
                    SheerResponse.Eval("var c = scForm.browser.getControl(\"" + nodeID + "\"); if (c != null) c.childNodes[0].innerHTML=" + StringUtil.EscapeJavascriptString(text));
                }
            }
        }

        private void ItemSavedNotification(object sender, ItemSavedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item item = args.Item.Database.GetItem(args.Item.ID, args.Item.Language, args.Item.Version);
                if (item != null)
                {
                    string shortID = item.ID.ToShortID().ToString();
                    string nodeID = this.GetNodeID(shortID);
                    Field field = item.Fields["Blob"];
                    bool arg_C8_0 = args.Changes.IsFieldModified(FieldIDs.Icon) || (field != null && args.Changes.IsFieldModified(field));
                    bool flag = args.Changes.IsFieldModified(FieldIDs.DisplayName) && UserOptions.View.UseDisplayName;
                    if (arg_C8_0 | flag)
                    {
                        string text = Tree.RenderIcon(item) + item.Appearance.DisplayName;
                        SheerResponse.Eval("var c = scForm.browser.getControl(\"" + nodeID + "\"); if (c != null) c.childNodes[0].innerHTML=" + StringUtil.EscapeJavascriptString(text));
                    }
                }
            }
        }

        private void ItemSortorderChangedNotification(object sender, ItemSortorderChangedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (this._disableEvents == 0)
            {
                Item parent = args.Item.Parent;
                if (parent != null)
                {
                    this.QueueRefresh(parent.Paths.LongID);
                }
            }
        }

        public override bool OnDataContextChanged(DataContext context, Message message)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(message, "message");
            string queryString = WebUtil.GetQueryString("ro");
            if (string.IsNullOrEmpty(queryString))
            {
                return true;
            }
            Item folder = context.GetFolder();
            Item item = (folder != null) ? folder.Database.GetItem(queryString) : null;
            if (item == null)
            {
                return true;
            }
            if (item.ID == folder.ID || item.Axes.IsAncestorOf(folder))
            {
                return true;
            }
            context.SetRootAndFolder(item.ID.ToString(), item.Uri);
            return false;
        }

        private void QueueRefresh(string longID)
        {
            Assert.ArgumentNotNullOrEmpty(longID, "longID");
            StringCollection stringCollection = this.UpdateQueue;
            if (stringCollection == null)
            {
                stringCollection = new StringCollection();
                this.UpdateQueue = stringCollection;
            }
            else
            {
                for (int i = stringCollection.Count - 1; i >= 0; i--)
                {
                    string text = stringCollection[i];
                    if (text.StartsWith(longID, StringComparison.OrdinalIgnoreCase))
                    {
                        stringCollection.RemoveAt(i);
                    }
                    else if (longID.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
            }
            stringCollection.Add(longID);
        }

        public virtual void Refresh(ID selected)
        {
            Assert.ArgumentNotNull(selected, "selected");
            string text = this.RenderChildNodes(selected);
            string nodeID = this.GetNodeID(selected.ToShortID().ToString());
            Context.ClientPage.ClientResponse.Eval(string.Concat(new string[]
            {
                "scContent.expandTreeNode(\"",
                nodeID,
                "\", ",
                StringUtil.EscapeJavascriptString(text),
                ")"
            }));
        }

        public void RefreshItem(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string text = Tree.RenderIcon(item) + item.Appearance.DisplayName;
            SheerResponse.Eval("var c = scForm.browser.getControl(\"" + this.GetNodeID(item.ID.ToShortID().ToString()) + "\"); if (c != null) c.childNodes[0].innerHTML=" + StringUtil.EscapeJavascriptString(text));
        }

        public virtual string RenderChildNodes(ID parent)
        {
            Assert.ArgumentNotNull(parent, "parent");
            Assert.IsNotNull(this.FolderItem, "FolderItem");
            Item item = this.FolderItem.Database.GetItem(parent, this.FolderItem.Language);
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
            if (item != null)
            {
                foreach (Item current in this.FilterChildren(item))
                {
                    this.RenderTreeNode(htmlTextWriter, current, string.Empty, current.ID == this.FolderItem.ID);
                }
            }
            return htmlTextWriter.InnerWriter.ToString();
        }

        public void RenderGutter(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            if (UserOptions.ContentEditor.ShowGutter)
            {
                
                output.Write("<div id=\"Gutter" + item.ID.ToShortID() + "\" class=\"scContentTreeNodeGutter\">");
                List<GutterRenderer> gutterRenderers = this.GutterRenderers;
                if (gutterRenderers != null)
                {
                    List<WorkflowState> list = gutterRenderers.OfType<WorkflowState>().ToList<WorkflowState>();
                    if (list.Any<WorkflowState>() && !item.Access.CanWrite())
                    {
                        list.ForEach(delegate (WorkflowState e)
                        {
                            gutterRenderers.Remove(e);
                        });
                    }
                    string value = GutterManager.Render(gutterRenderers, item);
                    if (!string.IsNullOrEmpty(value))
                    {
                        output.Write(value);
                    }
                }
                output.Write("</div>");
                return;
            }
            output.Write("<div style=\"position:absolute;width:0px;height:0px\"></div>");
        }

        private static string RenderIcon(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            UrlBuilder urlBuilder = new UrlBuilder(item.Appearance.Icon);
            if (item.Paths.IsMediaItem)
            {
                urlBuilder["rev"]= item.Statistics.Revision;
                urlBuilder["la"]= item.Language.ToString();
            }
            ImageBuilder expr_5A = new ImageBuilder();
            expr_5A.Src=(HttpUtility.HtmlDecode(urlBuilder.ToString()));
            expr_5A.Width=(16);
            expr_5A.Height=(16);
            expr_5A.Class=("scContentTreeNodeIcon");
            ImageBuilder imageBuilder = expr_5A;
            if (!string.IsNullOrEmpty(item.Help.Text))
            {
                imageBuilder.Title=(item.Help.Text);
            }
            return imageBuilder.ToString();
        }

        public string RenderTree()
        {
            return this.RenderTree(true);
        }

        public virtual string RenderTree(bool showRoot)
        {
            Item parent = this.FolderItem.Parent;
            string text;
            if (this.FolderItem.ID == this.RootItem.ID || parent == null)
            {
                text = this.RenderTree(this.RootItem, this.FolderItem, null, string.Empty);
            }
            else
            {
                text = this.RenderTree(this.RootItem, parent, null, string.Empty);
            }
            if (!showRoot)
            {
                return text;
            }
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
            this.RenderTreeNode(htmlTextWriter, this.RootItem, text, this.RootItem.ID == this.FolderItem.ID);
            return htmlTextWriter.InnerWriter.ToString();
        }

        private string RenderTree(Item rootItem, Item currentItem, Item innerItem, string inner)
        {
            Assert.ArgumentNotNull(rootItem, "rootItem");
            Assert.ArgumentNotNull(currentItem, "currentItem");
            Assert.ArgumentNotNull(inner, "inner");
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
            foreach (Item current in this.FilterChildren(currentItem))
            {
                this.RenderTreeNode(htmlTextWriter, current, (innerItem != null && current.ID == innerItem.ID) ? inner : string.Empty, current.ID == this.FolderItem.ID);
            }
            if (currentItem.ID != rootItem.ID)
            {
                Item parent = currentItem.Parent;
                if (parent != null)
                {
                    return this.RenderTree(rootItem, parent, currentItem, htmlTextWriter.InnerWriter.ToString());
                }
            }
            return htmlTextWriter.InnerWriter.ToString();
        }

        private void RenderTreeNode(HtmlTextWriter output, Item item, string inner, bool active)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(inner, "inner");
            string text = item.ID.ToShortID().ToString();
            output.Write("<div class=\"scContentTreeNode\">");
            this.RenderTreeNodeGlyph(output, text, inner, item);
            this.RenderGutter(output, item);
            string nodeID = this.GetNodeID(text);
            string className = Tree.GetClassName(item, active);
            output.Write("<a hidefocus=\"true\" id=\"");
            output.Write(nodeID);
            output.Write("\" href=\"#\" class=\"" + className + "\"");
            if (!string.IsNullOrEmpty(item.Help.Text))
            {
                output.Write("title=\"");
                output.Write(StringUtil.EscapeQuote(item.Help.Text));
                output.Write("\"");
            }
            output.Write(">");
            string style = Tree.GetStyle(item);
            output.Write("<span");
            output.Write(style);
            output.Write('>');
            Tree.RenderTreeNodeIcon(output, item);
            output.Write(item.Appearance.DisplayName);
            output.Write("</span>");
            output.Write("</a>");
            if (inner.Length > 0)
            {
                output.Write("<div>");
                output.Write(inner);
                output.Write("</div>");
            }
            output.Write("</div>");
        }

        private void RenderTreeNodeGlyph(HtmlTextWriter output, string id, string inner, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNullOrEmpty(id, "id");
            Assert.ArgumentNotNull(inner, "inner");
            Assert.ArgumentNotNull(item, "item");
            ImageBuilder imageBuilder = new ImageBuilder();
            if (inner.Length > 0)
            {
                imageBuilder.Src=("images/treemenu_expanded.png");
            }
            else
            {
                bool flag;
                if (!Settings.ContentEditor.CheckHasChildrenOnTreeNodes)
                {
                    flag = true;
                }
                else
                {
                    SecurityCheck securityCheck = (SecurityCheck) (Settings.ContentEditor.CheckSecurityOnTreeNodes ? 0 : 1);
                    flag = ItemManager.HasChildren(item, securityCheck);
                }
                imageBuilder.Src=(flag ? "images/treemenu_collapsed.png" : "images/noexpand15x15.gif");
            }
            imageBuilder.Class=("scContentTreeNodeGlyph");
            imageBuilder.ID=(base.ID + "_Glyph_" + id);
            output.Write(imageBuilder.ToString());
        }

        private static void RenderTreeNodeIcon(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            output.Write(Tree.RenderIcon(item));
        }

        public override void SetActiveItem(ID itemID)
        {
            Assert.ArgumentNotNull(itemID, "itemID");
            Item item = this.FolderItem.Database.GetItem(itemID);
            StringBuilder stringBuilder = new StringBuilder();
            if (item != null)
            {
                while (item != null && item.ID != this.RootItem.ID)
                {
                    stringBuilder.Insert(0, string.Concat(new object[]
                    {
                        "/",
                        base.ID,
                        "_Node_",
                        item.ID.ToShortID()
                    }));
                    item = item.Parent;
                }
                if (item != null)
                {
                    stringBuilder.Insert(0, string.Concat(new object[]
                    {
                        "/",
                        base.ID,
                        "_Node_",
                        item.ID.ToShortID()
                    }));
                }
            }
            Context.ClientPage.ClientResponse.Eval(string.Concat(new object[]
            {
                "scContent.setActiveTreeNode('",
                base.ID,
                "_Node_",
                itemID.ToShortID(),
                "', '",
                stringBuilder,
                "', '",
                base.ID,
                "')"
            }));
        }

        public override void Update(ID selectedID, bool forceUpdate)
        {
            Assert.ArgumentNotNull(selectedID, "selectedID");
            if (this.UpdateQueue != null)
            {
                StringEnumerator enumerator = this.UpdateQueue.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    string text = StringUtil.Right(enumerator.Current, 38);
                    Item item = this.FolderItem.Database.GetItem(text);
                    if (item != null)
                    {
                        this.Refresh(item.ID);
                    }
                }
            }
            Item item2 = this.FolderItem.Database.GetItem(selectedID);
            if (item2 != null)
            {
                this.FolderItem = item2;
            }
            this.UpdateQueue = null;
        }
    }
}
