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
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace LiveChartsGeneratedCode;

// ==============================================================================
// 
// this file contains the SkiaSharp (image generation) specific code for the SourceGenSKMapChart class,
// the rest of the code can be found in the _Shared project.
// 
// ==============================================================================

/// <inheritdoc cref="IGeoMapView" />
public partial class SourceGenSKMapChart : InMemorySkiaSharpChart, IGeoMapView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenSKMapChart"/> class.
    /// </summary>
    public SourceGenSKMapChart(IGeoMapView? mapView = null)
        : base(mapView)
    {
        AutoUpdateEnabled = false;

        InitializeChartControl();
    }

    /// <inheritdoc cref="IGeoMapView.DesignerMode" />
    bool IGeoMapView.DesignerMode => false;

    /// <inheritdoc cref="IGeoMapView.IsDarkMode" />
    bool IGeoMapView.IsDarkMode => false;

    /// <inheritdoc cref="IDrawnView.CoreCanvas"/>
    LvcSize IDrawnView.ControlSize => new() { Width = Width, Height = Height };

    /// <inheritdoc cref="InMemorySkiaSharpChart.DrawOnCanvas(SKCanvas)" />
    public override void DrawOnCanvas(SKCanvas canvas)
    {
        CoreCanvas.DisableAnimations = true;
        CoreChart.Measure();
        CoreCanvas.DrawFrame(new SkiaSharpDrawingContext(CoreCanvas, canvas, Background));
        CoreChart.Unload();
    }

    void IGeoMapView.InvokeOnUIThread(Action action) =>
        action();

    /// <inheritdoc cref="InMemorySkiaSharpChart.GetCoreChart"/>
    protected override Chart GetCoreChart() => throw new NotImplementedException();
}
