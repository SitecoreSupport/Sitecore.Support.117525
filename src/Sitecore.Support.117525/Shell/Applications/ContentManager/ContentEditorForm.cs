using Sitecore.Data;
using Sitecore.Data.Clones;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Layouts;
using Sitecore.Reflection;
using Sitecore.Shell.Applications.ContentEditor;
using Sitecore.Shell.Applications.ContentEditor.Gutters;
using Sitecore.Shell.Applications.ContentManager;
using Sitecore.Shell.Applications.ContentManager.Sidebars;
using Sitecore.Shell.Framework;
using Sitecore.Support.Shell.Applications.ContentManager.Sidebars;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using System;
using System.Web;
using System.Web.UI;

namespace Sitecore.Support.Shell.Applications.ContentManager
{
    public class ContentEditorForm : Sitecore.Shell.Applications.ContentManager.ContentEditorForm
    {
        private bool _hasPendingUpdate;

        private ItemUri _pendingUpdateItemUri;

        private string FrameName
        {
            get
            {
                return base.ServerProperties["FrameName"] as string;
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                base.ServerProperties["FrameName"] = value;
            }
        }

        private bool HasPendingUpdate
        {
            get
            {
                return this._hasPendingUpdate;
            }
            set
            {
                this._hasPendingUpdate = value;
            }
        }

        private ItemUri PendingUpdateItemUri
        {
            get
            {
                return this._pendingUpdateItemUri;
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                this._pendingUpdateItemUri = value;
            }
        }

        private static void UpdateGutter(Item folder)
        {
            Assert.ArgumentNotNull(folder, "folder");
            string text = GutterManager.Render(GutterManager.GetRenderers(), folder);
            SheerResponse.SetInnerHtml("Gutter" + folder.ID.ToShortID(), text);
        }

        private void UpdateGutter(string id)
        {
            Assert.ArgumentNotNullOrEmpty(id, "id");
            Item item;
            Item item2;
            this.ContentEditorDataContext.GetState(out item, out item2);
            Item item3 = item2.Database.GetItem(id);
            if (item3 != null)
            {
                ContentEditorForm.UpdateGutter(item3);
            }
        }

        private void CheckModified(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            message.Result=(false);
            foreach (FieldInfo fieldInfo in base.FieldInfo.Values)
            {
                System.Web.UI.Control control = Context.ClientPage.FindSubControl(fieldInfo.ID);
                if (control != null)
                {
                    string text;
                    if (control is IContentField)
                    {
                        text = (control as IContentField).GetValue();
                    }
                    else
                    {
                        text = ReflectionUtil.GetProperty(control, "Value").ToString();
                    }
                    string a = fieldInfo.Type.ToLowerInvariant();
                    if (a == "html" || a == "rich text")
                    {
                        text = XHtml.Convert(text);
                    }
                    if (Crc.CRC(text) != fieldInfo.Crc)
                    {
                        message.Result=(true);
                        break;
                    }
                }
            }
        }

        private Item GetCurrentItem(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string text = message["id"];
            string text2 = message["la"];
            string text3 = message["vs"];
            Item folder = this.ContentEditorDataContext.GetFolder();
            if (folder == null)
            {
                return null;
            }
            if (string.IsNullOrEmpty(text))
            {
                return folder;
            }
            if (text2 != null && text3 != null)
            {
                return Client.ContentDatabase.GetItem(text, Language.Parse(text2),Sitecore.Data.Version.Parse(text3));
            }
            return Client.ContentDatabase.GetItem(text, folder.Language);
        }

        private bool CheckIfMessageCanBeHandled(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string name = message.Name;
            return name == "event:click" || name == "datacontext:changed" || name == "item:refreshchildren" || Context.ContentDatabase == null || Context.ContentDatabase.GetItem(ID.Parse(this.ContentEditorDataContext.Folder)) != null;
        }

        private void AcceptNotification(Message message, Item item)
        {
            Notification notification = this.GetNotification(message, item);
            if (notification != null && item != null)
            {
                notification.Accept(item);
            }
        }

        private Notification CreateItemVersionNotification(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            ItemVersionNotification itemVersionNotification = new ItemVersionNotification();
            ID iD;
            if (!ID.TryParse(message["id"], out iD))
            {
                return null;
            }
            Language language;
            if (!Language.TryParse(message["language"], out language))
            {
                return null;
            }
            Sitecore.Data.Version version;
            if (!Sitecore.Data.Version.TryParse(message["version"], out version))
            {
                return null;
            }
            string text = message["database"];
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            itemVersionNotification.VersionUri=(new ItemUri(iD, iD.ToString(), language, version, text));
            return itemVersionNotification;
        }

        private void RejectNotification(Message message, Item item)
        {
            Notification notification = this.GetNotification(message, item);
            if (notification != null && item != null)
            {
                notification.Reject(item);
            }
        }

        private Notification GetNotification(Message message, Item item)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.ArgumentNotNull(item, "item");
            if (item == null)
            {
                return null;
            }
            if (item.Database.NotificationProvider == null)
            {
                return null;
            }
            string text = message["notification"];
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            if (string.Compare(text, "itemversionnotification", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return this.CreateItemVersionNotification(message);
            }
            ID iD;
            if (!ID.TryParse(text, out iD))
            {
                return null;
            }
            return item.Database.NotificationProvider.GetNotification(iD);
        }

        private bool IsEditing(string id)
        {
            Assert.ArgumentNotNull(id, "id");
            Item folder = this.ContentEditorDataContext.GetFolder();
            return folder != null && !(folder.ID.ToString() != id) && folder.State.CanSave() && !MainUtil.GetBool(Context.ClientPage.ServerProperties["ContentReadOnly"], true);
        }

        
        private void RefreshTreeNode(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string text = message["type"];
            if (!string.IsNullOrEmpty(text) && text == "attachment")
            {
                Sidebars.Tree tree = base.Sidebar as Sidebars.Tree;
                Attachment attachment = message.Sender as Attachment;
                if (attachment != null && tree != null)
                {
                    ID iD = ID.Parse(attachment.ItemID);
                    Item folder = this.ContentEditorDataContext.GetFolder();
                    if (folder != null)
                    {
                        Item item = folder.Database.GetItem(iD, folder.Language);
                        if (item != null)
                        {
                            tree.RefreshItem(item);
                        }
                    }
                }
            }
        }

        protected void Tree_Refresh(string id)
        {
            Assert.ArgumentNotNull(id, "id");
            Item folder = this.ContentEditorDataContext.GetFolder();
            if (folder != null)
            {
                ID iD = ID.Parse(ContentEditorForm.GetID(id));
                Sidebars.Tree tree = base.Sidebar as Sidebars.Tree;
                Assert.IsNotNull(tree, typeof(Sidebars.Tree));
                Item item = folder.Database.GetItem(iD, folder.Language);
                if (item != null)
                {
                    tree.FolderItem = item;
                    tree.Refresh(iD);
                }
            }
        }

        private static string GetID(string id)
        {
            Assert.ArgumentNotNull(id, "id");
            int num = id.LastIndexOf("_", StringComparison.InvariantCulture);
            if (num >= 0)
            {
                id = StringUtil.Mid(id, num + 1);
            }
            if (ShortID.IsShortID(id))
            {
                id = ShortID.Decode(id);
            }
            return id;
        }

        protected override Sidebar GetSidebar()
        {
            Sidebars.Tree expr_05 = new Sidebars.Tree();
            expr_05.ID=("Tree");
            DataContext expr_15 = new DataContext();
            expr_15.DataViewName=("Master");
            DataContext dataContext = expr_15;
            expr_05.DataContext = dataContext;
            return Assert.ResultNotNull<Sidebars.Tree>(expr_05);
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string name;
            switch (name = message.Name)
            {
                case "contenteditor:navigatetoitem":
                    this.HasPendingUpdate = false;
                    this.ContentEditorDataContext.SetFolder(new ItemUri(System.Web.HttpUtility.UrlDecode(message["uri"])));
                    this.PendingUpdateItemUri = this.ContentEditorDataContext.GetFolder().Uri;
                    break;
                case "datacontext:changed":
                    if (message["id"] == "ContentEditorDataContext")
                    {
                        this.OnDataContextChanged(message);
                    }
                    break;
                case "item:iscontenteditor":
                    message.Result = true;
                    break;
                case "item:isediting":
                    message.Result = this.IsEditing(message["id"]);
                    break;
                case "item:load":
                case "item:versionadded":
                    this.LoadItem(message);
                    break;
                case "item:modified":
                    this.CheckModified(message);
                    break;
                case "item:save":
                    this.Save(message);
                    return;
                case "item:updated":
                    if (this.ContentEditorDataContext.CurrentItem != null && this.ContentEditorDataContext.CurrentItem.ID.ToString() == message["id"])
                    {
                        this.LoadItem(message);
                    }
                    return;
                case "item:workflowhistory":
                    this.Workflow_History();
                    return;
                case "item:refreshchildren":
                    this.Tree_Refresh(message["id"]);
                    break;
                case "item:checkedin":
                case "item:checkedout":
                case "item:templatefieldadded":
                case "item:templatechanged":
                case "item:templatefielddeleted":
                    this.ContentEditorDataContext.Refresh();
                    break;
                case "item:refresh":
                    this.ContentEditorDataContext.Refresh();
                    this.RefreshTreeNode(message);
                    break;
                case "shell:useroptionschanged":
                    SheerResponse.CheckModified(false);
                    SheerResponse.SetLocation(string.Empty);
                    return;
                case "item:updategutter":
                    this.UpdateGutter(message["id"]);
                    break;
                case "contenteditor:switchto":
                    this.SwitchTo(System.Web.HttpUtility.UrlDecode(message["target"]));
                    break;
                case "contenteditor:showvalidationresult":
                    this.ShowValidationResult(this.GetCurrentItem(message));
                    return;
                case "notification:accept":
                    this.AcceptNotification(message, this.GetCurrentItem(message));
                    this.Reload();
                    return;
                case "notification:reject":
                    this.RejectNotification(message, this.GetCurrentItem(message));
                    this.Reload();
                    return;
            }
            if (!string.IsNullOrEmpty(this.FrameName))
            {
                message.Arguments.Add("frameName", this.FrameName);
            }
            if (!this.CheckIfMessageCanBeHandled(message))
            {
                SheerResponse.Alert("The selected item could not be found.\n\nIt may have been deleted by another user.\n\nSelect another item.", new string[0]);
                message.CancelDispatch = true;
                message.CancelBubble = true;
                return;
            }
            Dispatcher.Dispatch(message, this.GetCurrentItem(message));
        }
    }
}
