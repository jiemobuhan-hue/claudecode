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
using LiveChartsCore.Drawing;
using LiveChartsCore.Drawing.Segments;
using LiveChartsCore.Geo;
using LiveChartsCore.Measure;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using SkiaSharp;

namespace LiveChartsCore.SkiaSharpView;

/// <summary>
/// Defines a map builder.
/// </summary>
public class MapFactory : IMapFactory
{
    private readonly HashSet<LandAreaGeometry> _usedPathShapes = [];
    private readonly HashSet<Paint> _usedPaints = [];
    private readonly HashSet<string> _usedLayers = [];
    private readonly MapViewportTransform _viewportTransform = new();
    private IGeoMapView? _mapView;
    private LandAreaGeometry? _globeCircle;

    /// <inheritdoc cref="IMapFactory.GenerateLands(MapContext)"/>
    public void GenerateLands(MapContext context)
    {
        var projector = context.Projector;

        // Sync viewport transform with current chart state
        _viewportTransform.Zoom = context.CoreMap.ZoomLevel;
        _viewportTransform.PanX = context.CoreMap.PanOffset.X;
        _viewportTransform.PanY = context.CoreMap.PanOffset.Y;
        _viewportTransform.CenterX = context.View.ControlSize.Width * 0.5f;
        _viewportTransform.CenterY = context.View.ControlSize.Height * 0.5f;


        var isOrtho = projector is OrthographicProjector;
        var ortho = projector as OrthographicProjector;

        // Manage globe circle background for orthographic projection
        if (isOrtho && ortho is not null)
        {
            if (_globeCircle is null)
            {
                _globeCircle = new LandAreaGeometry();
            }

            var circlePath = new SKPath();
            circlePath.AddCircle(ortho.ScreenCenterX, ortho.ScreenCenterY, ortho.Radius);
            _globeCircle.SetBasePath(circlePath);
            _globeCircle.ViewportTransform = _viewportTransform;

            if (context.View.Fill is not null)
                context.View.Fill.AddGeometryToPaintTask(context.View.CoreCanvas, _globeCircle);
        }
        else if (_globeCircle is not null)
        {
            context.View.Fill?.RemoveGeometryFromPaintTask(context.View.CoreCanvas, _globeCircle);
            context.View.Stroke?.RemoveGeometryFromPaintTask(context.View.CoreCanvas, _globeCircle);
            _globeCircle = null;
        }

        var toRemoveLayers = new HashSet<string>(_usedLayers);
        var toRemovePathShapes = new HashSet<LandAreaGeometry>(_usedPathShapes);
        var toRemovePaints = new HashSet<Paint>(_usedPaints);

        var layersQuery = context.View.ActiveMap.Layers.Values
            .Where(x => x.IsVisible)
            .OrderByDescending(x => x.ProcessIndex);

        _mapView = context.View;

        foreach (var layer in layersQuery)
        {
            var stroke = context.View.Stroke ?? layer.Stroke;
            var fill = context.View.Fill ?? layer.Fill;

            if (fill is not null)
            {
                context.View.CoreCanvas.AddDrawableTask(fill);
                _ = _usedPaints.Add(fill);
                _ = toRemovePaints.Remove(fill);
            }
            if (stroke is not null)
            {
                context.View.CoreCanvas.AddDrawableTask(stroke);
                _ = _usedPaints.Add(stroke);
                _ = toRemovePaints.Remove(stroke);
            }

            _ = _usedLayers.Add(layer.Name);
            _ = toRemoveLayers.Remove(layer.Name);

            foreach (var landDefinition in layer.Lands.Values)
            {
                foreach (var landData in landDefinition.Data)
                {
                    landData.Land = landDefinition;

                    LandAreaGeometry shape;

                    if (landData.Shape is null)
                    {
                        landData.Shape = shape = new LandAreaGeometry();
                    }
                    else
                    {
                        shape = (LandAreaGeometry)landData.Shape;
                    }

                    _ = _usedPathShapes.Add(shape);
                    _ = toRemovePathShapes.Remove(shape);

                    shape.ViewportTransform = _viewportTransform;

                    if (isOrtho && ortho is not null)
                    {
                        var skPath = BuildOrthographicPath(ortho, landData.Coordinates);
                        if (skPath is null)
                        {
                            // Entire polygon is on the far side — hide it
                            stroke?.RemoveGeometryFromPaintTask(context.View.CoreCanvas, shape);
                            fill?.RemoveGeometryFromPaintTask(context.View.CoreCanvas, shape);
                            shape.Commands.Clear();
                            shape.SetBasePath(new SKPath());
                            continue;
                        }

                        stroke?.AddGeometryToPaintTask(context.View.CoreCanvas, shape);
                        fill?.AddGeometryToPaintTask(context.View.CoreCanvas, shape);
                        shape.Commands.Clear();
                        shape.SetBasePath(skPath);
                    }
                    else
                    {
                        stroke?.AddGeometryToPaintTask(context.View.CoreCanvas, shape);
                        fill?.AddGeometryToPaintTask(context.View.CoreCanvas, shape);

                        shape.Commands.Clear();

                        var isFirst = true;
                        float xp = 0, yp = 0;

                        foreach (var point in landData.Coordinates)
                        {
                            var p = projector.ToMap([point.X, point.Y]);

                            var x = p[0];
                            var y = p[1];

                            if (isFirst)
                            {
                                xp = x;
                                yp = y;
                            }

                            _ = shape.Commands.AddLast(new Segment
                            {
                                Xi = xp,
                                Yi = yp,
                                Xj = x,
                                Yj = y,
                            });
                        }

                        shape.MarkPathDirty();
                    }
                }
            }

            foreach (var shape in toRemovePathShapes)
            {
                stroke?.RemoveGeometryFromPaintTask(context.View.CoreCanvas, shape);
                fill?.RemoveGeometryFromPaintTask(context.View.CoreCanvas, shape);

                shape.Commands.Clear();

                _ = _usedPathShapes.Remove(shape);
            }
        }

        foreach (var paint in toRemovePaints)
        {
            _ = _usedPaints.Remove(paint);
            context.View.CoreCanvas.RemovePaintTask(paint);
        }

        foreach (var layerName in toRemoveLayers)
        {
            _ = context.MapFile.Layers.Remove(layerName);
            _ = _usedLayers.Remove(layerName);
        }
    }

    /// <inheritdoc cref="IMapFactory.ViewTo(GeoMapChart, object)"/>
    public void ViewTo(GeoMapChart sender, object? command) { }

    /// <inheritdoc cref="IMapFactory.Pan(GeoMapChart, LvcPoint)"/>
    public void Pan(GeoMapChart sender, LvcPoint delta)
    {
        _viewportTransform.PanX += delta.X;
        _viewportTransform.PanY += delta.Y;


        sender.PanOffset = new LvcPoint(_viewportTransform.PanX, _viewportTransform.PanY);
        sender.View.CoreCanvas.Invalidate();
    }

    /// <inheritdoc cref="IMapFactory.Zoom(GeoMapChart, LvcPoint, ZoomDirection)"/>
    public void Zoom(GeoMapChart sender, LvcPoint pivot, ZoomDirection direction)
    {
        _viewportTransform.CenterX = sender.View.ControlSize.Width * 0.5f;
        _viewportTransform.CenterY = sender.View.ControlSize.Height * 0.5f;

        var speed = sender.View.ZoomingSpeed;
        speed = speed < 0.1 ? 0.1 : (speed > 0.95 ? 0.95 : speed);
        speed = 1 - speed;

        var oldZoom = _viewportTransform.Zoom;
        var newZoom = direction == ZoomDirection.ZoomIn
            ? (float)(oldZoom / speed)
            : (float)(oldZoom * speed);

        var minZoom = (float)sender.View.MinZoomLevel;
        var maxZoom = (float)sender.View.MaxZoomLevel;

        // Allow slight overshoot past min for bounce-back feel
        if (newZoom < minZoom * 0.8f) newZoom = minZoom * 0.8f;
        if (newZoom > maxZoom) newZoom = maxZoom;

        // Adjust pan so the pivot point stays in place.
        // screen = base * zoom + (center*(1-zoom) + pan)
        // Solve: pivotScreen stays same => newPan = oldPan + (pivot - oldPan - center) * (1 - newZoom/oldZoom)
        // Simpler: pan_new = pivot - (pivot - tx_old) * newZoom / oldZoom  where tx = center*(1-zoom)+pan
        var cx = _viewportTransform.CenterX;
        var cy = _viewportTransform.CenterY;
        var txOld = cx * (1 - oldZoom) + _viewportTransform.PanX;
        var tyOld = cy * (1 - oldZoom) + _viewportTransform.PanY;

        var txNew = pivot.X - (pivot.X - txOld) * newZoom / oldZoom;
        var tyNew = pivot.Y - (pivot.Y - tyOld) * newZoom / oldZoom;

        var newPanX = txNew - cx * (1 - newZoom);
        var newPanY = tyNew - cy * (1 - newZoom);

        _viewportTransform.Zoom = newZoom;
        _viewportTransform.PanX = newPanX;
        _viewportTransform.PanY = newPanY;


        sender.ZoomLevel = newZoom;
        sender.PanOffset = new LvcPoint(newPanX, newPanY);
        sender.View.CoreCanvas.Invalidate();
    }

    /// <inheritdoc cref="IMapFactory.SetViewport(GeoMapChart, float, LvcPoint)"/>
    public void SetViewport(GeoMapChart sender, float zoom, LvcPoint panOffset)
    {
        _viewportTransform.Zoom = zoom;
        _viewportTransform.PanX = panOffset.X;
        _viewportTransform.PanY = panOffset.Y;
        _viewportTransform.CenterX = sender.View.ControlSize.Width * 0.5f;
        _viewportTransform.CenterY = sender.View.ControlSize.Height * 0.5f;

        sender.ZoomLevel = zoom;
        sender.PanOffset = panOffset;
        sender.View.CoreCanvas.Invalidate();
    }

    private static SKPath? BuildOrthographicPath(OrthographicProjector ortho, LvcPointD[] coordinates)
    {
        if (coordinates.Length == 0) return null;

        // Check if any point is visible
        var anyVisible = false;
        for (var i = 0; i < coordinates.Length; i++)
        {
            if (ortho.IsVisible(coordinates[i].X, coordinates[i].Y))
            {
                anyVisible = true;
                break;
            }
        }

        if (!anyVisible) return null;

        var path = new SKPath();
        var started = false;

        for (var i = 0; i < coordinates.Length; i++)
        {
            var cur = coordinates[i];
            var next = coordinates[(i + 1) % coordinates.Length];

            var curVis = ortho.IsVisible(cur.X, cur.Y);
            var nextVis = ortho.IsVisible(next.X, next.Y);

            if (curVis)
            {
                var p = ortho.ToMap([cur.X, cur.Y]);
                if (!started)
                {
                    path.MoveTo(p[0], p[1]);
                    started = true;
                }
                else
                {
                    path.LineTo(p[0], p[1]);
                }

                if (!nextVis && i < coordinates.Length - 1)
                {
                    // Transition visible → invisible: find horizon point
                    var hp = FindHorizonPoint(ortho, cur.X, cur.Y, next.X, next.Y);
                    var hpp = ortho.ToMap([hp[0], hp[1]]);
                    path.LineTo(hpp[0], hpp[1]);
                }
            }
            else if (nextVis)
            {
                // Transition invisible → visible: find horizon point and start from there
                var hp = FindHorizonPoint(ortho, next.X, next.Y, cur.X, cur.Y);
                var hpp = ortho.ToMap([hp[0], hp[1]]);
                if (!started)
                {
                    path.MoveTo(hpp[0], hpp[1]);
                    started = true;
                }
                else
                {
                    path.LineTo(hpp[0], hpp[1]);
                }
            }
        }

        path.Close();
        return path;
    }

    private static double[] FindHorizonPoint(
        OrthographicProjector ortho,
        double visLon, double visLat,
        double invisLon, double invisLat)
    {
        // Binary search to find the point on the horizon between visible and invisible
        var aLon = visLon;
        var aLat = visLat;
        var bLon = invisLon;
        var bLat = invisLat;

        for (var i = 0; i < 15; i++)
        {
            var midLon = (aLon + bLon) * 0.5;
            var midLat = (aLat + bLat) * 0.5;

            if (ortho.IsVisible(midLon, midLat))
            {
                aLon = midLon;
                aLat = midLat;
            }
            else
            {
                bLon = midLon;
                bLat = midLat;
            }
        }

        return [(aLon + bLon) * 0.5, (aLat + bLat) * 0.5];
    }

    /// <summary>
    /// Disposes the map factory.
    /// </summary>
    public void Dispose()
    {
        if (_mapView is not null)
        {
            var layersQuery = _mapView.ActiveMap.Layers.Values
               .Where(x => x.IsVisible)
               .OrderByDescending(x => x.ProcessIndex);

            foreach (var layer in layersQuery)
            {
                var stroke = _mapView.Stroke ?? layer.Stroke;
                var fill = _mapView.Fill ?? layer.Fill;

                foreach (var landDefinition in layer.Lands.Values)
                {
                    foreach (var landData in landDefinition.Data)
                    {
                        var shape = landData.Shape;
                        if (shape is null) continue;

                        stroke?.RemoveGeometryFromPaintTask(_mapView.CoreCanvas, shape);
                        fill?.RemoveGeometryFromPaintTask(_mapView.CoreCanvas, shape);

                        landData.Shape = null;
                    }
                }
                foreach (var paint in _usedPaints)
                {
                    _mapView.CoreCanvas.RemovePaintTask(paint);
                    paint.ClearGeometriesFromPaintTask(_mapView.CoreCanvas);
                }

                if (stroke is not null) _mapView.CoreCanvas.RemovePaintTask(stroke);
                if (fill is not null) _mapView.CoreCanvas.RemovePaintTask(fill);
            }
        }

        _usedPathShapes.Clear();
        _usedLayers.Clear();
        _usedPaints.Clear();
        _globeCircle = null;
    }
}
