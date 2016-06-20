using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quick.MVVM
{
	/// <summary>
	/// quick and dirty implementation of the relay command pattern
	/// </summary>
	public class RelayCommand : ICommand
	{
		Func<bool>     _canExecute;
		Action<object> _execute;

		/// <summary>
		/// construct an always can execute relay command with the given action
		/// </summary>
		/// <param name="execute"></param>
		public RelayCommand(Action<object> execute)
		{
			this._canExecute = () => true;
			this._execute = execute;
		}

		public RelayCommand(Func<bool> canExecute, Action<object> execute)
		{
			this._canExecute = canExecute;
			this._execute = execute;

			// pass through the requery event:
			CommandManager.RequerySuggested += (s, e) => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler CanExecuteChanged;

		public bool CanExecute(object parameter)
		{
			return _canExecute();
		}

		public void Execute(object parameter)
		{
			_execute(parameter);
		}
	}

	/// <summary>
	/// very basic view-model base
	/// </summary>
	public class SimpleViewModel : INotifyPropertyChanged
	{
		public SimpleViewModel()
		{

			this.FontFamily = new FontFamily("Segoe UI");
			this.FontStyle  = FontStyles.Normal;
			this.Foreground = Brushes.Black;
			this.Background = Brushes.White;
			this.FontWeight = FontWeights.Normal;

		}

		protected Dictionary<string, object> _values = new Dictionary<string, object>();

		protected T Get<T>(string name)
		{
			return (T)this[name];
		}

		/// <summary>
		/// gets or sets a value from the dictionary
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		protected object this[string name]
		{
			get
			{

				object tmp = null;
				if (_values.TryGetValue(name, out tmp))
					return tmp;
				else
					return null;
			}
			set { _values[name] = value; OnPropertyChanged(name); }
		}

		/// <summary>
		/// fires the PropertyChanged event for all values.
		/// </summary>
		protected void RefreshAll()
		{
			foreach (var k in _values.Keys)
			{
				OnPropertyChanged(k);
			}
		}

		public string WindowTitle
		{
			get { return this[nameof(WindowTitle)] as string; }
			set { this[nameof(WindowTitle)] = value; }
		}

		public string ToolTip
		{
			get { return this[nameof(ToolTip)] as string; }
			set { this[nameof(ToolTip)] = value; }
		}

		public Cursor Cursor
		{
			get { return this[nameof(Cursor)] as Cursor; }
			set { this[nameof(Cursor)] = value; }
		}

		public bool IsBusy
		{
			get { return (bool)this[nameof(IsBusy)]; }
			set {

				this[nameof(IsBusy)] = value;
				if (value)
				{
					this.Cursor = Cursors.Wait;
				}
				else
				{
					this.Cursor = Cursors.Arrow;
				}
			}
		}

		public Brush Foreground
		{
			get { return this[nameof(Foreground)] as Brush; }
			set { this[nameof(Foreground)] = value; }
		}

		public Brush Background
		{
			get { return this[nameof(Background)] as Brush; }
			set { this[nameof(Background)] = value; }
		}

		public FontFamily FontFamily
		{
			get { return this[nameof(FontFamily)] as FontFamily; }
			set { this[nameof(FontFamily)] = value; }
		}

		public FontWeight FontWeight
		{
			get { return (FontWeight)this[nameof(FontWeight)]; }
			set { this[nameof(FontWeight)] = value; }
		}

		public FontStyle FontStyle
		{
			get { return (FontStyle)this[nameof(FontStyle)]; }
			set { this[nameof(FontStyle)] = value; }
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		#region Safe Invokation

		/// <summary>
		/// invokes an action on the UI thread
		/// </summary>
		/// <param name="action"></param>
		public void SafeInvoke(Action action)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(action);
		}

		/// <summary>
		/// invokes an action on the UI thread and returns a result.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="callback"></param>
		/// <returns></returns>
		public T SafeInvoke<T>(Func<T> callback)
		{
			return System.Windows.Application.Current.Dispatcher.Invoke<T>(callback);
		}

		#endregion

		/// <summary>
		/// returns true if the application is in design-mode.
		/// </summary>
		public bool InDesignMode
		{
			get
			{
				return (bool)DependencyPropertyDescriptor.FromProperty(DesignerProperties.IsInDesignModeProperty, typeof(FrameworkElement)).Metadata.DefaultValue;
			}
		}
	}

	/// <summary>
	/// base class for a tree-view-model;
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class TreeViewModel<T> : TreeViewModel
	{
		/// <summary>
		/// the root nodes collection
		/// </summary>
		public ObservableCollection<T> Nodes { get; set; } = new ObservableCollection<T>();
		
	}

	/// <summary>
	/// base class for a tree-view-model;
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class TreeViewModel : SimpleViewModel
	{
		public TreeViewModel()
		{
			this.Position = Dock.Left;
			this.Visible = Visibility.Collapsed;
			this.IsBusy = false;
			this.IsExpanded = false;
			this.IsSelected = false;
		}

		public string Name
		{
			get { return this[nameof(Name)] as string; }
			set { this[nameof(Name)] = value; }
		}

		public Dock Position
		{
			get { return (Dock)this[nameof(Position)]; }
			set { this[nameof(Position)] = value; }
		}

		public Visibility Visible
		{
			get { return (Visibility)this[nameof(Visible)]; }
			set
			{
				this[nameof(Visible)] = value;
			}
		}

		public Action<SimpleViewModel> OnSelected { get; set; }
		public Action<SimpleViewModel> OnExpanded { get; set; }

		public ICommand Show { get { return new RelayCommand(() => Visible != Visibility.Visible, (o) => this.Visible = Visibility.Visible); } }

		public ICommand Hide { get { return new RelayCommand(() => Visible == Visibility.Visible, (o) => this.Visible = Visibility.Collapsed); } }

		
		public bool IsSelected
		{
			get { return (bool)this[nameof(IsSelected)]; }
			set
			{
				this[nameof(IsSelected)] = value;
				if (value)
				{
					if (OnSelected != null)
						OnSelected.Invoke(this);
				}
			}
		}

		public bool IsExpanded
		{
			get { return (bool)this[nameof(IsExpanded)]; }
			set
			{

				this[nameof(IsExpanded)] = value;
				if (value)
				{
					if (OnExpanded != null)
						OnExpanded.Invoke(this);
				}
			}
		}
	}
}
