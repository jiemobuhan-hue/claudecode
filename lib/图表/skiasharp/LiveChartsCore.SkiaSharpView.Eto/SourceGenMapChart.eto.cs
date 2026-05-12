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
using Eto.Drawing;
using Eto.Forms;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Eto;

namespace LiveChartsGeneratedCode;

// ===============================================
// this file contains the Eto specific code
// ===============================================

/// <inheritdoc cref="IChartView" />
public abstract partial class SourceGenMapChart : Panel, IGeoMapView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenMapChart"/> class.
    /// </summary>
    protected SourceGenMapChart()
    {
        var motionCanvas = new MotionCanvas();

        Content = motionCanvas;
        BackgroundColor = Colors.White;

        InitializeChartControl();

        Content.SizeChanged += (s, e) =>
            CoreChart.Update();

        Content.MouseWheel += OnMouseWheel;
        Content.MouseDown += OnMouseDown;
        Content.MouseMove += OnMouseMove;
        Content.MouseUp += OnMouseUp;
        Content.MouseLeave += OnMouseLeave;
    }

    /// <inheritdoc cref="IDrawnView.CoreCanvas"/>
    public CoreMotionCanvas CoreCanvas => ((MotionCanvas)Content).CanvasCore;

    bool IGeoMapView.DesignerMode => false;
    bool IGeoMapView.IsDarkMode => false;
    LvcSize IDrawnView.ControlSize => new() { Width = Content.Width, Height = Content.Height };

    void IGeoMapView.InvokeOnUIThread(Action action) =>
        _ = Application.Instance.InvokeAsync(action);

    /// <inheritdoc cref="Control.OnUnLoad(EventArgs)"/>
    protected override void OnUnLoad(EventArgs e)
    {
        base.OnUnLoad(e);
        CoreChart.Unload();
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        var p = e.Location;
        CoreChart?.InvokePointerWheel(
            new LvcPoint(p.X, p.Y),
            e.Delta.Height > 0 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut);
        e.Handled = true;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        var p = e.Location;
        CoreChart?.InvokePointerDown(new LvcPoint(p.X, p.Y));
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        var p = e.Location;
        CoreChart?.InvokePointerMove(new LvcPoint(p.X, p.Y));
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        var p = e.Location;
        CoreChart?.InvokePointerUp(new LvcPoint(p.X, p.Y));
    }

    private void OnMouseLeave(object? sender, MouseEventArgs e)
    {
        CoreChart?.InvokePointerLeft();
    }
}
