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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.Painting;
using LiveChartsCore.Themes;

namespace LiveChartsCore;

/// <summary>
/// Defines a geo map chart.
/// </summary>
public class GeoMapChart
{
    private readonly HashSet<IGeoSeries> _everMeasuredSeries = [];
    private readonly ActionThrottler _updateThrottler;
    private readonly ActionThrottler _panningThrottler;
    private readonly ActionThrottler _tooltipThrottler;
    private bool _isHeatInCanvas = false;
    private Paint _heatPaint;
    private Paint? _previousStroke;
    private Paint? _previousFill;
    private LvcPoint _pointerPanningPosition = new(-10, -10);
    private LvcPoint _pointerPreviousPanningPosition = new(-10, -10);
    private LvcPoint _pointerPosition = new(-10, -10);
    private bool _isPanning = false;
    private bool _isPointerIn = false;
    private IMapFactory _mapFactory;
    private DrawnMap? _activeMap;
    private bool _isUnloaded = false;
    private bool _isToolTipOpen = false;
    private LandDefinition? _hoveredLand;
    private float _zoomLevel = 1f;
    private LvcPoint _panOffset = new(0, 0);
    private bool _isBouncing = false;
    private System.Threading.Timer? _bounceTimer;
    private bool _isPointerDown = false;
    private LvcPoint _pointerDownPosition = new(-10, -10);
    private bool _pointerDownIsClick = false;
    private double _rotationX;
    private double _rotationY;
    private System.Threading.Timer? _rotationTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoMapChart"/> class.
    /// </summary>
    /// <param name="mapView"></param>
    public GeoMapChart(IGeoMapView mapView)
    {
        View = mapView;
        _updateThrottler = mapView.DesignerMode
                ? new ActionThrottler(() => Task.CompletedTask, TimeSpan.FromMilliseconds(50))
                : new ActionThrottler(UpdateThrottlerUnlocked, TimeSpan.FromMilliseconds(100));
        _heatPaint = LiveCharts.DefaultSettings.GetProvider().GetSolidColorPaint();
        _mapFactory = LiveCharts.DefaultSettings.GetProvider().GetDefaultMapFactory();

        PointerDown += Chart_PointerDown;
        PointerMove += Chart_PointerMove;
        PointerUp += Chart_PointerUp;
        PointerLeft += Chart_PointerLeft;

        _panningThrottler = new ActionThrottler(PanningThrottlerUnlocked, TimeSpan.FromMilliseconds(30));
        _tooltipThrottler = new ActionThrottler(TooltipThrottlerUnlocked, TimeSpan.FromMilliseconds(50));
    }

    internal event Action<LvcPoint> PointerDown;
    internal event Action<LvcPoint> PointerMove;
    internal event Action<LvcPoint> PointerUp;
    internal event Action PointerLeft;

    /// <summary>
    /// Occurs when a land (country) is clicked.
    /// </summary>
    public event Action<LandClickedEventArgs>? LandClicked;

    /// <summary>
    /// Gets the chart view.
    /// </summary>
    public IGeoMapView View { get; private set; }

    /// <summary>
    /// Gets the current zoom level.
    /// </summary>
    public float ZoomLevel
    {
        get => _zoomLevel;
        internal set => _zoomLevel = value;
    }

    /// <summary>
    /// Gets the current pan offset.
    /// </summary>
    public LvcPoint PanOffset
    {
        get => _panOffset;
        internal set => _panOffset = value;
    }

    /// <summary>
    /// Gets or sets the rotation center longitude (used for Orthographic projection).
    /// </summary>
    public double RotationX
    {
        get => _rotationX;
        set
        {
            _rotationX = value;
            Update(new ChartUpdateParams { Throttling = false });
        }
    }

    /// <summary>
    /// Gets or sets the rotation center latitude (used for Orthographic projection).
    /// </summary>
    public double RotationY
    {
        get => _rotationY;
        set
        {
            _rotationY = value;
            Update(new ChartUpdateParams { Throttling = false });
        }
    }

    /// <summary>
    /// Gets the active theme, ensuring it is set up for the current dark/light mode.
    /// </summary>
    public Theme GetTheme()
    {
        var theme = LiveCharts.DefaultSettings.GetTheme();
        theme.Setup(View.IsDarkMode);
        return theme;
    }

    /// <inheritdoc cref="IMapFactory.ViewTo(GeoMapChart, object)"/>
    public virtual void ViewTo(object? command) => _mapFactory.ViewTo(this, command);

    /// <inheritdoc cref="IMapFactory.Pan(GeoMapChart, LvcPoint)"/>
    public virtual void Pan(LvcPoint delta) => _mapFactory.Pan(this, delta);

    /// <inheritdoc cref="IMapFactory.Zoom(GeoMapChart, LvcPoint, ZoomDirection)"/>
    public virtual void Zoom(LvcPoint pivot, ZoomDirection direction) =>
        _mapFactory.Zoom(this, pivot, direction);

    /// <summary>
    /// Resets the viewport to the default zoom and pan.
    /// </summary>
    public void ResetViewport()
    {
        _isBouncing = false;
        _mapFactory.SetViewport(this, 1f, new LvcPoint(0, 0));
    }

    /// <summary>
    /// Animates the globe rotation to the specified longitude and latitude.
    /// Only has a visual effect when the projection is <see cref="Geo.MapProjection.Orthographic"/>.
    /// </summary>
    /// <param name="longitude">The target center longitude.</param>
    /// <param name="latitude">The target center latitude.</param>
    /// <param name="durationMs">The animation duration in milliseconds.</param>
    public void RotateTo(double longitude, double latitude, int durationMs = 800)
    {
        _rotationTimer?.Dispose();
        _rotationTimer = null;
        AnimateRotation(longitude, latitude, durationMs);
    }

    /// <summary>
    /// Invokes a pointer wheel event.
    /// </summary>
    /// <param name="point">The pointer position.</param>
    /// <param name="direction">The zoom direction.</param>
    protected internal void InvokePointerWheel(LvcPoint point, ZoomDirection direction) =>
        Zoom(point, direction);

    /// <summary>
    /// Queues a measure request to update the chart.
    /// </summary>
    /// <param name="chartUpdateParams"></param>
    public virtual void Update(ChartUpdateParams? chartUpdateParams = null)
    {
        chartUpdateParams ??= new ChartUpdateParams();

        if (chartUpdateParams.IsAutomaticUpdate && !View.AutoUpdateEnabled) return;

        if (!chartUpdateParams.Throttling)
        {
            _updateThrottler.ForceCall();
            return;
        }

        _updateThrottler.Call();
    }

    /// <summary>
    /// Unload the map resources.
    /// </summary>
    public void Unload()
    {
        if (View.Stroke is not null) View.CoreCanvas.RemovePaintTask(View.Stroke);
        if (View.Fill is not null) View.CoreCanvas.RemovePaintTask(View.Fill);

        _bounceTimer?.Dispose();
        _bounceTimer = null;
        _isBouncing = false;

        _rotationTimer?.Dispose();
        _rotationTimer = null;

        _everMeasuredSeries.Clear();
        _heatPaint = null!;
        _previousStroke = null!;
        _previousFill = null!;
        _isUnloaded = true;
        _mapFactory.Dispose();
        _activeMap?.Dispose();

        _activeMap = null!;
        _mapFactory = null!;

        View.CoreCanvas.Dispose();
    }

    /// <summary>
    /// Invokes the pointer down event.
    /// </summary>
    /// <param name="point">The pointer position.</param>
    protected internal void InvokePointerDown(LvcPoint point) => PointerDown?.Invoke(point);

    /// <summary>
    /// Invokes the pointer move event.
    /// </summary>
    /// <param name="point">The pointer position.</param>
    protected internal void InvokePointerMove(LvcPoint point) => PointerMove?.Invoke(point);

    /// <summary>
    /// Invokes the pointer up event.
    /// </summary>
    /// <param name="point">The pointer position.</param>
    protected internal void InvokePointerUp(LvcPoint point) => PointerUp?.Invoke(point);

    /// <summary>
    /// Invokes the pointer left event.
    /// </summary>
    protected internal void InvokePointerLeft() => PointerLeft?.Invoke();

    /// <summary>
    /// Called to measure the chart.
    /// </summary>
    /// <returns>The update task.</returns>
    protected virtual Task UpdateThrottlerUnlocked()
    {
        return Task.Run(() =>
        {
            View.InvokeOnUIThread(() =>
            {
                lock (View.CoreCanvas.Sync)
                {
                    if (_isUnloaded) return;
                    Measure();
                }
            });
        });
    }

    /// <summary>
    /// Measures the chart.
    /// </summary>
    protected internal void Measure()
    {
        if (_activeMap is not null && _activeMap != View.ActiveMap)
        {
            _previousStroke?.ClearGeometriesFromPaintTask(View.CoreCanvas);
            _previousFill?.ClearGeometriesFromPaintTask(View.CoreCanvas);

            _previousFill = null;
            _previousStroke = null;

            View.CoreCanvas.Clear();
        }
        _activeMap = View.ActiveMap;

        if (!_isHeatInCanvas)
        {
            View.CoreCanvas.AddDrawableTask(_heatPaint);
            _isHeatInCanvas = true;
        }

        if (_previousStroke != View.Stroke)
        {
            if (_previousStroke is not null)
                View.CoreCanvas.RemovePaintTask(_previousStroke);

            if (View.Stroke is not null)
            {
                if (View.Stroke.ZIndex == 0) View.Stroke.ZIndex = PaintConstants.GeoMapStrokeZIndex;
                View.Stroke.PaintStyle = PaintStyle.Stroke;
                View.CoreCanvas.AddDrawableTask(View.Stroke);
            }

            _previousStroke = View.Stroke;
        }

        if (_previousFill != View.Fill)
        {
            if (_previousFill is not null)
                View.CoreCanvas.RemovePaintTask(_previousFill);

            if (View.Fill is not null)
            {
                View.Fill.PaintStyle = PaintStyle.Fill;
                View.CoreCanvas.AddDrawableTask(View.Fill);
            }

            _previousFill = View.Fill;
        }

        var i = _previousFill?.ZIndex ?? 0;
        _heatPaint.ZIndex = i + 1;

        var context = new MapContext(
            this, View, View.ActiveMap,
            Maps.BuildProjector(
                View.MapProjection,
                [View.ControlSize.Width, View.ControlSize.Height],
                _rotationX, _rotationY));

        _mapFactory.GenerateLands(context);

        var toDeleteSeries = new HashSet<IGeoSeries>(_everMeasuredSeries);
        foreach (var series in View.Series?.Cast<IGeoSeries>() ?? [])
        {
            series.Measure(context);
            _ = _everMeasuredSeries.Add(series);
            _ = toDeleteSeries.Remove(series);
        }

        foreach (var series in toDeleteSeries)
        {
            series.Delete(context);
            _ = _everMeasuredSeries.Remove(series);
        }

        // Refresh tooltip if a land is currently hovered (data may have changed)
        if (_hoveredLand is not null && View.Tooltip is not null &&
            View.TooltipPosition != TooltipPosition.Hidden)
        {
            var value = 0d;
            var hasValue = false;
            foreach (var series in View.Series?.Cast<IGeoSeries>() ?? [])
            {
                if (series.TryGetValue(_hoveredLand.ShortName, out value))
                { hasValue = true; break; }
            }

            // Compute screen-space center from geographic coordinates via projector
            var center = ComputeLandScreenCenter(_hoveredLand, context.Projector);

            View.Tooltip.Show(
                new GeoTooltipPoint
                {
                    Land = _hoveredLand,
                    Value = value,
                    HasValue = hasValue,
                    LandCenter = center
                },
                this);
        }

        View.CoreCanvas.Invalidate();
    }

    private Task PanningThrottlerUnlocked()
    {
        return Task.Run(() =>
            View.InvokeOnUIThread(() =>
            {
                lock (View.CoreCanvas.Sync)
                {
                    Pan(
                        new LvcPoint(
                            (float)(_pointerPanningPosition.X - _pointerPreviousPanningPosition.X),
                            (float)(_pointerPanningPosition.Y - _pointerPreviousPanningPosition.Y)));
                    _pointerPreviousPanningPosition = new LvcPoint(_pointerPanningPosition.X, _pointerPanningPosition.Y);
                }
            }));
    }

    /// <summary>
    /// Finds the land definition at the specified pointer position, if any.
    /// </summary>
    /// <param name="pointerPosition">The pointer position in control coordinates.</param>
    /// <returns>A tuple of the land definition, heat value, whether a value exists, and screen-space center, or null.</returns>
    public (LandDefinition Land, double Value, bool HasValue, LvcPoint Center)? FindLandAt(LvcPoint pointerPosition)
    {
        if (_activeMap is null) return null;

        foreach (var layer in _activeMap.Layers.Values)
        {
            if (!layer.IsVisible) continue;

            foreach (var landDefinition in layer.Lands.Values)
            {
                foreach (var landData in landDefinition.Data)
                {
                    if (landData.Shape is null) continue;
                    if (!landData.Shape.ContainsPoint(pointerPosition.X, pointerPosition.Y)) continue;

                    // Look up the heat value from the series
                    var value = 0d;
                    var hasValue = false;
                    foreach (var series in View.Series?.Cast<IGeoSeries>() ?? [])
                    {
                        if (series.TryGetValue(landDefinition.ShortName, out value))
                        { hasValue = true; break; }
                    }

                    // Compute screen-space center using the projector (works for all projections)
                    var projector = Maps.BuildProjector(
                        View.MapProjection,
                        [View.ControlSize.Width, View.ControlSize.Height],
                        _rotationX, _rotationY);
                    var center = ComputeLandScreenCenter(landDefinition, projector);

                    return (landDefinition, value, hasValue, center);
                }
            }
        }

        return null;
    }

    private LvcPoint ComputeLandScreenCenter(LandDefinition land, MapProjector projector)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        var hasPoints = false;

        foreach (var data in land.Data)
        {
            foreach (var coord in data.Coordinates)
            {
                if (!projector.IsVisible(coord.X, coord.Y)) continue;
                var projected = projector.ToMap([coord.X, coord.Y]);
                var px = projected[0];
                var py = projected[1];
                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px > maxX) maxX = px;
                if (py > maxY) maxY = py;
                hasPoints = true;
            }
        }

        if (!hasPoints) return _pointerPosition;

        var baseCx = (minX + maxX) / 2f;
        var baseCy = (minY + maxY) / 2f;
        var ctrlCx = View.ControlSize.Width * 0.5f;
        var ctrlCy = View.ControlSize.Height * 0.5f;
        var tx = ctrlCx * (1 - _zoomLevel) + _panOffset.X;
        var ty = ctrlCy * (1 - _zoomLevel) + _panOffset.Y;
        return new LvcPoint(baseCx * _zoomLevel + tx, baseCy * _zoomLevel + ty);
    }

    private Task TooltipThrottlerUnlocked()
    {
        return Task.Run(() =>
            View.InvokeOnUIThread(() =>
            {
                lock (View.CoreCanvas.Sync)
                {
                    if (_isUnloaded || _isPanning || !_isPointerIn) return;
                    if (View.Tooltip is null || View.TooltipPosition == TooltipPosition.Hidden) return;

                    var result = FindLandAt(_pointerPosition);

                    if (result is null)
                    {
                        if (_isToolTipOpen)
                        {
                            _hoveredLand = null;
                            _isToolTipOpen = false;
                            View.Tooltip.Hide(this);
                            View.CoreCanvas.Invalidate();
                        }
                        return;
                    }

                    var (land, value, hasValue, center) = result.Value;

                    if (land == _hoveredLand) return;
                    _hoveredLand = land;
                    _isToolTipOpen = true;

                    View.Tooltip.Show(
                        new GeoTooltipPoint
                        {
                            Land = land,
                            Value = value,
                            HasValue = hasValue,
                            LandCenter = center
                        },
                        this);
                    View.CoreCanvas.Invalidate();
                }
            }));
    }

    private void Chart_PointerDown(LvcPoint pointerPosition)
    {
        _pointerPreviousPanningPosition = pointerPosition;
        _pointerDownPosition = pointerPosition;
        _pointerDownIsClick = true;
        _isPointerDown = true;
    }

    private void Chart_PointerMove(LvcPoint pointerPosition)
    {
        _pointerPosition = pointerPosition;
        _isPointerIn = true;

        if (_isPointerDown && !_isPanning)
        {
            var dx = pointerPosition.X - _pointerDownPosition.X;
            var dy = pointerPosition.Y - _pointerDownPosition.Y;
            if (dx * dx + dy * dy > 25) // 5px drag threshold
            {
                _isPanning = true;
                _pointerDownIsClick = false;

                // Hide tooltip while panning
                if (_isToolTipOpen)
                {
                    _hoveredLand = null;
                    _isToolTipOpen = false;
                    View.Tooltip?.Hide(this);
                }
            }
        }

        if (_isPanning)
        {
            _pointerPanningPosition = pointerPosition;
            _panningThrottler.Call();
        }
        else if (!_isPointerDown)
        {
            _tooltipThrottler.Call();
        }
    }

    private void Chart_PointerLeft()
    {
        _isPointerIn = false;

        if (_isToolTipOpen)
        {
            _hoveredLand = null;
            _isToolTipOpen = false;
            View.InvokeOnUIThread(() =>
            {
                View.Tooltip?.Hide(this);
                View.CoreCanvas.Invalidate();
            });
        }
    }

    private void Chart_PointerUp(LvcPoint pointerPosition)
    {
        var wasClick = _pointerDownIsClick;
        _pointerDownIsClick = false;
        _isPointerDown = false;

        if (_isPanning)
        {
            _isPanning = false;
            _panningThrottler.Call();
            BounceBack();
        }

        if (wasClick && LandClicked is not null)
        {
            var result = FindLandAt(pointerPosition);
            if (result is not null)
            {
                LandClicked.Invoke(new LandClickedEventArgs
                {
                    Land = result.Value.Land,
                    Value = result.Value.Value,
                    Position = pointerPosition
                });
            }
        }
    }

    private void BounceBack()
    {
        if (_isBouncing) return;

        var controlW = View.ControlSize.Width;
        var controlH = View.ControlSize.Height;
        if (controlW <= 0 || controlH <= 0) return;

        var cx = controlW * 0.5f;
        var cy = controlH * 0.5f;
        var zoom = _zoomLevel;
        var minZoom = (float)View.MinZoomLevel;
        var targetZoom = zoom < minZoom ? minZoom : zoom;

        // Map occupies [0,0]..[controlW,controlH] in base coordinates.
        // After transform: screenX = baseX * zoom + tx, where tx = cx*(1-zoom) + panX
        // Map left edge on screen = tx, map right edge on screen = controlW * zoom + tx
        //
        // Constraint: the map must fully cover the viewport (no background visible).
        // When zoomed in (map bigger than viewport):
        //   left edge <= 0  AND  right edge >= controlW
        //   tx <= 0  AND  tx >= controlW - mapScreenW  (i.e. controlW*(1-zoom))
        // When at 1x (map == viewport): tx must be 0, ty must be 0 → panX=0, panY=0

        var mapScreenW = controlW * targetZoom;
        var mapScreenH = controlH * targetZoom;

        // Compute current tx/ty at the target zoom using current pan
        var targetPanX = _panOffset.X;
        var targetPanY = _panOffset.Y;

        // If zoom is bouncing, reset pan to keep centered
        if (Math.Abs(targetZoom - zoom) > 1e-6)
        {
            // Scale current pan proportionally to new zoom
            targetPanX = _panOffset.X * targetZoom / zoom;
            targetPanY = _panOffset.Y * targetZoom / zoom;
        }

        var tx = cx * (1 - targetZoom) + targetPanX;
        var ty = cy * (1 - targetZoom) + targetPanY;

        // Clamp: map left edge must be <= 0 (can't show background on left)
        if (tx > 0) tx = 0;
        // Clamp: map right edge must be >= controlW (can't show background on right)
        if (tx + mapScreenW < controlW) tx = controlW - mapScreenW;

        // Clamp vertical
        if (ty > 0) ty = 0;
        if (ty + mapScreenH < controlH) ty = controlH - mapScreenH;

        targetPanX = tx - cx * (1 - targetZoom);
        targetPanY = ty - cy * (1 - targetZoom);

        var panDiffX = Math.Abs(targetPanX - _panOffset.X);
        var panDiffY = Math.Abs(targetPanY - _panOffset.Y);
        var zoomDiff = Math.Abs(targetZoom - zoom);

        if (panDiffX < 0.5f && panDiffY < 0.5f && zoomDiff < 0.001f) return;

        _isBouncing = true;
        AnimateBounce(targetPanX, targetPanY, targetZoom);
    }

    private void AnimateBounce(float targetPanX, float targetPanY, float targetZoom)
    {
        const int steps = 8;
        const int intervalMs = 16;
        var step = 0;

        var startPanX = _panOffset.X;
        var startPanY = _panOffset.Y;
        var startZoom = _zoomLevel;

        _bounceTimer?.Dispose();
        _bounceTimer = new System.Threading.Timer(_ =>
        {
            if (_isUnloaded)
            {
                _isBouncing = false;
                _bounceTimer?.Dispose();
                _bounceTimer = null;
                return;
            }

            step++;
            var t = (float)step / steps;
            // ease-out cubic
            t = 1 - (1 - t) * (1 - t) * (1 - t);

            var newPanX = startPanX + (targetPanX - startPanX) * t;
            var newPanY = startPanY + (targetPanY - startPanY) * t;
            var newZoom = startZoom + (targetZoom - startZoom) * t;

            View.InvokeOnUIThread(() =>
            {
                if (_isUnloaded) return;
                _mapFactory.SetViewport(this, newZoom, new LvcPoint(newPanX, newPanY));
            });

            if (step >= steps)
            {
                _isBouncing = false;
                _bounceTimer?.Dispose();
                _bounceTimer = null;
            }
        }, null, intervalMs, intervalMs);
    }

    private void AnimateRotation(double targetLon, double targetLat, int durationMs)
    {
        const int intervalMs = 16;
        var totalSteps = Math.Max(1, durationMs / intervalMs);
        var step = 0;

        var startLon = _rotationX;
        var startLat = _rotationY;

        // Normalize longitude difference to shortest path [-180, 180]
        var deltaLon = targetLon - startLon;
        if (deltaLon > 180) deltaLon -= 360;
        if (deltaLon < -180) deltaLon += 360;

        _rotationTimer = new System.Threading.Timer(_ =>
        {
            if (_isUnloaded)
            {
                _rotationTimer?.Dispose();
                _rotationTimer = null;
                return;
            }

            step++;
            var t = (float)step / totalSteps;

            // ease-in-out cubic
            t = t < 0.5f
                ? 4 * t * t * t
                : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2;

            _rotationX = startLon + deltaLon * t;
            _rotationY = startLat + (targetLat - startLat) * t;

            View.InvokeOnUIThread(() =>
            {
                if (_isUnloaded) return;
                lock (View.CoreCanvas.Sync)
                {
                    Measure();
                }
            });

            if (step >= totalSteps)
            {
                _rotationX = targetLon;
                _rotationY = targetLat;
                _rotationTimer?.Dispose();
                _rotationTimer = null;
            }
        }, null, intervalMs, intervalMs);
    }
}
