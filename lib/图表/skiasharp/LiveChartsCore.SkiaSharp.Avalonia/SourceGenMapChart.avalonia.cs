// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Rendering;
using Avalonia.Styling;
using Avalonia.Threading;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Avalonia;

namespace LiveChartsGeneratedCode;

// ==============================================================
// this file contains the shared code between all UI frameworks
// ==============================================================

/// <inheritdoc cref="IGeoMapView" />
public partial class SourceGenMapChart : UserControl, IGeoMapView, ICustomHitTest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenMapChart"/> class.
    /// </summary>
    public SourceGenMapChart()
    {
        Content = new MotionCanvas();
        InitializeChartControl();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerExited += OnPointerExited;
        PointerWheelChanged += OnPointerWheelChanged;

        SizeChanged += (s, e) =>
            CoreChart.Update();

        DetachedFromVisualTree += GeoMap_DetachedFromVisualTree;
    }

    private MotionCanvas MotionCanvas => (MotionCanvas)Content!;

    /// <inheritdoc cref="IDrawnView.CoreCanvas"/>
    public CoreMotionCanvas CoreCanvas => MotionCanvas.CanvasCore;

    bool IGeoMapView.DesignerMode => Design.IsDesignMode;
    bool IGeoMapView.IsDarkMode => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    LvcSize IDrawnView.ControlSize => new() { Width = (float)MotionCanvas.Bounds.Width, Height = (float)MotionCanvas.Bounds.Height };

    private void GeoMap_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (CoreChart is null) return;
        CoreChart.Unload();
    }

    void IGeoMapView.InvokeOnUIThread(Action action) =>
        Dispatcher.UIThread.Post(action);

    bool ICustomHitTest.HitTest(Point point) =>
        new Rect(Bounds.Size).Contains(point);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(this);
        CoreChart?.InvokePointerDown(new LvcPoint((float)p.X, (float)p.Y));
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        CoreChart?.InvokePointerMove(new LvcPoint((float)p.X, (float)p.Y));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var p = e.GetPosition(this);
        CoreChart?.InvokePointerUp(new LvcPoint((float)p.X, (float)p.Y));
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        CoreChart?.InvokePointerLeft();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        var p = e.GetPosition(this);
        CoreChart?.InvokePointerWheel(
            new LvcPoint((float)p.X, (float)p.Y),
            e.Delta.Y > 0 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut);
    }
}
