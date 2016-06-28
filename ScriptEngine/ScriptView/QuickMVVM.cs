using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Dynamic;


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

		/// <summary>
		/// dictionary storing the values for each property 
		/// </summary>
		protected Dictionary<string, object> _values = new Dictionary<string, object>();

		/// <summary>
		/// strongly typed get method for the named value
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns></returns>
		protected T Get<T>(string name)
		{
			object tmp = null;
			if (_values.TryGetValue(name, out tmp))
				return (T)tmp;
			else
				return default(T);
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
			set {

				object tmp = null;
				if (_values.TryGetValue(name, out tmp))
				{
					if (!Equals(tmp, value))
					{
						_values[name] = value;
						OnPropertyChanged(name);
					}
				}
				else
				{
					_values[name] = value;
					OnPropertyChanged(name);
				}
			}
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

		/// <summary>
		/// property to bind to the WindowTitle
		/// </summary>
		public string WindowTitle
		{
			get { return this[nameof(WindowTitle)] as string; }
			set { this[nameof(WindowTitle)] = value; }
		}

		/// <summary>
		/// easy way to add dynamic tool tips
		/// </summary>
		public string ToolTip
		{
			get { return this[nameof(ToolTip)] as string; }
			set { this[nameof(ToolTip)] = value; }
		}

		/// <summary>
		/// gets set to <see cref="Cursors.Wait"/> whenever <see cref="IsBusy"/> is true. Bind the form/control's cursor property to this value
		/// </summary>
		public Cursor Cursor
		{
			get { return this[nameof(Cursor)] as Cursor; }
			set { this[nameof(Cursor)] = value; }
		}

		/// <summary>
		/// gets or sets if the form is busy doing some processing etc.
		/// </summary>
		public bool IsBusy
		{
			get { return Get<bool>(nameof(IsBusy)); }
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

		/// <summary>
		/// bind to the foreground property to allow the ViewModel to control foreground colour
		/// </summary>
		public Brush Foreground
		{
			get { return this[nameof(Foreground)] as Brush; }
			set { this[nameof(Foreground)] = value; }
		}

		/// <summary>
		/// bind to the background property to allow the ViewModel to control background colour.
		/// </summary>
		public Brush Background
		{
			get { return this[nameof(Background)] as Brush; }
			set { this[nameof(Background)] = value; }
		}

		/// <summary>
		/// the type of font, eg "Consolas"
		/// </summary>
		public FontFamily FontFamily
		{
			get { return this[nameof(FontFamily)] as FontFamily; }
			set { this[nameof(FontFamily)] = value; }
		}

		/// <summary>
		/// the weight of the font (eg, Light, Bold, etc)
		/// </summary>
		public FontWeight FontWeight
		{
			get { return (FontWeight)this[nameof(FontWeight)]; }
			set { this[nameof(FontWeight)] = value; }
		}

		/// <summary>
		/// the style of the font, eg italics/oblique etc.
		/// </summary>
		public FontStyle FontStyle
		{
			get { return (FontStyle)this[nameof(FontStyle)]; }
			set { this[nameof(FontStyle)] = value; }
		}

		/// <summary>
		/// raised whenever a property value changes
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// raises the property changed event
		/// </summary>
		/// <param name="name"></param>
		protected void OnPropertyChanged(string name)
		{
			// use the elvis operator to invoke only when subscribed:
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		/// <summary>
		/// gets or sets the bitmap source for an icon for the bound UI
		/// </summary>
		public BitmapSource Icon
		{
			get { return Get<BitmapSource>(nameof(Icon)); }
			set { this[nameof(Icon)] = value; }
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

		/// <summary>
		/// set to true when the model wants to close the window.
		/// </summary>
		public bool CloseWindow
		{
			get { return Get<bool>(nameof(CloseWindow)); }
			set { this[nameof(CloseWindow)] = value; }
		}

		/// <summary>
		/// invoked by the <see cref="CmdCloseWindow"/> command.
		/// </summary>
		/// <param name="sender"></param>
		protected virtual void OnCloseWindow(object sender)
		{
			// sets the close-window value to true
			this.CloseWindow = true;
			this.CloseWindow = false;

		}

		/// <summary>
		/// command to close the window bound to the view-model **(where that window has a <see cref="WindowCloser"/> bound to the <see cref="CloseWindow"/> property of the view-model.
		/// </summary>
		public ICommand CmdCloseWindow
		{
			get { return new RelayCommand(OnCloseWindow); }
		}
	}

	/// <summary>
	/// extends the view-model base class with dialog properties. use this with the DialogBinder to easily create modal dialogs.
	/// </summary>
	public class DialogViewModel : SimpleViewModel
	{
		/// <summary>
		/// bind this property to the <see cref="DialogResultBinder.DialogResult"/> dependency property and the OK and Cancel commands
		/// will set the value as is appropriate.
		/// </summary>
		public bool? DialogResult
		{
			get { return Get<bool>(nameof(DialogResult)); }
			set { this[nameof(DialogResult)] = value;     }
		}

		/// <summary>
		/// string value for caption above text-box;
		/// </summary>
		public string Caption
		{
			get { return Get<string>(nameof(Caption)); }
			set { this[nameof(Caption)] = value; }
		}

		/// <summary>
		/// string value for input box;
		/// </summary>
		public string Value
		{
			get { return Get<string>(nameof(Value)); }
			set {
				this[nameof(Value)] = value;
				this.EnableOK = !string.IsNullOrEmpty(value);
			}
		}

		/// <summary>
		/// this method is invoked whenever the OK command is fired.
		/// </summary>
		/// <param name="sender"></param>
		protected virtual void OnOK(object sender)
		{
			this.DialogResult = true;
		}

		/// <summary>
		/// this method is invoked whenever the Cancel commnd is fired
		/// </summary>
		/// <param name="sender"></param>
		protected virtual void OnCancel(object sender)
		{
			this.DialogResult = false;
		}

		/// <summary>
		/// controls if the OK button is enabled
		/// </summary>
		public bool EnableOK
		{
			get { return Get<bool>(nameof(EnableOK)); }
			set { this[nameof(EnableOK)] = value; }
		}

		/// <summary>
		/// the OK command
		/// </summary>
		public ICommand CmdOK
		{
			get { return new RelayCommand(() => EnableOK, OnOK); }
		}

		/// <summary>
		/// the Cancel command
		/// </summary>
		public ICommand CmdCancel
		{
			get { return new RelayCommand(OnCancel); }
		}
	}

	/// <summary>
	/// simple control to place on a form allowing you to bind a ViewModel's DialogResult property to the dialog result of the window
	/// </summary>
	public class DialogResultBinder : FrameworkElement
	{
		public bool? DialogResult
		{
			get { return (bool?)GetValue(DialogResultProperty); }
			set { SetValue(DialogResultProperty, value); }
		}

		/// <summary>
		/// you can only set a DialogResult if the window is shown as a dialog.
		/// if the dialog-result is being set for a non-dialog window, should the window be closed?
		/// </summary>
		public bool CloseNonDialogs { get; set; }

		// Using a DependencyProperty as the backing store for DialogResult.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty DialogResultProperty =
			DependencyProperty.Register("DialogResult", typeof(bool?), typeof(DialogResultBinder), new PropertyMetadata(null, PropertyChanged));


		/// <summary>
		/// callback for the dependency property change event
		/// </summary>
		/// <param name="s">
		/// the dependency object that triggered the change
		/// </param>
		/// <param name="e">
		/// the arguments containing the change.
		/// </param>
		static void PropertyChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			// get the binder object instance:
			var binder = s as DialogResultBinder;

			// find the window that owns the object:
			var window = Window.GetWindow(s);

			if (window != null)
			{
				// allow for this to handle more than one property:
				switch (e.Property.Name)
				{
					case nameof(DialogResult):

						if (window.IsActive)
						{
							// you can only set the dialog result if the window is modal:
							if (window.IsModal())
							{
								// set the dialog result:
								window.DialogResult = (bool?)e.NewValue;
							}
							else
							{
								if (((bool?)e.NewValue).HasValue)
								{
									// close the form?
									if ((binder?.CloseNonDialogs).Value)
									{
										window.Close();
									}
								}
							}
						}
						break;

				}
			}
		}
	}

	/// <summary>
	/// used to allow the <see cref="SimpleViewModel"/>	to close the window(s) bound to it by binding a boolean (<see cref="SimpleViewModel.CloseWindow"/>) to the <see cref="CloseWindow"/> dependency property.
	/// </summary>
	public class WindowCloser : FrameworkElement
	{
		public bool CloseWindow
		{
			get { return (bool)GetValue(CloseWindowProperty); }
			set { SetValue(CloseWindowProperty, value); }
		}

		// Using a DependencyProperty as the backing store for CloseWindow.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty CloseWindowProperty =
			DependencyProperty.Register(nameof(CloseWindow), typeof(bool), typeof(WindowCloser), new PropertyMetadata(false, PropertyChanged));

		/// <summary>
		/// this gets invoked when the <see cref="CloseWindow"/> property is changed via binding.
		/// </summary>
		/// <param name="s">
		/// the object that hosted the change (will be a <see cref="WindowCloser"/>)
		/// </param>
		/// <param name="e">
		/// event arguments indicating what changed
		/// </param>
		static void PropertyChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			if ((bool)e.NewValue)
			{
				var wnd = Window.GetWindow(s);
				if (wnd != null)
				{
					wnd.Close();
				}
			}
		}
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
	/// some helpful extension methods.
	/// </summary>
	public static class MvvmExtensions
	{
		/// <summary>
		/// precompile a function to access the private field '_showingAsDialog' from the Window class
		/// </summary>
		static Func<Window, bool> m_isModalWindowFunc = typeof(Window).GetField("_showingAsDialog", BindingFlags.Instance | BindingFlags.NonPublic).CompileFieldAccessor<Window, bool>();

		/// <summary>
		/// accesses the private field "_showingAsDialog" from the <see cref="Window"/> class to determine if the window is modal.
		/// </summary>
		/// <param name="window"></param>
		/// <returns></returns>
		public static bool IsModal(this Window window)
		{
			// execute the function and return the value of the _showingAsDialog private field from the Window class
			return m_isModalWindowFunc(window);
		}

		/// <summary>
		/// gets the strongly typed view-model from the window's <see cref="FrameworkElement.DataContext"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="window"></param>
		/// <returns></returns>
		public static T GetViewModel<T>(this Window window) 
			where T:SimpleViewModel
		{
			return window.DataContext as T;
		}
	}

	/// <summary>
	/// helper methods to return images from the assembly's resource using the pack url.
	/// </summary>
	public class ImageResources
	{
		/// <summary>
		/// returns a new bitmap from the local resource (eg Images/image.png)
		/// </summary>
		/// <param name="localPath">the folder and file-name of the image to load</param>
		/// <returns></returns>
		public static BitmapSource GetImageSource(string localPath)
		{
			// declare a holder for the bitmap
			BitmapSource src = null;

				// load the bitmap from resources: (this must be invoked on the UI thread)
				Application.Current.Dispatcher.Invoke(() => src = new BitmapImage(new Uri($"pack://application:,,,/{localPath}", UriKind.Absolute)));


			return src;
		}

		/// <summary>
		/// returns a new image with the source set from the local pack url.
		/// </summary>
		/// <param name="localPath">
		/// the path to the image within the application: eg Images/picture.png (include the extension and any folders)
		/// </param>
		/// <returns>
		/// an <see cref="Image"/> control with the source set to the bitmap specified by the local path
		/// </returns>
		public static Image CreateImage(string localPath)
		{
			return new Image {
				Source = GetImageSource(localPath)
			};
		}

	}
}
