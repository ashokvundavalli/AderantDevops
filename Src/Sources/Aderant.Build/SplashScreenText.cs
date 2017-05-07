using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace Aderant.Build {
    internal class SplashScreenText {
        private const int Margin = 17;
        private const int ReskinMargin = 30;

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
        
        public Point Position { get; set; }

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
                Position = new Point(Margin + 574, Margin + 250),
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
                Position = new Point(Margin + 50, Margin + 180),
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
                Position = new Point(Margin + 50, Margin + 135),
                FontSize = 30,
                Text = text,
                Brush = new SolidColorBrush(Color.FromRgb(178, 8, 56)),
                Typeface = new Typeface(
                    FuturaStd, FontStyles.Normal, FontWeights.Medium,
                    FontStretches.Normal)
            };
        }

        internal static void EvaluateMargins(SplashScreenText[] parts) {
            double productTextMargin = ProductTextBottomMargin(parts[0].Text);
            parts[0].Position = new Point(ReskinMargin, 100);
            parts[1].Position = new Point(ReskinMargin, parts[0].Position.Y + parts[0].FormattedText.Height + productTextMargin);
            parts[2].Position = new Point(ReskinMargin, parts[1].Position.Y + parts[1].FormattedText.Height + 15);
        }

        internal static double ProductTextBottomMargin(string productText) {
            // Add 15 more px of margin if the Product title contains low hanging letters
            Regex lowHangingChars = new Regex("[gjpqyQ]");
            return lowHangingChars.IsMatch(productText) ? 15.0 : 0;
        }

        public static void UpdateTextForNewSplashScreens(SplashScreenText[] parts) {
            parts[0].Brush = new SolidColorBrush(Colors.White);
            parts[1].Brush = new SolidColorBrush(Colors.White);
            parts[2].Brush = new SolidColorBrush(Colors.DarkGray);
            parts[0].FontSize = 60;
            parts[1].FontSize = 27;
            parts[2].FontSize = 14;
            parts[2].Alignment = TextAlignment.Left;
            parts[2].Text = string.Format(
                @"Copyright © {0} Aderant Holdings, Inc. 
All rights reserved. 
Aderant Expert is a registered trademark 
of Aderant Holdings, Inc.", DateTime.UtcNow.Year);

            if (parts[0].FormattedText.Width > 480) {
                parts[0].FontSize = 35;
            }

            EvaluateMargins(parts);
        }
    }
}