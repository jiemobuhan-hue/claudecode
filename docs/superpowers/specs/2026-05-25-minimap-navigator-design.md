# Minimap Navigator for Image Zoom Overlay

**Date**: 2026-05-25
**Status**: in-progress
**Branch**: master

## Problem

The `ImgZoom` overlay currently supports panning via ScrollViewer scrollbars and
click-to-2× zoom, but with ultra-tall images (1149×7281 px, ~6:1 aspect ratio),
users cannot see which portion of the image they're viewing. Navigation is slow
because they scroll blindly.

## Solution

Add a minimap thumbnail navigator to the right control panel (below the `SpThumbs`
thumbnail strip). It shows the full image at tiny decode size (DecodePixelWidth=240)
with a blue semi-transparent viewport rectangle that can be dragged to pan the
main image in real time.

## Components

### 1. XAML changes (`UC_InspectionView.xaml`)

After the existing `SpThumbs` StackPanel (thumbnail strip), add:

```xml
<!-- 小地图导航器 -->
<Border CornerRadius="6" Height="180" Margin="0,8,0,0"
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

### 2. CS changes (`UC_InspectionView.xaml.cs`)

**New fields** (after `_isZoomed`):
```csharp
private bool _minimapDragging = false;
private Point _minimapDragStart;
```

**BuildMinimap()** — called from `PopulateZoom()` after existing image load:
```csharp
private void BuildMinimap()
{
    var img = _images[_zoomIdx];
    if (!File.Exists(img.ImagePath)) return;
    ImgMinimap.Source = LoadBitmapSafe(img.ImagePath, decodeWidth: 240);
}
```

**UpdateMinimapRect()** — called from `ScrollViewer.ScrollChanged` event handler:
```csharp
private void UpdateMinimapRect()
{
    if (ImgMinimap.Source == null) return;
    double mapW = CanvasMinimap.ActualWidth;
    double mapH = CanvasMinimap.ActualHeight;
    double imgW = ImgMinimap.Source.Width;
    double imgH = ImgMinimap.Source.Height;
    if (imgW <= 0 || imgH <= 0 || mapW <= 0 || mapH <= 0) return;

    // Uniform stretch factor inside minimap
    double scale = Math.Min(mapW / imgW, mapH / imgH);
    double dispW = imgW * scale;
    double dispH = imgH * scale;
    double offsetX = (mapW - dispW) / 2;
    double offsetY = (mapH - dispH) / 2;

    double ratioX = SvZoom.ExtentWidth > 0 ? SvZoom.ViewportWidth / SvZoom.ExtentWidth : 1;
    double ratioY = SvZoom.ExtentHeight > 0 ? SvZoom.ViewportHeight / SvZoom.ExtentHeight : 1;

    RectViewport.Width = dispW * ratioX;
    RectViewport.Height = dispH * ratioY;
    double scrollRatioX = SvZoom.ExtentWidth > 0 ? SvZoom.HorizontalOffset / (SvZoom.ExtentWidth - SvZoom.ViewportWidth) : 0;
    double scrollRatioY = SvZoom.ExtentHeight > 0 ? SvZoom.VerticalOffset / (SvZoom.ExtentHeight - SvZoom.ViewportHeight) : 0;

    Canvas.SetLeft(RectViewport, offsetX + scrollRatioX * (dispW - RectViewport.Width));
    Canvas.SetTop(RectViewport, offsetY + scrollRatioY * (dispH - RectViewport.Height));
}
```

**Minimap drag handlers:**
```csharp
private void Minimap_MouseDown(object sender, MouseButtonEventArgs e)
{
    _minimapDragging = true;
    _minimapDragStart = e.GetPosition(CanvasMinimap);
    CanvasMinimap.CaptureMouse();
    PanMinimapToPoint(_minimapDragStart);
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

private void PanMinimapToPoint(Point pt)
{
    if (ImgMinimap.Source == null) return;
    double mapW = CanvasMinimap.ActualWidth;
    double mapH = CanvasMinimap.ActualHeight;
    double imgW = ImgMinimap.Source.Width;
    double imgH = ImgMinimap.Source.Height;
    if (imgW <= 0 || imgH <= 0 || mapW <= 0 || mapH <= 0) return;

    // Uniform stretch dimensions
    double scale = Math.Min(mapW / imgW, mapH / imgH);
    double dispW = imgW * scale;
    double dispH = imgH * scale;
    double offsetX = (mapW - dispW) / 2;
    double offsetY = (mapH - dispH) / 2;

    // Map point to normalized position within displayed image
    double normX = Math.Max(0, Math.Min(1, (pt.X - offsetX) / dispW));
    double normY = Math.Max(0, Math.Min(1, (pt.Y - offsetY) / dispH));

    // Convert to ScrollViewer offset
    double maxOffX = Math.Max(0, SvZoom.ExtentWidth - SvZoom.ViewportWidth);
    double maxOffY = Math.Max(0, SvZoom.ExtentHeight - SvZoom.ViewportHeight);
    SvZoom.ScrollToHorizontalOffset(normX * maxOffX);
    SvZoom.ScrollToVerticalOffset(normY * maxOffY);
}
```

**ScrollChanged binding** — in `PopulateZoom()`, after `BuildMinimap()` call:

```csharp
// Bind ScrollViewer scroll events to minimap viewport tracking
SvZoom.ScrollChanged -= OnSvZoomScrollChanged;  // avoid double-subscribe
SvZoom.ScrollChanged += OnSvZoomScrollChanged;
```

Then:

```csharp
private void OnSvZoomScrollChanged(object sender, ScrollChangedEventArgs e)
{
    UpdateMinimapRect();
}
```

Also call `UpdateMinimapRect()` at the end of `PopulateZoom()` after the LayoutTransform reset so the viewport rect is correct from the start.

**In `PopulateZoom()`, add after `BuildMinimap()` call:**

```csharp
// 小地图
BuildMinimap();
SvZoom.ScrollChanged -= OnSvZoomScrollChanged;
SvZoom.ScrollChanged += OnSvZoomScrollChanged;

// ... rest of PopulateZoom logic ...

// At the end of PopulateZoom, after all layout is done:
Dispatcher.BeginInvoke(new Action(() => UpdateMinimapRect()),
    System.Windows.Threading.DispatcherPriority.Loaded);
```

The `Dispatcher.BeginInvoke` with `Loaded` priority ensures the Canvas has been measured/arranged before we read `ActualWidth`/`ActualHeight`.

## Data Flow

```
PopulateZoom()
  → BuildMinimap()          // load tiny thumbnail
  → UpdateMinimapRect()     // initial viewport rect

User scrolls main image
  → SvZoom.ScrollChanged    // WPF event
  → UpdateMinimapRect()     // reposition viewport rect

User drags on minimap
  → Minimap_MouseDown/Move
  → PanMinimapToPoint()
  → SvZoom.ScrollTo...      // pan main image
  → SvZoom.ScrollChanged fires
  → UpdateMinimapRect()     // sync rect back (no-op, already correct)

User clicks 2× zoom on main image
  → ImgZoom.LayoutTransform changes
  → SvZoom.ExtentWidth/Height change
  → SvZoom.ScrollChanged fires
  → UpdateMinimapRect()     // resize viewport rect proportionally
```

## Files Changed

| File | Change |
|------|--------|
| `View/StateCards/UC_InspectionView.xaml` | Add minimap Border/Grid/Image/Rectangle after SpThumbs |
| `View/StateCards/UC_InspectionView.xaml.cs` | Add fields, BuildMinimap, UpdateMinimapRect, drag handlers, ScrollChanged wiring |

## Edge Cases

- **Image switches** (prev/next): `PopulateZoom` calls `BuildMinimap` fresh, no stale state
- **Empty image set**: `BuildMinimap` returns early if no file exists
- **Image not yet loaded** (cache miss): `ImgMinimap.Source == null`, `UpdateMinimapRect` returns early
- **Scroll at boundary**: `ratioX/Y` clamped implicitly by ScrollViewer bounds
- **Minimap click at edge**: `PanMinimapToPoint` clamps to valid scroll range
