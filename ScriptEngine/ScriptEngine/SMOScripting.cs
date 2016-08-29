using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using System.IO;
using Microsoft.SqlServer.Management.Common;

namespace DataScriptEngine
{
	public class SMOScripting
	{
		public static void ScriptTableDef(string connect, string dbName, string tblName, Stream output)
		{
			using (var conn = new System.Data.SqlClient.SqlConnection(connect))
			{
				conn.Open();
				var scn = new ServerConnection(conn) { };
				var txt = new StreamWriter(output);

				scn.Connect();
				try
				{
					var srv = new Server(scn);
					
					var dbs = new Database(srv, dbName);
					
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
		}

		public static string GetScriptTableDef(string serverName, string dbName, string tbName)
		{
			using (var ms = new MemoryStream())
			{
				ScriptTableDef(serverName, dbName, tbName, ms);
				return Encoding.UTF8.GetString(ms.ToArray());
			}
		}


		public static void ScriptEntireDB(string serverName, string dbName, Stream output)
		{
			// create a stream-writer:
			var text = new StreamWriter(output);

			// connect to the server:
			var server = new Server(serverName);

			// reference the database:
			Database db = server.Databases[dbName];

			// define the scripter object:
			var scripter = new Scripter(server);

			scripter.Options.ScriptSchema = true;
			//scripter.Options.ScriptData = true;
			scripter.Options.ScriptDrops = false;
			scripter.Options.WithDependencies = true;
			scripter.Options.Indexes = true;
			scripter.Options.DriAllConstraints = true;

			foreach (Table obj in db.Tables)
			{
				if (!obj.IsSystemObject)
				{
					var script = scripter.Script(new[] { obj });

					foreach (var line in script)
					{
						text.WriteLine(line);
					}
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
		}

	}
}
