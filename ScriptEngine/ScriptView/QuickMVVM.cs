using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quick.MVVM
{
	/// <summary>
	/// base class implementing <see cref="INotifyPropertyChanged"/>
	/// </summary>
	public class PropertyChangedBase :  DynamicObject, INotifyPropertyChanged
	{
		/// <summary>
		/// event raised when the value of a property changes
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// raises the <see cref="PropertyChanged"/> event with the given property-name
		/// </summary>
		/// <param name="name"></param>
		protected virtual void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}

	/// <summary>
	///  stores values in a dictionary, has generic get and set methods with property-changed notification.
	/// </summary>
	public class AutoPropertyChangedBase : PropertyChangedBase
	{
		/// <summary>
		/// stores the values for the properties
		/// </summary>
		protected Dictionary<string, object> m_values = new Dictionary<string, object>();

		/// <summary>
		/// gets the value of the member specified in the member expression. Generic type parameters should be inferred.
		/// </summary>
		/// <typeparam name="T">the property type</typeparam>
		/// <param name="memberExpression">lambda expression referring to the property invoking this method</param>
		/// <returns>the value, or default(T)</returns>
		public T GetValue<T>(Expression<Func<T>> memberExpression)
		{
			var body = memberExpression.Body as MemberExpression;
			if (body != null)
			{
				object value;
				if (m_values.TryGetValue(body.Member.Name, out value))
				{
					// return the value;
					return (T)value;
				}
			}

			// return a default:
			return default(T);
		}

		/// <summary>
		/// sets the value
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="memberExpression"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool SetValue<T>(Expression<Func<T>> memberExpression, T value)
		{
			// is the value different from the existing?
			if (EqualityComparer<T>.Default.Equals(value, GetValue(memberExpression)))
			{
				return false;
			}

			// fetch the name of the property:
			var body = memberExpression.Body as MemberExpression;
			if (body != null)
			{
				// set the value:
				m_values[body.Member.Name] = value;

				// raise the property-changed event
				OnPropertyChanged(body.Member.Name);
			}

			// return true for changed:
			return true;
		}

		/// <summary>
		/// try to get the value from the dictionary dynamically
		/// </summary>
		/// <param name="binder"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (m_values.TryGetValue(binder.Name, out result))
			{
				return true;
			}
			return base.TryGetMember(binder, out result);
		}

		/// <summary>
		/// try to set a value dynamically.
		/// </summary>
		/// <param name="binder"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			object existing;
			if (m_values.TryGetValue(binder.Name, out existing))
			{
				if (EqualityComparer<object>.Default.Equals(existing, value))
				{
					// value is the same
					return true;
				}
			}
			// set the new value, raise the event
			m_values[binder.Name] = value;
			OnPropertyChanged(binder.Name);
			return true;
		}
	}

	/// <summary>
	/// quick and dirty implementation of the relay command pattern
	/// </summary>
	public class RelayCommand : ICommand
	{
		Func<bool> _canExecute;
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
	public class SimpleViewModel : AutoPropertyChangedBase
	{
		public SimpleViewModel()
		{
			this.Foreground = Brushes.Black;
			this.Background = Brushes.White;
		}

		/// <summary>
		/// fires the PropertyChanged event for all values.
		/// </summary>
		protected void RefreshAll()
		{
			foreach (var k in m_values.Keys)
			{
				OnPropertyChanged(k);
			}
		}

		/// <summary>
		/// property to bind to the WindowTitle
		/// </summary>
		public string WindowTitle
		{
			get { return GetValue(() => WindowTitle); }
			set { SetValue(() => WindowTitle, value); }
		}

		/// <summary>
		/// gets or sets status text for a status bar
		/// </summary>
		public string Status
		{
			get { return GetValue(() => Status); }
			set { SetValue(() => Status, value); }
		}

		/// <summary>
		/// easy way to add dynamic tool tips
		/// </summary>
		public string ToolTip
		{
			get { return GetValue(() => ToolTip); }
			set { SetValue(() => ToolTip, value); }
		}

		/// <summary>
		/// gets set to <see cref="Cursors.Wait"/> whenever <see cref="IsBusy"/> is true. Bind the form/control's cursor property to this value
		/// </summary>
		public Cursor Cursor
		{
			get { return GetValue(() => Cursor); }
			set { SetValue(() => Cursor, value); }
		}

		/// <summary>
		/// gets or sets if the form is busy doing some processing etc.
		/// </summary>
		public bool IsBusy
		{
			get { return GetValue(() => IsBusy); }
			set { SetValue(() => IsBusy, value); }
		}

		/// <summary>
		/// bind to the foreground property to allow the ViewModel to control foreground colour
		/// </summary>
		public Brush Foreground
		{
			get { return GetValue(() => Foreground); }
			set { SetValue(() => Foreground, value); }
		}

		/// <summary>
		/// bind to the background property to allow the ViewModel to control background colour.
		/// </summary>
		public Brush Background
		{
			get { return GetValue(() => Background); }
			set { SetValue(() => Background, value); }
		}

		/// <summary>
		/// gets or sets the bitmap source for an icon for the bound UI
		/// </summary>
		public BitmapSource Icon
		{
			get { return GetValue(() => Icon); }
			set { SetValue(() => Icon, value); }
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

		public bool CloseWindow
		{
			get { return GetValue(() => CloseWindow); }
			set { SetValue(() => CloseWindow, value); }
		}

		protected virtual void OnCloseWindow(object sender)
		{
			// sets the close-window value to true, then back to false;
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
	/// basic view model
	/// </summary>
	/// <typeparam name="TModel"></typeparam>
	public class SimpleViewModel<TModel> : SimpleViewModel
	{
		public SimpleViewModel()
			: base()
		{

		}

		public SimpleViewModel(TModel model)
		{
			this.Model = model;
		}

		public TModel Model
		{
			get { return GetValue(() => Model); }
			set
			{
				if (SetValue(() => Model, value))
				{
					OnModelChanged(value);
				}
			}
		}

		protected virtual void OnModelChanged(TModel changedModel)
		{

		}
	}

	/// <summary>
	/// view model for handling a list of objects.
	/// </summary>
	/// <typeparam name="TModel"></typeparam>
	public class ListViewModel<TModel> : SimpleViewModel
	{
		public ListViewModel() { }

		public ListViewModel(IEnumerable<TModel> list)
			: base()
		{
			foreach (var item in list)
			{
				// adding items to observable collections must happen on the UI thread.
				SafeInvoke(() => Items.Add(item));
			}
		}

		/// <summary>
		/// the selected model
		/// </summary>
		public TModel Selected
		{
			get { return GetValue(() => Selected); }
			set { SetValue(() => Selected, value); }
		}

		/// <summary>
		/// the list of items
		/// </summary>
		public ObservableCollection<TModel> Items { get; } = new ObservableCollection<TModel>();

	}

	/// <summary>
	/// extends the view-model base class with dialog properties. use this with the DialogBinder to easily create modal dialogs.
	/// </summary>
	public class DialogViewModel : SimpleViewModel
	{

		public DialogViewModel()
			: base()
		{
			// make sure dialog result is null
			this.DialogResult = null;
		}

		/// <summary>
		/// data-context for the dialog
		/// </summary>
		public SimpleViewModel DataContext
		{
			get { return GetValue(() => DataContext); }
			set { SetValue(() => DataContext, value); }
		}

		/// <summary>
		/// bind this property to the <see cref="DialogResultBinder.DialogResult"/> dependency property and the OK and Cancel commands
		/// will set the value as is appropriate.
		/// </summary>
		public bool? DialogResult
		{
			get { return GetValue(() => DialogResult); }
			set { SetValue(() => DialogResult, value); }
		}

		/// <summary>
		/// string value for caption above text-box;
		/// </summary>
		public string Caption
		{
			get { return GetValue(() => Caption); }
			set { SetValue(() => Caption, value); }
		}

		/// <summary>
		/// string value for input box;
		/// </summary>
		public string Value
		{
			get { return GetValue(() => Value); }
			set
			{
				SetValue(() => Value, value);
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
			get { return GetValue(() => EnableOK); }
			set { SetValue(() => EnableOK, value); }
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

									// set the property back to null;
									s.SetValue(e.Property, null);
								}
							}
						}
						break;

				}
			}
		}
	}

	/// <summary>
	/// simple control to place on a <see cref="Window"/> that makes it easy to close that window.
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

		static void PropertyChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			if ((bool)e.NewValue)
			{
				var wnd = Window.GetWindow(s);
				if (wnd != null && wnd.IsActive)
				{
					// close the window:
					wnd.Close();
				}

				// need to reset the value of the dependency property:
				s.SetValue(e.Property, false);
			}

		}


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
			where T : SimpleViewModel
		{
			return window.DataContext as T;
		}

		/// <summary>
		/// creates a compiled function to access a field
		/// </summary>
		/// <typeparam name="Tin"></typeparam>
		/// <typeparam name="Tout"></typeparam>
		/// <param name="field"></param>
		/// <returns></returns>
		public static Func<Tin, Tout> CompileFieldAccessor<Tin, Tout>(this FieldInfo field)
		{
			// define the instance parameter:
			var instanceParam = System.Linq.Expressions.Expression.Parameter(typeof(Tin), "Instance");

			// create an expression to access a field:
			var accessField = System.Linq.Expressions.Expression.Field(instanceParam, field);

			// create a lambda (add the parameter(s))
			var lambda = System.Linq.Expressions.Expression.Lambda<Func<Tin, Tout>>(accessField, instanceParam);

			// compile the field accessor and return
			return lambda.Compile();
		}


		/// <summary>
		/// extension method for easy property-changed implementation.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="handler"></param>
		/// <param name="field"></param>
		/// <param name="value"></param>
		/// <param name="member"></param>
		/// <returns></returns>
		public static bool ChangeAndNotify<T>(this PropertyChangedEventHandler handler, ref T field, T value, Expression<Func<T>> member)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;

			// assign the value:
			field = value;

			// pull out the  details of the property:
			var body = member.Body as MemberExpression;
			if (body != null)
			{
				var vm = body.Expression as ConstantExpression;
				if (vm != null)
				{
					var lambda = System.Linq.Expressions.Expression.Lambda(vm);
					var func = lambda.Compile();
					var sender = func.DynamicInvoke();
					handler?.Invoke(sender, new PropertyChangedEventArgs(body.Member.Name));
				}
			}

			// changed:
			return true;

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
			get { return GetValue(() => Name); }
			set { SetValue(() => Name, value); }
		}

		public Dock Position
		{
			get { return GetValue(() => Position); }
			set { SetValue(() => Position, value); }
		}

		public Visibility Visible
		{
			get { return GetValue(() => Visible); }
			set
			{
				SetValue(() => Visible, value);
			}
		}

		public Action<SimpleViewModel> OnSelected { get; set; }
		public Action<SimpleViewModel> OnExpanded { get; set; }

		public ICommand Show { get { return new RelayCommand(() => Visible != Visibility.Visible, (o) => this.Visible = Visibility.Visible); } }

		public ICommand Hide { get { return new RelayCommand(() => Visible == Visibility.Visible, (o) => this.Visible = Visibility.Collapsed); } }


		public bool IsSelected
		{
			get { return GetValue(() => IsSelected); }
			set
			{
				SetValue(() => IsSelected, value);
				if (value)
				{
					if (OnSelected != null)
						OnSelected.Invoke(this);
				}
			}
		}

		public bool IsExpanded
		{
			get { return GetValue(() => IsExpanded); }
			set
			{

				SetValue(() => IsExpanded, value);
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
			return new Image
			{
				Source = GetImageSource(localPath)
			};
		}

	}

	/// <summary>
	/// Defines a table that has two columns with any number of rows. 
	/// </summary>
	/// <remarks>
	/// This panel is designed for use in configuration/settings windows where you typically
	/// have a pairs of "Label: SomeControl" organized in rows.
	/// 
	/// The width of the first column is determined by the widest item that column and the width of the 
	/// second column is expanded to occupy all remaining space.
	/// 
	/// Written by: Isak Savo, isak.savo@gmail.com
	/// Licensed under the Code Project Open License http://www.codeproject.com/info/cpol10.aspx
	/// </remarks>
	public class TwoColumnGrid : Panel
	{
		private double Column1Width;
		private List<Double> RowHeights = new List<double>();

		/// <summary>
		/// Gets or sets the amount of spacing (in device independent pixels) between the rows.
		/// </summary>
		public double RowSpacing
		{
			get { return (double)GetValue(RowSpacingProperty); }
			set { SetValue(RowSpacingProperty, value); }
		}

		/// <summary>
		/// Identifies the ColumnSpacing dependency property
		/// </summary>
		public static readonly DependencyProperty RowSpacingProperty =
			DependencyProperty.Register("RowSpacing", typeof(double), typeof(TwoColumnGrid),
			new FrameworkPropertyMetadata(0.0d, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure));

		/// <summary>
		/// Gets or sets the amount of spacing (in device independent pixels) between the columns.
		/// </summary>
		public double ColumnSpacing
		{
			get { return (double)GetValue(ColumnSpacingProperty); }
			set { SetValue(ColumnSpacingProperty, value); }
		}

		/// <summary>
		/// Identifies the ColumnSpacing dependency property
		/// </summary>
		public static readonly DependencyProperty ColumnSpacingProperty =
			DependencyProperty.Register("ColumnSpacing", typeof(double), typeof(TwoColumnGrid),
			new FrameworkPropertyMetadata(0.0d, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure));


		/// <summary>
		/// Measures the size required for all the child elements in this panel.
		/// </summary>
		/// <param name="constraint">The size constraint given by our parent.</param>
		/// <returns>The requested size for this panel including all children</returns>
		protected override Size MeasureOverride(Size constraint)
		{
			double col1Width = 0;
			double col2Width = 0;
			RowHeights.Clear();
			// First, measure all the left column children
			for (int i = 0; i < VisualChildrenCount; i += 2)
			{
				var child = Children[i];
				child.Measure(constraint);
				col1Width = Math.Max(child.DesiredSize.Width, col1Width);
				RowHeights.Add(child.DesiredSize.Height);
			}
			// Then, measure all the right column children, they get whatever remains in width
			var newWidth = Math.Max(0, constraint.Width - col1Width - ColumnSpacing);
			Size newConstraint = new Size(newWidth, constraint.Height);
			for (int i = 1; i < VisualChildrenCount; i += 2)
			{
				var child = Children[i];
				child.Measure(newConstraint);
				col2Width = Math.Max(child.DesiredSize.Width, col2Width);
				RowHeights[i / 2] = Math.Max(RowHeights[i / 2], child.DesiredSize.Height);
			}

			Column1Width = col1Width;
			return new Size(
				col1Width + ColumnSpacing + col2Width,
				RowHeights.Sum() + ((RowHeights.Count - 1) * RowSpacing));
		}

		/// <summary>
		/// Position elements and determine the final size for this panel.
		/// </summary>
		/// <param name="arrangeSize">The final area where child elements should be positioned.</param>
		/// <returns>The final size required by this panel</returns>
		protected override Size ArrangeOverride(Size arrangeSize)
		{
			double y = 0;
			for (int i = 0; i < VisualChildrenCount; i++)
			{
				var child = Children[i];
				double height = RowHeights[i / 2];
				if (i % 2 == 0)
				{
					// Left child
					var r = new Rect(0, y, Column1Width, height);
					child.Arrange(r);
				}
				else
				{
					// Right child
					var r = new Rect(Column1Width + ColumnSpacing, y, arrangeSize.Width - Column1Width - ColumnSpacing, height);
					child.Arrange(r);
					y += height;
					y += RowSpacing;
				}
			}
			return base.ArrangeOverride(arrangeSize);
		}

	}

}
