using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using System.IO;
using Microsoft.SqlServer.Management.Common;
using System.Threading.Tasks;

namespace DataScriptEngine
{
	public class SMOScripting
	{
		public static void ScriptTableDef(string connect, string tblName, Stream output)
		{
			using (var conn = new System.Data.SqlClient.SqlConnection(connect))
			{
				CreateTableScript(tblName, output, conn);
			}
		}

		public static void CreateTableScript(string tblName, Stream output, System.Data.SqlClient.SqlConnection conn)
		{
			// open the connection if it is not already open
			if (conn.State != System.Data.ConnectionState.Open)
				conn.Open();

			
			// create the SMO server-connection
			var scn = new ServerConnection(conn) { };

			// create the writer to append to the output stream
			var txt = new StreamWriter(output);

			// connect the server-connection
			scn.Connect();
			try
			{
				var srv = new Server(scn);
				var dbs = srv.Databases[conn.Database];
				var tbl = dbs.Tables[tblName];
				if (tbl != null)
				{
					// create the scripter:
					var script = new Scripter(srv);

					// setup:
					script.Options.ScriptData = false;
					script.Options.ScriptSchema = true;
					script.Options.WithDependencies = true;
					script.Options.DriAllConstraints = true;
					script.Options.ClusteredIndexes = true;
					script.Options.Indexes = true;

					// generate the script:
					foreach (var line in script.Script(new[] { tbl }))
					{
						// output the line:
						txt.WriteLine(line);
					}


				}


			}
			finally
			{
				txt.Dispose();
			}
		}

		public static string GetCreateTableScript(string tbName, System.Data.SqlClient.SqlConnection conn)
		{
			using (var ms = new MemoryStream())
			{
				CreateTableScript(tbName, ms, conn);
				return Encoding.UTF8.GetString(ms.ToArray());
			}
		}

		public static async Task<string> GetCreateTableScriptAsync(string tbName, System.Data.SqlClient.SqlConnection conn)
		{
			return await Task.Run(() => GetCreateTableScript(tbName, conn));
		}


		public static async void ScriptEntireDB(System.Data.SqlClient.SqlConnection conn, Stream output)
		{
			await Task.Run(() =>
			{
				// create a stream-writer:
				var text = new StreamWriter(output);

				// create the server connection;
				var scn = new ServerConnection(conn);

				// connect to the server:
				var server = new Server(scn);

				// reference the database:
				Database db = server.Databases[conn.Database];

				// define the scripter object:
				var scripter = new Scripter(server);

				scripter.Options.ScriptSchema = true;
				//scripter.Options.ScriptData = true;
				scripter.Options.ScriptDrops = false;
				scripter.Options.WithDependencies = true;
				scripter.Options.Indexes = true;
				scripter.Options.DriAllConstraints = true;

				var tables = (from Table t in db.Tables where !t.IsSystemObject select t).ToArray();
				{
					var script = scripter.Script(tables);

					foreach (var line in script)
					{
						text.WriteLine(line);
					}

				}


				foreach (SqlSmoObject obj in db.StoredProcedures)
				{
					var script = scripter.Script(new[] { obj });
					foreach (var line in script)
					{
						text.WriteLine(line);
					}
				}

				foreach (SqlSmoObject obj in db.Views)
				{
					var script = scripter.Script(new[] { obj });
					foreach (var line in script)
					{
						text.WriteLine(line);
					}
				}

				foreach (SqlSmoObject obj in db.UserDefinedFunctions)
				{
					var script = scripter.Script(new[] { obj });
					foreach (var line in script)
					{
						text.WriteLine(line);
					}
				}

				foreach (SqlSmoObject obj in db.Users)
				{
					var script = scripter.Script(new[] { obj });
					foreach (var line in script)
					{
						text.WriteLine(line);
					}
				}

				foreach (SqlSmoObject obj in server.Logins)
				{
					var script = scripter.Script(new[] { obj });
					foreach (var line in script)
					{
						text.WriteLine(line);
					}
				}
			});
		}

	}
}
