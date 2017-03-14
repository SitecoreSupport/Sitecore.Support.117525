using ComponentArt.Web.UI;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.Data.Treeviews;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Reflection;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Shell.Web;
using Sitecore.Support.Shell.Applications.ContentManager.Sidebars;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.WebControls.Ribbons;
using System;
using System.Collections.Specialized;
using System.Web.UI;
using Sitecore.Shell.Applications.Install.Controls;
using Page = System.Web.UI.Page;

namespace Sitecore.Support.Shell.Applications.ContentManager
{
	public class ExecutePage : Page
	{
		private string DesignTimeConvert()
		{
			string queryString = WebUtil.GetQueryString("mode");
			string text = StringUtil.GetString(new string[]
			{
				base.Request.Form["html"]
			});
			if (queryString == "HTML")
			{
				text = RuntimeHtml.Convert(text, Settings.HtmlEditor.SupportWebControls);
			}
			else
			{
				NameValueCollection nameValueCollection = new NameValueCollection(1)
				{
					{
						"sc_live",
						"0"
					}
				};
				string text2 = string.Empty;
				if (Settings.HtmlEditor.SupportWebControls)
				{
					text2 = "control:IDEHtmlEditorControl";
				}
				text = DesignTimeHtml.Convert(text, text2, nameValueCollection);
			}
			return "<?xml:namespace prefix = sc />" + text;
		}

		private static string ExpandTreeViewToNode()
		{
			string text = WebUtil.GetQueryString("root");
			string text2 = WebUtil.GetQueryString("id");
			string queryString = WebUtil.GetQueryString("la");
			if (text2.IndexOf('_') >= 0)
			{
				text2 = StringUtil.Mid(text2, text2.LastIndexOf('_') + 1);
			}
			if (text.IndexOf('_') >= 0)
			{
				text = StringUtil.Mid(text, text.LastIndexOf('_') + 1);
			}
			if (text2.Length > 0 && text.Length > 0)
			{
				Language language = Language.Parse(queryString);
				Item item = Client.ContentDatabase.GetItem(ShortID.DecodeID(text2), language);
				Item item2 = Client.ContentDatabase.GetItem(ShortID.DecodeID(text), language);
				if (item != null && item2 != null)
				{
					return ExecutePage.GetTree(item, item2).RenderTree(false);
				}
			}
			return string.Empty;
		}

		private string GetContextualTabs()
		{
			string queryString = WebUtil.GetQueryString("parameters");
			if (!string.IsNullOrEmpty(queryString) && queryString.IndexOf("&fld=", StringComparison.InvariantCulture) >= 0)
			{
				return this.GetFieldContextualTab(queryString);
			}
			return string.Empty;
		}

		private string GetFieldContextualTab(string parameters)
		{
			Assert.ArgumentNotNull(parameters, "parameters");
			int num = parameters.IndexOf("&fld=", StringComparison.InvariantCulture);
			ItemUri itemUri = ItemUri.Parse(StringUtil.Left(parameters, num));
			if (itemUri != null)
			{
				Item item = Database.GetItem(itemUri);
				if (item == null)
				{
					return string.Empty;
				}
				NameValueCollection nameValueCollection = WebUtil.ParseUrlParameters(StringUtil.Mid(parameters, num));
				string @string = StringUtil.GetString(new string[]
				{
					nameValueCollection["fld"]
				});
				string string2 = StringUtil.GetString(new string[]
				{
					nameValueCollection["ctl"]
				});
				Field expr_94 = item.Fields[@string];
				TemplateField templateField = (expr_94 != null) ? expr_94.GetTemplateField() : null;
				if (templateField == null)
				{
					return string.Empty;
				}
				string string3 = StringUtil.GetString(new string[]
				{
					templateField.TypeKey,
					"text"
				});
				Item fieldTypeItem = FieldTypeManager.GetFieldTypeItem(string3);
				if (fieldTypeItem == null)
				{
					return string.Empty;
				}
				Database database = Sitecore.Context.Database;
				if (database == null)
				{
					return string.Empty;
				}
				Item item2;
				if (string3 == "rich text")
				{
					string queryString = WebUtil.GetQueryString("mo", "Editor");
					string text = StringUtil.GetString(new string[]
					{
						templateField.Source,
						(queryString == "IDE") ? "/sitecore/system/Settings/Html Editor Profiles/Rich Text IDE" : Settings.HtmlEditor.DefaultProfile
					}) + "/Ribbon";
					item2 = database.GetItem(text);
				}
				else
				{
					item2 = fieldTypeItem.Children["Ribbon"];
				}
				if (item2 != null)
				{
					Ribbon expr_175 = new Ribbon();
					expr_175.ID = "Ribbon";
					CommandContext commandContext = new CommandContext(item);
					expr_175.CommandContext=(commandContext);
					commandContext.Parameters["FieldID"] = @string;
					commandContext.Parameters["ControlID"] = string2;
					string text2;
					string text3;
					expr_175.Render(item2, true, out text2, out text3);
					base.Response.Write(string.Concat(new string[]
					{
						"{ \"navigator\": ",
						StringUtil.EscapeJavascriptString(text2),
						", \"strips\": ",
						StringUtil.EscapeJavascriptString(text3),
						" }"
					}));
				}
			}
			return string.Empty;
		}

		private static Tree GetTree(Item folder, Item root)
		{
			Assert.IsNotNull(folder, "folder is null");
			Assert.IsNotNull(root, "root is null");
			Tree expr_1B = new Tree();
			expr_1B.ID=(WebUtil.GetSafeQueryString("treeid"));
			expr_1B.FolderItem = folder;
			expr_1B.RootItem = root;
			DataContext expr_3E = new DataContext();
			expr_3E.DataViewName=("Master");
			DataContext dataContext = expr_3E;
			expr_1B.DataContext = dataContext;
			return expr_1B;
		}

		private static string GetTreeViewChildren()
		{
			string queryString = WebUtil.GetQueryString("id");
			string queryString2 = WebUtil.GetQueryString("la");
			if (string.IsNullOrEmpty(queryString))
			{
				return string.Empty;
			}
			Language language = Language.Parse(queryString2);
			Item item = Client.ContentDatabase.GetItem(ShortID.DecodeID(queryString), language);
			Item item2 = (item != null) ? item.Database.GetRootItem(language) : null;
			if (item2 == null)
			{
				return string.Empty;
			}
			return ExecutePage.GetTree(item, item2).RenderChildNodes(item.ID);
		}

		private string GetTreeViewContent()
		{
			base.Response.ContentType = "text/xml";
			ItemUri itemUri = ItemUri.ParseQueryString();
			if (itemUri == null)
			{
				return string.Empty;
			}
			Item item = Database.GetItem(itemUri);
			if (item == null)
			{
				return string.Empty;
			}
			Type typeInfo = ReflectionUtil.GetTypeInfo(WebUtil.GetQueryString("typ"));
			if (typeInfo == null)
			{
				return string.Empty;
			}
			TreeviewSource treeviewSource = ReflectionUtil.CreateObject(typeInfo) as TreeviewSource;
			if (treeviewSource == null)
			{
				return string.Empty;
			}
            ComponentArt.Web.UI.TreeView treeView = new ComponentArt.Web.UI.TreeView();
			treeviewSource.Render(treeView, item);
			return treeView.GetXml();
		}

		protected void Page_Load(object sender, EventArgs e)
		{
			Assert.ArgumentNotNull(sender, "sender");
			Assert.ArgumentNotNull(e, "e");
			if (!ShellPage.IsLoggedIn())
			{
				return;
			}
			string s = string.Empty;
			string a = WebUtil.GetQueryString("cmd").ToLowerInvariant();
			if (!(a == "gettreeviewchildren"))
			{
				if (!(a == "expandtreeviewtonode"))
				{
					if (!(a == "getcontextualtabs"))
					{
						if (!(a == "convert"))
						{
							if (a == "treeviewcontent")
							{
								s = this.GetTreeViewContent();
							}
						}
						else
						{
							s = this.DesignTimeConvert();
						}
					}
					else
					{
						s = this.GetContextualTabs();
					}
				}
				else
				{
					s = ExecutePage.ExpandTreeViewToNode();
				}
			}
			else
			{
				s = ExecutePage.GetTreeViewChildren();
			}
			base.Response.Write(s);
		}
	}
}
