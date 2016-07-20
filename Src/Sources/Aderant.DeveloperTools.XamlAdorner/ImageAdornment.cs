using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Image = System.Windows.Controls.Image;

namespace Aderant.DeveloperTools.XamlAdorner {

    [Name("ImageAdornment"), Export(typeof(ImageAdornment))]
    public class ImageAdornment : AdornmentBase {

        public ImageAdornment(IWpfTextView view)
            : base(view, new [] { @"ExpertImages\S*\.\S+\s*\}" }, "ImageAdornment", "Aderant.PresentationFramework.Images", new [] { "ExpertImages" }) {
        }

        protected override void AdornmentAction(object resource, IWpfTextViewLineCollection textViewLines, SnapshotSpan span) {
            if (resource is ImageSource) {
                var drawingImage = (ImageSource)resource;
                ShowImage(textViewLines, span, drawingImage);
            }
        }

        public void ShowImage(IWpfTextViewLineCollection textViewLines, SnapshotSpan span, ImageSource imageSource) {
            PathGeometry markerGeometry = (PathGeometry)(textViewLines.GetMarkerGeometry(span));
            if (markerGeometry != null)
            {
                Image image = new Image();
                image.Source = imageSource;
                image.SnapsToDevicePixels = true;
                image.Width = 13;
                image.Height = 13;

                //Align the image with the top of the bounds of the text geometry
                Canvas.SetLeft(image, 2); //markerGeometry.Bounds.Left + markerGeometry.Bounds.Width + 10);
                Canvas.SetTop(image, markerGeometry.Bounds.Top + 2);

                this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, image, null);
            }
        }
    }
}
