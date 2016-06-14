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
	class SqlEnvironmentModel : Quick.MVVM.TreeViewModel<SqlServerModel>
	{
		public SqlEnvironmentModel(DataSetViewModel owner)	 
			:base()
		{
			this.Visible = System.Windows.Visibility.Visible;
			this.Owner   = owner;
			
			Task.Run(()=>QueryServers());
		}

		/// <summary>
		/// the owning data-set-view-model;
		/// </summary>
		public DataSetViewModel Owner { get; set; }

		public SqlServerModel SelectedServer
		{
			get { return this[nameof(SelectedServer)] as SqlServerModel; }
			set { this[nameof(SelectedServer)] = value; }
		}

		protected void QueryServers()
		{
			try
			{
				this.IsBusy = true;
				foreach (var info in SqlServerInfo.GetDataSources(false))
				{
					// construct the server info model:
					var serverModel = new SqlServerModel(info) { Owner = this.Owner };

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
	class SqlServerModel : TreeModel<SqlServerInfo, SqlDatabaseModel>
	{	
		public SqlServerModel(SqlServerInfo info)  :base(info)
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
					var db_model = new SqlDatabaseModel(db_info) { Owner = this.Owner } ;

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

	class SqlDatabaseModel  : TreeModel<SqlDbInfo, SqlTableModel>
	{
		
		public SqlDatabaseModel(SqlDbInfo info) : base(info)
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
					var db_model = new SqlTableModel(tbl_info) { Owner = this.Owner };

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

	class SqlTableModel	: TreeModel<SqlDbTableInfo, SqlColumnModel>
	{
		
		public SqlTableModel(SqlDbTableInfo info) : base(info)
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
					var db_model = new SqlColumnModel(tbl_info);

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

	class SqlColumnModel : Quick.MVVM.SimpleViewModel
	{
		public SqlColumnModel(SqlDbColumnInfo info)
		{
			this.Name = info.Description;
		}

		public string Name
		{
			get { return this[nameof(Name)] as string; }
			set { this[nameof(Name)] = value; }
		}
	}


	abstract class TreeModel<T,C> : Quick.MVVM.TreeViewModel
	{
		bool m_sentinal = false;

		public TreeModel(T info)
		{
			this.Info = info;
			#pragma warning disable RECS0021 // Warns about calls to virtual member functions occuring in the constructor
			this.OnSelected = OnNodeSelected;
			#pragma warning restore RECS0021 // Warns about calls to virtual member functions occuring in the constructor
		}

		public BitmapImage Icon
		{
			get { return this[nameof(Icon)] as BitmapImage; }
			set { this[nameof(Icon)] = value; }
		}

		public T Info
		{
			get { return (T)this[nameof(Info)]; }
			set { this[nameof(Info)] = value; }
		}

		/// <summary>
		/// the owning data-set-view-model;
		/// </summary>
		public DataSetViewModel Owner { get; set; }

		protected virtual void OnNodeSelected(SimpleViewModel sender)
		{													 
			if (!m_sentinal)
			{
				m_sentinal = true;
				Task.Run(() => QueryNodes());
			}
		}

		protected abstract void QueryNodes();

		public ObservableCollection<C> Nodes { get; } = new ObservableCollection<C>();

	}

}
