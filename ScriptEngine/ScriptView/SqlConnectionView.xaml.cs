﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
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
	/// Interaction logic for SqlConnectionView.xaml
	/// </summary>
	public partial class SqlConnectionView : Window
	{

		public SqlConnectionView()
		{
			InitializeComponent();
		}

		/// <summary>
		/// set the property PWD on the Data-Model to be the secure-string generated by the PasswordBox control whenever the password changes
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			if (DataContext != null)
			{
				// set the dynamic property PWD from the secure password
				((dynamic)DataContext).PWD = ((PasswordBox)sender).SecurePassword;
			}
		}
	}

	/// <summary>
	/// conversion between visibility and boolean - inverted (false = visible, hidden/collapse = true) 
	/// </summary>
	public class NotBooleanToVisibiltyConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool visible = (bool)value;
			if (visible)
				return Visibility.Hidden;
			else
				return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			Visibility visible = (Visibility)value;
			return !(visible == Visibility.Visible);
		}
	}
}
