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

namespace VideoCompressor
{
	/// Interaction logic for ProgressWindow.xaml
	public partial class ProgressWindow : Window
	{
		public ProgressWindow()
		{
			InitializeComponent();
			
		}
		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				this.DragMove();
		}

		private void primaryButton_Click(object sender, RoutedEventArgs e)
		{	
			if (GlobalState.wasFileDragged)
			{
				Environment.Exit(0);
			}
			var mainWindow = new MainWindow();
			mainWindow.Show();
			Close();
		}
	}
}
