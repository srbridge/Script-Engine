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
			this.DatabaseCount = "None";

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

		/// <summary>
		/// view-model for the servers tree-view
		/// </summary>
		public SqlEnvironmentViewModel Servers
		{
			get { return this[nameof(Servers)] as SqlEnvironmentViewModel; }
			set { this[nameof(Servers)] = value; }
		}

		/// <summary>
		/// the sql server selected in the tree-view
		/// </summary>
		public SqlServerInfo SelectedSqlServer
		{
			get { return this[nameof(SelectedSqlServer)] as SqlServerInfo; }
			set
			{
				this[nameof(SelectedSqlServer)] = value;
			}
		}

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
				return new RelayCommand(() => Model != null, ExecuteSaveAsXML);
			}
		}

		/// <summary>
		/// command to load data set from xml
		/// </summary>
		public ICommand LoadFromXML
		{
			get
			{
				return new RelayCommand(() => Model != null, ExecuteLoadFromXML);
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
		/// command to create the select statement to query the currently selected table
		/// </summary>
		public ICommand CreateSelectStatement
		{
			get { return new RelayCommand(() => SelectedConnectionTable != null, ExecuteCreateSelect); }
		}


		/// <summary>
		/// should the script use a transaction?
		/// </summary>
		public bool UseTransaction
		{
			get { return (bool)this[nameof(UseTransaction)]; }
			set
			{
				this[nameof(UseTransaction)] = value;
			}

		}

		/// <summary>
		/// how often should the script add a progress print statement
		/// </summary>
		public int PrintStatusCount
		{
			get { return (int)this[nameof(PrintStatusCount)]; }
			set { this[nameof(PrintStatusCount)] = value; }
		}

		/// <summary>
		/// should the script-output be copied to the clipboad instead of saved to disk?
		/// </summary>
		public bool ScriptToClipboard
		{
			get { return (bool)this[nameof(ScriptToClipboard)]; }
			set { this[nameof(ScriptToClipboard)] = value; }
		}

		/// <summary>
		/// builds the context menu items for the selected data-set-table
		/// </summary>
		public IEnumerable<FrameworkElement> SelectedTableContextActions
		{
			get
			{

				// build columns menu items
				var mnuCols = new MenuItem { Header = "Columns" };
				var mnuPKey = new MenuItem { Header = "Update Primary Key", ToolTip = "Sets the table's primary-key to be the checked columns", IsEnabled = false };

				// create a sub-context menu containing one of each column:
				// to enable the operator to change the primary key sequence:
				foreach (DataColumn col in SelectedTable.Columns)
				{
					var column = new MenuItem() { Header = col.ColumnName };
					column.IsCheckable = true;
					column.IsChecked = SelectedTable.PrimaryKey.Contains(col);
					if (column.IsChecked)
					{
						column.FontWeight = FontWeights.Bold;
						column.Foreground = Brushes.Blue;
					}
					column.StaysOpenOnClick = true;
					column.Tag = col;
					column.Checked += (s, e) => mnuPKey.IsEnabled = true;
					column.Unchecked += (s, e) => mnuPKey.IsEnabled = true;

					// add to the 'columns' menu
					mnuCols.Items.Add(column);
				}

				mnuCols.Items.Add(new Separator());
				mnuCols.Items.Add(mnuPKey);

				mnuPKey.Click += (s, e) => {
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

				var mnuScript = new MenuItem { Header = $"Script [{SelectedTable.TableName}]" };
				var mnuScriptInsert = new MenuItem { Header = "As Insert" };
				mnuScriptInsert.Click += (s, e) =>
				{
					this.SelectedScriptType = DbScriptType.Insert;
					this.ExecuteCreateScript(null);
				};
				var mnuScriptUpdate = new MenuItem { Header = "As Update" };
				mnuScriptUpdate.Click += (s, e) => {
					this.SelectedScriptType = DbScriptType.Update;
					this.ExecuteCreateScript(null);
				};
				var mnuScriptDelete = new MenuItem { Header = "As Delete" };
				mnuScriptDelete.Click += (s, e) => {
					this.SelectedScriptType = DbScriptType.Delete;
					this.ExecuteCreateScript(null);
				};
				mnuScript.Items.Add(mnuScriptInsert);
				mnuScript.Items.Add(mnuScriptUpdate);
				mnuScript.Items.Add(mnuScriptDelete);

				var mnuRemove = new MenuItem { Header = $"Remove [{SelectedTable.TableName}]", Tag = SelectedTable };
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
					var mnuName = new MenuItem { Header = "Set Script Name" };

					// create a text-box:
					var txt = new TextBox() { Text = SelectedTable.ExtendedProperties["scriptName"] as string };
					txt.HorizontalAlignment = HorizontalAlignment.Stretch;
					txt.HorizontalContentAlignment = HorizontalAlignment.Left;
					txt.MinWidth = 100;
					txt.FontFamily = new FontFamily("Consolas");

					// add to the menu:
					mnuName.Items.Add(new TextBlock() { Text = "Update Script Output Name:" });
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


		protected void ExecuteCreateScript(object param)
		{
			if (SelectedTable != null)
			{


				var dlg = new Microsoft.Win32.SaveFileDialog();
				bool? result = null;

				if (!ScriptToClipboard)
				{
					dlg.Filter = "SQL Scripts (*.SQL)|*.sql";
					dlg.Title = $"Save {this.SelectedScriptType} Script for {SelectedTable.TableName} As";
					dlg.FileName = $"{SelectedScriptType}_{SelectedTable.TableName}_{DateTime.Now.ToString("yyyyMMddhhmm")}.SQL";

					result = dlg.ShowDialog();
				}
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

						if (SelectedTable.ExtendedProperties.ContainsKey("scriptName"))
						{
							tbl.TableName = SelectedTable.ExtendedProperties["scriptName"] as string;
						}

						// set the comment on the table; this will be added to the top of the script.
						if (SelectedTable.ExtendedProperties.ContainsKey("select"))
						{
							if (SelectedTable.ExtendedProperties.ContainsKey("connect"))
							{
								tbl.Comment = $"original query: {SelectedTable.ExtendedProperties["select"]}\r\n    from: {SelectedTable.ExtendedProperties["connect"]}";
							}
							else
							{
								tbl.Comment = $"original query: {SelectedTable.ExtendedProperties["select"]}";
							}
						}

						if (ScriptToClipboard)
						{
							using (var ms = new MemoryStream())
							{
								tbl.GenerateScript(ms, this.SelectedScriptType, this.UseTransaction, this.PrintStatusCount);

								System.Windows.Clipboard.SetText(Encoding.UTF8.GetString(ms.ToArray()));
								System.Windows.MessageBox.Show("Script Copied to Clipboard");
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
						this.IsBusy = false;
					}
				}
			}
		}

		protected void ExecuteLoadFromXML(object param)
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

		protected void ExecuteSaveAsXML(object param)
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


		protected void ExecuteCreateSelect(object param)
		{
			var tableName = SelectedConnectionTable?.TableName;

			if (!string.IsNullOrEmpty(tableName))
			{
				this.CommandText = $"SELECT * FROM [{tableName}]";
			}
		}


		public string GenerateScriptButtonText
		{
			get
			{
				return $"Generate {SelectedScriptType} Script for {SelectedTable.TableName}";
			}
		}

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

		public DbScriptType SelectedScriptType
		{
			get { return (DbScriptType)this[nameof(SelectedScriptType)]; }
			set
			{
				this[nameof(SelectedScriptType)] = value;
				OnPropertyChanged(nameof(GenerateScriptButtonText));
			}
		}


		public string DatabaseCount
		{
			get { return (string)this[nameof(DatabaseCount)]; }
			set { this[nameof(DatabaseCount)] = value; }
		}


		public SqlDbInfo SelectedConnection
		{
			get { return this[nameof(SelectedConnection)] as SqlDbInfo; }
			set
			{
				this[nameof(SelectedConnection)] = value;
			}
		}

		public SqlDbTableInfo SelectedConnectionTable
		{
			get { return this[nameof(SelectedConnectionTable)] as SqlDbTableInfo; }
			set
			{
				this[nameof(SelectedConnectionTable)] = value;
			}
		}



		/// <summary>
		/// gets or sets a query to execute.
		/// </summary>
		public string CommandText
		{
			get { return this[nameof(CommandText)] as string; }
			set
			{
				this[nameof(CommandText)] = value;
			}
		}

		/// <summary>
		/// gets or sets the model as a DataSet;
		/// </summary>
		public DataSet Model
		{
			get { return this[nameof(Model)] as DataSet; }
			set
			{
				this[nameof(Model)] = value;

				// these properties also change:
				OnPropertyChanged(nameof(Tables));
				OnPropertyChanged(nameof(SelectedTable));
			}
		}

		/// <summary>
		/// the selected table from the data-set;
		/// </summary>
		public DataTable SelectedTable
		{
			get { return this[nameof(SelectedTable)] as DataTable; }
			set
			{
				this[nameof(SelectedTable)] = value;

				OnPropertyChanged(nameof(GenerateScriptButtonText));
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


	}

	public class DataTableMount
	{
		DataTable m_tbl;
		public DataTableMount(DataTable tbl)
		{
			m_tbl = tbl;
		}
		public string Name { get { return m_tbl.TableName; } set { m_tbl.TableName = value; } }
		public int RowCount { get { return m_tbl.Rows.Count; } }
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

		public override string ToString()
		{
			return m_tbl.ToString();
		}

		public static implicit operator DataTable(DataTableMount m)
		{
			return m.m_tbl;
		}
		public static implicit operator DataTableMount(DataTable tbl)
		{
			return new DataTableMount(tbl);
		}
	}

}