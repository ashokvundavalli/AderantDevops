using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Aderant.Build {
    internal class SplashScreenText {
        private const int Margin = 17;

        internal const string Product = "Product";
        internal const string Version = "Version";
        internal const string Copyright = "Copyright";
        private static readonly ICollection<FontFamily> fonts = FontInstaller.InstallFonts();

        private SplashScreenText() {
            Alignment = TextAlignment.Left;
        }

        private static FontFamily FuturaStd {
            get { return fonts.FirstOrDefault(); }
        }

        public FormattedText FormattedText {
            get {
                var text = new FormattedText(Text,
                                             CultureInfo.InvariantCulture,
                                             FlowDirection.LeftToRight,
                                             Typeface,
                                             FontSize,
                                             Brush) {
                                                 TextAlignment = Alignment
                                             };
                return text;
            }
        }

        public TextAlignment Alignment { get; set; }

        public string Text { get; set; }

        public int FontSize { get; set; }

        public Typeface Typeface { get; set; }

        public Point TopRight { get; set; }

        public SolidColorBrush Brush { get; set; }

        public FlowDirection FlowDirection { get; set; }

        public static SplashScreenText Create(string type, string text) {
            switch (type) {
                case Product:
                    return CreateProductText(text);
                case Version:
                    return CreateVersionText(text);
                case Copyright:
                    return CreateCopyrightText(text);
            }
            return null;
        }

        private static SplashScreenText CreateCopyrightText(string text) {
            var part = new SplashScreenText {
                TopRight = new Point(Margin + 574, Margin + 250),
                FontSize = 12,
                Text = text,
                Brush = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                Typeface = new Typeface(
                    new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Regular,
                    FontStretches.Normal)
            };
            part.Alignment = TextAlignment.Right;
            return part;
        }

        private static SplashScreenText CreateVersionText(string text) {
            return new SplashScreenText {
                TopRight = new Point(Margin + 50, Margin + 180),
                FontSize = 16,
                Text = text,
                Brush = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                Typeface = new Typeface(
                    FuturaStd, FontStyles.Normal, FontWeights.Medium,
                    FontStretches.Normal)
            };
        }

        private static SplashScreenText CreateProductText(string text) {
            return new SplashScreenText {
                TopRight = new Point(Margin + 50, Margin + 135),
                FontSize = 30,
                Text = text,
                Brush = new SolidColorBrush(Color.FromRgb(178, 8, 56)),
                Typeface = new Typeface(
                    FuturaStd, FontStyles.Normal, FontWeights.Medium,
                    FontStretches.Normal)
            };
        }
    }
}