using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using Aderant.DeveloperTools.Shared;

namespace Aderant.DeveloperTools {
    public class OptionPageGrid : DialogPage {
        /// <summary>
        /// Gets or sets a value indicating whether solution badges should be enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if solution badges should be enabled; otherwise, <c>false</c>.
        /// </value>
        [Category("General")]
        [DisplayName("Enable this extension")]
        [Description("Globally enables this extension. If this setting is changed, it requires a restart of Visual Studio to take effect.")]
        public bool EnableExtension {
            get { return Options.EnableExtension; }
            set { Options.EnableExtension = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether solution badges should be enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if solution badges should be enabled; otherwise, <c>false</c>.
        /// </value>
        [Category("TFS Branch Visibility")]
        [DisplayName("Enable solution badges")]
        [Description("Change the task bar preview thumbnail of an ExpertSuite solution to clearly show the module name and the branch where it was opened from.")]
        public bool EnableSolutionBadges {
            get { return Options.EnableSolutionBadges; }
            set { Options.EnableSolutionBadges = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether window titles should be changed.
        /// </summary>
        /// <value>
        /// <c>true</c> if window titles should be changed; otherwise, <c>false</c>.
        /// </value>
        [Category("TFS Branch Visibility")]
        [DisplayName("Change window titles")]
        [Description("Change the Visual Studio main window title of an ExpertSuite solution to include the branch where it was opened from.")]
        public bool ChangeWindowTitles {
            get { return Options.ChangeWindowTitles; }
            set { Options.ChangeWindowTitles = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Solution Explorer titles should be changed.
        /// </summary>
        /// <value>
        /// <c>true</c> if Solution Explorer titles should be changed; otherwise, <c>false</c>.
        /// </value>
        [Category("TFS Branch Visibility")]
        [DisplayName("Change Solution Explorer titles")]
        [Description("Change the Solution Explorer title of an ExpertSuite solution to include the branch where it was opened from.")]
        public bool ChangeSolutionExplorerTitles {
            get { return Options.ChangeSolutionExplorerTitles; }
            set { Options.ChangeSolutionExplorerTitles = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether XAML adorners should be enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if XAML adorners should be enabled; otherwise, <c>false</c>.
        /// </value>
        [Category("XAML Editor")]
        [DisplayName("Enable resource adorner")]
        [Description("Draws an adorner for x:Static usage (ExpertResources, ExpertBrushes), e.g. colors, brushes, fonts, sizes etc. into the editor. If this setting is changed, it requires a restart of Visual Studio to take effect.")]
        public bool EnableXamlAdorner {
            get { return Options.EnableXamlAdorner; }
            set { Options.EnableXamlAdorner = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether XAML image previews should be enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if XAML image previews should be enabled; otherwise, <c>false</c>.
        /// </value>
        [Category("XAML Editor")]
        [DisplayName("Enable image preview")]
        [Description("Draws an image preview for x:Static usage (ExpertImages) into the editor. If this setting is changed, it requires a restart of Visual Studio to take effect.")]
        public bool EnableImagePreview {
            get { return Options.EnableImagePreview; }
            set { Options.EnableImagePreview = value; }
        }
    }
}
