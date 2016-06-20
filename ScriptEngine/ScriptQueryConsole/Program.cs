using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace DataScriptEngine
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				WriteLine(new String('*', Console.WindowWidth - 1));
				WriteLine($"SQL Data Scripting Engine. Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}", ConsoleColor.Green, Alignment.Middle);
				WriteLine(new String('*', Console.WindowWidth - 1));
				WriteLine("");

				if (args.Length < 3)
				{
					WriteLine("Usage:");
					WriteLine("\tDataScriptEngine.exe %1 %2 %3 %4");
					WriteLine("\t\t%1 = Connection String ");
					WriteLine("\t\t%2 = Table Name");
					WriteLine("\t\t%3 = Where Clause");
					WriteLine("\t\t%4 = (optional) output path and file-name");
					return;
				}



				string fnfmt = "{0}_{1}_insert.sql";
				string connectionString, tableName, whereClause, fileName; int ver = 0;

				connectionString = args[0];
				tableName		 = args[1];
				whereClause		 = args[2];


				try
				{
					// validate connection string
					var csb = new SqlConnectionStringBuilder(connectionString);
					WriteLine($"Server   : { csb.DataSource }",		ConsoleColor.Cyan);
					WriteLine($"Database : { csb.InitialCatalog }", ConsoleColor.Cyan);

				}
				catch (FormatException fe)
				{
					WriteLine("Invalid Connection String!" + fe.Message, ConsoleColor.Red);
					WriteLine("Please Correct the Connection String and Retry", ConsoleColor.Red);

					return;
				}

				if (args.Length > 3)
				{
					// set the file-name from the args;
					fileName = args[3];
				}
				else
				{
					// calculate a file-name:
					while (true)
					{
						fileName = String.Format(fnfmt, tableName, ver++);
						if (!File.Exists(fileName))
							break;
					}
				}

				// report the file-name;
				WriteLine("File-Name: " + fileName);

				try
				{
					// create the connection:
					using (var connect = new SqlConnection(args[0]))
					{
						// open:
						connect.Open();

						// check state:
						if (connect.State == System.Data.ConnectionState.Open)
						{
							WriteLine("Connected...", ConsoleColor.Green);
						}

						// create the table info object:
						var tbl = new DbScriptTable(args[1], args[2]);

						// execute the fill:
						WriteLine($"Table: {tbl.TableName}", ConsoleColor.Cyan, Alignment.Middle);
						WriteLine("... executing fill ...",  ConsoleColor.Green, Alignment.Middle);
						WriteLine();

						var sw = new System.Diagnostics.Stopwatch();
						sw.Start();
						tbl.Fill(connect);
						sw.Stop();

						// report records returned:
						WriteLine($"Filled {tbl.Rows.Count} records in {sw.ElapsedMilliseconds} ms.", ConsoleColor.Yellow);
						
						// build the output script:
						sw.Reset(); sw.Start(); int count = 0;
						using (var writer = new StreamWriter(File.OpenWrite(fileName)))
						{
							writer.WriteLine($"-- auto generated script created by {Environment.UserName} at {DateTime.Now} on {Environment.MachineName}");
							writer.WriteLine($"-- insert {tbl.Rows.Count} records into {tbl.TableName} where {tbl.WhereClause}");
							writer.WriteLine($"BEGIN TRANSACTION");
							foreach (var row in tbl.Rows)
							{
								writer.WriteLine(row.InsertStatement);	count++;
								if (count % 10 == 0)
									writer.WriteLine($"PRINT 'Inserted {count}/{tbl.Rows.Count}'");
							}
							writer.WriteLine($"PRINT 'Inserted {count}/{tbl.Rows.Count}'");
							writer.WriteLine($"COMMIT");
							writer.WriteLine($"GO");
						}

						sw.Stop();
						WriteLine($"Exported {count} Rows to {fileName} in {sw.ElapsedMilliseconds} ms.", ConsoleColor.Yellow);

					}
				}
				catch (Exception e)
				{
					WriteLine("Errors Occurred!", ConsoleColor.Red);
					string tab = "";
					while (e != null)
					{
						WriteLine(tab + e.GetType().Name, ConsoleColor.Yellow);
						WriteLine(tab + e.Message,		  ConsoleColor.Yellow);
						WriteLine(tab + e.StackTrace,     ConsoleColor.Yellow);
						e = e.InnerException;
						tab += "\t";
					}
				}

			}
			finally
			{
				WriteLine("Any Key to Exit", ConsoleColor.Green, Alignment.Middle);
				Console.ReadKey(true);
			}
		}

		public enum Alignment
		{
			Left, Middle, Right
		}

		public static void WriteLine(string message = "", ConsoleColor color = ConsoleColor.White, Alignment textAlign = Alignment.Left)
		{
			string pad = null;
			switch (textAlign)
			{
				case Alignment.Left:
					pad = "";
					break;
				case Alignment.Right:
					pad = new string(' ', Console.WindowWidth - message.Length - 1);
					break;
				case Alignment.Middle:
					pad = new string(' ', (Console.WindowWidth - message.Length - 1) / 2);
					break;
			}
			var bk = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(pad + message);
			Console.ForegroundColor = bk;

		}
	}
}
