using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;

namespace DataScriptEngine
{
	/// <summary>
	/// script types supported by the engine
	/// </summary>
	public enum DbScriptType
	{
		/// <summary>
		/// creates a set of insert statements to insert all the records into the target table.
		/// if the record already exists a key violation will occur
		/// </summary>
		Insert,
		/// <summary>
		/// creates a set of update statements to update all the records in the target table. 
		/// if the record doesn't exist, no changes will be made
		/// </summary>
		Update,
		/// <summary>
		/// creates a set of delete statements to remove the records from the target table.
		/// </summary>
		Delete,
		/// <summary>
		/// creates a script that for each record, executes an IF (EXISTS()) statement then inserts or updates where appropriate.
		/// </summary>
		InsertUpdate,
		/// <summary>
		/// creates a set of delete statements followed by insert statements. effectively updates all records.
		/// </summary>
		DeleteInsert
	}

	/// <summary>
	/// a set of data in a form suitable for generating scripts.
	/// </summary>
	public class DbScriptTable
	{
		#region Properties

		/// <summary>
		/// the name of the table
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// where clause to use to restrict recods on fill.
		/// </summary>
		public string WhereClause { get; set; }

		/// <summary>
		/// comment text to be added to the top of the script output for this table
		/// </summary>
		public string Comment { get; set; }

		/// <summary>
		/// the rows loaded in to script
		/// </summary>
		public List<DbScriptRow> Rows { get;  } = new List<DbScriptRow>();

		/// <summary>
		/// schema info for each column
		/// </summary>
		public DbScriptColumnSchema[] SchemaInfo { get; set; } = null;

		#endregion

		#region Constructors

		/// <summary>
		/// construct with table-name and no where-clause
		/// </summary>
		/// <param name="tableName"></param>
		public DbScriptTable(string tableName)
		{
			this.TableName = tableName;
		}

		/// <summary>
		/// construct with a table name and where clause
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="where"></param>
		public DbScriptTable(string tableName, string where)
		{
			this.TableName   = tableName;
			this.WhereClause = where;
		}

		#endregion
	
		/// <summary>
		/// using the rows currently filled in the table, generate an Insert, Update, Delete, DeleteInsert, InsertUpdate script.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="type"></param>
		/// <param name="useTransaction"></param>
		/// <param name="printStatusGap"></param>
		public void GenerateScript(Stream target, DbScriptType type, bool useTransaction, int printStatusGap)
		{
			using (var writer = new StreamWriter(target))
			{
				writer.WriteLine($"/** Auto-Generated {type} Script. Created {DateTime.Now} by {Environment.UserName}@{Environment.UserDomainName} on {Environment.MachineName} **/");
				writer.WriteLine($"/** {type} {this.Rows.Count} Rows. Table: {this.TableName}{(!string.IsNullOrEmpty(WhereClause) ? $" Where: {WhereClause}" : "")} **/");

				if (!string.IsNullOrEmpty(Comment))
				{
					// insert the table comment:
					writer.WriteLine($"/** {Comment} **/");
				}

				if (useTransaction)
				{
					writer.WriteLine("BEGIN TRANSACTION");
				}

				int count = 0;
				foreach (var row in Rows)
				{
					if (!string.IsNullOrEmpty(row.Comment))
					{
						writer.WriteLine($"/** {row.Comment} **/");
					}
					writer.WriteLine($"/****** {count} ******/");
					switch (type)
					{

						case DbScriptType.Insert:
							writer.WriteLine(row.InsertStatement);
							break;
						case DbScriptType.Update:
							writer.WriteLine(row.UpdateStatement);
							break;
						case DbScriptType.Delete:
							writer.WriteLine(row.DeleteStatement);
							break;
						case DbScriptType.InsertUpdate:
							writer.WriteLine($"IF EXISTS(select * from {TableName} where {row.WhereClause})");
							writer.WriteLine($"{row.UpdateStatement}");
							writer.WriteLine($"ELSE");
							writer.WriteLine($"{row.InsertStatement}");
							break;
						case DbScriptType.DeleteInsert:
							writer.WriteLine(row.DeleteStatement);
							writer.WriteLine(row.InsertStatement);
							break;
						default:
							break;
					}
					count++;
					if (printStatusGap > 0 && count > 0)
					{
						if (count % printStatusGap == 0)
						{
							writer.WriteLine($"PRINT '{count}/{Rows.Count} COMPLETE'");
						}
					}
				}
				if (printStatusGap > 0)
				{
					writer.WriteLine($"PRINT '{count}/{Rows.Count} COMPLETE'");
				}
				if (useTransaction)
				{
					writer.WriteLine("COMMIT;");
				}
			}
		}

		/// <summary>
		/// returns the DDL required to create the table
		/// </summary>
		public string CreateTable
		{
			get
			{
				// define the format of the create table command:
				var fmt = "CREATE TABLE [{0}](\r\n{1}\r\n)";

				// for building the column-definitions:
				var coldef = new StringBuilder();

				// enumerate the column definitions:
				foreach (var col in this.SchemaInfo)
				{

					// get the sql-db type name appropriate to the columns data-type
					string datatype = col.SqlDbType.ToString().ToLower();

					// some data-types require parameters:
					string datatypeParam = "";

					// is the sql type a string type?
					if (col.SqlDbType.IsStringType())
					{
						// has the size been specified:
						if (col.ColumnSize > 0)
						{
							// set the data-type parameter to the specified size;
							datatypeParam = string.Format("({0})", col.ColumnSize);
						}
						else
						{
							// if no size is specified, use MAX
							datatypeParam = "(MAX)";
						}
					}
					else
					{
						// decimal types require precision and scale:
						if (col.SqlDbType == SqlDbType.Decimal)
						{
							datatypeParam = string.Format("({0},{1})", col.NumericPrecision, col.NumericScale);
						}
					}
					// just formatting:
					if (coldef.Length > 0)
					{
						coldef.AppendLine(",\r\n");
					}
					// add the column definition to the string:
					var f = new StringBuilder("\t[{0}] [{1}]{2}");
					if (col.IsIdentity)
					{
						f.Append(" IDENTITY(1,1)");
					}
					if (col.AllowDBNull)
						f.Append(" NULL");
					else
						f.Append(" NOT NULL");


					coldef.AppendFormat(f.ToString(), col.ColumnName, datatype, datatypeParam);
	
				}

				// select the primary keys from the columns enumeration:
				var pkeys = (from c in SchemaInfo where c.IsKey select c).ToArray();

				// are any primary keys defined?
				if (pkeys.Length > 0)
				{
					// calculate the name for the constraint:
					var pkName = "PK_" + TableName;

					// create the primary-key constraint definition:
					var pkCols = new StringBuilder();
					foreach (var k in pkeys)
					{
						if (pkCols.Length > 0)
							pkCols.Append(",\r\n");
						pkCols.AppendFormat("\t\t[{0}] ASC", k.ColumnName);

					}

					// prepend a comma
					if (coldef.Length > 0)
					{
						coldef.AppendLine(",\r\n");
					}

					//
					coldef.AppendFormat("\tCONSTRAINT [{0}] PRIMARY KEY CLUSTERED\r\n\t(\r\n{1}\r\n\t)\r\n", pkName, pkCols);
				}

				return string.Format(fmt, TableName, coldef);

			}
		}


		#region Fill - Methods to fill the scripting table holder

		/// <summary>
		/// populate from a data-set instead of a connection.
		/// </summary>
		/// <param name="ds"></param>
		public void Fill(DataSet ds)
		{
			if (ds.Tables.Contains(this.TableName))
			{
				// use the matching table
				var tbl = ds.Tables[this.TableName];

				// fill from that table
				Fill(tbl);
			}
			else
			{
				throw new ApplicationException($"Cannot find table {TableName}");
			}

		}

		/// <summary>
		/// fill by selecting from the specified table
		/// </summary>
		/// <param name="tbl"></param>
		public void Fill(DataTable tbl)
		{
			// get the schema from the table:
			if (SchemaInfo == null)
			{
				SchemaInfo = tbl.GetTableInfo();
			}
			
			int rowNum = 0;
			var where = this.WhereClause;
			if (string.IsNullOrEmpty(this.WhereClause))
				where = "1=1";

			foreach (var tblRow in tbl.Select(where))
			{
				var scriptRow = new DbScriptRow(this) { RowID = rowNum++ };
				foreach (var info in SchemaInfo)
				{
					scriptRow.Columns.Add(new DbScriptColumn(scriptRow)
					{
						ColumnInfo = info,
						Value = tblRow[info.ColumnName]
					});
				}
				Rows.Add(scriptRow);
			}
		}

		/// <summary>
		/// populate from an SQL server database connection
		/// </summary>
		/// <param name="connect"></param>
		public void Fill(SqlConnection connect)
		{
			if (connect.State != ConnectionState.Open)
				connect.Open();

			if (SchemaInfo == null)
				SchemaInfo = connect.GetTableInfo(this.TableName);

			int rowNum = 0;
			using (var cmd = connect.CreateCommand())
			{
				cmd.CommandText = $"SELECT * FROM [{TableName}] WHERE {WhereClause}";
				using (var rdr = cmd.ExecuteReader(CommandBehavior.KeyInfo))
				{
					while (rdr.Read())
					{
						var row = new DbScriptRow(this) { RowID = rowNum++ };
						foreach (var info in SchemaInfo)
						{
							row.Columns.Add(new DbScriptColumn(row)
							{
								ColumnInfo = info,
								Value = rdr.GetValue(info.ColumnOrdinal)
							});
						}
						Rows.Add(row);
					}
				}
			}
		}

		#endregion

		/// <summary>
		/// string representation
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			if (SchemaInfo == null)
			{
				return $"{TableName} Where {WhereClause}";
			}
			else
			{
				return $"{TableName} Where {WhereClause} Rows {Rows.Count}";
			}
		}

	}

	/// <summary>
	/// represents a single row in a table to be scripted
	/// </summary>
	public class DbScriptRow
	{
		/// <summary>
		/// construct the row and pass in the parent table reference
		/// </summary>
		/// <param name="tbl"></param>
		public DbScriptRow(DbScriptTable tbl)
		{
			this.Table = tbl;
		}

		/// <summary>
		/// a comment to be included in the script before this row is scripted
		/// </summary>
		public string Comment { get; set; }

		/// <summary>
		/// the table that owns the row
		/// </summary>
		public DbScriptTable Table { get; set; }

		/// <summary>
		/// the zero based row id within the <see cref="Table"/>
		/// </summary>
		public int RowID { get; set; }

		/// <summary>
		/// the columns in the row
		/// </summary>
		public List<DbScriptColumn> Columns { get; set; } = new List<DbScriptColumn>();

		/// <summary>
		/// delimited list of fields for the row (only populated non-identity fields)
		/// </summary>
		public string Fields
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in Columns)
				{
					// check it has a value, is not an identity field and is not read-only
					if (value.Value != null && !value.IsIdentity && !value.ColumnInfo.IsReadOnly)
					{
						if (sb.Length > 0)
							sb.Append(",");
						sb.Append($"[{value.ColumnName}]");
					}
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// delimited list of values for an insert-statment (matches the field list)
		/// </summary>
		public string Values
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in Columns)
				{        
					// check it has a value, is not an identity field and is not read-only
					if (value.Value != null && !value.IsIdentity && !value.ColumnInfo.IsReadOnly)
					{
						if (sb.Length > 0)
							sb.Append(",");
						sb.Append($"{value.ScriptValue}");
					}
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// produces a set list of values from the current row
		/// </summary>
		public string SetValues
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in Columns.Where((c) => !c.ColumnInfo.IsReadOnly && !c.ColumnInfo.IsIdentity && c.Value != null && c.Value != DBNull.Value))
				{
					if (sb.Length > 0)
						sb.Append(", ");
					sb.Append($"[{value.ColumnName}] = {value.ScriptValue}");
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// returns a where-clause that uniquely identifies the current row. there must be a UNIQUEID, IDENTITY or PRIMARY-KEY column.
		/// </summary>
		public string WhereClause
		{
			get
			{
				if (!string.IsNullOrEmpty(WhereClause__KEY))
					return WhereClause__KEY;
				if (!string.IsNullOrEmpty(WhereClause__UNIQUEID))
					return WhereClause__UNIQUEID;
				if (!string.IsNullOrEmpty(WhereClause__IDENTITY))
					return WhereClause__IDENTITY;

				throw new ApplicationException("Couldn't build a where-clause! No appropriate columns!");
			}
		}

		/// <summary>
		/// returns a where-clause built using the Primary-Key Columns
		/// </summary>
		public string WhereClause__KEY
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in Columns.Where((c) => c.ColumnInfo.IsKey && c.Value != null && c.Value != DBNull.Value))
				{
					if (sb.Length > 0)
						sb.Append(", ");
					sb.Append($"[{value.ColumnName}] = {value.ScriptValue}");
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// returns a where-clause built using any IDENTITY columns (columns where ColumnInfo.IsIdentity = true)
		/// </summary>
		public string WhereClause__IDENTITY
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in Columns.Where((c) => c.ColumnInfo.IsIdentity && c.Value != null && c.Value != DBNull.Value))
				{
					if (sb.Length > 0)
						sb.Append(", ");
					sb.Append($"[{value.ColumnName}] = {value.ScriptValue}");
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// returnd a where-clause built using any IsUnique columns
		/// </summary>
		public string WhereClause__UNIQUEID
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in Columns.Where((c) => c.ColumnInfo.IsUnique && c.Value != null && c.Value != DBNull.Value))
				{
					if (sb.Length > 0)
						sb.Append(", ");
					sb.Append($"[{value.ColumnName}] = {value.ScriptValue}");
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// returns an insert statement for the current row.
		/// </summary>
		public string InsertStatement
		{
			get
			{
				return $"INSERT INTO [{Table.TableName}] ({Fields})\r\nVALUES ({Values})";
			}
		}

		/// <summary>
		/// returns an update statement for the current row.
		/// </summary>
		public string UpdateStatement
		{
			get
			{
				return $"UPDATE [{Table.TableName}]\r\n   SET {SetValues}\r\n WHERE {WhereClause}";
			}
		}

		/// <summary>
		/// returns a delete statement for the current row.
		/// </summary>
		public string DeleteStatement
		{
			get
			{
				return $"DELETE FROM [{Table.TableName}]\r\n WHERE {WhereClause}";
			}
		}
	}

	/// <summary>
	/// represents a single column in a table with a value, used for generating scripts.
	/// </summary>
	public class DbScriptColumn
	{
		/// <summary>
		/// construct with parent reference
		/// </summary>
		/// <param name="owner"></param>
		public DbScriptColumn(DbScriptRow owner)
		{
			this.Row   = owner;
			this.Table = owner.Table;
		}

		/// <summary>
		/// the owning table
		/// </summary>
		public DbScriptTable Table { get; set; }

		/// <summary>
		/// the owning row
		/// </summary>
		public DbScriptRow Row { get; set; }

		/// <summary>
		/// the detailed schema information for the column
		/// </summary>
		public DbScriptColumnSchema ColumnInfo { get; set; }

		/// <summary>
		/// name of the column
		/// </summary>
		public string ColumnName { get { return ColumnInfo.ColumnName; } }

		/// <summary>
		/// the sql data type for the column
		/// </summary>
		public SqlDbType DbType { get { return ColumnInfo.SqlDbType; } }

		/// <summary>
		/// the .NET equivalent data type for the column
		/// </summary>
		public Type ClrType { get { return ColumnInfo.DataType; } }

		/// <summary>
		/// current value
		/// </summary>
		public object Value { get; set; }

		/// <summary>
		/// is this an identity column (shouldn't be on an insert statement)
		/// </summary>
		public bool IsIdentity { get { return ColumnInfo.IsIdentity; } }

		/// <summary>
		/// gets the delimited text value for this column formatted for output on an SQL script.
		/// </summary>
		public string ScriptValue
		{
			get
			{
				// return "null" for a DBNull
				if (Value == DBNull.Value || Value == null)
					return "null";

				// varchar etc, delimited text using single quotes:
				if (DbType.IsStringType())
				{
					return $"'{Value}'";
				}

				// format a date-time for sql server insert (0000-01-01 00:00:00)
				if (ClrType.Equals(typeof(DateTime)))
				{
					// cast to a date-time:
					DateTime dt = (DateTime)Value;

					// format to an sql server date string:
					return $"'{dt.ToString("yyyy-MM-dd hh:mm:ss")}'";
				}

				// a boolean is returned as 0 or 1
				if (ClrType.Equals(typeof(bool)))
				{
					if ((bool)Value)
					{
						return "1";
					}
					else
					{
						return "0";
					}
				}

				// everything else: just the string representation right now
				return Value.ToString();

			}
		}

		/// <summary>
		/// string representation of the column
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"{ColumnName} = {ScriptValue}";
		}
	}

	/// <summary>
	/// extension methods
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// determines if the SqlDBType is a string-type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool IsStringType(this SqlDbType type)
		{
			switch (type)
			{
				case System.Data.SqlDbType.VarChar:
				case System.Data.SqlDbType.NVarChar:
				case System.Data.SqlDbType.Char:
				case System.Data.SqlDbType.NChar:
				case System.Data.SqlDbType.NText:
				case System.Data.SqlDbType.Text:
					return true;

				default:
					return false;
			}
		}

		/// <summary>
		/// helper method: computes an SQL-DB-Type compatible with the data-type.
		/// </summary>
		/// <param name="dataType"></param>
		/// <returns></returns>
		public static System.Data.SqlDbType GetSqlDbType(this Type dataType)
		{
			if (dataType.Equals(typeof(TimeSpan)))
			{
				// there is no type code for time-span, so this must be handled seperately.
				return System.Data.SqlDbType.Time;
			}

			switch (Type.GetTypeCode(dataType))
			{
				case TypeCode.Boolean:
					return System.Data.SqlDbType.Bit;

				case TypeCode.Byte:
					return System.Data.SqlDbType.TinyInt;

				case TypeCode.Char:
					return System.Data.SqlDbType.Char;

				case TypeCode.DateTime:
					return System.Data.SqlDbType.DateTime;

				case TypeCode.Decimal:
					return System.Data.SqlDbType.Decimal;

				case TypeCode.Double:
					return System.Data.SqlDbType.Float;

				case TypeCode.Int16:
					return System.Data.SqlDbType.SmallInt;

				case TypeCode.Int32:
					return System.Data.SqlDbType.Int;

				case TypeCode.Int64:
					return System.Data.SqlDbType.BigInt;

				case TypeCode.SByte:
					return System.Data.SqlDbType.TinyInt;

				case TypeCode.Single:
					return System.Data.SqlDbType.Real;

				case TypeCode.String:
					return System.Data.SqlDbType.VarChar;

				case TypeCode.UInt16:
					return System.Data.SqlDbType.SmallInt;

				case TypeCode.UInt32:
					return System.Data.SqlDbType.Int;

				case TypeCode.UInt64:
					return System.Data.SqlDbType.BigInt;

				case TypeCode.Object:

					// object type-code means non-of-the-above:
					if (dataType.Equals(typeof(byte[])))
					{
						return System.Data.SqlDbType.Image;
					}
					else
					{
						if (dataType.Equals(typeof(Guid)))
						{
							return System.Data.SqlDbType.UniqueIdentifier;
						}
					}

					// fallback for object type: a variant column can contain different types of data.
					return System.Data.SqlDbType.Variant;

				case TypeCode.DBNull:
				case TypeCode.Empty:
				default:
					return System.Data.SqlDbType.VarChar;

			}
		}

		/// <summary>
		/// calculates the clr-type for the sql type.
		/// </summary>
		/// <param name="dbType"></param>
		/// <returns></returns>
		public static Type GetClrType(this SqlDbType dbType)
		{
			switch (dbType)
			{
				case SqlDbType.BigInt:
					return typeof(long);

				case SqlDbType.Binary:
					return typeof(byte[]);

				case SqlDbType.Bit:
					return typeof(bool);

				case SqlDbType.Char:
					return typeof(char);

				case SqlDbType.Date:
				case SqlDbType.DateTime:
				case SqlDbType.DateTime2:
				case SqlDbType.DateTimeOffset:
				case SqlDbType.SmallDateTime:
					return typeof(DateTime);

				case SqlDbType.Money:
				case SqlDbType.Decimal:
				case SqlDbType.SmallMoney:
					return typeof(decimal);

				case SqlDbType.Float:
					return typeof(double);

				case SqlDbType.Image:
					return typeof(byte[]);

				case SqlDbType.Int:
					return typeof(int);

				case SqlDbType.NChar:
				case SqlDbType.NText:
				case SqlDbType.NVarChar:
				case SqlDbType.Text:
				case SqlDbType.VarChar:
					return typeof(string);

				case SqlDbType.Real:
					return typeof(Single);

				case SqlDbType.SmallInt:
					return typeof(short);

				case SqlDbType.Structured:
					return typeof(object);

				case SqlDbType.Time:
					return typeof(TimeSpan);

				case SqlDbType.Timestamp:
				case SqlDbType.VarBinary:
					return typeof(byte[]);

				case SqlDbType.TinyInt:
					return typeof(byte);

				case SqlDbType.Udt:
					return typeof(object);

				case SqlDbType.UniqueIdentifier:
					return typeof(Guid);

				case SqlDbType.Variant:
					return typeof(object);

				case SqlDbType.Xml:
					return typeof(string);

				default:
					return typeof(string);
			}
		}

		/// <summary>
		/// gets schema information for a table from the database connected via conn
		/// </summary>
		/// <param name="conn"></param>
		/// <param name="tableName"></param>
		/// <returns></returns>
		public static DbScriptColumnSchema[] GetTableInfo(this SqlConnection conn, string tableName)
		{
			List<DbScriptColumnSchema> columns = new List<DbScriptColumnSchema>();

			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = $"select * from {tableName} where 1=2";
				using (var rdr = cmd.ExecuteReader())
				{
					var tbl = rdr.GetSchemaTable();
					var props = typeof(DbScriptColumnSchema).GetProperties();
					foreach (DataRow row in tbl.Rows)
					{
						var info = new DbScriptColumnSchema();

						foreach (var p in props)
						{
							if (tbl.Columns.Contains(p.Name))
							{
								var value = row[p.Name];
								if (value != DBNull.Value)
								{
									p.SetValue(info, value, null);
								}


							}
						}

						columns.Add(info);
					}
				}
				return columns.ToArray();
			}
				
		}

		/// <summary>
		/// gets schema information for a table from a data-set.
		/// </summary>
		/// <param name="tbl"></param>
		/// <returns></returns>
		public static DbScriptColumnSchema[] GetTableInfo(this DataTable tbl)
		{
			var columns = new List<DbScriptColumnSchema>();
			foreach (DataColumn col in tbl.Columns)
			{
				columns.Add(new DbScriptColumnSchema
				{
					ColumnName = col.ColumnName,
					AllowDBNull = col.AllowDBNull,
					BaseTableName = tbl.TableName,
					ColumnOrdinal = col.Ordinal,
					DataType = col.DataType,
					DataTypeName = col.DataType.Name,
					IsAutoIncrement = col.AutoIncrement,
					IsIdentity = col.AutoIncrement,
					IsKey = tbl.PrimaryKey.Contains(col),
					IsUnique = col.Unique ,
					IsReadOnly = col.ReadOnly
				});
			}
			return columns.ToArray();
		}

	}

	/// <summary>
	/// represents schema information about a single column in a table.
	/// </summary>
	[Serializable]
	public class DbScriptColumnSchema
	{
		#region Read/Write Property Stubs.

		/// <summary>
		/// is this a (read-only) column?
		/// </summary>
		public bool IsReadOnly { get; set; }

		/// <summary>
		/// the column-name
		/// </summary>
		public string ColumnName { get; set; }

		/// <summary>
		/// the column position (1 based)
		/// </summary>
		public int ColumnOrdinal { get; set; }

		/// <summary>
		/// the column size (ie Length)
		/// </summary>
		public int ColumnSize { get; set; }

		/// <summary>
		/// maximum precision of the column
		/// </summary>
		public short NumericPrecision { get; set; }

		/// <summary>
		/// number of digits to the right of the decimal point.
		/// </summary>
		public short NumericScale { get; set; }

		/// <summary>
		/// is this column a Key.
		/// </summary>
		public bool IsKey { get; set; }

		/// <summary>
		/// is this column Unique.
		/// </summary>
		public bool IsUnique { get; set; }

		/// <summary>
		/// the .NET datatype of the column.
		/// </summary>
		public Type DataType { get; set; }

		/// <summary>
		/// true if the column allows null values.
		/// </summary>
		public bool AllowDBNull { get; set; }

		/// <summary>
		/// true if this column is part of the key.
		/// </summary>
		public bool IsIdentity { get; set; }

		/// <summary>
		/// true if this column automatically increments itself.
		/// </summary>
		public bool IsAutoIncrement { get; set; }

		/// <summary>
		/// the name of the data type contained in the column.
		/// </summary>
		public string DataTypeName { get; set; }

		/// <summary>
		/// the sql db type equivalent.
		/// </summary>
		public System.Data.SqlDbType SqlDbType
		{
			get
			{
				return this.DataType.GetSqlDbType();
			}
		}

		/// <summary>
		/// the name of the table.
		/// </summary>
		public string BaseTableName { get; set; }

		#endregion Read/Write Property Stubs.

		/// <summary>
		/// string representation.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "Db Column " + ColumnName + "[" + ColumnOrdinal + "] Type: " + DataType.Name;
		}
	}
}
