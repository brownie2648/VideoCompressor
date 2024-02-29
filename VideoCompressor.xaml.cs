using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using Microsoft.Win32;
using System.CodeDom;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VideoCompressor.Windows;

/* Todolist:
- Ask C# discord about making published builds smaller
- Add more error catching so things don't go as wrong
- Look into filesize again
- Implement check for expected file type
*/

namespace VideoCompressor
{
	// Class for global variables
	public static class GlobalState
	{
		public static string[] cliArgs;

		public static string outputPath;

		public static bool wasFileDragged = false;

		public static double inputFileTotalSeconds;

		public static ProgressWindow progressWindow;

		public static MainWindow mainWindow;

		public static VideoCodecType activeCodec;

		public static bool isDone;

		public static IMediaAnalysis mediaInfo;
	}

	public partial class MainWindow : Window
	{
		// Initial startup logic
		public MainWindow()
		{
			// Get CLI args
			GlobalState.cliArgs = Environment.GetCommandLineArgs();

			// Check if file was dragged onto the .exe
			if (GlobalState.cliArgs.Length >= 2) GlobalState.wasFileDragged = true;

			// Sets current working directory to the .exe path
			// string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string workingDir = AppContext.BaseDirectory;
			Directory.SetCurrentDirectory(workingDir);

			// Check if ffmpeg directory exists in working directory
			bool ffmpegDirExists = Directory.Exists("ffmpeg");

			if (!ffmpegDirExists)
			{
				Directory.CreateDirectory("ffmpeg");
			}

			// Check if tmp directory exists
			bool tmpDirExists = Directory.Exists("tmp");

			if (!tmpDirExists)
			{
				Directory.CreateDirectory("tmp");
			}

			// Check if ffmpeg binary exists, download+extract it if not, then delete zip file
			bool ffmpegExists = File.Exists("ffmpeg/ffmpeg.exe");

			DownloadWindow downloadWindow = new();

			if (!ffmpegExists)
			{
				downloadWindow.Show();
				WebClient downloadClient = new WebClient();
				downloadClient.DownloadFile("https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v6.1/ffmpeg-6.1-win-64.zip", "ffmpeg/ffmpeg.zip");
				System.IO.Compression.ZipFile.ExtractToDirectory("ffmpeg/ffmpeg.zip", "ffmpeg");
				File.Delete("ffmpeg/ffmpeg.zip");
			}

			// Check if ffprobe binary exists, download+extract it if not, then delete zip file
			bool ffprobeExists = File.Exists("ffmpeg/ffprobe.exe");

			if (!ffprobeExists)
			{
				downloadWindow.Show();
				WebClient downloadClient = new WebClient();
				downloadClient.DownloadFile("https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v6.1/ffprobe-6.1-win-64.zip", "ffmpeg/ffprobe.zip");
				System.IO.Compression.ZipFile.ExtractToDirectory("ffmpeg/ffprobe.zip", "ffmpeg");
				File.Delete("ffmpeg/ffprobe.zip");
			}
			downloadWindow.Close();
			// Edit FFOptions to point to binaries and tmp folder
			GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "ffmpeg", TemporaryFilesFolder = "tmp" });



			// Initializes window
			InitializeComponent();

			if (GPUCheck.GetVendor() == GPUCheck.GPUVendor.NVIDIA_RTX)
			{
				CodecSelector.SelectedItem = VideoCodecType.x264_NVENC;
			}

			/* GPU Vendor debug
			MessageBox.Show(GPUCheck.GetVendor().ToString());
			*/

			// If a file was dragged onto the .EXE, execute the RunCompression function
			if (GlobalState.wasFileDragged)
			{
				// Get video length
				IMediaAnalysis mediaInfo = FFProbe.Analyse(GlobalState.cliArgs[1]);
				GlobalState.inputFileTotalSeconds = mediaInfo.Duration.TotalSeconds;

				string outputFolder = System.IO.Path.GetDirectoryName(GlobalState.cliArgs[1]);
				string outputFile = "Compressed - " + System.IO.Path.GetFileName(GlobalState.cliArgs[1]);

				/*
				System.IO.Path.Combine(outputFolder, outputFile)
				*/

				// Call RunCompression function
				RunCompression(GlobalState.cliArgs[1], GlobalState.inputFileTotalSeconds, 24, System.IO.Path.Combine(Convert.ToString(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)), outputFile));
			}
		}

		// Logic for file dialog on clicking the Browse button, sets filePath variable to the selected video file
		private void InputBrowseButton_Click(object sender, RoutedEventArgs e)
		{
			// Create file dialog
			OpenFileDialog openFileDialog = new OpenFileDialog();

			// Define settings for file dialog, and open it on clicking the "Browse" button
			openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
			openFileDialog.Filter = "Video files|*.mp4;*.mov;*.avi;*.mkv;|All files (*.*)|*.*";
			openFileDialog.FilterIndex = 1;

			// Open file dialog, and set filePath variable to the selected video file
			if (openFileDialog.ShowDialog() == true)
			{
				textBoxFilePath.Text = openFileDialog.FileName;
			}
			else
			{
				return;
			}

			// Check if file is supported

			string fileExtension = System.IO.Path.GetExtension(openFileDialog.FileName);
			switch (fileExtension.ToString())
			{
				case ".mp4":
					break;
				case ".mov":
					break;
				case ".avi":
					break;
				case ".webm":
					break;
				case ".mkv":
					break;
				default:
					MessageBox.Show("Error: File type not recognised. Supported file types include:.mp4, .mov, .avi, and .mkv", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					textBoxFilePath.Text = null;
					return;
			}
		}

		// Only allow numbers in the file size text box
		private void textBoxFileSize_TextChanged(object sender, TextChangedEventArgs e)
		{
			// Absolutely no idea how this works, it does though so I'll just leave it
			Regex regex = new Regex("[^0-9]+");
			bool handle = regex.IsMatch(textBoxFileSize.Text);
			if (handle)
			{
				StringBuilder dd = new StringBuilder();
				int i = -1;
				int cursor = -1;
				foreach (char item in textBoxFileSize.Text)
				{
					i++;
					if (char.IsDigit(item))
						dd.Append(item);
					else if (cursor == -1)
						cursor = i;
				}
				textBoxFileSize.Text = dd.ToString();

				if (i == -1)
					textBoxFileSize.SelectionStart = textBoxFileSize.Text.Length;
				else
					textBoxFileSize.SelectionStart = cursor;
			}
		}

		// Logic for Start! button
		private void ButtonProcess_Click(object sender, RoutedEventArgs e)
		{
			// Check if file path exists, else return
			if (!System.IO.Path.Exists(Convert.ToString(textBoxFilePath.Text)))
			{
				MessageBox.Show("Error: Input path doesn't exist!", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}

			// Configure save file dialog
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
			saveFileDialog.Filter = "Video files|*.mp4;*.mov;*.avi;*.mkv;|All files (*.*)|*.*";

			// Open save file dialog for where to save the compressed file
			if (saveFileDialog.ShowDialog() != true)
			{
				return;
			}

			// Start compression with calculated bitrate
			RunCompression(textBoxFilePath.Text, GlobalState.inputFileTotalSeconds, Convert.ToInt32(textBoxFileSize.Text), saveFileDialog.FileName);
		}

		// Logic for progress bar
		public static void ProgressFunction(double Percentage)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				GlobalState.progressWindow.ProgressText.Text = "Compressing with " + GlobalState.activeCodec + ": " + Percentage + "%";
				GlobalState.progressWindow.ProgressBar.Value = Percentage;
				if (Percentage == 100)
				{
					GlobalState.isDone = true;
					GlobalState.progressWindow.ProgressText.Text = "Done! \nCompressed video has been saved to: " + Convert.ToString(GlobalState.outputPath);
					GlobalState.progressWindow.ProgressBar.Visibility = Visibility.Hidden;
					GlobalState.progressWindow.primaryButton.Visibility = Visibility.Visible;
				}
			});

		}

		// Logic for processing video
		public void RunCompression(string FilePath, double TotalDurationSec, int FileSize, string OutputPath)
		{

			// Put a comment here when you can think becuase tnsldfgjkhnbsdjkhglsdfkjghkdszlkjhfgslkjdhfglkdjfghsjkdlghsdglkhsdlfgksdkfg
			GlobalState.outputPath = OutputPath;

			if (!System.IO.Path.Exists(Convert.ToString(FilePath)))
			{
				MessageBox.Show("Input path doesn't exist!");
				return;
			}

			GlobalState.mediaInfo = FFProbe.Analyse(Convert.ToString(FilePath));
			GlobalState.inputFileTotalSeconds = GlobalState.mediaInfo.Duration.TotalSeconds;
			
			// audio_bitrate * duration * nb_audio_tracks
			double audioSize = GlobalState.mediaInfo.PrimaryAudioStream.BitRate * GlobalState.mediaInfo.PrimaryAudioStream.Duration.TotalSeconds * GlobalState.mediaInfo.PrimaryAudioStream.Channels;
			int audioRoundedSize = Convert.ToInt32(Math.Ceiling(audioSize));

			/*
			MessageBox.Show("Bitrate:" + GlobalState.mediaInfo.PrimaryAudioStream.BitRate.ToString());
			MessageBox.Show("Channels:" + GlobalState.mediaInfo.PrimaryAudioStream.Channels.ToString());
			MessageBox.Show("Duration:" + GlobalState.mediaInfo.PrimaryAudioStream.Duration.TotalSeconds.ToString());
			MessageBox.Show(((double)audioRoundedSize / 8000 / 8000).ToString());
			*/

			// Calculate the bitrate needed for a 25MB file size
			double idealBitrate = ((FileSize - 1) * 8000 - (audioRoundedSize / 8000)) / GlobalState.inputFileTotalSeconds;
			int roundedBitrate = Convert.ToInt32(Math.Floor(idealBitrate));

			// Stupid ffmpeg doesn't overwrite, so I have to do it myself with this stupid poopy
			if (System.IO.Path.Exists(OutputPath))
			{
				MessageBoxResult dialogResult = MessageBox.Show("File already exists, overwrite it?", "Overwrite warning", MessageBoxButton.YesNo);
				if (dialogResult == MessageBoxResult.Yes)
				{
					File.Delete(OutputPath);
				}
				else if (dialogResult == MessageBoxResult.No)
				{
					GlobalState.progressWindow.Close();
					return;
				}
			}

			// Hide main window before compressing
			Hide();
			GlobalState.progressWindow = new();
			GlobalState.progressWindow.Show();

			GlobalState.activeCodec = (VideoCodecType)CodecSelector.SelectedItem;

			// Code for codec selector
			string codecString;
			switch (GlobalState.activeCodec)
			{
				case VideoCodecType.x264:
					codecString = "libx264";
					break;
				case VideoCodecType.x265:
					codecString = "libx265";
					break;
				case VideoCodecType.x264_NVENC:
					codecString = "h264_nvenc";
					break;
				case VideoCodecType.x265_NVENC:
					codecString = "hevc_nvenc";
					break;
				default:
					codecString = "libx264";
					break;
			}


			/*
			MessageBox.Show(codecString);
			MessageBox.Show(FilePath);
			MessageBox.Show(Convert.ToString(roundedBitrate));
			MessageBox.Show(OutputPath);
			MessageBox.Show(mediaInfo.Duration.ToString());
			*/

			// Start compression with calculated bitrate
			FFMpegArguments
			.FromFileInput(FilePath, true, options => options
			.WithHardwareAcceleration(HardwareAccelerationDevice.Auto))
		.OutputToFile(Convert.ToString(OutputPath), false, options => options
			.WithVideoBitrate(roundedBitrate)
			.UsingMultithreading(true)
			.WithVideoCodec(codecString)
			.OverwriteExisting()
			.WithFastStart())
		.NotifyOnProgress(ProgressFunction, GlobalState.mediaInfo.Duration)
		.ProcessAsynchronously();
		}

		// Function for window movement on mouse1
		private void Window_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				this.DragMove();
		}

		// Logic for close button on main window
		private void ExitButton_Click(object sender, RoutedEventArgs e)
		{
			Environment.Exit(0);
		}

		// Logic for changing codec on selection in ComboBox
		private void CodecSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			VideoCodecType codec = (VideoCodecType)CodecSelector.SelectedItem;

			if (nvencWarning != null)
			{
				if (codec == VideoCodecType.x264_NVENC | codec == VideoCodecType.x265_NVENC)
				{
					nvencWarning.Visibility = Visibility.Visible;
				}
				else
				{
					nvencWarning.Visibility = Visibility.Hidden;

				}
			}

		}
	}
}