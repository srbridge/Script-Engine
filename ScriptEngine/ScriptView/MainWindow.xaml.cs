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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ScriptView
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		GridLength m_last_lpanel_width = new GridLength(100);
		GridLength m_last_qpanel_height = new GridLength(70);

		public MainWindow()
		{
			InitializeComponent();
		}

		void Button_Click(object sender, RoutedEventArgs e)
		{
			ToggleLeftPanel();
			ToggleQueryPanel();
		}

		public bool LeftPanelOpen
		{
			get { return LeftPanelColumn.Width.Value > 0; }
		}

		public bool QueryPanelOpen
		{
			get { return RowQueryDatabase.Height.Value > 0; }
		}

		void ToggleQueryPanel()
		{
			if (QueryPanelOpen)
			{
				m_last_qpanel_height    = RowQueryDatabase.Height;
				RowQueryDatabase.Height = new GridLength(0);
			}
			else
			{
				RowQueryDatabase.Height = m_last_qpanel_height;
			}
		}

		void ToggleLeftPanel()
		{
			if (this.LeftPanelOpen)
			{
				m_last_lpanel_width = LeftPanelColumn.Width;
				this.LeftPanelColumn.Width = new GridLength(0);
			}
			else
				this.LeftPanelColumn.Width = m_last_lpanel_width;
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			ToggleLeftPanel(); ToggleQueryPanel();
		}
	}
}
