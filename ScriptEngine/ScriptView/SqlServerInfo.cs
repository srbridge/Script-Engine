using Quick.MVVM;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptView
{
	/// <summary>
	/// information about an SQL server instance.
	/// </summary>
	public class SqlServerInfo :  IEquatable<SqlServerInfo>
	{
		#region Constructor

		/// <summary>
		/// construct from a server-name (eg DECDEV-SQL1) or a server-name and instance name (eg H07251\SQLEXPRESS)
		/// </summary>
		/// <param name="server">
		/// server name and instance name
		/// </param>
		public SqlServerInfo(string server, bool getDetails = true)
		{
			// is there a back-slash indicating the instance name?
			var pos = server.IndexOf("\\", StringComparison.Ordinal);
			if (pos > 0)
			{
				this.ServerName = server.Substring(0, pos);
				this.InstanceName = server.Substring(pos + 1);
			}
			else
			{
				this.ServerName = server;
			}

			if (getDetails)
				// run the query of the database names in another thread:
				this.DatabaseListTask = Task.Run(() => QueryDatabases());
			else
				this.DatabaseListTask = Task.Run(() => { });

		}

		/// <summary>
		/// construct from the server-discovery row fields (server\instance\version)
		/// </summary>
		/// <param name="serverName"></param>
		/// <param name="instanceName"></param>
		/// <param name="version"></param>
		public SqlServerInfo(string serverName, string instanceName, string version, bool getDetails = false)
		{
			this.ServerName = serverName;
			this.InstanceName = instanceName;
			this.Version = version;
			if (getDetails)
				this.DatabaseListTask = Task.Run(() => QueryDatabases());
			else
				this.DatabaseListTask = Task.Run(() => { });
		}

		#endregion

		#region Properties

		/// <summary>
		/// gets a connection string for this SQL server
		/// </summary>
		public string ConnectionString
		{

			get
			{
				return $"Data Source ={DataSource};Integrated Security=True;Connection Timeout=1";
			}
		}

		/// <summary>
		/// gets the DataSource part of the connection string.
		/// </summary>
		public string DataSource
		{
			get
			{

				if (!string.IsNullOrEmpty(InstanceName))
					return $"{ServerName}\\{InstanceName}";
				else
					return ServerName;

			}
		}

		/// <summary>
		/// this is the task retrieving the database names from the server
		/// </summary>
		public Task DatabaseListTask { get; protected set; }

		/// <summary>
		/// the name of the server (computer name)
		/// </summary>
		public string ServerName { get; set; } = ""; // don't want this to be able to be null;

		/// <summary>
		/// the instance name if required
		/// </summary>
		public string InstanceName { get; set; } = "";

		/// <summary>
		/// the version of the server (if known)
		/// </summary>
		public string Version { get; set; } = "";

		/// <summary>
		/// description of the server including instance name and version (if specified)
		/// </summary>
		public string Description
		{
			get
			{
				if (!string.IsNullOrEmpty(InstanceName))
					return $"{ServerName}\\{InstanceName} ({Version})";
				else
					return ServerName;
			}
		}

		/// <summary>
		/// a list of databases on that server.
		/// </summary>
		public List<SqlDbInfo> Databases { get; } = new List<SqlDbInfo>();

		#endregion

		#region Methods

		/// <summary>
		/// pulls out a list of databases on the server.
		/// </summary>
		protected void QueryDatabases()
		{
			foreach (var db in EnumerateChildren(true))
			{
				this.Databases.Add(db);
			}
		}

		/// <summary>
		/// pulls out a list of databases on the server as an enumerable;
		/// </summary>
		public IEnumerable<SqlDbInfo> EnumerateChildren(bool fetchDetails = false)
		{
			// create a new connection using the connection string:
			using (var conn = new SqlConnection(this.ConnectionString))
			{
				// open and query for databases:
				conn.Open();

				// get the list of databases:
				var tbl = conn.GetSchema("Databases");

				// enumerate the databases and create a DBInfo for each row:
				foreach (DataRow row in tbl.Select("1=1", "database_name ASC"))
				{
					// add the db-info to the databases collection.
					SqlDbInfo info = null;
					try
					{
						info = new SqlDbInfo(ServerName, InstanceName, row.Field<string>("database_name"), fetchDetails);
					}
					catch(Exception e) {

						System.Diagnostics.Debug.Print(e.ToString());
					}

					if (info != null)
						yield return info;
				}
			}
		}

		/// <summary>
		/// compare two <see cref="SqlServerInfo"/> instances for equivalency.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(SqlServerInfo other)
		{
			if (this.ServerName.Equals(other.ServerName, StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(other.InstanceName, InstanceName, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		/// <summary>
		/// returns the description of the server/
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return this.Description;
		}

		#endregion

		#region Static

		/// <summary>
		/// uses the <see cref="System.Data.Sql.SqlDataSourceEnumerator"/> to discover all SQL servers visible from the current network location.
		/// </summary>
		/// <param name="queryDatabases">
		/// when true, each <see cref="SqlServerInfo"/> instance generated will start an asynchronous task to lookup a list of databases for that server.
		/// each database found will also query it's schema and get a list of tables, each table will fetch a list of columns etc.
		/// </param>
		/// <returns></returns>
		public static IEnumerable<SqlServerInfo> GetDataSources(bool queryDatabases = true)
		{
			// query for SQL servers:
			var tbl = System.Data.Sql.SqlDataSourceEnumerator.Instance.GetDataSources();

			// build a DbServerInfo for each discovered server:
			foreach (DataRow row in tbl.Rows)
			{
				yield return new SqlServerInfo(
					row.Field<string>("ServerName"),
					row.Field<string>("InstanceName"),
					row.Field<string>("Version"),
					queryDatabases
					);
			}
		}

		#endregion

	}

	/// <summary>
	/// information about an SQL server database.
	/// </summary>
	public class SqlDbInfo 
	{
		#region Constructor

		/// <summary>
		/// construct the database information.
		/// </summary>
		/// <param name="server"></param>
		/// <param name="instance"></param>
		/// <param name="db"></param>
		public SqlDbInfo(string server, string instance, string db, bool fetchDetails = false)
		{
			this.ServerName   = server;
			this.InstanceName = instance;
			this.DataBaseName       = db;

			if (fetchDetails)
				this.DetailsTask = Task.Run(() => QueryTables());
			else
				this.DetailsTask = Task.Run(() => { });
		}

		/// <summary>
		/// construct from one of the database records in a <see cref="SqlServerInfo"/>
		/// </summary>
		/// <param name="serverInfo"></param>
		/// <param name="idx"></param>
		public SqlDbInfo(SqlServerInfo serverInfo, int idx)
			: this(serverInfo.ServerName, serverInfo.InstanceName, serverInfo.Databases[idx].DataBaseName)
		{

		}

		#endregion

		#region Properties

		/// <summary>
		/// gets the task used to fetch the details of the connection
		/// </summary>
		public Task DetailsTask { get; protected set; }

		/// <summary>
		/// description of the database
		/// </summary>
		public string Description
		{
			get
			{
				return $"{DataSource}.[{DataBaseName}]";
			}
		}

		/// <summary>
		/// calculates a connection string to the database
		/// </summary>
		protected string ConnectionString
		{
			get { return $"Data Source={DataSource};Initial Catalog={DataBaseName};Integrated Security=True; Connection Timeout={ConnectionTimeout}"; }
		}

		/// <summary>
		/// the name of the database
		/// </summary>
		public string DataBaseName { get; set; }

		/// <summary>
		/// the name of the server hosting the database
		/// </summary>
		public string ServerName { get; set; }

		/// <summary>
		/// the name of the instance on the server if any.
		/// </summary>
		public string InstanceName { get; set; }

		/// <summary>
		/// gets or sets the connection timeout part of the connection string.
		/// </summary>
		public int ConnectionTimeout { get; set; } = 2;

		/// <summary>
		/// gets the data source part of the connection string.
		/// </summary>
		public string DataSource
		{
			get
			{
				if (!string.IsNullOrEmpty(InstanceName))
					return $"{ServerName}\\{InstanceName}";
				else
					return ServerName;
			}
		}

		/// <summary>
		/// lists the tables in the database
		/// </summary>
		public List<SqlDbTableInfo> Tables { get; } = new List<SqlDbTableInfo>();

		#endregion

		#region Methods

		/// <summary>
		/// queries the database for a list of tables, and adds the name of each table to the <see cref="Tables"/> collection.
		/// </summary>
		protected void QueryTables()
		{
			foreach (var info in EnumerateChildren(true))
			{
				this.Tables.Add(info);
			}
		}

		/// <summary>
		/// gets a connection string to the database
		/// </summary>
		/// <returns></returns>
		public string GetConnectionString()
		{
			return this.ConnectionString;
		}

		/// <summary>
		/// creates a connection to the database
		/// </summary>
		/// <returns></returns>
		public SqlConnection CreateConnection()
		{
			return new SqlConnection(this.ConnectionString);
		}

		/// <summary>
		/// enumerates the child objects (tables) of the current (database)
		/// </summary>
		/// <param name="fetchDetails"></param>
		/// <returns></returns>
		public IEnumerable<SqlDbTableInfo> EnumerateChildren(bool fetchDetails = false)
		{
			// open a connection
			using (var conn = new SqlConnection(this.ConnectionString))
			{
				try
				{
					conn.Open();
				}
				catch
				{
					yield break;
				}

				// query the schema for tables
				var tables = conn.GetSchema("Tables");

				// add the name of each table to the Tables list:
				foreach (DataRow row in tables.Select("1=1","TABLE_NAME ASC"))
				{
					SqlDbTableInfo info = null;
					try
					{
						info = new SqlDbTableInfo(this, row.Field<string>("TABLE_NAME"), fetchDetails);
					}
					catch (Exception error)
					{
						System.Diagnostics.Debug.Print(error.ToString());
					}

					if (info != null)
						yield return info;
				}
			}

		}

		/// <summary>
		/// string description
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			if (DetailsTask.IsCompleted)
				return $"{ServerName}.{DataBaseName} ({Tables.Count} tables)";
			else
				return $"{ServerName}.{DataBaseName}";
		}

		#endregion
	}

	/// <summary>
	/// information about an SQL server table
	/// </summary>
	public class SqlDbTableInfo
	{
		/// <summary>
		/// construct the table info
		/// </summary>
		/// <param name="db">
		/// the database the table is from
		/// </param>
		/// <param name="tableName">
		/// the name of the table
		/// </param>
		/// <param name="fetchDetails">
		/// should the details (column info) be looked up for the table?
		/// </param>
		public SqlDbTableInfo(SqlDbInfo db, string tableName, bool fetchDetails = false)
		{
			this.Database = db;
			this.TableName = tableName;
			this.RowCount = CountRows();

			if (fetchDetails)
				DetailsTask = Task.Run(() => GetDetails());
			else
				DetailsTask = Task.Run(() => { });
		}

		#region Properties

		/// <summary>
		/// gets or sets the name of the table
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// gets or sets the number of rows in the table
		/// </summary>
		public int RowCount { get; set; }

		/// <summary>
		/// gets or sets the database information the table is from
		/// </summary>
		public SqlDbInfo Database { get; set; }

		/// <summary>
		/// list of columns in the table (if fetchDetails true on construct)
		/// </summary>
		public List<SqlDbColumnInfo> Columns { get; } = new List<SqlDbColumnInfo>();

		/// <summary>
		/// the task fetching the column details (status etc)
		/// </summary>
		public Task DetailsTask { get; protected set; }

		/// <summary>
		/// description of the table including row-count
		/// </summary>
		public string Description
		{
			get { return $".{TableName} Rows: {RowCount}"; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// enumerates the child information (columns) about the current object (table)
		/// </summary>
		/// <returns></returns>
		public IEnumerable<SqlDbColumnInfo> EnumerateChildren()
		{
			using (var connect = Database.CreateConnection())
			{
				connect.Open();
				using (var da = new SqlDataAdapter($"select * from {TableName}", connect))
				{
					var dt = new DataTable();
					try
					{
						da.FillSchema(dt, SchemaType.Source);
					}
					catch { yield break; }

					foreach (var col in dt.Columns.Cast<DataColumn>().OrderBy((c)=>c.ColumnName))
					{
						SqlDbColumnInfo info = null;
						try
						{
							 info = new SqlDbColumnInfo
							 {
								 AllowNulls = col.AllowDBNull,
								 ColumnName = col.ColumnName,
								 DataType   = col.DataType.Name,
								 IsKey      = dt.PrimaryKey.Contains(col),
								 Ordinal    = col.Ordinal
							 };

						}
						catch (Exception cols_error)
						{
							System.Diagnostics.Debug.Print(cols_error.ToString());
						}

						if (info != null)
							yield return info;
					}
				}
			}
		}

		/// <summary>
		/// counts the rows in the table
		/// </summary>
		/// <returns></returns>
		int CountRows()
		{
			try
			{
				using (var connect = this.Database.CreateConnection())
				{
					connect.Open();
					using (var cmd = connect.CreateCommand())
					{
						cmd.CommandText = $"select count(*) from {TableName}";
						return Convert.ToInt32(cmd.ExecuteScalar());
					}
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.Print(e.Message);
				return -1;
			}
		}

		/// <summary>
		/// fetches the details (column details for the table)
		/// </summary>
		void GetDetails()
		{
			foreach (var info in EnumerateChildren())
			{
				this.Columns.Add(info);
			}
		}

		public override string ToString()
		{
			return $"{TableName} ({RowCount} Rows)";
		}

		#endregion

	}

	/// <summary>
	/// information about an SQL server table column
	/// </summary>
	public class SqlDbColumnInfo 
	{
		/// <summary>
		/// gets or sets the name of the column
		/// </summary>
		public string ColumnName { get; set; }

		/// <summary>
		/// gets or sets the data-type of the column
		/// </summary>
		public string DataType { get; set; }

		/// <summary>
		/// gets or sets the column ordinal
		/// </summary>
		public int Ordinal { get; set; }

		/// <summary>
		/// gets or sets if this column is a key-column
		/// </summary>
		public bool IsKey { get; set; }

		/// <summary>
		/// gets or sets if the column allows nulls.
		/// </summary>
		public bool AllowNulls { get; set; }

		/// <summary>
		/// gets a description of the column
		/// </summary>
		public string Description
		{
			get { return $"[{ColumnName}] ({DataType}) {(AllowNulls ? "null" : "not null")} {(IsKey ? "(PKEY)" : "")}"; }
		}

		/// <summary>
		/// string description
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"[{ColumnName}] ({DataType}) {(AllowNulls ? "null" : "not null")} {(IsKey ? "(PKEY)" : "")}";
		}
	}
}
