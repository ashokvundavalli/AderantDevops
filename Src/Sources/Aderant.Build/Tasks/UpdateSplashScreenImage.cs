using System;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aderant.Build;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    public sealed class UpdateSplashScreenImage : TrackedSourcesBuildTask {

        protected override string WriteTLogFilename {
            get { return "UpdateSplashScreenImage.write.TLog"; }
        }

        protected override string[] ReadTLogFilenames {
            get {
                return new[] {
                    "UpdateSplashScreenImage.read.TLog"
                };
            }
        }

        [Required]
        public string Text { get; set; }

        [Required]
        public string Version { get; set; }

        public string Style { get; set; }
        
        protected override bool ExecuteInternal() {
            var splashScreen = Sources[0];
            string splashScreenPath = splashScreen.GetMetadata("FullPath");
            
            UpdateSplashScreen(splashScreenPath, Text, Style);

            return !Log.HasLoggedErrors;
        }

        internal void UpdateSplashScreen(string splashScreenPath, string productName, string splashScreenStyle) {
            SplashScreenText[] parts = CreateSplashScreenText(productName);

            if (string.IsNullOrEmpty(splashScreenStyle)) {
                // If we don't any color style then override the "Grey" splashscreen style which uses grey and red fonts
                foreach (SplashScreenText text in parts) {
                    text.Brush = new SolidColorBrush(Colors.White);
                }
            }

            var file = new FileInfo(splashScreenPath);
            FileInfo copy = null;
            if (file.Exists) {
                if (OutputFile != null) {
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputFile));

                    copy = file.CopyTo(OutputFile, true);
                    copy.IsReadOnly = false;
                    copy.Refresh();
                }
            } else {
                throw new FileNotFoundException("Could not find the file", file.FullName);
            }

            BitmapFrame originalImageSource = BitmapFrame.Create(new Uri(file.FullName), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var visual = new DrawingVisual();

            using (DrawingContext drawingContext = visual.RenderOpen()) {
                drawingContext.DrawImage(originalImageSource,
                                         new Rect(0, 0, originalImageSource.PixelWidth, originalImageSource.PixelHeight));
                foreach (SplashScreenText text in parts) {
                    if (text.Text != null) {
                        drawingContext.DrawText(text.FormattedText, text.TopRight);
                    }
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

            if (copy != null) {
                using (FileStream stream = copy.OpenWrite()) {
                    encoder.Save(stream);
                }
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