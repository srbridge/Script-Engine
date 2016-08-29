using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sandbox
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			while (true)
			{
				if (Clipboard.ContainsData(DataFormats.CommaSeparatedValue))
				{
					string csv = Clipboard.GetText(TextDataFormat.CommaSeparatedValue);
					List<string[]> rows = new List<string[]>();
					foreach (var r in csv.Split('\r'))
					{
						rows.Add(r.Split(','));
					}

				}



				Console.ReadKey();
			}


		}
	}
}
