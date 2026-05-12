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

using LiveChartsCore.Drawing;

namespace LiveChartsCore.Geo;

/// <summary>
/// Represents a hovered land on a geo map, used for tooltip display.
/// </summary>
public class GeoTooltipPoint
{
    /// <summary>
    /// Gets or sets the land definition.
    /// </summary>
    public LandDefinition Land { get; set; } = null!;

    /// <summary>
    /// Gets or sets the heat value of the land.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets whether the land has a heat value assigned.
    /// </summary>
    public bool HasValue { get; set; }

    /// <summary>
    /// Gets or sets the heat color of the land.
    /// </summary>
    public LvcColor Color { get; set; }

    /// <summary>
    /// Gets or sets the visual center of the land in screen coordinates.
    /// </summary>
    public LvcPoint LandCenter { get; set; }
}
