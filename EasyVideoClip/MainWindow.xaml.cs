using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace EasyVideoClip
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindowViewModel ViewModel { get; set; }

        public enum LogLevel
        {
            Trace,
            Info,
            Warning,
            Error
        }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            DataContext = ViewModel;
            LogRichTextBox.Document.Blocks.Clear();
            LoadEmbeddedImage();
        }

        private void BrowseFromFile_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFileDialog()
            {
                Title = "选择输入的视频",
                InitialDirectory = Path.GetDirectoryName(ViewModel.FromRootFilePath)
            };
            if (folderDialog.ShowDialog() == true)
            {
                ViewModel.FromRootFilePath = folderDialog.FileName;
            }
        }

        private void BrowseToFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog()
            {
                Title = "选择输出文件夹",
                InitialDirectory = ViewModel.ToRootFolderPath
            };
            if (folderDialog.ShowDialog() == true)
            {
                ViewModel.ToRootFolderPath = folderDialog.FolderName;
            }
        }

        private void OpenFromFile_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ViewModel.FromRootFilePath))
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "explorer.exe";
                    process.StartInfo.Arguments = ViewModel.FromRootFilePath;
                    process.Start();
                }
            }
            else
            {
                MessageBox.Show("原视频文件不存在，请重新选择", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenToFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(ViewModel.ToRootFolderPath))
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "explorer.exe";
                    process.StartInfo.Arguments = ViewModel.ToRootFolderPath;
                    process.Start();
                }
            }
            else
            {
                MessageBox.Show("导出文件夹不存在，请重新选择", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            // 检查是否是文件拖放
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 获取拖放的文件路径
                string[] droppedFilePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (droppedFilePaths.Length > 0)
                {
                    ViewModel.FromRootFilePath = droppedFilePaths[0];
                }
            }
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private async void Clip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = ViewModel.ClipCommand;
                    process.StartInfo.UseShellExecute = false; // 不使用系统外壳程序启动进程
                    process.StartInfo.CreateNoWindow = true; // 不显示窗口
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    AppendLog($"开始转换：{process.StartInfo.Arguments}", LogLevel.Info);

                    // 设置事件处理程序以异步读取输出
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppendLog(e.Data, LogLevel.Trace);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppendLog(e.Data, LogLevel.Error);
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.WaitForExitAsync();

                    process.CancelOutputRead();
                    process.CancelErrorRead();

                    AppendLog($"转换结束：{process.StartInfo.Arguments}", LogLevel.Info);
                }
            }
            catch (Exception err)
            {
                AppendLog($"转换出错：{err.Message}", LogLevel.Error);
            }
        }

        private void OpenClipFile_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ViewModel.ClipFilePath))
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "explorer.exe";
                    process.StartInfo.Arguments = ViewModel.ClipFilePath;
                    process.Start();
                }
            }
            else
            {
                MessageBox.Show("原视频文件不存在，请重新选择", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void AppendLog(string message, LogLevel level = LogLevel.Trace)
        {
            Dispatcher.Invoke(() => // 在 UI 线程中更新日志
            {
                var paragraph = new System.Windows.Documents.Paragraph();
                //paragraph.Inlines.Add(new Run($"[{DateTime.Now:HH:mm:ss}] ") { Foreground = Brushes.Gray });

                Brush color = Brushes.Black;
                switch (level)
                {
                    case LogLevel.Trace: color = Brushes.Gray; break;
                    case LogLevel.Info: color = Brushes.Green; break;
                    case LogLevel.Warning: color = Brushes.Orange; break;
                    case LogLevel.Error: color = Brushes.Red; break;
                }

                paragraph.Inlines.Add(new Run(message) { Foreground = color });
                LogRichTextBox.Document.Blocks.Add(paragraph);
                LogRichTextBox.ScrollToEnd();
            });
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void LoadEmbeddedImage()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "EasyVideoClip.Resources.qrcode_for_gh.png";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        QrcodeForGHImage.Source = bitmap;
                    }
                    else
                    {
                        MessageBox.Show($"找不到嵌入资源: {resourceName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}");
            }
        }

        private void GongZhongHao_MouseEnter(object sender, MouseEventArgs e)
        {
            imagePopup.IsOpen = true;
        }

        private void GongZhongHao_MouseLeave(object sender, MouseEventArgs e)
        {
            imagePopup.IsOpen = false;
        }
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel()
        {
            setClipCommand();
        }

        private string _fromRootFilePath = Settings.Default.fromRootFilePath;
        public string FromRootFilePath
        {
            get => _fromRootFilePath;
            set
            {
                if (_fromRootFilePath != value)
                {
                    _fromRootFilePath = value;
                    Settings.Default.fromRootFilePath = value;
                    Settings.Default.Save();
                    OnPropertyChanged();
                    setClipCommand();
                }
            }
        }

        private string _toRootFolderPath = Settings.Default.toRootFolderPath;
        public string ToRootFolderPath
        {
            get => _toRootFolderPath;
            set
            {
                if (_toRootFolderPath != value)
                {
                    _toRootFolderPath = value;
                    Settings.Default.toRootFolderPath = value;
                    Settings.Default.Save();
                    OnPropertyChanged();
                    setClipCommand();
                }
            }
        }

        public string ClipFilePath
        {
            get
            {
                return Path.Combine(_toRootFolderPath, Path.GetFileName(_fromRootFilePath));
            }
        }



        private string _ss = Settings.Default.ss;
        public string SS
        {
            get => _ss;
            set
            {
                if (_ss != value)
                {
                    _ss = value;
                    Settings.Default.ss = value;
                    Settings.Default.Save();
                    OnPropertyChanged();
                    setClipCommand();
                }
            }
        }

        private string _to = Settings.Default.to;
        public string TO
        {
            get => _to;
            set
            {
                if (_to != value)
                {
                    _to = value;
                    Settings.Default.to = value;
                    Settings.Default.Save();
                    OnPropertyChanged();
                    setClipCommand();
                }
            }
        }

        private string _clipcommand = string.Empty;
        public string ClipCommand
        {
            get => _clipcommand;
            set
            {
                if (_clipcommand != value)
                {
                    _clipcommand = value;
                    OnPropertyChanged();
                }
            }
        }

        private void setClipCommand()
        {
            string inputVideo = @$"{FromRootFilePath}";
            string outputVideo = @$"{ClipFilePath}";
            string to = TO == "00:00:00" ? "" : $"-to {TO}";
            ClipCommand = $"/k ffmpeg  -i \"{inputVideo}\" -ss {SS} {to} -c copy \"{outputVideo}\" -y & exit"; ;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
