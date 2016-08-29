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
	/// Interaction logic for DbRelationView.xaml
	/// </summary>
	public partial class DbRelationView : Window
	{
		public DbRelationView()
		{
			InitializeComponent();
		}

		public DbRelationViewModel ViewModel
		{
			get { return this.DataContext as DbRelationViewModel; }
			set { this.DataContext = value; }
		}
	}
}
