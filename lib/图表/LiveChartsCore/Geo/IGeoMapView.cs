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

using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Painting;

namespace LiveChartsCore.Geo;

/// <summary>
/// Defines a geographic map.
/// </summary>
public interface IGeoMapView : IDrawnView
{
    /// <summary>
    /// Gets the core chart.
    /// </summary>
    GeoMapChart CoreChart { get; }

    /// <summary>
    /// Gets or sets the active map.
    /// </summary>
    DrawnMap ActiveMap { get; set; }

    /// <summary>
    /// Gets or sets the stroke.
    /// </summary>
    Paint? Stroke { get; set; }

    /// <summary>
    /// Gets or sets the fill.
    /// </summary>
    Paint? Fill { get; set; }

    /// <summary>
    /// Gets or sets whether the chart auto-updates are enabled.
    /// </summary>
    bool AutoUpdateEnabled { get; set; }

    /// <summary>
    /// Gets or sets the projection.
    /// </summary>
    MapProjection MapProjection { get; set; }

    /// <summary>
    /// Gets whether the control is in designer mode.
    /// </summary>
    bool DesignerMode { get; }

    /// <summary>
    /// Gets or sets the Synchronization Context, use this property to
    /// use an external object to handle multi threading synchronization.
    /// </summary>
    object SyncContext { get; set; }

    ///// <summary>
    ///// Gets or sets the view command.
    ///// </summary>
    //object? ViewCommand { get; set; }

    /// <summary>
    /// Invokes an action in the UI thread.
    /// </summary>
    /// <param name="action"></param>
    void InvokeOnUIThread(Action action);

    /// <summary>
    /// Gets or sets the series.
    /// </summary>
    IEnumerable<IGeoSeries> Series { get; set; }

    /// <summary>
    /// Gets whether the UI is in dark mode.
    /// </summary>
    bool IsDarkMode { get; }

    /// <summary>
    /// Gets or sets the tooltip.
    /// </summary>
    IGeoMapTooltip? Tooltip { get; set; }

    /// <summary>
    /// Gets or sets the tooltip position.
    /// </summary>
    TooltipPosition TooltipPosition { get; set; }

    /// <summary>
    /// Gets or sets the tooltip text paint.
    /// </summary>
    Paint? TooltipTextPaint { get; set; }

    /// <summary>
    /// Gets or sets the tooltip background paint.
    /// </summary>
    Paint? TooltipBackgroundPaint { get; set; }

    /// <summary>
    /// Gets or sets the tooltip text size.
    /// </summary>
    double TooltipTextSize { get; set; }

    /// <summary>
    /// Gets or sets the zooming speed, a value between 0.1 and 0.95.
    /// </summary>
    double ZoomingSpeed { get; set; }

    /// <summary>
    /// Gets or sets the minimum zoom level. Defaults to 1.
    /// </summary>
    double MinZoomLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum zoom level. Defaults to 100.
    /// </summary>
    double MaxZoomLevel { get; set; }
}
