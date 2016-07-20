using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Brush = System.Windows.Media.Brush;
using Image = System.Windows.Controls.Image;
using Pen = System.Windows.Media.Pen;

namespace Aderant.DeveloperTools.XamlAdorner {

    [Name("ColorAdornment"), Export(typeof(ColorAdornment))]
    [Serializable]
    public class ColorAdornment : AdornmentBase {

        public ColorAdornment(IWpfTextView view)
            : base(view, new[] { @"ExpertResources\S*\.\S+\s*\}", @"ExpertBrushes\S*\.\S+\s*\}" }, "ColorArdornment", "Aderant.PresentationFramework.Windows", new [] { "ExpertResources", "ExpertBrushes" }) {
        }

        protected override void AdornmentAction(object resource, IWpfTextViewLineCollection textViewLines, SnapshotSpan span) {
            if (resource is System.Windows.Media.Color) {
                var color = (System.Windows.Media.Color) resource;
                DrawAdornment(textViewLines, span, new SolidColorBrush(color), new Pen(new SolidColorBrush(color), 1));
                var text = string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
                DrawAdornment(textViewLines, span, text, Brushes.DeepPink);
            }
            else if (resource is Brush) {
                var brush = (Brush) resource;
                DrawAdornment(textViewLines, span, brush, new Pen(brush, 1));
                if (brush is SolidColorBrush) {
                    var color = (brush as SolidColorBrush).Color;
                    var text = string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
                    DrawAdornment(textViewLines, span, text, Brushes.DeepPink);
                }
            } else if (resource is double) {
                var number = (double)resource;
                DrawAdornment(textViewLines, span, number.ToString(), Brushes.OrangeRed);
            } else if (resource is int) {
                var number = (int)resource;
                DrawAdornment(textViewLines, span, number.ToString(), Brushes.OrangeRed);
            } else if (resource is FontFamily) {
                var fontFamily = (FontFamily)resource;
                var text = fontFamily.FamilyNames.Values.First();
                DrawAdornment(textViewLines, span, text, Brushes.DodgerBlue);
            } else if (resource is DrawingImage) {
                var drawingImage = (DrawingImage)resource;
                var imageAdornment = new ImageAdornment(this.view);
                imageAdornment.ShowImage(textViewLines, span, drawingImage);
            }
        }

        public void DrawAdornment(IWpfTextViewLineCollection textViewLines, SnapshotSpan span, string text, Brush textBrush) {
            PathGeometry markerGeometry = (PathGeometry)(textViewLines.GetMarkerGeometry(span));
            if (markerGeometry != null) {
                var g = new RectangleGeometry(new Rect {
                    Height = 11,
                    Width = markerGeometry.Bounds.Width,
                    X = markerGeometry.Bounds.X,
                    Y = markerGeometry.Bounds.Y
                });

                var textBlock = new TextBlock() {
                    FontWeight = FontWeights.Normal,
                    Text = text,
                    FontSize = 8,
                    Foreground = textBrush,
                    Background = Brushes.White,
                    SnapsToDevicePixels = true,
                    
                };
                var brush = new VisualBrush(textBlock) {
                    TileMode = TileMode.None,
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Right,
                    AlignmentY = AlignmentY.Top,
                };
                var pen = new Pen(Brushes.Black, 0);
                GeometryDrawing drawing = new GeometryDrawing(brush, pen, g);

                DrawingImage drawingImage = new DrawingImage(drawing);

                Image image = new Image();
                image.Source = drawingImage;
                image.SnapsToDevicePixels = true;

                //Align the image with the top of the bounds of the text geometry
                Canvas.SetLeft(image, g.Bounds.Left);
                Canvas.SetTop(image, g.Bounds.Top + markerGeometry.Bounds.Height - 6);

                this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);
            }
        }

        public void DrawAdornment(IWpfTextViewLineCollection textViewLines, SnapshotSpan span, Brush brush, Pen pen) {
            PathGeometry markerGeometry = (PathGeometry)(textViewLines.GetMarkerGeometry(span));
            if (markerGeometry != null) {
                var g = new RectangleGeometry(new Rect {
                    Height = 1,
                    Width = markerGeometry.Bounds.Width,
                    X = markerGeometry.Bounds.X,
                    Y = markerGeometry.Bounds.Y
                });
                
                GeometryDrawing drawing = new GeometryDrawing(brush, pen, g);
                drawing.Freeze();

                DrawingImage drawingImage = new DrawingImage(drawing);
                drawingImage.Freeze();

                Image image = new Image();
                image.Source = drawingImage;
                image.SnapsToDevicePixels = true;

                //Align the image with the top of the bounds of the text geometry
                Canvas.SetLeft(image, g.Bounds.Left);
                Canvas.SetTop(image, g.Bounds.Top + markerGeometry.Bounds.Height - 1);

                this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);
            }
        }
    }
}
