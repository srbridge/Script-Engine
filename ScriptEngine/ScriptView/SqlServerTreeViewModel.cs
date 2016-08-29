using Quick.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScriptView
{
	/// <summary>
	/// bind the tree-view's ItemsSource to this object's Nodes collection, bind dock, visibility, tooltip, cursor etc. to this object too.
	/// </summary>
	class SqlEnvironmentViewModel : TreeViewModel<DataSetViewModel, SqlServerViewModel>
	{
		public SqlEnvironmentViewModel(DataSetViewModel owner)	 
			:base(owner)
		{
			this.Visible = System.Windows.Visibility.Visible;
			this.Owner   = owner;
			
			// immediately start query for sql server:
			Task.Run(()=> QueryNodes());
		}

		/// <summary>
		/// searches for and adds discovered sql servers.
		/// </summary>
		protected override void QueryNodes()
		{
			
			try
			{
				this.IsBusy = true;
				this.Owner.IsBusy = true;

				foreach (var info in SqlServerInfo.GetDataSources(false).OrderBy((i)=> i.ServerName))
				{
					// construct the server info model:
					var serverModel = new SqlServerViewModel(info) { Owner = this.Owner };

					// add it to the nodes collection:
					SafeInvoke(() => Nodes.Add(serverModel));
				}

			}
			finally
			{
				this.IsBusy = false;
				this.Owner.IsBusy = false;
			}
		}
	}

	/// <summary>
	/// bind a hierarchal data template to this view-model. Each instance of this model represents a SERVER node.
	/// </summary>
	class SqlServerViewModel : TreeViewModel<SqlServerInfo, SqlDatabaseViewModel>
	{           
		/// <summary>
		/// load a single copy of the image into a static context
		/// </summary>
		static BitmapSource icon = ImageResources.GetImageSource("Images/server_Local_16xLG.png");

		/// <summary>
		/// construct with the info supplied by the <see cref="SqlEnvironmentViewModel.QueryNodes"/> method
		/// </summary>
		/// <param name="info"></param>
		public SqlServerViewModel(SqlServerInfo info)  :base(info)
		{	
			// set the name based on whether there is an instance:												 
			if (string.IsNullOrEmpty(info.InstanceName))
				this.Name = info.ServerName;
			else
				this.Name = $"{info.ServerName}\\{info.InstanceName}";

			// assign from the static context;
			this.Icon = icon;
		}
		
		/// <summary>
		/// override the OnNodeSelected method to set the <see cref="DataSetViewModel.SelectedSqlServer"/> property.
		/// </summary>
		/// <param name="sender"></param>
		protected override void OnNodeSelected(ViewModelBase sender)
		{
			if (Owner != null)
				Owner.SelectedSqlServer = this.Info;

			// activates "QueryNodes" the first time:
			base.OnNodeSelected(sender);

		}

		/// <summary>
		/// the first time the node is selected, this method builds the child nodes collection
		/// </summary>
		protected override void QueryNodes()
		{
			try
			{
				// set to busy
				this.IsBusy = true;
				
				// enumerate the child nodes using the EnumerateChildren method of the server-info object.
				foreach (var db_info in Info.EnumerateChildren())
				{
					// create the database model:
					var db_model = new SqlDatabaseViewModel(db_info) { Owner = this.Owner } ;

					// add it to the nodes collection:
					SafeInvoke(() => Nodes.Add(db_model));
				}
			}
			catch (Exception enumerateError)
			{
				System.Windows.MessageBox.Show(enumerateError.Message, Info.Description);
			}
			finally
			{
				// clear the busy flag
				this.IsBusy = false;
			}
		}

		/// <summary>
		/// add items to the base context menu;
		/// </summary>
		public override ContextMenu ContextMenu
		{
			get
			{
				// create the context-menu:
				var mnu = base.ContextMenu;

				// add the 'download schema' context-menu item.
				mnu.Items.Add(new MenuItem() { Header = "Add Server", Command = this.Owner.DownloadSchema });

				// return the menu:
				return mnu;
			}
		}

		public ICommand AddServer
		{
			get; set;
		}

		protected virtual void OnExecAddServer(object p)
		{
			// open the input box to retreive the server-name:
			var sn = InputBox.GetInput("Enter Server Name", "SQL Server Connection");

			if (!string.IsNullOrEmpty(sn))
			{

			}

		}

	}

	/// <summary>
	/// each instance of this model represents a database within a server
	/// </summary>
	class SqlDatabaseViewModel  : TreeViewModel<SqlDbInfo, SqlTableViewModel>
	{
		/// <summary>
		/// load a single copy of the image into a static context
		/// </summary>
		static BitmapSource icon = ImageResources.GetImageSource("Images/database_16xLG.png");
		
		/// <summary>
		/// construct the database node
		/// </summary>
		/// <param name="info"></param>
		public SqlDatabaseViewModel(SqlDbInfo info) : base(info)
		{								
			// set the name and icon					
			this.Name = info.DataBaseName;
			this.Icon = icon;
		}

		/// <summary>
		/// update the <see cref="DataSetViewModel.SelectedConnection"/> property when this node is selected.
		/// </summary>
		/// <param name="sender"></param>
		protected override void OnNodeSelected(ViewModelBase sender)
		{
			// set the selected connection (assuming owner is not null)
			if (Owner != null)
				Owner.SelectedConnection = this.Info;

			// invoke the base method:
			base.OnNodeSelected(sender);

		}

		/// <summary>
		/// implement the method to query the nodes collection
		/// </summary>
		protected override void QueryNodes()
		{
			try
			{
				// set to busy
				this.IsBusy = true;

				// enumerate the table information 
				foreach (var tbl_info in Info.EnumerateChildren())
				{
					// create the table view model:
					var db_model = new SqlTableViewModel(tbl_info) { Owner = this.Owner };

					// add it to the nodes collection:
					SafeInvoke(() => Nodes.Add(db_model));
				}
			}
			finally
			{
				// clear the busy flag:
				this.IsBusy = false;
			}
		}

		/// <summary>
		/// add items to the base context menu;
		/// </summary>
		public override ContextMenu ContextMenu
		{
			get
			{
				// create the context-menu:
				var mnu = base.ContextMenu;

				// add the 'download schema' context-menu item.
				mnu.Items.Add(new MenuItem() { Header = "Download Schema", Command = this.Owner.DownloadSchema });

				// return the menu:
				return mnu;
			}
		}

	}

	/// <summary>
	/// a table within a database
	/// </summary>
	class SqlTableViewModel	: TreeViewModel<SqlDbTableInfo, SqlColumnViewModel>
	{
		/// <summary>
		/// load one static copy of the bitmap
		/// </summary>
		public static readonly BitmapSource icon = ImageResources.GetImageSource("Images/Table_748.png");

		/// <summary>
		/// construct the table tree-view-model using the specified table information 
		/// </summary>
		/// <param name="info"></param>
		public SqlTableViewModel(SqlDbTableInfo info) : base(info)
		{
			// set the name:
			this.Name = info.TableName;
			this.Icon = icon;

		}

		protected override void OnNodeSelected(ViewModelBase sender)
		{
			// set the selected table on the owning view-model;
			if (Owner != null)
				Owner.SelectedConnectionTable = this.Info;

			base.OnNodeSelected(sender);
		}

		protected override void QueryNodes()
		{
			try
			{
				this.IsBusy = true;

				foreach (var tbl_info in Info.EnumerateChildren())
				{
					// create the database model:
					var db_model = new SqlColumnViewModel(tbl_info);

					// add it to the nodes collection:
					SafeInvoke(() => Nodes.Add(db_model));
				}
			}
			finally
			{
				this.IsBusy = false;
			}
		}

		/// <summary>
		/// creates the context menu for the node
		/// </summary>
		public override ContextMenu ContextMenu
		{
			get
			{
				var mnu = base.ContextMenu;
					mnu.Items.Add(new MenuItem() { Header = "Create Select Query",	        Command = CreateSelect });
				    mnu.Items.Add(new MenuItem() { Header = "Select Top 1000 Records",      Command = CreateExecuteSelect });
					mnu.Items.Add(new MenuItem() { Header = "Copy Table Definition Script", Command = ScriptTableDef });
				return mnu;
			}
		}

		public ICommand ScriptTableDef
		{
			get
			{

				return new RelayCommand((o) =>
				{
					System.Windows.Clipboard.SetText(DataScriptEngine.SMOScripting.GetScriptTableDef(this.Info.Database.GetConnectionString(), this.Info.Database.DataBaseName, this.Name));
					System.Windows.MessageBox.Show("Table Definition Copied to Clipboard");
				});
			}
		}

		public ICommand CreateSelect
		{
			get
			{
				return new RelayCommand((o) => this.Owner.CommandText = $"SELECT * FROM [{this.Info.TableName}]");
			}
		}

		public ICommand CreateExecuteSelect
		{
			get
			{
				return new RelayCommand((o) => {
					this.Owner.CommandText = $"SELECT TOP 1000 * FROM [{this.Info.TableName}]";
					this.Owner.ExecuteSelect.Execute(null);    
					});
			}
		}
	}

	/// <summary>
	/// a column within a table
	/// </summary>
	class SqlColumnViewModel : ViewModelBase
	{
		/// <summary>
		/// load one static copy of the bitmap
		/// </summary>
		public static readonly BitmapSource icon = ImageResources.GetImageSource("Images/column_16xLG.png");

		/// <summary>
		/// construct the view-model for the column
		/// </summary>
		/// <param name="info"></param>
		public SqlColumnViewModel(SqlDbColumnInfo info)
		{
			this.Name       = info.Description;
			this.Foreground = Brushes.Blue;
			this.Icon		= icon;

		}

		public string Name
		{
			get { return GetValue(() => Name); }
			set { SetValue(() => Name, value); }
		}
	}

	/// <summary>
	/// reduces the amount of repetition coding the above classes.
	/// </summary>
	/// <typeparam name="infoType"></typeparam>
	/// <typeparam name="childType"></typeparam>
	abstract class TreeViewModel<infoType, childType> : TreeViewModel
	{
		/// <summary>
		/// used to trap the first OnSelected.
		/// </summary>
		bool m_sentinal = false;

		/// <summary>
		/// construct with the required info.
		/// </summary>
		/// <param name="info"></param>
		public TreeViewModel(infoType info)
		{
			this.Info = info;
			#pragma warning disable RECS0021	// Warns about calls to virtual member functions occuring in the constructor
			this.OnSelected = OnNodeSelected;	// assigning virtual method in constructor but not calling it. warning is oversensitive.
			#pragma warning restore RECS0021	// Warns about calls to virtual member functions occuring in the constructor
		}

		/// <summary>
		/// the information the node is displaying.
		/// </summary>
		public infoType Info
		{
			get { return GetValue(() => Info); }
			set { SetValue(() => Info, value); }
		}
										
		/// <summary>
		/// the owning data-set-view-model;
		/// </summary>
		public DataSetViewModel Owner { get; set; }

		/// <summary>
		/// invoked when the node is selected
		/// </summary>
		/// <param name="sender"></param>
		protected virtual void OnNodeSelected(ViewModelBase sender)
		{													 
			if (!m_sentinal)
			{
				m_sentinal = true;
				Task.Run(() => QueryNodes());
			}
		}

		/// <summary>
		/// when the first time the node is selected, this method will be executed. override to add records to the nodes collection
		/// </summary>
		protected abstract void QueryNodes();

		/// <summary>
		/// the nodes collection
		/// </summary>
		public ObservableCollection<childType> Nodes { get; } = new ObservableCollection<childType>();

		/// <summary>
		/// base context menu for all TreeViewModels
		/// </summary>
		public virtual ContextMenu	ContextMenu {

			get {

				// create the context menu
				var ctx = new ContextMenu();

				// add in a refresh item
				ctx.Items.Add(new MenuItem() { Header = "Refresh", Command = Refresh });

				// return the context menu
				return ctx;

			}

		}

		/// <summary>
		/// refreshes the child list of the 
		/// </summary>
		public ICommand Refresh
		{
			get
			{
				return new RelayCommand((o) => {

					// reset the sentinal and clear the nodes collection
					// m_sentinal = false;
					SafeInvoke(() => Nodes.Clear());
					Task.Run(()   => QueryNodes());
				});
			}

		}

	}

}
