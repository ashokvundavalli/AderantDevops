using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aderant.Build;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class UpdateSplashScreenImage : Task {
        [Required]
        public ITaskItem[] ProductInfo { get; set; }

        public string Version { get; set; }

        public string SplashScreenStyle { get; set; }

        public override bool Execute() {
            foreach (ITaskItem taskItem in ProductInfo) {
                string splashScreenPath = taskItem.ItemSpec;

                string productName = taskItem.GetMetadata("ProductName");
                if (string.IsNullOrEmpty(productName)) {
                    throw new ArgumentNullException("No product name was found for splash screen " +
                                                    splashScreenPath);
                }

                string style = taskItem.GetMetadata("SplashScreenStyle");
                if (!string.IsNullOrEmpty(style)) {
                    SplashScreenStyle = style;
                }

                UpdateSplashScreen(splashScreenPath, productName, SplashScreenStyle);
            }


            return !Log.HasLoggedErrors;
        }

        private void UpdateSplashScreen(string splashScreenPath, string productName, string splashScreenStyle) {
            SplashScreenText[] parts = CreateSplashScreenText(productName);

            if (string.IsNullOrEmpty(splashScreenStyle)) {
                // If we don't any color style then override the "Grey" splashscreen style which uses grey and red fonts
                foreach (SplashScreenText text in parts) {
                    text.Brush = new SolidColorBrush(Colors.White);
                }
            }

            var file = new FileInfo(splashScreenPath);
            if (file.Exists) {
                file.IsReadOnly = false;
                file.Refresh();
            }

            BitmapFrame originalImageSource = BitmapFrame.Create(new Uri(splashScreenPath), BitmapCreateOptions.None,
                                                                 BitmapCacheOption.OnLoad);
            var visual = new DrawingVisual();

            using (DrawingContext drawingContext = visual.RenderOpen()) {
                drawingContext.DrawImage(originalImageSource,
                                         new Rect(0, 0, originalImageSource.PixelWidth, originalImageSource.PixelHeight));
                foreach (SplashScreenText text in parts) {
                    drawingContext.DrawText(text.FormattedText, text.TopRight);
                }
            }

            var renderTargetBitmap = new RenderTargetBitmap(originalImageSource.PixelWidth,
                                                            originalImageSource.PixelHeight,
                                                            originalImageSource.DpiX, originalImageSource.DpiY,
                                                            PixelFormats.Pbgra32);

            renderTargetBitmap.Render(visual);

            BitmapFrame bitmapFrame = BitmapFrame.Create(renderTargetBitmap);
            BitmapEncoder encoder = new PngBitmapEncoder();

            encoder.Frames.Add(bitmapFrame);

            using (FileStream stream = file.OpenWrite()) {
                encoder.Save(stream);
            }
        }

        private SplashScreenText[] CreateSplashScreenText(string productName) {
            var parts = new[] {
                SplashScreenText.Create(SplashScreenText.Product, productName),
                SplashScreenText.Create(SplashScreenText.Version, Version),
                SplashScreenText.Create(SplashScreenText.Copyright, string.Format(
                    "Copyright © {0} Aderant Holdings, Inc. All rights reserved.\nAderant Expert is a registered trademark of Aderant Holdings, Inc.",
                    DateTime.UtcNow.Year))
            };
            return parts;
        }
    }
}