# Image Loading Performance Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce `BuildImageCards()` 60-image render time from 16s freeze to <100ms UI + progressive background loading, via DecodePixelWidth + ThumbnailCache + placeholder pattern.

**Architecture:** Three layers — (1) `ThumbnailCache` service converts 20MB BMP to ~80KB JPEG thumbnails keyed by SHA256(srcPath+mtime), (2) `BuildImageCards` rewrite renders 60 lightweight placeholder cards instantly then loads real images via `Task.Run`, (3) `BuildPlaceholderCard` mirrors `BuildCard` structure without image loading. CancellationTokenSource cancels stale loads on product switch.

**Tech Stack:** .NET Framework 4.8, WPF, System.Security.Cryptography (SHA256), System.IO, System.Threading.Tasks

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Service/ThumbnailCache.cs` | **CREATE** | Cache directory management, SHA256 keying, BMP→JPEG thumbnail encoding, per-key concurrent-access locking |
| `View/StateCards/UC_InspectionView.xaml.cs` | **MODIFY** | New `BuildImageCards` with placeholder+background pattern, `BuildPlaceholderCard`, `ReplacePlaceholderImage`, `_loadCts` / `_thumbnailCache` fields. `BuildCard` unchanged (used by `PopulateZoom`) |
| `ZenergyBFSI.csproj` | **MODIFY** | Register `ThumbnailCache.cs` in Compile |

---

### Task 1: Create ThumbnailCache service

**Files:**
- Create: `Service/ThumbnailCache.cs`

- [ ] **Step 1: Write ThumbnailCache.cs**

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 本地缩略图缓存。将大尺寸 BMP 原图解码缩放到指定宽度，编码为 JPEG 存入
    /// %LocalAppData%/ZenergyBFSI/thumbcache/，后续请求直接返回缓存文件路径。
    /// 缓存键 = SHA256(源文件绝对路径 + 最后修改时间 Ticks)，源文件更新自动重建。
    /// </summary>
    public static class ThumbnailCache
    {
        private static readonly string CacheDir;
        private static readonly ConcurrentDictionary<string, object> _locks =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        static ThumbnailCache()
        {
            CacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZenergyBFSI", "thumbcache");
            Directory.CreateDirectory(CacheDir);
        }

        /// <summary>
        /// 获取或创建缩略图。返回缓存 JPEG 文件的完整路径。
        /// 首次调用时从原图解码降采样并编码写入缓存；后续命中直接返回路径。
        /// </summary>
        /// <param name="sourcePath">原图 BMP 绝对路径</param>
        /// <param name="decodeWidth">解码目标宽度（像素）</param>
        /// <returns>缓存文件路径，失败返回 null</returns>
        public static string GetOrCreate(string sourcePath, int decodeWidth)
        {
            if (!File.Exists(sourcePath)) return null;

            var cacheKey = ComputeCacheKey(sourcePath);
            var cacheFile = Path.Combine(CacheDir, cacheKey + ".jpg");

            if (File.Exists(cacheFile)) return cacheFile;

            // 同源文件只允许一个线程解码（避免重复 IO）
            var keyLock = _locks.GetOrAdd(cacheKey, _ => new object());
            lock (keyLock)
            {
                if (File.Exists(cacheFile)) return cacheFile; // 双重检查

                try
                {
                    return BuildThumbnail(sourcePath, decodeWidth, cacheFile);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>清除全部缓存文件。</summary>
        public static void Clear()
        {
            try
            {
                foreach (var f in Directory.GetFiles(CacheDir, "*.jpg"))
                    File.Delete(f);
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════
        //  内部
        // ════════════════════════════════════════════════════════

        private static string ComputeCacheKey(string sourcePath)
        {
            var raw = sourcePath + File.GetLastWriteTime(sourcePath).Ticks;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string BuildThumbnail(string sourcePath, int decodeWidth, string cacheFile)
        {
            // 1. 解码 BMP 到缩略尺寸
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(sourcePath, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.EndInit();
            bmp.Freeze();

            // 2. 编码为 JPEG 写入缓存
            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            var stride = w * 4; // BGRA
            var pixels = new byte[stride * h];
            bmp.CopyPixels(pixels, stride, 0);

            JpegBitmapEncoder encoder = new JpegBitmapEncoder
            {
                QualityLevel = 85
            };
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using (var fs = new FileStream(cacheFile, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }

            return cacheFile;
        }
    }
}
```

- [ ] **Step 2: Verify ThumbnailCache.cs compiles**

Build in Visual Studio or run:
```bash
msbuild ZenergyBFSI.sln /p:Configuration=Debug /p:Platform="Any CPU" /t:Build
```

- [ ] **Step 3: Commit**

```bash
git add Service/ThumbnailCache.cs
git commit -m "feat: add ThumbnailCache service for BMP to JPEG thumbnail caching"
```

---

### Task 2: Register ThumbnailCache.cs in csproj

**Files:**
- Modify: `ZenergyBFSI.csproj`

- [ ] **Step 1: Add Compile include**

In `ZenergyBFSI.csproj`, after the existing `Service/` Compile items (near line 465), add:

```xml
    <Compile Include="Service\ThumbnailCache.cs" />
```

- [ ] **Step 2: Verify build**

```bash
msbuild ZenergyBFSI.sln /p:Configuration=Debug /p:Platform="Any CPU" /t:Build
```

- [ ] **Step 3: Commit**

```bash
git add ZenergyBFSI.csproj
git commit -m "chore: register ThumbnailCache.cs in project"
```

---

### Task 3: Add fields and rewrite BuildImageCards in UC_InspectionView

**Files:**
- Modify: `View/StateCards/UC_InspectionView.xaml.cs`

- [ ] **Step 1: Add new private fields**

After line 122 (`private readonly Dictionary<string, Border> _cardMap`), add:

```csharp
// 后台加载取消令牌（切换产品时取消旧加载任务）
private CancellationTokenSource _loadCts;
// 占位卡片引用列表（用于后台加载时替换图片）
private readonly List<Border> _placeholders = new List<Border>();
```

- [ ] **Step 2: Rewrite BuildImageCards() method**

Replace the existing `BuildImageCards()` at lines 396-410:

```csharp
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
```

- [ ] **Step 3: Add BuildPlaceholderCard method**

Insert after the rewritten `BuildImageCards`:

```csharp
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
    // 存储角度名方便调试
    imgGrid.Tag = "placeholder";
    imgGrid.Children.Add(new TextBlock
    {
        Text = img.AngleName,
        Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
        FontSize = 14,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    });
    stack.Children.Add(imgGrid);

    // ── 信息区 ────────────────────────────────────────
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
        VerticalAlignment = VerticalAlignment.Center
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
        VerticalAlignment = VerticalAlignment.Center
    });
    if (!string.IsNullOrEmpty(img.NgType))
        visionRow.Children.Add(MakeTag(img.NgType));
    info.Children.Add(visionRow);

    // ── 放大按钮（悬停显示）──
    var zoomBtn = new Button
    {
        Style = (Style)FindResource("MaterialDesignIconButton"),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
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
```

- [ ] **Step 4: Add ReplacePlaceholderImage method**

Insert after `BuildPlaceholderCard`:

```csharp
/// <summary>
/// 将占位卡片的灰色背景替换为已加载的缩略图。
/// </summary>
/// <param name="index">_placeholders 中的索引</param>
/// <param name="bmp">已 Freeze 的 BitmapImage</param>
private void ReplacePlaceholderImage(int index, BitmapImage bmp)
{
    if (index < 0 || index >= _placeholders.Count) return;

    var card = _placeholders[index];
    // card.Child 是 StackPanel, StackPanel.Children[0] 是 imgGrid
    var stack = card.Child as StackPanel;
    if (stack == null || stack.Children.Count == 0) return;

    var imgGrid = stack.Children[0] as Grid;
    if (imgGrid == null) return;

    // 移除占位文字，替换为真实图片
    imgGrid.Children.Clear();
    var wpfImg = new System.Windows.Controls.Image
    {
        Stretch = Stretch.Uniform,
        Source = bmp
    };
    imgGrid.Children.Add(wpfImg);
}
```

- [ ] **Step 5: Add using for ThumbnailCache + CancellationToken**

At the top, confirm these usings exist:
- `using System.Threading;` — already present (line 12)
- `using ZenergyBFSI.Service;` — already present (line 19)
- `using System.Windows.Media.Imaging;` — already present (line 20)

No new usings needed.

- [ ] **Step 6: Verify build**

```bash
msbuild ZenergyBFSI.sln /p:Configuration=Debug /p:Platform="Any CPU" /t:Build
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add View/StateCards/UC_InspectionView.xaml.cs
git commit -m "perf: rewrite BuildImageCards with placeholder pattern and async thumbnail loading"
```

---

### Task 4: Manual verification checklist

No automated tests exist for this project. Verify manually:

- [ ] **Step 1: Cold start — first load with empty cache**

1. Delete `%LocalAppData%/ZenergyBFSI/thumbcache/` if exists
2. Launch app, search for a product with ~60 images
3. **Expected**: Cards appear immediately as gray placeholders with angle names; images progressively fill in over 3-5s; UI remains responsive during loading
4. Verify `%LocalAppData%/ZenergyBFSI/thumbcache/*.jpg` files are created

- [ ] **Step 2: Warm start — cache hit**

1. Search for the same product again
2. **Expected**: Placeholders appear immediately; images fill in within ~0.5s (all cache hits)

- [ ] **Step 3: Product switch mid-load**

1. Start a search with many images, immediately switch to a different product before all images load
2. **Expected**: Old loading tasks cancel silently; new product's placeholders appear; no stale images from previous product

- [ ] **Step 4: Zoom overlay still works**

1. Click on a loaded image card
2. **Expected**: Overlay opens with `BuildCard`-style rendering (original `BuildCard` unchanged), shows full-resolution image for inspection

- [ ] **Step 5: Edge case — missing source file**

1. Manually delete a `.bmp` file that was part of the search result
2. Search again
3. **Expected**: That card stays as placeholder (gray), other cards load normally; no crash
