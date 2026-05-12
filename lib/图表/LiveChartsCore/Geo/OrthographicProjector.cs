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

namespace LiveChartsCore.Geo;

/// <summary>
/// Projects latitude and longitude coordinates using the Orthographic (globe) projection.
/// Points on the far side of the globe are not visible.
/// </summary>
/// <seealso cref="MapProjector" />
public class OrthographicProjector : MapProjector
{
    private readonly double _centerLon;
    private readonly double _centerLat;
    private readonly double _sinCenterLat;
    private readonly double _cosCenterLat;
    private readonly float _radius;
    private readonly float _screenCenterX;
    private readonly float _screenCenterY;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrthographicProjector"/> class.
    /// </summary>
    /// <param name="mapWidth">Width of the map area.</param>
    /// <param name="mapHeight">Height of the map area.</param>
    /// <param name="offsetX">The offset x.</param>
    /// <param name="offsetY">The offset y.</param>
    /// <param name="centerLon">The center longitude (where the globe is facing).</param>
    /// <param name="centerLat">The center latitude (where the globe is facing).</param>
    public OrthographicProjector(
        float mapWidth, float mapHeight, float offsetX, float offsetY,
        double centerLon, double centerLat)
    {
        _centerLon = centerLon;
        _centerLat = centerLat;
        _sinCenterLat = Math.Sin(centerLat * Math.PI / 180d);
        _cosCenterLat = Math.Cos(centerLat * Math.PI / 180d);
        _radius = Math.Min(mapWidth, mapHeight) / 2f;
        _screenCenterX = mapWidth / 2f + offsetX;
        _screenCenterY = mapHeight / 2f + offsetY;

        XOffset = offsetX;
        YOffset = offsetY;
        MapWidth = mapWidth;
        MapHeight = mapHeight;
    }

    /// <summary>
    /// Gets the center longitude.
    /// </summary>
    public double CenterLongitude => _centerLon;

    /// <summary>
    /// Gets the center latitude.
    /// </summary>
    public double CenterLatitude => _centerLat;

    /// <summary>
    /// Gets the globe radius in screen units.
    /// </summary>
    public float Radius => _radius;

    /// <summary>
    /// Gets the screen X of the globe center.
    /// </summary>
    public float ScreenCenterX => _screenCenterX;

    /// <summary>
    /// Gets the screen Y of the globe center.
    /// </summary>
    public float ScreenCenterY => _screenCenterY;

    /// <summary>
    /// Gets the preferred ratio (1:1 for a circular globe).
    /// </summary>
    public static float[] PreferredRatio => [1f, 1f];

    /// <inheritdoc cref="MapProjector.IsVisible(double, double)"/>
    public override bool IsVisible(double longitude, double latitude)
    {
        var latRad = latitude * Math.PI / 180d;
        var lonDiff = (longitude - _centerLon) * Math.PI / 180d;

        var cosC = _sinCenterLat * Math.Sin(latRad) +
                   _cosCenterLat * Math.Cos(latRad) * Math.Cos(lonDiff);

        return cosC > 0;
    }

    /// <inheritdoc cref="MapProjector.ToMap(double[])"/>
    public override float[] ToMap(double[] point)
    {
        var lon = point[0];
        var lat = point[1];

        var latRad = lat * Math.PI / 180d;
        var lonDiff = (lon - _centerLon) * Math.PI / 180d;

        var x = _radius * Math.Cos(latRad) * Math.Sin(lonDiff);
        var y = _radius * (_cosCenterLat * Math.Sin(latRad) -
                           _sinCenterLat * Math.Cos(latRad) * Math.Cos(lonDiff));

        return
        [
            (float)(_screenCenterX + x),
            (float)(_screenCenterY - y)
        ];
    }
}
