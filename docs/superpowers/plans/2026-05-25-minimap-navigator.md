# Minimap Navigator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a draggable minimap navigator to the zoom overlay right panel, showing full-image thumbnail with viewport rectangle for real-time panning.

**Architecture:** A 240×180px Canvas in the right control panel loads a tiny thumbnail (DecodePixelWidth=240) of the current image. A blue semi-transparent Rectangle shows the visible viewport. Dragging on the minimap calls `SvZoom.ScrollToHorizontalOffset/VerticalOffset` in real time. `SvZoom.ScrollChanged` → `UpdateMinimapRect()` keeps the rectangle synced when the user scrolls/zooms the main image directly.

**Tech Stack:** WPF .NET Framework 4.8, Canvas absolute positioning, ScrollViewer.ScrollChanged event

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `View/StateCards/UC_InspectionView.xaml` | MODIFY | Add minimap Canvas+Image+Rectangle between title header and 视觉系统判断 Expander in right panel |
| `View/StateCards/UC_InspectionView.xaml.cs` | MODIFY | Add fields, BuildMinimap, UpdateMinimapRect, mouse drag handlers, ScrollChanged wiring |

---

### Task 1: Add minimap XAML to right panel

**Files:**
- Modify: `View/StateCards/UC_InspectionView.xaml`

- [ ] **Step 1: Insert minimap Canvas after the title header Border**

In `UC_InspectionView.xaml`, find the title header Border (lines 433-446) and the 视觉系统判断 Expander (line 449). Insert between them:

```xml
                                <!-- 小地图导航器 -->
                                <Border CornerRadius="6" Height="180" Margin="16,8,16,0"
                                        Background="#FF1E1E1E" BorderBrush="#33FFFFFF" BorderThickness="1">
                                    <Canvas x:Name="CanvasMinimap" ClipToBounds="True"
                                            MouseDown="Minimap_MouseDown"
                                            MouseMove="Minimap_MouseMove"
                                            MouseUp="Minimap_MouseUp"
                                            Cursor="Hand">
                                        <Image x:Name="ImgMinimap" Stretch="Uniform"
                                               HorizontalAlignment="Center" VerticalAlignment="Center"
                                               Width="{Binding ActualWidth, ElementName=CanvasMinimap}"
                                               Height="{Binding ActualHeight, ElementName=CanvasMinimap}"/>
                                        <Rectangle x:Name="RectViewport"
                                                   Fill="#332196F3" Stroke="#992196F3"
                                                   StrokeThickness="1.5" RadiusX="2" RadiusY="2"
                                                   IsHitTestVisible="False"/>
                                    </Canvas>
                                </Border>
```

The exact insertion point is after the closing `</Border>` of the title header (line 446) and before `<!-- 视觉系统信息 -->` (line 448).

- [ ] **Step 2: Commit**

```bash
git add View/StateCards/UC_InspectionView.xaml
git commit -m "feat: add minimap Canvas XAML to zoom overlay right panel"
```

---

### Task 2: Add minimap code-behind logic

**Files:**
- Modify: `View/StateCards/UC_InspectionView.xaml.cs`

- [ ] **Step 1: Add minimap fields**

After `private bool _isZoomed = false;` (near line 127), add:

```csharp
// 小地图拖拽状态
private bool _minimapDragging = false;
```

No need for `_minimapDragStart` — `PanMinimapToPoint` computes position directly from mouse coordinates.

- [ ] **Step 2: Add BuildMinimap and UpdateMinimapRect methods**

Insert after `BuildThumbs()` method (after its closing brace around line 745):

```csharp
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
```

- [ ] **Step 3: Add minimap drag event handlers**

Insert after `UpdateMinimapRect`:

```csharp
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
```

- [ ] **Step 4: Add ScrollChanged handler and wire up in PopulateZoom**

After the drag handlers, add:

```csharp
private void OnSvZoomScrollChanged(object sender, ScrollChangedEventArgs e)
{
    UpdateMinimapRect();
}
```

In `PopulateZoom()`, find the line `BuildThumbs();` (around line 854). Add the minimap setup immediately after:

```csharp
// 小地图
BuildMinimap();
SvZoom.ScrollChanged -= OnSvZoomScrollChanged;
SvZoom.ScrollChanged += OnSvZoomScrollChanged;
```

At the END of `PopulateZoom()`, after all layout operations (after the last `UpdateSubmitBtn();` call), add:

```csharp
// 小地图视口矩形延迟更新（等待 Canvas 完成 Measure/Arrange）
Dispatcher.BeginInvoke(new Action(() => UpdateMinimapRect()),
    System.Windows.Threading.DispatcherPriority.Loaded);
```

- [ ] **Step 5: Commit**

```bash
git add View/StateCards/UC_InspectionView.xaml.cs
git commit -m "feat: add minimap navigator with draggable viewport rect"
```

---

### Task 3: Manual verification

No tests exist for this project. Verify manually:

- [ ] **Step 1: Minimap loads on zoom open**

1. Search for a product with images
2. Click a card to open zoom overlay
3. **Expected**: Right panel shows minimap at top with full-image thumbnail and blue viewport rectangle

- [ ] **Step 2: Viewport rectangle follows scroll**

1. In zoom overlay, scroll the main image (scrollbar or mouse wheel)
2. **Expected**: Blue rectangle in minimap moves to reflect new viewport position

- [ ] **Step 3: Drag minimap to pan**

1. Click and drag on minimap
2. **Expected**: Main image scrolls in real time following the drag; blue rectangle follows

- [ ] **Step 4: Click minimap to jump**

1. Click a point on minimap (without dragging)
2. **Expected**: Main image jumps to center on that area

- [ ] **Step 5: 2× zoom sync**

1. Click main image to toggle 2× zoom
2. **Expected**: Blue rectangle resizes proportionally (gets smaller in minimap, since more of the image is visible)

- [ ] **Step 6: Prev/Next image**

1. Click prev/next buttons
2. **Expected**: Minimap updates to show the new image; viewport rectangle resets

- [ ] **Step 7: Drag at boundary**

1. Drag minimap to extreme edge
2. **Expected**: Main image scrolls to boundary smoothly; no crash, no NaN
