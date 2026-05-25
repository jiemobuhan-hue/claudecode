using DevExpress.Xpf.Charts;
using DevExpress.Xpf.Grid;
using DevExpress.XtraRichEdit.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Threading;
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
using System.Windows.Threading;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service;
using static ZenergyBFSI.Model.InspectionUtils;

namespace ZenergyBFSI.View.StateCards
{
    /// <summary>
    /// UC_InspectionView.xaml 的交互逻辑
    /// </summary>
    public partial class UC_InspectionView : UserControl
    {
        // ════════════════════════════════════════════════════════
        //  公开 API
        // ════════════════════════════════════════════════════════

        /// <summary>搜索按钮触发，参数包含全部搜索条件</summary>
        public event EventHandler<SearchArgs> SearchRequested;

        /// <summary>列表选中某产品，需要加载其图片</summary>
        public event EventHandler<string> LoadImagesRequested;  // cellCode

        /// <summary>用户提交复检结果</summary>
        public event EventHandler<SaveReviewArgs> SaveReviewRequested;

        // ── 由外部（ViewModel/Service）调用以推送数据 ────────────

        /// <summary>推送搜索结果（在后台查完后调用）</summary>
        public void SetSearchResults(List<InspectionRecord> records, int total,
                                     int pageIndex, int pageSize)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _total = total;
                _pageIndex = pageIndex;
                _pageSize = pageSize;

                // 包装：给 ListView ItemTemplate 添加 NgTagList 属性
                LvRecords.ItemsSource = records.Select(r => new RecordVm(r)).ToList();

                int totalPages = (int)Math.Ceiling(total / (double)pageSize);
                TxtPageInfo.Text = $"第 {pageIndex + 1} 页 / 共 {totalPages} 页";
                TxtPageNum.Text = $"{pageIndex + 1} / {totalPages}";
                TxtResultCount.Text = $"共 {total} 条结果";

                BtnPrev.IsEnabled = pageIndex > 0;
                BtnNext.IsEnabled = (pageIndex + 1) < totalPages;
            });
        }

        /// <summary>推送产品图片（在选中产品并加载完成后调用）</summary>
        public void SetProductImages(string cellCode, List<ImageRecord> images)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _images = images;
                BuildImageCards();
            });
        }

        /// <summary>
        /// 某图片复检保存成功后调用，刷新卡片状态。
        /// 若放大弹窗仍打开，同步刷新右侧历史记录显示。
        /// </summary>
        public void OnReviewSaved(string imageId, string result,
                                  string user, string comment)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var img = _images.FirstOrDefault(x => x.ImageId == imageId);
                if (img == null) return;

                img.IsManualReviewed = true;
                img.ManualResult = result;
                img.ManualUser = user;
                img.ManualComment = comment;
                img.ManualTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 更新对应卡片底部的 Badge
                if (_cardMap.TryGetValue(imageId, out var card))
                    RefreshCardFooter(card, img);

                // 若弹窗显示的正是这张图，刷新历史记录
                if (GridOverlay.Visibility == Visibility.Visible &&
                    _images[_zoomIdx].ImageId == imageId)
                    PopulateHistoryPanel(img);
            });
        }

        // ════════════════════════════════════════════════════════
        //  内部状态
        // ════════════════════════════════════════════════════════

        // 分页
        private int _total = 0, _pageIndex = 0, _pageSize = 20;

        // 图片数据
        private List<ImageRecord> _images = new List<ImageRecord>();
        private int _zoomIdx = 0;

        // 图片卡片引用（imageId → Border 卡片）
        private readonly Dictionary<string, Border> _cardMap = new Dictionary<string, Border>();

        // 后台加载取消令牌（切换产品时取消旧加载任务）
        private CancellationTokenSource _loadCts;
        // 占位卡片引用列表（用于后台加载时替换图片）
        private readonly List<Border> _placeholders = new List<Border>();

        // 图片缩放状态
        private bool _isZoomed = false;

        // 小地图拖拽状态
        private bool _minimapDragging = false;

        // ListView 绑定用包装类
        private class RecordVm
        {
            public string CellCode { get; }
            public string DateTime { get; }
            public string StationId { get; }
            public string OverallResult { get; }
            public string NgTypes { get; }
            public List<string> NgTagList { get; }
            public string RecordId { get; }
            public string Recordpath { get; set; }
            // 保存原始对象方便 SelectionChanged 取用
            public InspectionRecord Raw { get; }

            public RecordVm(InspectionRecord r)
            {
                Raw = r;
                CellCode = r.CellCode;
                DateTime = r.DateTime;
                StationId = r.StationId;
                OverallResult = r.OverallResult;
                NgTypes = r.NgTypes;
                RecordId = r.RecordId;
                NgTagList = (r.NgTypes ?? "")
                    //.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
                    .Split('|').ToList();
                Recordpath = r.Recordpath;
            }
        }

        // ════════════════════════════════════════════════════════
        //  构造
        // ════════════════════════════════════════════════════════
        public UC_InspectionView()
        {
            InitializeComponent();
            DpFrom.SelectedDate = DateTime.Today;
            DpTo.SelectedDate = DateTime.Today;
            BtnPrev.IsEnabled = false;
            BtnNext.IsEnabled = false;
            SearchRequested 
            += async (sender, e) =>
            {
                await UC_InspectionView_SearchRequestedAsync(sender, e);
            };
            LoadImagesRequested += UC_InspectionView_LoadImagesRequested;

        }

        private void UC_InspectionView_LoadImagesRequested(object sender, string e)
        {

            List<string> NGFiles = new List<string>();
            List<string> OKFiles = new List<string>();
            var files = Directory.GetFiles(e, "*__NG.bmp", SearchOption.AllDirectories);
            NGFiles.AddRange(files);
            var files2 = Directory.GetFiles(e, "*__OK.bmp", SearchOption.AllDirectories);
            OKFiles.AddRange(files2);

            List<ImageRecord> temp = new List<ImageRecord>();
            foreach(var path in OKFiles)
            {
                // 分割路径，过滤掉空项（网络路径开头的两个空串会被保留）
                string[] parts = path.Split(System.IO.Path.DirectorySeparatorChar);
                // 取最后3段：右侧面、XYZ法向图-未扣图、08-20-42__OK.bmp
                string[] lastThree = parts.Skip(parts.Length - 4).ToArray();
                temp.Add(new ImageRecord()
                {
                    ImageId = lastThree[3],
                    CellCode = lastThree[0],
                    StationId = lastThree[1],
                    AngleName = lastThree[2],
                    VisionResult= "OK",
                    NgType = "OK",
                    VisionScore = 100,
                    ImagePath = path
                });
            }
            foreach (var path in NGFiles)
            {
                // 分割路径，过滤掉空项（网络路径开头的两个空串会被保留）
                string[] parts = path.Split(System.IO.Path.DirectorySeparatorChar);
                // 取最后3段：右侧面、XYZ法向图-未扣图、08-20-42__OK.bmp
                string[] lastThree = parts.Skip(parts.Length - 4).ToArray();
                temp.Add(new ImageRecord()
                {
                    ImageId = lastThree[3],
                    CellCode = lastThree[0],
                    StationId = lastThree[1],
                    AngleName = lastThree[2],
                    VisionResult = "NG",
                    NgType = lastThree[1]+ lastThree[2]+ "检测NG",
                    VisionScore = 100,
                    ImagePath = path
                });
            }
            SetProductImages("111", temp);
        }
        /// <summary>
        /// 在指定目录下递归搜索文件名包含 cellCode 的文件（忽略大小写）
        /// </summary>
        /// <param name="directoryPath">根目录（如 @"\\192.168.1.13\d\CameraRaw\"）</param>
        /// <param name="cellCode">电芯码关键词</param>
        /// <returns>匹配的文件完整路径列表</returns>
        private List<string> SearchFilesByCellCode(string directoryPath, string cellCode)
        {
            var result = new List<string>();
            if (!Directory.Exists(directoryPath))
                return result;
             
            // 构造通配符模式：*code*
            string pattern = $"*{cellCode}*";

            // 获取所有匹配的子文件夹（递归搜索所有层级） 
            List<string> matchedDirs = Directory.GetDirectories(directoryPath, pattern, SearchOption.AllDirectories).ToList();



            return matchedDirs;
        }
        private async Task UC_InspectionView_SearchRequestedAsync(object sender, SearchArgs e)
        {

            var datafrom = DpFrom.DisplayDate.ToString("yyyy-MM-dd");
            var dataTo = DpTo.DisplayDate.ToString("yyyy-MM-dd"); 
            string networkPath = @"\\192.168.1.33\d\CameraRaw\" +$"{dataTo}" ; // 1. 定义UNC路径
            if (!Directory.Exists(networkPath))
            {
 
                MessageBox.Show($"未找到网络路径: {networkPath}");
                return;
            }

            var result = new List<string>();

            // 异步搜索（若需要同步则直接调用同步方法）
            var matchedFiles =await Task.Run(() => SearchFilesByCellCode(networkPath, e.CellCode));


            var x = e.CellCode;
             List<InspectionRecord> temppic = new();

            List<string> NGFiles = new List<string>();
            List<string> OKFiles = new List<string>();
            foreach (var pathfiles in matchedFiles)
            {
                // 递归搜索该子文件夹下所有 *__OK.bmp 文件
                var files = Directory.GetFiles(pathfiles, "*__NG.bmp", SearchOption.AllDirectories);
                NGFiles.AddRange(files);
                var files2 = Directory.GetFiles(pathfiles, "*__OK.bmp", SearchOption.AllDirectories);
                OKFiles.AddRange(files2);
                if (NGFiles.Count > 0)
                {
                    temppic.Add(new InspectionRecord()
                    {
                        CellCode = System.IO.Path.GetFileName(pathfiles),
                        LineId = "LineId1",
                        OverallResult = "NG",
                        NgTypes = $"{NGFiles.Count}",
                        DateTime = dataTo,
                        StationId = "StationIdA",
                        ProcessMs = 1,
                        Recordpath = pathfiles
                    });
                }
                else
                {
                    temppic.Add(new InspectionRecord()
                    {
                        CellCode = System.IO.Path.GetFileName(pathfiles),
                        LineId = "LineId1",
                        OverallResult = "OK",
                        NgTypes = $"{OKFiles.Count}",
                        DateTime = dataTo,
                        StationId = "StationIdA",
                        ProcessMs = 1,
                        Recordpath = pathfiles
                    });
                }
            }

            SetSearchResults(temppic, 5, 0, 5);
        }

        // ════════════════════════════════════════════════════════
        //  搜索栏事件
        // ════════════════════════════════════════════════════════
        private void BtnSearch_Click(object sender, RoutedEventArgs e) => FireSearch(0);
        private void TxtCellCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) FireSearch(0);
        }

        private void FireSearch(int page)
        {
            _pageIndex = page;
            var resultMap = new[] { "ALL", "OK", "NG" };
            var args = new SearchArgs
            {
                CellCode = TxtCellCode.Text.Trim(),
                DateFrom = DpFrom.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                DateTo = DpTo.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                ResultFilter = resultMap[Math.Max(0, CmbResult.SelectedIndex)],
                PageIndex = page,
                PageSize = _pageSize
            };
            SearchRequested?.Invoke(this, args);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TxtCellCode.Clear();
            DpFrom.SelectedDate = DateTime.Today;
            DpTo.SelectedDate = DateTime.Today;
            CmbResult.SelectedIndex = 0;
            LvRecords.ItemsSource = null;
            TxtResultCount.Text = "输入条件后搜索";
            TxtPageInfo.Text = "-";
            TxtPageNum.Text = "-";
            HideDetailPanel();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => FireSearch(_pageIndex - 1);
        private void BtnNext_Click(object sender, RoutedEventArgs e) => FireSearch(_pageIndex + 1);

        // ════════════════════════════════════════════════════════
        //  列表选中 → 加载图片
        // ════════════════════════════════════════════════════════
        private void LvRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvRecords.SelectedItem is RecordVm vm) 
            {
            
            }
            else
            {
                return;
            }

            // 更新 Header
            CardDetailHeader.Visibility = Visibility.Visible;
            TxtDetailCode.Text = vm.CellCode;
            TxtDetailMeta.Text = $"{vm.DateTime} | {vm.StationId}";
            SetDetailBadge(vm.OverallResult);

            // 清空图片区，显示加载占位
            _images.Clear();
            _cardMap.Clear();
            WpImages.Children.Clear();
            GridEmpty.Visibility = Visibility.Collapsed;
            SvImages.Visibility = Visibility.Visible;

            // 触发外部加载图片
            LoadImagesRequested?.Invoke(this, vm.Recordpath);
        }

        private void HideDetailPanel()
        {
            CardDetailHeader.Visibility = Visibility.Collapsed;
            SvImages.Visibility = Visibility.Collapsed;
            GridEmpty.Visibility = Visibility.Visible;
            WpImages.Children.Clear();
            _images.Clear();
            _cardMap.Clear();
        }

        // ════════════════════════════════════════════════════════
        //  图片卡片生成（code-behind 动态构建）
        // ════════════════════════════════════════════════════════
        private void BuildImageCards()
        {
            // 取消上一次未完成的加载任务
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            WpImages.Children.Clear();
            _cardMap.Clear();
            _placeholders.Clear();
            TxtImgCount.Text = $"{_images.Count} 张图片";

            // ① 同步渲染占位卡片（无图片加载，<100ms）
            for (int i = 0; i < _images.Count; i++)
            {
                var img = _images[i];
                var idx = i;
                var placeholder = BuildPlaceholderCard(img, () => OpenZoom(idx));
                _cardMap[img.ImageId] = placeholder;
                _placeholders.Add(placeholder);
                WpImages.Children.Add(placeholder);
            }

            // ② 后台逐张加载真实缩略图
            Task.Run(async () =>
            {
                for (int i = 0; i < _images.Count; i++)
                {
                    if (token.IsCancellationRequested) return;
                    try
                    {
                        var img = _images[i];
                        var cachedPath = ThumbnailCache.GetOrCreate(img.ImagePath, 400);
                        if (cachedPath == null) continue;

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(cachedPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        var idx = i;
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            ReplacePlaceholderImage(idx, bmp);
                        });
                    }
                    catch
                    {
                        // 单张加载失败不影响其余图片
                    }
                }
            }, token);
        }

        /// <summary>
        /// 构建不含图片的占位卡片，与 BuildCard 结构一致但图片区仅显示灰底+角度名。
        /// </summary>
        private Border BuildPlaceholderCard(ImageRecord img, Action onZoom)
        {
            bool isNg = img.VisionResult == "NG";
            bool manOk = img.IsManualReviewed && img.ManualResult == "OK";
            bool manNg = img.IsManualReviewed && img.ManualResult == "NG";

            var borderColor = manOk ? Color.FromRgb(0x4C, 0xAF, 0x50)
                            : manNg ? Color.FromRgb(0xF4, 0x43, 0x36)
                            : isNg ? Color.FromArgb(0x66, 0xF4, 0x43, 0x36)
                            : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF);

            var card = new Border
            {
                Width = 240,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(borderColor),
                Background = (Brush)FindResource("MaterialDesignCardBackground"),
                Cursor = Cursors.Hand,
                Tag = img.ImageId
            };

            card.MouseDown += (_, __) => onZoom();

            var stack = new StackPanel();
            card.Child = stack;

            // ── 图片占位区（灰底 + 角度名文字）──
            var imgGrid = new Grid { Height = 170, Background = Brushes.Black };
            imgGrid.Tag = "placeholder";
            imgGrid.Children.Add(new TextBlock
            {
                Text = img.AngleName,
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            stack.Children.Add(imgGrid);

            // ── 信息区 ──
            var info = new StackPanel { Margin = new Thickness(10, 8, 10, 10) };
            stack.Children.Add(info);

            info.Children.Add(new TextBlock
            {
                Text = img.AngleName,
                FontSize = 12,
                FontWeight = FontWeights.Medium
            });

            var visionRow = new StackPanel { Orientation = Orientation.Horizontal };
            visionRow.Children.Add(new TextBlock
            {
                Text = "视觉:",
                FontSize = 10,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            visionRow.Children.Add(MakeBadge(img.VisionResult ?? "--",
                img.VisionResult == "OK" ? Color.FromRgb(0x4C, 0xAF, 0x50)
                                         : Color.FromRgb(0xF4, 0x43, 0x36)));
            visionRow.Children.Add(new TextBlock
            {
                Text = $"{img.VisionScore * 100:F1}%",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            if (!string.IsNullOrEmpty(img.NgType))
                visionRow.Children.Add(MakeTag(img.NgType));
            info.Children.Add(visionRow);

            // ── 复检状态区（可重刷）──
            var footerHolder = new ContentControl { Tag = img.ImageId };
            info.Children.Add(footerHolder);
            card.Tag = (img, footerHolder, onZoom);
            RefreshCardFooter(card, img);

            // ── 放大按钮（悬停显示）──
            var zoomBtn = new Button
            {
                Style = (Style)FindResource("MaterialDesignIconButton"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Opacity = 0,
                ToolTip = "放大查看"
            };
            zoomBtn.Content = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = MaterialDesignThemes.Wpf.PackIconKind.ZoomIn,
                Width = 18,
                Height = 18
            };
            zoomBtn.Click += (_, __) => onZoom();
            imgGrid.Children.Add(zoomBtn);
            card.MouseEnter += (_, __) => zoomBtn.Opacity = 1;
            card.MouseLeave += (_, __) => zoomBtn.Opacity = 0;

            return card;
        }

        /// <summary>
        /// 将占位卡片的灰色背景替换为已加载的缩略图。
        /// </summary>
        private void ReplacePlaceholderImage(int index, BitmapImage bmp)
        {
            if (index < 0 || index >= _placeholders.Count) return;

            var card = _placeholders[index];
            var stack = card.Child as StackPanel;
            if (stack == null || stack.Children.Count == 0) return;

            var imgGrid = stack.Children[0] as Grid;
            if (imgGrid == null) return;

            // 移除占位文字（保留 zoom 按钮等其他控件）
            var toRemove = imgGrid.Children.OfType<TextBlock>().ToList();
            foreach (var tb in toRemove) imgGrid.Children.Remove(tb);
            // 插入真实图片到最底层（zoom 按钮保持在上层）
            var wpfImg = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                Source = bmp
            };
            imgGrid.Children.Insert(0, wpfImg);
        }

        private Border BuildCard(ImageRecord img, Action onZoom)
        {
            bool isNg = img.VisionResult == "NG";
            bool manOk = img.IsManualReviewed && img.ManualResult == "OK";
            bool manNg = img.IsManualReviewed && img.ManualResult == "NG";

            // 边框颜色
            var borderColor = manOk ? Color.FromRgb(0x4C, 0xAF, 0x50)
                            : manNg ? Color.FromRgb(0xF4, 0x43, 0x36)
                            : isNg ? Color.FromArgb(0x66, 0xF4, 0x43, 0x36)
                            : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF);

            var card = new Border
            {
                Width = 240,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(borderColor),
                Background = (Brush)FindResource("MaterialDesignCardBackground"),
                Cursor = Cursors.Hand,
                Tag = img.ImageId
            };

            card.MouseDown += (_, __) => onZoom();

            var stack = new StackPanel();
            card.Child = stack;

            // ── 图片区 ────────────────────────────────────────
            var imgGrid = new Grid { Height = 170, Background = Brushes.Black };
            stack.Children.Add(imgGrid);

            var wpfImg = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                 
                //RenderTransform
                //RenderOptions = { BitmapScalingMode = BitmapScalingMode.HighQuality }
            };
            if (File.Exists(img.ImagePath))
                wpfImg.Source = LoadBitmapSafe(img.ImagePath);

            imgGrid.Children.Add(wpfImg);

            // NG 角标
            if (isNg)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 2, 7, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Margin = new Thickness(8),
                };
                badge.Child = new TextBlock
                {
                    Text = "NG",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas")
                };
                imgGrid.Children.Add(badge);
            }

            // 放大按钮
            var zoomBtn = new Button
            {
                Style = (Style)FindResource("MaterialDesignIconButton"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Opacity = 0,
                ToolTip = "放大查看"
            };
            //zoomBtn.Content = new md::PackIcon { Kind = md::PackIconKind.ZoomIn, Width = 18, Height = 18 };
            zoomBtn.Content = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ZoomIn, Width = 18, Height = 18 };
            zoomBtn.Click += (_, __) => onZoom();
            imgGrid.Children.Add(zoomBtn);
            // 鼠标进入显示放大按钮
            card.MouseEnter += (_, __) => zoomBtn.Opacity = 1;
            card.MouseLeave += (_, __) => zoomBtn.Opacity = 0;

            // ── 信息区 ────────────────────────────────────────
            var info = new StackPanel { Margin = new Thickness(10, 8, 10, 10)};
            stack.Children.Add(info);

            // 角度名
            info.Children.Add(new TextBlock
            {
                Text = img.AngleName,
                FontSize = 12,
                FontWeight = FontWeights.Medium
            });

            // 视觉判定行
            var visionRow = new StackPanel { Orientation = Orientation.Horizontal };
            visionRow.Children.Add(new TextBlock
            {
                Text = "视觉:",
                FontSize = 10,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            visionRow.Children.Add(MakeBadge(img.VisionResult ?? "--",
                img.VisionResult == "OK" ? Color.FromRgb(0x4C, 0xAF, 0x50)
                                         : Color.FromRgb(0xF4, 0x43, 0x36)));
            visionRow.Children.Add(new TextBlock
            {
                Text = $"{img.VisionScore * 100:F1}%",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            if (!string.IsNullOrEmpty(img.NgType))
                visionRow.Children.Add(MakeTag(img.NgType));
            info.Children.Add(visionRow);

            // 复检状态区（可重刷）
            var footerHolder = new ContentControl { Tag = img.ImageId };
            info.Children.Add(footerHolder);
            card.Tag = footerHolder;   // 用 Tag 存 footerHolder 供 RefreshCardFooter 用

            // 重用逻辑
            card.Tag = (img, footerHolder, onZoom);
            RefreshCardFooter(card, img);

            return card;
        }

        // 刷新卡片底部的复检状态
        private void RefreshCardFooter(Border card, ImageRecord img)
        {
            //if (card.Tag is not (ImageRecord _, ContentControl holder, Action onZoom)) return;



            ////if (card.Tag  is (ImageRecord _, ContentControl holder, Action onZoom))
            ////{

            //    //}
            //    //else
            //    //{
            //    //    return;
            //    //}
            //if (card.Tag is ImageRecord)
            //{
            // var holder = card.content
            //}
            //else
            //{
            //    return;
            //}
            //holder.Content = img.IsManualReviewed
            //    ? BuildReviewedFooter(img)
            //    : BuildNotReviewedFooter(onZoom);
        }

        private UIElement BuildReviewedFooter(ImageRecord img)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new MaterialDesignThemes.Wpf.PackIcon
            {
                //Kind = md::PackIconKind.CheckCircle,
                Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle,
                Width = 14,
                Height = 14,
                Foreground = Brushes.Gray,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{img.ManualUser} 已复检",
                FontSize = 10,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            row.Children.Add(MakeBadge(img.ManualResult,
                img.ManualResult == "OK" ? Color.FromRgb(0x4C, 0xAF, 0x50)
                                         : Color.FromRgb(0xF4, 0x43, 0x36)));
            return row;
        }

        private UIElement BuildNotReviewedFooter(Action onZoom)
        {
            var btn = new Button
            {
                Content = "点击复检",
                Style = (Style)FindResource("MaterialDesignOutlinedButton"),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btn.Click += (_, __) => onZoom();
            return btn;
        }

        // ════════════════════════════════════════════════════════
        //  放大弹窗
        // ════════════════════════════════════════════════════════
        private void OpenZoom(int idx)
        {
            _zoomIdx = idx;
            _isZoomed = false;
            GridOverlay.Visibility = Visibility.Visible;
            PopulateZoom();
        }

        private void CloseOverlay()
        {
            GridOverlay.Visibility = Visibility.Collapsed;
            TglOk.IsChecked = false;
            TglNg.IsChecked = false;
        }

        private void PopulateZoom()
        {
            if (_images.Count == 0) return;
            var img = _images[_zoomIdx];

            // 图片
            ImgZoom.Source = File.Exists(img.ImagePath) ? LoadBitmapSafe(img.ImagePath) : null;
            ImgZoom.RenderTransform = Transform.Identity;
            _isZoomed = false;

            // 缺陷标注框
            BboxCanvas.Children.Clear();
            if (!string.IsNullOrEmpty(img.DefectBbox))
                DrawBbox(img.DefectBbox);

            // 文本信息
            TxtZoomCounter.Text = $"{_zoomIdx + 1} / {_images.Count}";
            TxtZoomAngle.Text = img.AngleName;
            TxtZoomTitle.Text = $"{img.AngleName}";
            TxtZoomStation.Text = $"工位 {img.StationId}";
            TxtVisionResult.Text = img.VisionResult ?? "--";
            TxtVisionScore.Text = $"{img.VisionScore * 100:F1}%";
            PbScore.Value = img.VisionScore;
            TxtNgType.Text = string.IsNullOrEmpty(img.NgType) ? "无" : img.NgType;
            TxtBbox.Text = string.IsNullOrEmpty(img.DefectBbox) ? "--" : img.DefectBbox;

            // 视觉结果颜色
            bool vOk = img.VisionResult == "OK";
            TxtVisionResult.Foreground = vOk
                ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                : new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            BorderVision.Background = vOk
                ? new SolidColorBrush(Color.FromArgb(0x20, 0x4C, 0xAF, 0x50))
                : new SolidColorBrush(Color.FromArgb(0x20, 0xF4, 0x43, 0x36));

            // 导航按钮
            BtnPrevImg.IsEnabled = _zoomIdx > 0;
            BtnNextImg.IsEnabled = _zoomIdx < _images.Count - 1;

            // 缩略图导航
            BuildThumbs();

            // 小地图
            BuildMinimap();
            SvZoom.ScrollChanged -= OnSvZoomScrollChanged;
            SvZoom.ScrollChanged += OnSvZoomScrollChanged;

            // 防止拖拽出界后鼠标捕获丢失
            CanvasMinimap.LostMouseCapture -= OnMinimapLostCapture;
            CanvasMinimap.LostMouseCapture += OnMinimapLostCapture;

            // 历史复检
            PopulateHistoryPanel(img);

            // 复检状态初始化
            TglOk.IsChecked = img.IsManualReviewed && img.ManualResult == "OK";
            TglNg.IsChecked = img.IsManualReviewed && img.ManualResult == "NG";
            UpdateSubmitBtn();

            // 小地图视口矩形延迟更新（等待 Canvas 完成 Measure/Arrange）
            Dispatcher.BeginInvoke(new Action(() => UpdateMinimapRect()),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void DrawBbox(string bbox)
        {
            // 格式: "x;y;w;h"（像素，相对原图）
            var parts = bbox.Split(';');
            if (parts.Length != 4) return;
            if (!double.TryParse(parts[0], out double bx) ||
                !double.TryParse(parts[1], out double by) ||
                !double.TryParse(parts[2], out double bw) ||
                !double.TryParse(parts[3], out double bh)) return;

            // 换算为 Canvas 相对坐标（简化：假设图片充满 Canvas）
            // 实际生产中需按图片原始分辨率与显示尺寸的比例换算
            var rect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                StrokeThickness = 2,
                Width = bw,
                Height = bh,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0xF4, 0x43, 0x36),
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };
            Canvas.SetLeft(rect, bx);
            Canvas.SetTop(rect, by);
            BboxCanvas.Children.Add(rect);
        }

        private void BuildThumbs()
        {
            SpThumbs.Children.Clear();
            for (int i = 0; i < _images.Count; i++)
            {
                var idx = i;
                var img = _images[i];

                var thumb = new Border
                {
                    Width = 50,
                    Height = 36,
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(2),
                    BorderBrush = new SolidColorBrush(
                        i == _zoomIdx ? Color.FromRgb(0x21, 0x96, 0xF3) : Colors.Transparent),
                    Background = Brushes.Black,
                    Cursor = Cursors.Hand,
                    ToolTip = img.AngleName
                };
                thumb.MouseDown += (_, __) => { _zoomIdx = idx; PopulateZoom(); };

                if (File.Exists(img.ImagePath))
                {
                    thumb.Child = new System.Windows.Controls.Image
                    {
                        Source = LoadBitmapSafe(img.ImagePath, 50),
                        Stretch = Stretch.UniformToFill
                    };
                }
                SpThumbs.Children.Add(thumb);
            }
        }

        /// <summary>加载小地图缩略图（极低分辨率解码）。</summary>
        private void BuildMinimap()
        {
            var img = _images[_zoomIdx];
            if (!File.Exists(img.ImagePath)) return;
            ImgMinimap.Source = LoadBitmapSafe(img.ImagePath, decodeWidth: 240);
        }

        /// <summary>根据主图 ScrollViewer 偏移量更新小地图视口矩形的位置和大小。</summary>
        private void UpdateMinimapRect()
        {
            if (ImgMinimap.Source == null) return;
            double mapW = CanvasMinimap.ActualWidth;
            double mapH = CanvasMinimap.ActualHeight;
            double imgW = ImgMinimap.Source.Width;
            double imgH = ImgMinimap.Source.Height;
            if (imgW <= 0 || imgH <= 0 || mapW <= 0 || mapH <= 0) return;

            // Uniform stretch dimensions inside minimap canvas
            double scale = Math.Min(mapW / imgW, mapH / imgH);
            double dispW = imgW * scale;
            double dispH = imgH * scale;
            double offsetX = (mapW - dispW) / 2;
            double offsetY = (mapH - dispH) / 2;

            double ratioX = SvZoom.ExtentWidth > 0 ? SvZoom.ViewportWidth / SvZoom.ExtentWidth : 1;
            double ratioY = SvZoom.ExtentHeight > 0 ? SvZoom.ViewportHeight / SvZoom.ExtentHeight : 1;

            RectViewport.Width = dispW * ratioX;
            RectViewport.Height = dispH * ratioY;

            double maxOffX = SvZoom.ExtentWidth - SvZoom.ViewportWidth;
            double maxOffY = SvZoom.ExtentHeight - SvZoom.ViewportHeight;
            double scrollRatioX = maxOffX > 0 ? SvZoom.HorizontalOffset / maxOffX : 0;
            double scrollRatioY = maxOffY > 0 ? SvZoom.VerticalOffset / maxOffY : 0;

            Canvas.SetLeft(RectViewport, offsetX + scrollRatioX * (dispW - RectViewport.Width));
            Canvas.SetTop(RectViewport, offsetY + scrollRatioY * (dispH - RectViewport.Height));
        }

        private void Minimap_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _minimapDragging = true;
            CanvasMinimap.CaptureMouse();
            PanMinimapToPoint(e.GetPosition(CanvasMinimap));
        }

        private void Minimap_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_minimapDragging) return;
            PanMinimapToPoint(e.GetPosition(CanvasMinimap));
        }

        private void Minimap_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _minimapDragging = false;
            CanvasMinimap.ReleaseMouseCapture();
        }

        /// <summary>将小地图上的点击/拖拽位置映射为 ScrollViewer 滚动偏移。</summary>
        private void PanMinimapToPoint(Point pt)
        {
            if (ImgMinimap.Source == null) return;
            double mapW = CanvasMinimap.ActualWidth;
            double mapH = CanvasMinimap.ActualHeight;
            double imgW = ImgMinimap.Source.Width;
            double imgH = ImgMinimap.Source.Height;
            if (imgW <= 0 || imgH <= 0 || mapW <= 0 || mapH <= 0) return;

            double scale = Math.Min(mapW / imgW, mapH / imgH);
            double dispW = imgW * scale;
            double dispH = imgH * scale;
            double offsetX = (mapW - dispW) / 2;
            double offsetY = (mapH - dispH) / 2;

            double normX = Math.Max(0, Math.Min(1, (pt.X - offsetX) / dispW));
            double normY = Math.Max(0, Math.Min(1, (pt.Y - offsetY) / dispH));

            double maxOffX = Math.Max(0, SvZoom.ExtentWidth - SvZoom.ViewportWidth);
            double maxOffY = Math.Max(0, SvZoom.ExtentHeight - SvZoom.ViewportHeight);
            SvZoom.ScrollToHorizontalOffset(normX * maxOffX);
            SvZoom.ScrollToVerticalOffset(normY * maxOffY);
        }

        private void OnSvZoomScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateMinimapRect();
        }

        private void OnMinimapLostCapture(object sender, EventArgs e)
        {
            _minimapDragging = false;
        }

        private void PopulateHistoryPanel(ImageRecord img)
        {
            if (img.IsManualReviewed)
            {
                ExpHistory.Visibility = Visibility.Visible;
                TxtHistResult.Text = img.ManualResult;
                TxtHistUser.Text = img.ManualUser;
                TxtHistTime.Text = img.ManualTime;

                bool histOk = img.ManualResult == "OK";
                TxtHistResult.Foreground = histOk
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                BorderHistResult.Background = histOk
                    ? new SolidColorBrush(Color.FromArgb(0x20, 0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Color.FromArgb(0x20, 0xF4, 0x43, 0x36));

                if (!string.IsNullOrEmpty(img.ManualComment))
                {
                    TxtHistComment.Text = img.ManualComment;
                    BorderHistComment.Visibility = Visibility.Visible;
                }
                else
                {
                    BorderHistComment.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ExpHistory.Visibility = Visibility.Collapsed;
            }
        }

        // ════════════════════════════════════════════════════════
        //  复检操作
        // ════════════════════════════════════════════════════════
        private void TglOk_Checked(object sender, RoutedEventArgs e)
        {
            TglNg.IsChecked = false;
            UpdateSubmitBtn();
        }
        private void TglOk_Unchecked(object sender, RoutedEventArgs e) => UpdateSubmitBtn();
        private void TglNg_Checked(object sender, RoutedEventArgs e)
        {
            TglOk.IsChecked = false;
            UpdateSubmitBtn();
        }
        private void TglNg_Unchecked(object sender, RoutedEventArgs e) => UpdateSubmitBtn();
        private void ReviewInput_Changed(object sender, TextChangedEventArgs e) => UpdateSubmitBtn();

        private void UpdateSubmitBtn()
        {
            bool judged = TglOk.IsChecked == true || TglNg.IsChecked == true;
            bool hasUser = !string.IsNullOrWhiteSpace(TxtUser.Text);
            BtnSubmit.IsEnabled = judged && hasUser;
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_images.Count == 0 || _zoomIdx >= _images.Count) return;
            var img = _images[_zoomIdx];

            var args = new SaveReviewArgs
            {
                ImageId = img.ImageId,
                Result = TglOk.IsChecked == true ? "OK" : "NG",
                User = TxtUser.Text.Trim(),
                Comment = TxtComment.Text.Trim()
            };
            SaveReviewRequested?.Invoke(this, args);
        }

        // ════════════════════════════════════════════════════════
        //  遮罩 & 图片导航事件
        // ════════════════════════════════════════════════════════
        private void GridOverlay_MouseDown(object sender, MouseButtonEventArgs e) => CloseOverlay();
        private void DialogCard_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;
        private void BtnCloseOverlay_Click(object sender, RoutedEventArgs e) => CloseOverlay();

        private void BtnPrevImg_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomIdx > 0) { _zoomIdx--; PopulateZoom(); }
        }
        private void BtnNextImg_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomIdx < _images.Count - 1) { _zoomIdx++; PopulateZoom(); }
        }

        // 点击图片切换 2× 放大
        private void ImgZoom_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isZoomed = !_isZoomed;
            ImgZoom.LayoutTransform = _isZoomed
                ? new ScaleTransform(2, 2)
                : Transform.Identity;
        }

        // ════════════════════════════════════════════════════════
        //  辅助方法
        // ════════════════════════════════════════════════════════
        private void SetDetailBadge(string result)
        {
            TxtDetailBadge.Text = result;
            if (result == "OK")
            {
                TxtDetailBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                BorderDetailBadge.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x4C, 0xAF, 0x50));
            }
            else
            {
                TxtDetailBadge.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                BorderDetailBadge.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xF4, 0x43, 0x36));
            }
        }

        private static Border MakeBadge(string text, Color color)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 1, 7, 1),
                Background = new SolidColorBrush(Color.FromArgb(0x25, color.R, color.G, color.B)),
                Child = new TextBlock
                {
                    Text = text,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color)
                }
            };
        }

        private static Border MakeTag(string text)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xC1, 0x07)),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07))
                }
            };
        }

        private static BitmapImage LoadBitmapSafe(string path, int decodeWidth = 0)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return new BitmapImage(); }
        }
    }
    public class EmptyStringToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果值为 null 或空字符串，返回 Collapsed；否则返回 Visible
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 通常不需要反向转换，直接抛出异常或返回默认值
            throw new NotImplementedException();
        }
    }
}
