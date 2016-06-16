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
		/// <summary>
		/// returns the description of the server/
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return this.Description;
		}

		/// <summary>
		/// gets a connection string for this SQL server
		/// </summary>
		protected string ConnectionString
		{

			get
			{
				return $"Data Source ={DataSource};Integrated Security=True;Connection Timeout=1";
			}
		}

		/// <summary>
		/// gets the DataSource part of the connection string.
		/// </summary>
		protected string DataSource
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

		/// <summary>
		/// this is the task retrieving the database names from the server
		/// </summary>
		public Task DatabaseListTask { get; set; }

		/// <summary>
		/// the name of the server (computer name)
		/// </summary>
		public string ServerName { get; set; } = ""; // don't want this to be able to be null;

		/// <summary>
		/// the instance name if required
		/// </summary>
		public string InstanceName { get; set; }

		/// <summary>
		/// the version of the server (if known)
		/// </summary>
		public string Version { get; set; }

		/// <summary>
		/// a list of databases on that server.
		/// </summary>
		public List<SqlDbInfo> Databases { get; } = new List<SqlDbInfo>();
		
		/// <summary>
		/// pulls out a list of databases on the server.
		/// </summary>
		public void QueryDatabases()
		{
			try
			{
				// create a new connection using the connection string:
				using (var conn = new SqlConnection(this.ConnectionString))
				{
					// open and query for databases:
					conn.Open();
					var tbl = conn.GetSchema("Databases");

					// enumerate the databases and create a DBInfo for each row:
					foreach (DataRow row in tbl.Rows)
					{
						// add the db-info to the databases collection.
						Databases.Add(new SqlDbInfo(ServerName, InstanceName, row.Field<string>("database_name"), true));
					}
				}
			}
			catch { }
		}

		/// <summary>
		/// pulls out a list of databases on the server as an enumerable;
		/// </summary>
		public IEnumerable<SqlDbInfo> EnumerateChildren()
		{
			// create a new connection using the connection string:
			using (var conn = new SqlConnection(this.ConnectionString))
			{
				// open and query for databases:
				conn.Open();

				// get the list of databases:
				var tbl = conn.GetSchema("Databases");

				// enumerate the databases and create a DBInfo for each row:
				foreach (DataRow row in tbl.Select("1=1","database_name asc"))
				{
					// add the db-info to the databases collection.
					yield return new SqlDbInfo(ServerName, InstanceName, row.Field<string>("database_name"), false);
				}
			}
		}

		/// <summary>
		/// property returning the results of <see cref="EnumerateChildren"/>
		/// </summary>
		public IEnumerable<SqlDbInfo> Children {  get { return EnumerateChildren(); } }

		/// <summary>
		/// description of the server including instance name and version (if specified)
		/// </summary>
		public string Description
		{
			get {
				if (!string.IsNullOrEmpty(InstanceName))
					return $"{ServerName}\\{InstanceName} ({Version})";
				else
					return ServerName;
			}
		}

		/// <summary>
		/// uses the <see cref="System.Data.Sql.SqlDataSourceEnumerator"/> to discover all SQL servers visible from the current network location.
		/// </summary>
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
	}

	/// <summary>
	/// information about an SQL server database.
	/// </summary>
	public class SqlDbInfo 
	{
		/// <summary>
		/// construct the database information.
		/// </summary>
		/// <param name="server"></param>
		/// <param name="instance"></param>
		/// <param name="db"></param>
		public SqlDbInfo(string server, string instance, string db, bool fetchDetails = false)
		{
			this.ServerName = server;
			this.InstanceName = instance;
			this.DBName = db;

			if (fetchDetails)
				this.DetailsTask = Task.Run(() => GetDetails());
			else
				this.DetailsTask = Task.Run(() => { });
		}

		/// <summary>
		/// construct from one of the database records in a <see cref="SqlServerInfo"/>
		/// </summary>
		/// <param name="serverInfo"></param>
		/// <param name="idx"></param>
		public SqlDbInfo(SqlServerInfo serverInfo, int idx)
			: this(serverInfo.ServerName, serverInfo.InstanceName, serverInfo.Databases[idx].DBName)
		{

		}

		public override string ToString()
		{
			if (DetailsTask.IsCompleted)
				return $"{ServerName}.{DBName} ({Tables.Count} tables)";
			else
				return $"{ServerName}.{DBName} - Working";

		}

		/// <summary>
		/// gets the task used to fetch the details of the connection
		/// </summary>
		public Task DetailsTask { get; protected set; }

		/// <summary>
		/// queries the database for a list of tables, and adds the name of each table to the <see cref="Tables"/> collection.
		/// </summary>
		void GetDetails()
		{
			try
			{
				// open a connection
				using (var conn = new SqlConnection(this.ConnectionString))
				{
					conn.Open();

					// query the schema for tables
					var tables = conn.GetSchema("Tables");

					// add the name of each table to the Tables list:
					foreach (DataRow row in tables.Rows)
					{
						Tables.Add(new SqlDbTableInfo(this, row.Field<string>("TABLE_NAME"), true));
					}
				}
			}
			catch { }
		}


		public IEnumerable<SqlDbTableInfo> EnumerateChildren()
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
				foreach (DataRow row in tables.Rows)
				{
					yield return new SqlDbTableInfo(this, row.Field<string>("TABLE_NAME"));
				}
			}

		}

		/// <summary>
		/// children of the database: tables
		/// </summary>
		public IEnumerable<SqlDbTableInfo> Children {  get { return EnumerateChildren(); } }

		public string Description
		{
			get
			{
				return $"{ServerName}.{DBName}";
			}
		}

		/// <summary>
		/// calculates a connection string to the database
		/// </summary>
		protected string ConnectionString
		{
			get { return $"Data Source={DataSource};Initial Catalog={DBName};Integrated Security=True; Connection Timeout={ConnectionTimeout}"; }
		}

		/// <summary>
		/// gets a connection string to the database
		/// </summary>
		/// <returns></returns>
		public string GetConnectionString()
		{
			return this.ConnectionString;
		}

		public SqlConnection CreateConnection()
		{
			return new SqlConnection(this.ConnectionString);
		}

		/// <summary>
		/// the name of the database
		/// </summary>
		public string DBName { get; set; }

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
		public int ConnectionTimeout { get; set; } = 1;

		/// <summary>
		/// gets the data source part of the connection string.
		/// </summary>
		public string DataSource
		{
			get
			{
				if (InstanceName != null)
					return $"{ServerName}\\{InstanceName}";
				else
					return ServerName;
			}
		}

		/// <summary>
		/// lists the tables in the database
		/// </summary>
		public List<SqlDbTableInfo> Tables { get; } = new List<SqlDbTableInfo>();

	}

	/// <summary>
	/// information about an SQL server table
	/// </summary>
	public class SqlDbTableInfo
	{
		public string TableName { get; set; }
		public int RowCount { get; set; }
		public SqlDbInfo Database { get; set; }
		public List<SqlDbColumnInfo> Columns { get; } = new List<SqlDbColumnInfo>();

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

					foreach (DataColumn col in dt.Columns)
					{
						yield return new SqlDbColumnInfo { AllowNulls = col.AllowDBNull, ColumnName = col.ColumnName, DataType = col.DataType.Name, IsKey = dt.PrimaryKey.Contains(col), Ordinal = col.Ordinal };
					}

				}

			}
		}

		public IEnumerable<SqlDbColumnInfo> Children
		{
			get { return EnumerateChildren(); }
		}

		public string Description
		{
			get { return $".{TableName} Rows: {RowCount}"; }
		}

		public SqlDbTableInfo(SqlDbInfo db, string tableName, bool fetchDetails = false)
		{
			this.Database  = db;
			this.TableName = tableName;

			try
			{
				using (var connect = db.CreateConnection())
				{
					connect.Open();
					using (var cmd = connect.CreateCommand())
					{
						cmd.CommandText = $"select count(*) from {tableName}";
						this.RowCount = Convert.ToInt32(cmd.ExecuteScalar());
					}
				}
			}
			catch(Exception e) {
				System.Diagnostics.Debug.Print(e.Message); }

			if (fetchDetails)
				Task.Run(() => GetDetails(db, tableName));
		}

		void GetDetails(SqlDbInfo db, string tableName)
		{
			this.TableName = tableName;
			using (var connect = db.CreateConnection())
			{
				connect.Open();
				using (var da = new SqlDataAdapter($"select * from {tableName}", connect))
				{
					var dt = new DataTable();

					da.FillSchema(dt, SchemaType.Source);

					foreach (DataColumn col in dt.Columns)
					{
						Columns.Add(new SqlDbColumnInfo { AllowNulls = col.AllowDBNull, ColumnName = col.ColumnName, DataType = col.DataType.Name, IsKey = dt.PrimaryKey.Contains(col), Ordinal = col.Ordinal });
					}

				}
			}
		}

		public override string ToString()
		{
			return $"{TableName} ({RowCount} Rows)";
		}
	}

	/// <summary>
	/// information about an SQL server table column
	/// </summary>
	public class SqlDbColumnInfo 
	{
		public string ColumnName { get; set; }
		public string DataType { get; set; }
		public int Ordinal { get; set; }
		public bool IsKey { get; set; }
		public bool AllowNulls { get; set; }
		public string Description
		{
			get { return $"[{ColumnName}] ({DataType}) {(AllowNulls ? "null" : "not null")} {(IsKey ? "(PKEY)" : "")}"; }
		}
		public override string ToString()
		{
			return $"[{ColumnName}] ({DataType}) {(AllowNulls ? "null" : "not null")} {(IsKey ? "(PKEY)" : "")}";
		}
	}
}
