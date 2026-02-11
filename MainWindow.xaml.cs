using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats;

namespace KoThumbMini
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string sizeStr && double.TryParse(sizeStr, out double size))
            {
                SizeSlider.Value = size;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                await ProcessPaths(paths);
            }
        }

        private string GetString(string key, params object[] args)
        {
            var res = Application.Current.TryFindResource(key) as string ?? key;
            return args.Length > 0 ? string.Format(res, args) : res;
        }

        private async Task ProcessPaths(string[] paths)
        {
            ProgressOverlay.Visibility = Visibility.Visible;
            StatusText.Text = GetString("StatusCollecting");
            
            int targetSize = (int)SizeSlider.Value;
            string format = RadioJpg.IsChecked == true ? "JPG" : (RadioPng.IsChecked == true ? "PNG" : "WebP");

            var files = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    files.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f => IsSupportedImage(f)));
                }
                else if (File.Exists(path) && IsSupportedImage(path))
                {
                    files.Add(path);
                }
            }

            if (files.Count == 0)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                StatusText.Text = GetString("StatusNoImages");
                return;
            }

            MainProgressBar.Maximum = files.Count;
            MainProgressBar.Value = 0;
            int processed = 0;
            int success = 0;

            await Task.Run(() =>
            {
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
                {
                    try
                    {
                        ProcessImage(file, targetSize, format);
                        Interlocked.Increment(ref success);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {file}: {ex.Message}");
                    }

                    int current = Interlocked.Increment(ref processed);
                    Dispatcher.Invoke(() =>
                    {
                        MainProgressBar.Value = current;
                        ProgressText.Text = GetString("StatusProcessing", current, files.Count);
                    });
                });
            });

            ProgressOverlay.Visibility = Visibility.Collapsed;
            StatusText.Text = GetString("StatusFinished", success);
            
            if (success > 0)
            {
                // Open the first result's folder as a convenience
                try
                {
                    string firstFile = files[0];
                    string? dir = Path.GetDirectoryName(firstFile);
                    if (dir != null)
                    {
                        string outputDir = Path.Combine(dir, "resized");
                        if (Directory.Exists(outputDir))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", outputDir);
                        }
                    }
                }
                catch { }
            }
        }

        private bool IsSupportedImage(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".bmp" || ext == ".gif" || ext == ".tiff";
        }

        private void ProcessImage(string inputPath, int targetSize, string format)
        {
            string? dir = Path.GetDirectoryName(inputPath);
            if (dir == null) return;

            string outputDir = Path.Combine(dir, "resized");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            using var image = SixLabors.ImageSharp.Image.Load(inputPath);

            // Calculate resize dimensions (Long Side)
            int width = image.Width;
            int height = image.Height;
            int newWidth, newHeight;

            if (width >= height)
            {
                newWidth = targetSize;
                newHeight = (int)((double)height * targetSize / width);
            }
            else
            {
                newHeight = targetSize;
                newWidth = (int)((double)width * targetSize / height);
            }

            // Only resize if target is smaller or if we want to force resize (default to always resize for simplicity)
            image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));

            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = format.ToLower();
            if (extension == "jpg") extension = "jpg";
            
            string outputPath = Path.Combine(outputDir, $"{fileName}.{extension}");

            IImageEncoder encoder = format switch
            {
                "PNG" => new PngEncoder(),
                "WebP" => new WebpEncoder { Quality = 90 },
                _ => new JpegEncoder { Quality = 90 }
            };

            image.Save(outputPath, encoder);
        }
    }
}