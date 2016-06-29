using DataScriptEngine;
using Quick.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScriptView
{
	/// <summary>
	/// script target enumeration
	/// </summary>
	public enum ScriptTo
	{
		File, Clipboard
	}

	/// <summary>
	/// view model for a data-set and query view
	/// </summary>
	class DataSetViewModel : SimpleViewModel
	{
		public DataSetViewModel()
			: base()
		{
			// create a new data-set;
			this.Model = new DataSet();

			// start querying for sql servers:
			this.Servers = new SqlEnvironmentViewModel(this);

			// set initial options:
			this.UseTransaction = true;
			this.PrintStatusCount = 50;
			this.SelectedScriptType = DbScriptType.InsertUpdate;
			this.ScriptToClipboard = false;


			if (InDesignMode)
			{
				// add example data for design mode:
				var t = new DataTable("ExampleTable");
				t.Columns.Add("Name", typeof(string));
				t.Columns.Add("Value", typeof(string));
				t.Rows.Add("Why", "Because");
				t.Rows.Add("Thing", "Thingy");

				this.Model.Tables.Add(t);
				this.SelectedTable = this.Tables.FirstOrDefault();
				OnPropertyChanged(nameof(Tables));
				this.CommandText = "select * from [ExampleTable]";
				this.SelectedConnection = new SqlDbInfo("SomeServer", "SomeInstance", "SomeDatabase");
			}

		}

		#region SQL Servers

		/// <summary>
		/// gets or sets a query to execute against the selected connection.
		/// </summary>
		public string CommandText
		{
			get { return GetValue(() => CommandText); }
			set { SetValue(() => CommandText, value); }
		}

		/// <summary>
		/// view-model for the servers tree-view
		/// </summary>
		public SqlEnvironmentViewModel Servers
		{
			get { return GetValue(() => Servers); }
			set { SetValue(() => Servers, value); }
		}

		/// <summary>
		/// the sql server selected in the tree-view
		/// </summary>
		public SqlServerInfo SelectedSqlServer
		{
			get { return GetValue(() => SelectedSqlServer); }
			set { SetValue(() => SelectedSqlServer, value); }
		}

		/// <summary>
		/// the currently selected database	connection
		/// </summary>
		public SqlDbInfo SelectedConnection
		{
			get { return GetValue(() => SelectedConnection); }
			set { SetValue(() => SelectedConnection, value); }
		}

		/// <summary>
		/// the currently selected database connection table
		/// </summary>
		public SqlDbTableInfo SelectedConnectionTable
		{
			get { return GetValue(() => SelectedConnectionTable); }
			set { SetValue(() => SelectedConnectionTable, value); }
		}

		#endregion

		#region Commands

		/// <summary>
		/// command to execute the select statement and return the result to the data-set;
		/// </summary>
		public ICommand ExecuteSelect
		{
			get
			{
				return new RelayCommand(
					() => this.SelectedConnection != null,
					ExecuteQuery
					);
			}
		}

		/// <summary>
		/// command to save the data-set to xml
		/// </summary>
		public ICommand SaveAsXML
		{
			get
			{
				return new RelayCommand(() => Model != null, ExecuteSave);
			}
		}

		/// <summary>
		/// command to load data set from xml
		/// </summary>
		public ICommand LoadFromXML
		{
			get
			{
				return new RelayCommand(() => Model != null, ExecuteLoad);
			}

		}

		/// <summary>
		/// command to generate the script with the currently selected options
		/// </summary>
		public ICommand GenerateScript
		{
			get
			{
				return new RelayCommand(() => SelectedTable != null, ExecuteCreateScript);
			}
		}

		/// <summary>
		/// command to generate the script for the entire set
		/// </summary>
		public ICommand GenerateDataSetScript
		{
			get
			{
				// command to script the entire data-set:
				return new RelayCommand(ExecuteScriptDataSet);
			}
		}

		/// <summary>
		/// command to create the select statement to query the currently selected table
		/// </summary>
		public ICommand CreateSelectStatement
		{
			get { return new RelayCommand(() => SelectedConnectionTable != null, ExecuteCreateSelect); }
		}

		/// <summary>
		/// command to download schema for the current connection
		/// </summary>
		public ICommand DownloadSchema
		{
			get { return new RelayCommand(() => SelectedConnection != null, ExecuteGetSchema); }
		}

		public ICommand AddServer
		{
			get { return new RelayCommand(ExecuteAddServer); }
		}

		#endregion

		#region Scripting Options Properties

		/// <summary>
		/// the available types of scripts to create
		/// </summary>
		public IEnumerable<DbScriptType> ScriptTypes
		{
			get
			{
				yield return DbScriptType.Insert;
				yield return DbScriptType.InsertUpdate;
				yield return DbScriptType.DeleteInsert;
				yield return DbScriptType.Update;
				yield return DbScriptType.Delete;
			}
		}

		/// <summary>
		/// the type of script to create
		/// </summary>
		public DbScriptType SelectedScriptType
		{
			get { return GetValue(() => SelectedScriptType); }
			set { SetValue(() => SelectedScriptType, value); }
		}

		/// <summary>
		/// should the script use a transaction?
		/// </summary>
		public bool UseTransaction
		{
			get { return GetValue(() => UseTransaction); }
			set { SetValue(() => UseTransaction, value); }

		}

		/// <summary>
		/// how often should the script add a progress print statement
		/// </summary>
		public int PrintStatusCount
		{
			get { return GetValue(() => PrintStatusCount); }
			set { SetValue(() => PrintStatusCount, value); }
		}

		/// <summary>
		/// should the script-output be copied to the clipboad instead of saved to disk?
		/// </summary>
		public bool ScriptToClipboard
		{
			get { return GetValue(() => ScriptToClipboard); }
			set { SetValue(() => ScriptToClipboard, value); }
		}

		#endregion

		/// <summary>
		/// builds the context menu items for the selected data-set-table
		/// </summary>
		public IEnumerable<FrameworkElement> SelectedTableContextActions
		{
			get
			{
				if (SelectedTable == null)
					yield break;

				// create the required items:
				var mnuCols = new MenuItem { Header = "Columns" };
				var mnuPKey = new MenuItem { Header = "Update Primary Key", ToolTip = "Sets the table's primary-key to be the checked columns", IsEnabled = false };
				var mnuScript = new MenuItem { Header = $"Script {SelectedTable.TableName}" };
				var mnuScriptInsert = new MenuItem { Header = "As Insert" };
				var mnuScriptUpdate = new MenuItem { Header = "As Update" };
				var mnuScriptDelete = new MenuItem { Header = "As Delete" };
				var mnuRemove = new MenuItem { Header = $"Remove {SelectedTable.TableName}", Tag = SelectedTable };

				// create a sub-context menu containing one of each column:
				// to enable the operator to change the primary key sequence:
				foreach (var col in SelectedTable.Columns.Cast<DataColumn>())
				{
					var mnuCol = new MenuItem
					{
						Header = col.ColumnName,
						IsCheckable = true,
						IsChecked = SelectedTable.PrimaryKey.Contains(col),
						StaysOpenOnClick = true,
						Tag = col
					};
					if (mnuCol.IsChecked)
					{
						mnuCol.FontWeight = FontWeights.Bold;
						mnuCol.Foreground = Brushes.Blue;
					}
					mnuCol.Checked   += (s, e) => mnuPKey.IsEnabled = true;
					mnuCol.Unchecked += (s, e) => mnuPKey.IsEnabled = true;

					// add to the 'columns' menu
					mnuCols.Items.Add(mnuCol);
				}

				mnuCols.Items.Add(new Separator());
				mnuCols.Items.Add(mnuPKey);

				
				mnuScript.Icon = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/images/script_32xLG.png", UriKind.Absolute)) };
				mnuScript.Items.Add(mnuScriptInsert);
				mnuScript.Items.Add(mnuScriptUpdate);
				mnuScript.Items.Add(mnuScriptDelete);
				

				// assign the click event handlers:
				mnuPKey.Click += (s, e) =>
				{
					var menu = s as MenuItem;
					var p = menu?.Parent as MenuItem;
					DataTable tbl = null;
					if (p != null)
					{
						// build a list of primary key columns from the checked boxes:
						List<DataColumn> pkey = new List<DataColumn>();
						foreach (var item in p.Items)
						{
							var mnuItem = item as MenuItem;
							if (mnuItem != null)
							{
								var col = mnuItem.Tag as DataColumn;
								if (col != null)
								{
									if (tbl == null)
										tbl = col.Table;

									if (mnuItem.IsChecked)
									{
										pkey.Add(col);
									}
								}

							}

						}
						if (tbl != null)
						{
							try
							{
								// update the primary key
								tbl.PrimaryKey = pkey.ToArray();
								menu.IsEnabled = false;
							}
							catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
						}
					}
				};
				mnuScriptInsert.Click += (s, e) =>
				{
					this.SelectedScriptType = DbScriptType.Insert;
					this.ExecuteCreateScript(null);
				};
				mnuScriptUpdate.Click += (s, e) => 
				{
					this.SelectedScriptType = DbScriptType.Update;
					this.ExecuteCreateScript(null);
				};
				mnuScriptDelete.Click += (s, e) => 
				{
					this.SelectedScriptType = DbScriptType.Delete;
					this.ExecuteCreateScript(null);
				};
				mnuRemove.Click += (s, e) =>
				{
					var mnu = s as MenuItem;
					if (mnu != null)
					{
						var tbl = mnu.Tag as DataTable;
						if (tbl != null)
						{
							var set = tbl.DataSet;
							if (set != null)
							{
								if (System.Windows.MessageBox.Show($"Remove {SelectedTable.TableName} From the Data-Set?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
								{
									set.Tables.Remove(tbl);
									this.Model = set;
									if (set.Tables.Count > 0)
										this.SelectedTable = set.Tables[0];
									else
										this.SelectedTable = null;
								}
							}
						}


					}
				};

				// create a menu-item to set the "scriptName" extended property (if it is set)
				if (SelectedTable.ExtendedProperties.ContainsKey("scriptName"))
				{
					// create the menu:
					var mnuName = new MenuItem { Header = "Set Script Output Name" };

					// create a text-box:
					var txt = new TextBox { Text = SelectedTable.ExtendedProperties["scriptName"] as string };

					txt.HorizontalAlignment = HorizontalAlignment.Stretch;
					txt.HorizontalContentAlignment = HorizontalAlignment.Left;
					txt.MinWidth   = 100;
					txt.Padding    = new Thickness(2);
					txt.Margin     = new Thickness(3);
					txt.FontFamily = new FontFamily("Consolas");

					// add to the menu:
					mnuName.Items.Add("Update Script Output Name:");
					mnuName.Items.Add(txt);
					mnuName.StaysOpenOnClick = true;

					// update the property value whenever the text changes:
					txt.TextChanged += (s, e) => SelectedTable.ExtendedProperties["scriptName"] = ((TextBox)s).Text;

					yield return mnuName;
				}

				// yield the menu as built:
				yield return mnuRemove;
				yield return new Separator();
				yield return mnuScript;
				yield return new Separator();
				yield return mnuCols;
				
			}
		}

		/// <summary>
		/// text for the generate script button
		/// </summary>
		public string GenerateScriptButtonText
		{
			get
			{
				return $"{SelectedScriptType} for {SelectedTable.TableName}";
			}
		}

		/// <summary>
		/// text for the generate script button
		/// </summary>
		public string GenerateDataSetScriptButtonText
		{
			get
			{
				return $"{SelectedScriptType} for All";
			}
		}

		#region Command Methods

		/// <summary>
		/// creates a script from the selected data-table with the selected options
		/// </summary>
		/// <param name="param">not used, pass in as null</param>
		protected void ExecuteCreateScript(object param)
		{
			if (SelectedTable != null)
			{
				// create a file-save dialog:
				var dlg = new Microsoft.Win32.SaveFileDialog();

				// store the dialog result:
				bool? result = null;

				// saving to disk?
				if (!ScriptToClipboard)
				{
					// setup and display the dialog:
					dlg.Filter = "SQL Scripts (*.SQL)|*.sql";
					dlg.Title = $"Save {this.SelectedScriptType} Script for {SelectedTable.TableName} As";
					dlg.FileName = $"{SelectedScriptType}_{SelectedTable.TableName}_{DateTime.Now.ToString("yyyyMMddhhmm")}.SQL";

					// record the result:
					result = dlg.ShowDialog();
				}

				// end point is setup:
				if (this.ScriptToClipboard || result.HasValue && result.Value)
				{
					try
					{
						// set the form to busy
						this.IsBusy = true;

						// create the table script container:
						var tbl = new DbScriptTable(SelectedTable.TableName);

						// fill from the data-set;
						tbl.Fill(SelectedTable);

						// set the script table name from the "scriptName" extended property
						if (SelectedTable.ExtendedProperties.ContainsKey("scriptName"))
						{
							tbl.TableName = SelectedTable.ExtendedProperties["scriptName"] as string;
						}

						// set the comment on the table; this will be added to the top of the script;
						// does the table reference the original select statement?
						if (SelectedTable.ExtendedProperties.ContainsKey("select"))
						{
							// include the original select
							// does it also reference the connection string?
							if (SelectedTable.ExtendedProperties.ContainsKey("connect"))
							{
								// set the table comment to quote the original select statement and connect string.
								tbl.Comment = $"original query: {SelectedTable.ExtendedProperties["select"]}\r\n    from: {SelectedTable.ExtendedProperties["connect"]}";
							}
							else
							{
								// just set the table comment to quote the original select statement
								tbl.Comment = $"original query: {SelectedTable.ExtendedProperties["select"]}";
							}
						}

						if (ScriptToClipboard)
						{
							// write the script into an in-memory stream
							using (var ms = new MemoryStream())
							{
								// generate the script using the current options and writing into the memory stream
								tbl.GenerateScript(ms, this.SelectedScriptType, this.UseTransaction, this.PrintStatusCount);

								// set the clipboard text:
								Clipboard.SetText(Encoding.UTF8.GetString(ms.ToArray()));

								// confirmation message box:
								MessageBox.Show("Script Copied to Clipboard");
							}
						}
						else
						{
							// now generate the script:
							using (var fs = File.OpenWrite(dlg.FileName))
							{
								// generate the script:
								tbl.GenerateScript(fs, this.SelectedScriptType, this.UseTransaction, this.PrintStatusCount);
							}
						}
					}
					catch (Exception e)
					{
						// show the exception message:
						System.Windows.MessageBox.Show(e.Message);

					}
					finally
					{
						// clear the wait cursor
						this.IsBusy = false;
					}
				}
			}
		}

		/// <summary>
		/// scripts all tables in the set using selected options.
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteScriptDataSet(object param)
		{
			// target for script generation
			Stream target = null;

			// set memory stream for clipboard target:
			if (this.ScriptToClipboard)
			{
				target = new MemoryStream();
			}
			else
			{
				// get file-name from user:
				var dlg = new Microsoft.Win32.SaveFileDialog();
				dlg.Title    = "Save Data-Set Script As";
				dlg.FileName = $"Data_{this.SelectedScriptType}.SQL";
				dlg.Filter   = "T-SQL Script Files (*.SQL)|*.SQL";
				var r = dlg.ShowDialog();
				if (r.HasValue && r.Value)
				{
					// set the target as a file-stream:
					target = File.OpenWrite(dlg.FileName);
				}
				else
					return;
			}
			try
			{
				// enumerate the data-tables in the set:
				foreach (DataTable tbl in this.Model.Tables)
				{
					// create and fill a scripting table:
					var scriptTbl = new DbScriptTable(tbl.TableName);
					    scriptTbl.Fill(this.Model);

					// set the table-name from the script-name property:
					if (tbl.ExtendedProperties.ContainsKey("scriptName"))
					{
						// allows multiple tables to output to the same destination:
						scriptTbl.TableName = tbl.ExtendedProperties["scriptName"].ToString();
					}

					// output the script to the target stream;
					scriptTbl.GenerateScript(target, this.SelectedScriptType, this.UseTransaction, this.PrintStatusCount);

				}

				if (ScriptToClipboard)
				{
					// set the contents of the clipboard
					Clipboard.SetText(Encoding.UTF8.GetString(((MemoryStream)target).ToArray()));

					// notification
					MessageBox.Show("Script Copied To Clipboard");
				}
			}
			catch (Exception e)
			{
				// show the exception message:
				MessageBox.Show(e.Message);
			}
			finally
			{
				// cleanup stream:
				target.Dispose();
			}
			
		}

		/// <summary>
		/// adds a server 
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteAddServer(object param)
		{
			var sn = InputBox.GetInput("Enter Server Name", "Add SQL Server");
			if (!string.IsNullOrWhiteSpace(sn))
			{
				// add the node:
				var info = new SqlServerInfo(sn, false);
				var vmdl = new SqlServerViewModel(info) { Owner = this };

				SafeInvoke(() => this.Servers.Nodes.Add(vmdl));
				
			}
		}

		/// <summary>
		/// load the data-set from disk (XML)
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteLoad(object param)
		{
			var dlg = new Microsoft.Win32.OpenFileDialog();
			dlg.Filter = "XML Files (*.XML)|*.xml";
			dlg.Title = "Open XML data-set";
			var result = dlg.ShowDialog();
			if (result.HasValue && result.Value)
			{
				try
				{
					// set form busy:
					this.IsBusy = true;

					// load the data set:
					DataSet ds = new DataSet();
					ds.ReadXmlSchema(dlg.FileName);
					ds.ReadXml(dlg.FileName);

					// set the dataset:
					this.Model = ds;
					if (ds.Tables.Count > 0)
						this.SelectedTable = ds.Tables[0];
				}
				catch (Exception e)
				{
					System.Windows.MessageBox.Show(e.Message);
				}
				finally
				{
					this.IsBusy = false;
				}
			}
		}

		/// <summary>
		/// save the data-set to disk (XML)
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteSave(object param)
		{
			var dlg = new Microsoft.Win32.SaveFileDialog();
			dlg.Filter = "XML (*.xml)|*.xml";
			dlg.Title = "Save Data-Set as XML";
			var result = dlg.ShowDialog();
			if (result.HasValue && result.Value)
			{
				try
				{
					this.IsBusy = true;
					this.Model.WriteXml(dlg.FileName, XmlWriteMode.WriteSchema);
				}
				catch (Exception e)
				{
					System.Windows.MessageBox.Show(e.Message);
				}
				finally
				{
					this.IsBusy = false;
				}
			}
		}

		/// <summary>
		/// executes <see cref="CommandText"/> against <see cref="SelectedConnection"/> and stores the data-table in the data-set.
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteQuery(object param)
		{
			if (this.SelectedConnection != null && !string.IsNullOrEmpty(this.CommandText) && this.CommandText.ToLower().Contains("select"))
			{
				this.IsBusy = true;
				try
				{
					// create the connection:
					using (var connection = this.SelectedConnection.CreateConnection())
					{
						// build an adapter:
						using (var da = new SqlDataAdapter(this.CommandText, connection))
						{
							// setup a table:
							var dt = new DataTable();

							// fill the table with schema info:
							da.FillSchema(dt, SchemaType.Source);

							// store the original select statement;
							dt.ExtendedProperties["select"] = CommandText;
							dt.ExtendedProperties["connect"] = SelectedConnection.GetConnectionString();
							dt.ExtendedProperties["scriptName"] = dt.TableName;

							// use the data adapter to fill the table
							da.Fill(dt);

							// add the table to the data-set;
							this.Model.Tables.Add(dt);

							// select the table
							this.SelectedTable = dt;
						}
						OnPropertyChanged(nameof(Tables));
					}
				}
				finally
				{
					this.IsBusy = false;
				}
			}
		}

		/// <summary>
		/// sets the command-text to select all rows from the currently selected connection table
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteCreateSelect(object param)
		{
			if (SelectedConnectionTable != null)
			{
				// set the command-text 
				this.CommandText = $"SELECT * FROM [{SelectedConnectionTable.TableName}]";
			}
		}

		/// <summary>
		/// downloads schema from the current connection
		/// </summary>
		/// <param name="param"></param>
		protected void ExecuteGetSchema(object param)
		{
			// there must be a connection selected
			if (SelectedConnection != null)
			{
				try
				{
					// set the form to busy
					this.IsBusy = true;

					// create a connection object
					using (var conn = SelectedConnection.CreateConnection())
					{
						// open the connection
						conn.Open();

						// retrieve schema list:
						var schemas = conn.GetSchema();

						// enumerate the rows in the table:
						foreach (var row in schemas.Select())
						{
							// get the schema name
							var schemaName = row.Field<string>("collectionName");

							// download the schema table (except "StructuredTypeMembers" this always throws an error)
							if (!schemaName.Equals("StructuredTypeMembers"))
							{
								// download the schema-table:
								var schemaTable = conn.GetSchema(schemaName);

								// set the table-name to be appropriate for the database and schema-type:
								schemaTable.TableName = SelectedConnection.DataBaseName + "." + schemaName;

								// add the table to the data-set
								Model.Tables.Add(schemaTable);
							}
						}

						// indicate the tables list changed:
						OnPropertyChanged(nameof(Tables));
					}
				}
				catch (Exception e)
				{
					MessageBox.Show(e.Message);
				}
				finally
				{
					this.IsBusy = false;
				}
			}
		}

		#endregion

		#region DataSet Properties

		/// <summary>
		/// gets or sets the model as a DataSet;
		/// </summary>
		public DataSet Model
		{
			get { return GetValue(() => Model); }
			set {

				SetValue(() => Model, value); 

				// these properties also change:
				OnPropertyChanged(nameof(Tables));
				OnPropertyChanged(nameof(SelectedTable));
				OnPropertyChanged(nameof(GenerateScriptButtonText));
				OnPropertyChanged(nameof(GenerateDataSetScriptButtonText));
				OnPropertyChanged(nameof(SelectedTableContextActions));
			}
		}

		/// <summary>
		/// the selected table from the data-set;
		/// </summary>
		public DataTable SelectedTable
		{
			get { return GetValue(() => SelectedTable); }
			set
			{
				SetValue(() => SelectedTable, value);

				OnPropertyChanged(nameof(GenerateScriptButtonText));
				OnPropertyChanged(nameof(GenerateDataSetScriptButtonText));
				OnPropertyChanged(nameof(SelectedTableContextActions));

			}
		}

		/// <summary>
		/// list of tables;
		/// </summary>
		public IEnumerable<DataTable> Tables
		{
			get
			{
				if (Model != null)
				{
					foreach (DataTable tbl in Model.Tables)
					{
						yield return tbl;
					}
				}
			}
		}

		#endregion

	}

	/// <summary>
	/// provides some additional properties to the <see cref="DataTable"/> for display in a list-box
	/// </summary>
	public class DataTableMount
	{
		/// <summary>
		/// the data table
		/// </summary>
		DataTable m_tbl;

		/// <summary>
		/// constructor
		/// </summary>
		/// <param name="tbl"></param>
		public DataTableMount(DataTable tbl)
		{
			m_tbl = tbl;
		}

		/// <summary>
		/// gets/sets the name of the data-table
		/// </summary>
		public string Name  { get { return m_tbl.TableName; }
							  set { m_tbl.TableName = value; } }

		/// <summary>
		/// gets the number of rows
		/// </summary>
		public int RowCount { get { return m_tbl.Rows.Count; } }

		/// <summary>
		/// gets the original select statement used to populate the table (if set)
		/// </summary>
		public string SelectStatement
		{
			get
			{
				if (m_tbl.ExtendedProperties.ContainsKey("select"))
					return m_tbl.ExtendedProperties["select"] as string;
				else
					return "";

			}
		}

		/// <summary>
		/// gets the table's alias for scripting.
		/// </summary>
		public string ScriptName
		{
			get
			{
				if (m_tbl.ExtendedProperties.ContainsKey("scriptName"))
					return m_tbl.ExtendedProperties["scriptName"] as string;
				else
					return m_tbl.TableName;
			}

		}

		/// <summary>
		/// gets a description of the table
		/// </summary>
		public string Description
		{
			get
			{

				if (ScriptName != Name)
					return $"{Name} as {ScriptName}";
				else
					return Name;
			}
		}

		/// <summary>
		/// string representation copied from the data-table.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return m_tbl.ToString();
		}

		#region Implicit Conversion Operators: TO/FRO DataTable
		public static implicit operator DataTable(DataTableMount m)
		{
			return m.m_tbl;
		}
		public static implicit operator DataTableMount(DataTable tbl)
		{
			return new DataTableMount(tbl);
		}
		#endregion

	}

}