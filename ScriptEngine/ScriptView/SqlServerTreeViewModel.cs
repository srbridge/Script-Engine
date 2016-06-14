using Quick.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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
			}
		}
	}

	/// <summary>
	/// bind a hierarchal data template to this view-model. Each instance of this model represents a SERVER node.
	/// </summary>
	class SqlServerViewModel : TreeViewModel<SqlServerInfo, SqlDatabaseViewModel>
	{	
		public SqlServerViewModel(SqlServerInfo info)  :base(info)
		{													 
			if (string.IsNullOrEmpty(info.InstanceName))
				this.Name = info.ServerName;
			else
				this.Name = $"{info.ServerName}\\{info.InstanceName}";
			try
			{
				SafeInvoke(() =>
				{
					this.Icon = new BitmapImage(new Uri("pack://application:,,,/images/server_Local_16xLG.png", UriKind.Absolute));				});
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
			}

		}
		
		protected override void OnNodeSelected(SimpleViewModel sender)
		{
			if (Owner != null)
				Owner.SelectedSqlServer = this.Info;

			base.OnNodeSelected(sender);

		}

		/// <summary>
		/// when the parent node is selected, this
		/// </summary>
		protected override void QueryNodes()
		{
			try
			{
				this.IsBusy = true;
				
				foreach (var db_info in Info.EnumerateChildren())
				{
					// create the database model:
					var db_model = new SqlDatabaseViewModel(db_info) { Owner = this.Owner } ;

					// add it to the nodes collection:
					SafeInvoke(() => Nodes.Add(db_model));
				}
			}
			finally
			{
				this.IsBusy = false;
			}
		}

	}

	/// <summary>
	/// each instance of this model represents a database within a server
	/// </summary>
	class SqlDatabaseViewModel  : TreeViewModel<SqlDbInfo, SqlTableViewModel>
	{
		
		public SqlDatabaseViewModel(SqlDbInfo info) : base(info)
		{													
			this.Name = info.DBName;
			SafeInvoke(()=>this.Icon = new BitmapImage(new Uri("pack://application:,,,/images/database_16xLG.png", UriKind.Absolute)));
		}

		protected override void OnNodeSelected(SimpleViewModel sender)
		{
			if (Owner != null)
				Owner.SelectedConnection = this.Info;
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
					var db_model = new SqlTableViewModel(tbl_info) { Owner = this.Owner };

					// add it to the nodes collection:
					SafeInvoke(() => Nodes.Add(db_model));
				}
			}
			finally
			{
				this.IsBusy = false;
			}
		}
	}

	/// <summary>
	/// a table within a database
	/// </summary>
	class SqlTableViewModel	: TreeViewModel<SqlDbTableInfo, SqlColumnViewModel>
	{
		
		public SqlTableViewModel(SqlDbTableInfo info) : base(info)
		{
			this.Name = info.TableName;
			SafeInvoke(() => Icon = new BitmapImage(new Uri("pack://application:,,,/images/Table_748.png", UriKind.Absolute)));
		}

		protected override void OnNodeSelected(SimpleViewModel sender)
		{
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
	}

	/// <summary>
	/// a column within a table
	/// </summary>
	class SqlColumnViewModel : SimpleViewModel
	{
		public SqlColumnViewModel(SqlDbColumnInfo info)
		{
			this.Name = info.Description;
		}

		public string Name
		{
			get { return this[nameof(Name)] as string; }
			set { this[nameof(Name)] = value; }
		}
	}

	/// <summary>
	/// reduces the amount of repetition coding the above classes.
	/// </summary>
	/// <typeparam name="tInfo"></typeparam>
	/// <typeparam name="tChildViewModel"></typeparam>
	abstract class TreeViewModel<tInfo, tChildViewModel> : TreeViewModel
	{
		/// <summary>
		/// used to trap the first OnSelected.
		/// </summary>
		bool m_sentinal = false;

		/// <summary>
		/// construct with the required info.
		/// </summary>
		/// <param name="info"></param>
		public TreeViewModel(tInfo info)
		{
			this.Info = info;
			#pragma warning disable RECS0021	// Warns about calls to virtual member functions occuring in the constructor
			this.OnSelected = OnNodeSelected;	// assigning virtual method in constructor but not calling it. warning is oversensitive.
			#pragma warning restore RECS0021	// Warns about calls to virtual member functions occuring in the constructor
		}

		/// <summary>
		/// an image-source to bind to an image for the node.
		/// </summary>
		public BitmapImage Icon
		{
			get { return this[nameof(Icon)] as BitmapImage; }
			set { this[nameof(Icon)] = value; }
		}

		/// <summary>
		/// the information the node is displaying.
		/// </summary>
		public tInfo Info
		{
			get { return (tInfo)this[nameof(Info)]; }
			set { this[nameof(Info)] = value; }
		}

		/// <summary>
		/// the owning data-set-view-model;
		/// </summary>
		public DataSetViewModel Owner { get; set; }

		/// <summary>
		/// invoked when the node is selected
		/// </summary>
		/// <param name="sender"></param>
		protected virtual void OnNodeSelected(SimpleViewModel sender)
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
		public ObservableCollection<tChildViewModel> Nodes { get; } = new ObservableCollection<tChildViewModel>();

	}

}
