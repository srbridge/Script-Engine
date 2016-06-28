using Quick.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ScriptView
{
	/// <summary>
	/// Interaction logic for InputBox.xaml
	/// </summary>
	public partial class InputBox : Window
	{
		public InputBox()
		{
			InitializeComponent();
		}

		/// <summary>
		/// gets the dialog view model.
		/// </summary>
		public DialogViewModel ViewModel
		{
			get { return DataContext as DialogViewModel; }
		}

		/// <summary>
		/// retrieves the entered string value
		/// </summary>
		/// <returns></returns>
		public static string GetInput()
		{
			var input = new InputBox();
			var r = input.ShowDialog();
			if (r.HasValue && r.Value)
			{
				return input.GetViewModel<DialogViewModel>().Value;
			}

			return null;
		}

		/// <summary>
		/// retrieves the entered string value, setting the window-title and caption also
		/// </summary>
		/// <param name="caption">
		/// caption text to go above the input box
		/// </param>
		/// <param name="title">
		/// title for the input box
		/// </param>
		/// <returns>
		/// the entered string (or null)
		/// </returns>
		public static string GetInput(string caption, string title)
		{
			var input = new InputBox();

			// set the caption and title:
			input.ViewModel.Caption = caption;
			input.ViewModel.WindowTitle = title;

			// show the dialog:
			var r = input.ShowDialog();

			// check for positive response:
			if (r.HasValue && r.Value)
			{
				//	return the entered value:
				return input.ViewModel.Value;
			}

			return null;
		}
	}
}
