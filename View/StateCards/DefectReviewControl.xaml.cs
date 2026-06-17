using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.View.StateCards
{
    public partial class DefectReviewControl : UserControl
    {
        private readonly string _originalImagePath;
        private readonly DefectRegion _defect;

        private string _selectedResult = "";

        public event Action<ReviewResult> ReviewSubmitted;
        public event Action BackRequested;

        public DefectReviewControl(string originalImagePath, DefectRegion defect)
        {
            InitializeComponent();
            _originalImagePath = originalImagePath;
            _defect = defect;

            LoadCroppedImage();
            PopulateInfo();
        }

        private void LoadCroppedImage()
        {
            try
            {
                // 优先使用缺陷专用图
                string imagePath = !string.IsNullOrEmpty(_defect.DefectImagePath)
                    ? _defect.DefectImagePath
                    : _originalImagePath;

                if (string.IsNullOrEmpty(_defect.DefectImagePath))
                {
                    // 无专用图：从原图裁剪
                    var bitmap = LoadBitmapSafe(imagePath);
                    if (bitmap == null) { ImgDefect.Source = null; return; }

                    int pixelX = (int)(_defect.X * bitmap.PixelWidth);
                    int pixelY = (int)(_defect.Y * bitmap.PixelHeight);
                    int pixelW = (int)(_defect.Width * bitmap.PixelWidth);
                    int pixelH = (int)(_defect.Height * bitmap.PixelHeight);

                    int padX = Math.Max(0, pixelW / 5);
                    int padY = Math.Max(0, pixelH / 5);
                    int cropX = Math.Max(0, pixelX - padX / 2);
                    int cropY = Math.Max(0, pixelY - padY / 2);
                    int cropW = Math.Min(pixelW + padX, bitmap.PixelWidth - cropX);
                    int cropH = Math.Min(pixelH + padY, bitmap.PixelHeight - cropY);

                    if (cropW <= 0 || cropH <= 0) return;
                    ImgDefect.Source = new CroppedBitmap(bitmap, new Int32Rect(cropX, cropY, cropW, cropH));
                }
                else
                {
                    // 有专用图：直接加载
                    ImgDefect.Source = LoadBitmapSafe(imagePath);
                }
            }
            catch
            {
                ImgDefect.Source = null;
            }
        }

        private void PopulateInfo()
        {
            RunDefectType.Text = _defect.DefectType;
            RunConfidence.Text = $"{_defect.Confidence * 100:F0}%";
            RunCoordinates.Text = $"坐标: ({_defect.X:F2}, {_defect.Y:F2})  ·  尺寸: {_defect.Width * 100:F0}×{_defect.Height * 100:F0}%";
        }

        private void BtnOK_Checked(object sender, RoutedEventArgs e)
        {
            _selectedResult = "OK";
            BtnNG.IsChecked = false;
            BtnOK.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
        }

        private void BtnOK_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_selectedResult == "OK") _selectedResult = "";
            BtnOK.ClearValue(BackgroundProperty);
        }

        private void BtnNG_Checked(object sender, RoutedEventArgs e)
        {
            _selectedResult = "NG";
            BtnOK.IsChecked = false;
            BtnNG.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        }

        private void BtnNG_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_selectedResult == "NG") _selectedResult = "";
            BtnNG.ClearValue(BackgroundProperty);
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedResult))
            {
                ShowError("请选择复判结果（OK / NG）");
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtUser.Text))
            {
                ShowError("请输入操作员");
                return;
            }

            TxtError.Visibility = Visibility.Collapsed;
            ReviewSubmitted?.Invoke(new ReviewResult
            {
                DefectId = _defect.DefectId,
                Result = _selectedResult,
                User = TxtUser.Text.Trim(),
                Comment = TxtComment.Text.Trim()
            });
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke();
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }

        private static BitmapImage LoadBitmapSafe(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ReviewResult
    {
        public string DefectId { get; set; } = "";
        public string Result { get; set; } = "";
        public string User { get; set; } = "";
        public string Comment { get; set; } = "";
    }
}
