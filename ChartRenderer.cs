using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HeartRateMonitor
{
    public static class ChartRenderer
    {
        public static void DrawChart(
            Canvas canvas, 
            List<HeartRatePoint> history, 
            double windowDurationSeconds, 
            bool autoYScale, 
            double manualMaxY, 
            bool showGrid, 
            int age, 
            bool isActiveSession)
        {
            if (canvas == null) return;
            canvas.Children.Clear();

            if (history == null || history.Count == 0) return;

            double canvasWidth = canvas.ActualWidth;
            double canvasHeight = canvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            // 1. Determine X Bounds
            double xMax = history.Last().Time;
            double xMin = 0;

            if (isActiveSession)
            {
                xMin = Math.Max(0, xMax - windowDurationSeconds);
                if (xMax < windowDurationSeconds)
                {
                    xMax = windowDurationSeconds;
                    xMin = 0;
                }
            }
            else
            {
                xMin = 0;
                if (xMax < 10) xMax = 10;
            }

            // 2. Determine Y Bounds
            double yMin = 50;
            double yMax = 180;
            
            if (autoYScale || !isActiveSession)
            {
                var visiblePoints = history
                    .Where(p => !isActiveSession || p.Time >= xMin)
                    .Select(p => p.Bpm)
                    .ToList();
                if (visiblePoints.Count > 0)
                {
                    yMin = visiblePoints.Min() - 5;
                    yMax = visiblePoints.Max() + 5;
                    yMin = Math.Max(30, Math.Min(yMin, 80));
                    yMax = Math.Min(220, Math.Max(yMax, 130));
                }
            }
            else
            {
                yMax = manualMaxY;
                yMin = 40;
            }

            // 3. Draw Age-Based Background Zone Bands
            double maxHr = FitnessCalculator.CalculateMaxHeartRate(age);
            for (int zone = 1; zone <= 5; zone++)
            {
                var limits = FitnessCalculator.GetZoneLimits(zone, age);
                double pyLow = MapBpmToY(limits.Low, yMin, yMax, canvasHeight);
                double pyHigh = MapBpmToY(limits.High, yMin, yMax, canvasHeight);

                double rectBottom = Math.Min(canvasHeight - 25, pyLow);
                double rectTop = Math.Max(0, pyHigh);
                double rectHeight = rectBottom - rectTop;

                if (rectHeight > 0 && rectTop < canvasHeight && rectBottom > 0)
                {
                    Color color = FitnessCalculator.GetZoneColor(zone);
                    string name = FitnessCalculator.GetZoneName(zone);

                    // Draw band
                    var rect = new Rectangle
                    {
                        Width = canvasWidth,
                        Height = rectHeight,
                        Fill = new SolidColorBrush(Color.FromArgb(10, color.R, color.G, color.B)),
                        Margin = new Thickness(0, rectTop, 0, 0)
                    };
                    canvas.Children.Add(rect);

                    // Draw zone label text
                    var label = new TextBlock
                    {
                        Text = $"{name} ({limits.Low}-{limits.High})",
                        Foreground = new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B)),
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, rectTop + 4, 16, 0),
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    Canvas.SetLeft(label, canvasWidth - 180);
                    Canvas.SetTop(label, rectTop + 2);
                    label.TextAlignment = TextAlignment.Right;
                    label.Width = 170;
                    canvas.Children.Add(label);
                }
            }

            // 4. Draw Grid Lines and Labels
            if (showGrid)
            {
                double step = 20; // grid interval
                double startGridY = Math.Ceiling(yMin / step) * step;
                for (double yVal = startGridY; yVal <= yMax; yVal += step)
                {
                    double py = MapBpmToY(yVal, yMin, yMax, canvasHeight);
                    if (py < 0 || py > canvasHeight - 25) continue;

                    var line = new Line
                    {
                        X1 = 0,
                        Y1 = py,
                        X2 = canvasWidth,
                        Y2 = py,
                        Stroke = new SolidColorBrush(Color.FromRgb(40, 40, 51)),
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(line);

                    var text = new TextBlock
                    {
                        Text = $"{yVal} BPM",
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 165)),
                        FontSize = 10,
                        FontWeight = FontWeights.Medium,
                        Margin = new Thickness(8, py - 8, 0, 0),
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    canvas.Children.Add(text);
                }

                // Draw X-axis grid lines and time labels
                double xRange = xMax - xMin;
                double xStep = 10; // default interval in seconds
                if (xRange > 180) xStep = 60; // 1 minute
                else if (xRange > 90) xStep = 30; // 30 seconds
                else if (xRange > 45) xStep = 15; // 15 seconds

                double startGridX = Math.Ceiling(xMin / xStep) * xStep;
                for (double xVal = startGridX; xVal <= xMax; xVal += xStep)
                {
                    double px = (xVal - xMin) / (xMax - xMin) * canvasWidth;
                    if (px < 0 || px > canvasWidth) continue;

                    var line = new Line
                    {
                        X1 = px,
                        Y1 = 0,
                        X2 = px,
                        Y2 = canvasHeight - 25,
                        Stroke = new SolidColorBrush(Color.FromRgb(40, 40, 51)),
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(line);

                    int mins = (int)(xVal / 60);
                    int secs = (int)(xVal % 60);
                    string timeStr = mins > 0 ? $"{mins}:{secs:D2}" : $"{secs}s";

                    var text = new TextBlock
                    {
                        Text = timeStr,
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 165)),
                        FontSize = 10,
                        FontWeight = FontWeights.Medium,
                        Margin = new Thickness(px - 15, canvasHeight - 22, 0, 0),
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    canvas.Children.Add(text);
                }
            }

            // 5. Downsample points and map to pixels
            var rawPoints = history
                .Where(item => !isActiveSession || item.Time >= xMin - 10)
                .ToList();

            var downsampled = Downsample(rawPoints, xMin, xMax, canvasWidth, 2.0);

            var points = new List<Point>();
            if (downsampled.Count > 0)
            {
                // Smooth with Exponential Moving Average (EMA)
                var smoothedBpmList = new List<double>();
                double currentSmoothed = downsampled[0].Bpm;
                smoothedBpmList.Add(currentSmoothed);

                double alpha = 0.25; 
                for (int i = 1; i < downsampled.Count; i++)
                {
                    currentSmoothed = alpha * downsampled[i].Bpm + (1 - alpha) * currentSmoothed;
                    smoothedBpmList.Add(currentSmoothed);
                }

                for (int i = 0; i < downsampled.Count; i++)
                {
                    var item = downsampled[i];
                    double smoothedBpm = smoothedBpmList[i];

                    double px = (item.Time - xMin) / (xMax - xMin) * canvasWidth;
                    double py = MapBpmToY(smoothedBpm, yMin, yMax, canvasHeight);
                    points.Add(new Point(px, py));
                }
            }

            if (points.Count == 0) return;

            var themeColor = Color.FromRgb(0, 120, 212); // Default Blue

            // 6. Draw Area Path (filled gradient)
            if (points.Count >= 2)
            {
                var areaGeom = GetBezierAreaPath(points, canvasHeight);
                var areaBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(55, themeColor.R, themeColor.G, themeColor.B), 0));
                areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, themeColor.R, themeColor.G, themeColor.B), 1));

                var areaPath = new System.Windows.Shapes.Path
                {
                    Data = areaGeom,
                    Fill = areaBrush
                };
                canvas.Children.Add(areaPath);
            }

            // 7. Draw Line Path
            var lineGeom = GetBezierPath(points);
            var linePath = new System.Windows.Shapes.Path
            {
                Data = lineGeom,
                Stroke = new SolidColorBrush(themeColor),
                StrokeThickness = 3.5
            };
            canvas.Children.Add(linePath);

            // 8. Draw latest point as a glowing dot
            if (points.Count > 0 && isActiveSession)
            {
                var lastPoint = points.Last();
                if (lastPoint.X >= 0 && lastPoint.X <= canvasWidth)
                {
                    var glowCircle = new Ellipse
                    {
                        Width = 16,
                        Height = 16,
                        Fill = new SolidColorBrush(Color.FromArgb(60, themeColor.R, themeColor.G, themeColor.B)),
                        Margin = new Thickness(lastPoint.X - 8, lastPoint.Y - 8, 0, 0)
                    };
                    canvas.Children.Add(glowCircle);

                    var solidCircle = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = new SolidColorBrush(themeColor),
                        Stroke = Brushes.White,
                        StrokeThickness = 1.5,
                        Margin = new Thickness(lastPoint.X - 4, lastPoint.Y - 4, 0, 0)
                    };
                    canvas.Children.Add(solidCircle);
                }
            }
        }

        private static double MapBpmToY(double bpm, double yMin, double yMax, double canvasHeight)
        {
            return canvasHeight - 25 - (bpm - yMin) / (yMax - yMin) * (canvasHeight - 45);
        }

        private static List<HeartRatePoint> Downsample(List<HeartRatePoint> rawPoints, double xMin, double xMax, double canvasWidth, double minPixelSpacing)
        {
            if (rawPoints == null || rawPoints.Count <= 2) return rawPoints ?? new List<HeartRatePoint>();
            double timeRange = xMax - xMin;
            if (timeRange <= 0) return rawPoints;

            var result = new List<HeartRatePoint>();
            result.Add(rawPoints[0]);

            double lastX = (rawPoints[0].Time - xMin) / timeRange * canvasWidth;
            for (int i = 1; i < rawPoints.Count - 1; i++)
            {
                var pt = rawPoints[i];
                double px = (pt.Time - xMin) / timeRange * canvasWidth;
                if (px - lastX >= minPixelSpacing)
                {
                    result.Add(pt);
                    lastX = px;
                }
            }
            result.Add(rawPoints.Last());
            return result;
        }

        private static PathGeometry GetBezierPath(List<Point> points)
        {
            var geometry = new PathGeometry();
            if (points == null || points.Count == 0) return geometry;

            var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };

            if (points.Count == 1)
            {
                geometry.Figures.Add(figure);
                return geometry;
            }
            else if (points.Count == 2)
            {
                figure.Segments.Add(new LineSegment(points[1], true));
                geometry.Figures.Add(figure);
                return geometry;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Point p0 = i > 0 ? points[i - 1] : points[i];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = i + 2 < points.Count ? points[i + 2] : p2;

                double tension = 0.25;

                Point c1 = new Point(p1.X + (p2.X - p0.X) * tension, p1.Y + (p2.Y - p0.Y) * tension);
                Point c2 = new Point(p2.X - (p3.X - p1.X) * tension, p2.Y - (p3.Y - p1.Y) * tension);

                c1.Y = Math.Max(0, c1.Y);
                c2.Y = Math.Max(0, c2.Y);

                figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        private static PathGeometry GetBezierAreaPath(List<Point> points, double canvasHeight)
        {
            var geometry = new PathGeometry();
            if (points == null || points.Count < 2) return geometry;

            var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };

            for (int i = 0; i < points.Count - 1; i++)
            {
                Point p0 = i > 0 ? points[i - 1] : points[i];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = i + 2 < points.Count ? points[i + 2] : p2;

                double tension = 0.25;

                Point c1 = new Point(p1.X + (p2.X - p0.X) * tension, p1.Y + (p2.Y - p0.Y) * tension);
                Point c2 = new Point(p2.X - (p3.X - p1.X) * tension, p2.Y - (p3.Y - p1.Y) * tension);

                c1.Y = Math.Max(0, c1.Y);
                c2.Y = Math.Max(0, c2.Y);

                figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
            }

            figure.Segments.Add(new LineSegment(new Point(points[points.Count - 1].X, canvasHeight - 25), false));
            figure.Segments.Add(new LineSegment(new Point(points[0].X, canvasHeight - 25), false));

            geometry.Figures.Add(figure);
            return geometry;
        }
    }
}
