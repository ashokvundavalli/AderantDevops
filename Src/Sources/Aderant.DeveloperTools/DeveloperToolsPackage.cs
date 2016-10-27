using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aderant.DeveloperTools.Shared;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.WindowsAPICodePack.Taskbar;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;

namespace Aderant.DeveloperTools {
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(GuidList.guidVSPackage2PkgString)]
    [ProvideAutoLoad(UIContextGuids.NoSolution)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionPageGrid), "Aderant Developer Tools", "Options", 0, 0, true)]
    public sealed class DeveloperToolsPackage : Package {


        private string currentCaptionSuffix;
        private System.Timers.Timer timer = new System.Timers.Timer(100);
        private FileSystemWatcher watcher;
        private Dispatcher dispatcher;

        private DTE2 dtePropertyValue;

        public DTE2 DTE
        {
            get { return dtePropertyValue ?? (dtePropertyValue = GetGlobalService(typeof(SDTE)) as DTE2); }
        }

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public DeveloperToolsPackage() {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        private SolutionEvents solutionEvents;
        private DebuggerEvents debuggerEvents;
        private WindowEvents windowEvents;
        private DocumentEvents documentEvents;

        private TabbedThumbnail customThumbnail;

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize() {

            OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            Options.EnableExtension = page.EnableExtension;

            Options.EnableSolutionBadges = page.EnableSolutionBadges;
            Options.ChangeWindowTitles = page.ChangeWindowTitles;
            Options.ChangeSolutionExplorerTitles = page.ChangeSolutionExplorerTitles;

            Options.EnableXamlAdorner = page.EnableXamlAdorner;
            Options.EnableImagePreview = page.EnableImagePreview;

            if (!Options.EnableExtension || (!Options.EnableSolutionBadges && !Options.ChangeWindowTitles && !Options.ChangeWindowTitles)) {
                return;
            }

            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            AllowTaskbarWindowMessagesThroughUIPI();

            // save solution events as they get disposed otherwise
            solutionEvents = DTE.Events.SolutionEvents;
            debuggerEvents = DTE.Events.DebuggerEvents;
            windowEvents = DTE.Events.WindowEvents;
            documentEvents = DTE.Events.DocumentEvents;

            // listen to any solution that gets opened
            solutionEvents.Opened += SolutionEvents_Opened;

            dispatcher = Dispatcher.CurrentDispatcher;
        }

        private void SolutionEvents_Opened() {

            // detach from solution events
            debuggerEvents.OnEnterBreakMode -= OnIdeEvent;
            debuggerEvents.OnEnterRunMode -= OnIdeEvent;
            debuggerEvents.OnEnterDesignMode -= OnIdeEvent;
            debuggerEvents.OnContextChanged -= OnIdeEvent;
            solutionEvents.AfterClosing -= OnIdeEvent;
            solutionEvents.Opened -= OnIdeEvent;
            solutionEvents.Renamed -= OnIdeEvent;
            windowEvents.WindowCreated -= OnIdeEvent;
            windowEvents.WindowClosing -= OnIdeEvent;
            windowEvents.WindowActivated -= OnIdeEvent;
            documentEvents.DocumentOpened -= OnIdeEvent;
            documentEvents.DocumentClosing -= OnIdeEvent;

            currentCaptionSuffix = string.Empty;

            // if there was already a custom thumbnail created, remove and dispose it
            if (customThumbnail != null) {
                TaskbarManager.Instance.TabbedThumbnail.RemoveThumbnailPreview(customThumbnail);
                if (customThumbnail != null) {
                    customThumbnail.TabbedThumbnailActivated -= CustomThumbnailOnTabbedThumbnailActivated;
                    customThumbnail.Dispose();
                }
            }


            // if this seems to be an ExpertSuite solution, change the preview thumbnail
            var fileName = DTE.Solution.FileName;
            var folders = fileName.Split('\\').ToList();
            var rootDir = Path.GetDirectoryName(fileName);
            var gitDirAvailable = Directory.Exists(Path.Combine(rootDir, ".git"));
            if (gitDirAvailable || folders.IndexOf("Modules") >= 0) {
                ChangePreviewThumbnail(fileName, gitDirAvailable);
            } else {
                customThumbnail = null;
                SetMainWindowTitle(cleanOnly: true);
            }
        }

        private void ChangePreviewThumbnail(string fileName, bool gitDirAvailable) {

            try {
                // create custom preview thumbnail
                customThumbnail = new TabbedThumbnail((IntPtr)DTE.MainWindow.HWnd, (IntPtr)DTE.MainWindow.HWnd);
                customThumbnail.TabbedThumbnailActivated += CustomThumbnailOnTabbedThumbnailActivated;
                customThumbnail.TabbedThumbnailClosed += CustomThumbnailOnTabbedThumbnailClosed;
                TaskbarManager.Instance.TabbedThumbnail.AddThumbnailPreview(customThumbnail);

                // create abbreviated title for solution
                var solutionName = Path.GetFileNameWithoutExtension(fileName);
                var titleBuilder = new StringBuilder();
                foreach (var solutionNamePart in solutionName.Split('.')) {
                    if (titleBuilder.Length > 0) {
                        titleBuilder.Append(".");
                    }
                    var strippedSolutionPartName =
                        solutionNamePart
                            .Replace("Applications", "Apps")
                            .Replace("Libraries", "Libs")
                            .Replace("Services", "SVCs");
                    titleBuilder.Append(strippedSolutionPartName);
                }
                customThumbnail.Title = titleBuilder.ToString();

                // generate custom image for preview thumbnail
                Bitmap bitmap = GenerateBitmap(fileName, gitDirAvailable);
                if (bitmap != null) {
                    customThumbnail.SetImage(bitmap);

                    // show Expert icon in header next to title
                    customThumbnail.SetWindowIcon(Resources.ExpertIcon);
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }

        private void CustomThumbnailOnTabbedThumbnailClosed(object sender, TabbedThumbnailClosedEventArgs tabbedThumbnailClosedEventArgs) {
            // activate VS main window and PInvoke a close message (calling Close() on DTE.MainWindow crashed the IDE)
            DTE.MainWindow.Activate();
            DTE.MainWindow.SetFocus();
            CloseWindow(tabbedThumbnailClosedEventArgs.WindowHandle);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        private const UInt32 WM_CLOSE = 0x0010;

        private void CloseWindow(IntPtr hwnd) {
            SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        private void CustomThumbnailOnTabbedThumbnailActivated(object sender, TabbedThumbnailEventArgs tabbedThumbnailEventArgs) {
            // activate VS main window on click
            DTE.MainWindow.Activate();
            DTE.MainWindow.SetFocus();
        }

        private Bitmap GenerateBitmap(string fileName, bool gitDirAvailable) {

            // calculate branch and branch area
            string branch = string.Empty;
            string branchArea = string.Empty;

            if (!gitDirAvailable) {
                var folders = fileName.Split('\\').ToList();
                var modulesIndex = folders.IndexOf("Modules");
                if (modulesIndex > 1) {
                    branch = folders[modulesIndex - 1];
                    branchArea = folders[modulesIndex - 2];
                }
                if (branch == "Main") {
                    branch = "MAIN";
                }
            } else {
                // this is a git branch
                branchArea = "ExpertSuite";
                var rootDir = Path.GetDirectoryName(fileName);
                var gitDir = Path.Combine(rootDir, ".git");
                var headFile = Path.Combine(gitDir, "HEAD");
                if (File.Exists(headFile)) {
                    var headFileContent = File.ReadAllText(headFile);
                    branch = headFileContent.Split('/').Last().Replace("\n", string.Empty);
                }

                // watch git branch changes
                if (watcher == null) {
                    watcher = new FileSystemWatcher();
                    watcher.Path = gitDir;
                    watcher.NotifyFilter = NotifyFilters.LastWrite;
                    watcher.Filter = "HEAD";
                    watcher.Changed += OnHeadChanged;
                    watcher.EnableRaisingEvents = true;
                }
            }

            string branchDisplayName = string.Concat(branchArea.ToUpperInvariant(), " · ", branch.ToUpperInvariant());

            if (branchArea.Equals("ExpertSuite", StringComparison.InvariantCultureIgnoreCase)) {
                branchArea = string.Empty;
                branchDisplayName = branch.ToUpperInvariant();
            }

            currentCaptionSuffix = string.Concat(dot, branchDisplayName);


            OnIdeEvent();


            Bitmap bitmap = null;
            if (Options.EnableSolutionBadges) {

                // outer Grid with StackPanel
                var mainGrid = new Grid {
                    Width = 195,
                    Height = 105,
                    Background = Brushes.White
                };
                var stackPanel = new StackPanel() {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(5, 0, 5, 0)
                };
                mainGrid.Children.Add(stackPanel);

                // split up solution name and display every part in StackPanel
                var solutionName = Path.GetFileNameWithoutExtension(fileName);
                foreach (var solutionNamePart in solutionName.Split('.')) {
                    var textBlock = new TextBlock() {
                        Text = solutionNamePart,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, -5)
                    };
                    // shrink text to fit
                    var viewBox = new Viewbox {
                        StretchDirection = StretchDirection.DownOnly,
                        Stretch = Stretch.Uniform
                    };
                    viewBox.Child = textBlock;
                    stackPanel.Children.Add(viewBox);
                }

                // display branch information
                var innergrid = new Grid {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(5, 2, 5, 2)
                };

                // branch name
                var branchTextBlock = new TextBlock() {
                    Text = branch,
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Foreground = Brushes.DodgerBlue,
                };
                if (branch.StartsWith("Framework")) {
                    branchTextBlock.Foreground = Brushes.ForestGreen;
                } else if (branch.StartsWith("Time")) {
                    branchTextBlock.Foreground = Brushes.DarkOrchid;
                } else if (branch.StartsWith("Case")) {
                    branchTextBlock.Foreground = Brushes.DarkRed;
                } else if (branch.StartsWith("Billing")) {
                    branchTextBlock.Foreground = Brushes.Goldenrod;
                } else if (branch == "MAIN" || branch == "master") {
                    branchTextBlock.Foreground = Brushes.OrangeRed;
                    branchTextBlock.FontSize = 24;
                }

                // parent folder of branch
                var branchAreaTextBlock = new TextBlock() {
                    Text = branchArea,
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Foreground = Brushes.DarkGray,
                };

                // Main branch has no parent folder
                if (branchArea == string.Empty) {
                    branchTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                }

                innergrid.Children.Add(branchTextBlock);
                innergrid.Children.Add(branchAreaTextBlock);

                if (branchArea == string.Empty) {
                    // shrink text to fit
                    var branchviewBox = new Viewbox {
                        StretchDirection = StretchDirection.DownOnly,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Bottom
                    };
                    branchviewBox.Child = innergrid;
                    mainGrid.Children.Add(branchviewBox);
                } else {
                    mainGrid.Children.Add(innergrid);
                }

                // measure and arrange before converting into image
                mainGrid.Measure(new System.Windows.Size(195, 105));
                mainGrid.Arrange(new Rect(new System.Windows.Size(195, 105)));
                mainGrid.UpdateLayout();

                // convert visual into bitmap
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(195, 105, 96, 96, PixelFormats.Default);
                renderTargetBitmap.Render(mainGrid);
                BitmapSource bitmapSource = renderTargetBitmap;
                bitmap = ConvertToBitmap(bitmapSource);

            }

            // attach to solution events
            debuggerEvents.OnEnterBreakMode += OnIdeEvent;
            debuggerEvents.OnEnterRunMode += OnIdeEvent;
            debuggerEvents.OnEnterDesignMode += OnIdeEvent;
            debuggerEvents.OnContextChanged += OnIdeEvent;
            solutionEvents.AfterClosing += OnIdeEvent;
            solutionEvents.Opened += OnIdeEvent;
            solutionEvents.Renamed += OnIdeEvent;
            windowEvents.WindowCreated += OnIdeEvent;
            windowEvents.WindowClosing += OnIdeEvent;
            windowEvents.WindowActivated += OnIdeEvent;
            documentEvents.DocumentOpened += OnIdeEvent;
            documentEvents.DocumentClosing += OnIdeEvent;

            return bitmap;
        }

        private void OnHeadChanged(object sender, FileSystemEventArgs e) {
            dispatcher.Invoke(SolutionEvents_Opened);
        }

        private void OnIdeEvent(EnvDTE.Window gotfocus, EnvDTE.Window lostfocus) {
            OnIdeEvent();
        }

        private void OnIdeEvent(Document document) {
            OnIdeEvent();
        }

        private void OnIdeEvent(EnvDTE.Window window) {
            OnIdeEvent();
        }

        private void OnIdeEvent(string oldname) {
            OnIdeEvent();
        }

        private void OnIdeEvent(dbgEventReason reason) {
            OnIdeEvent();
        }

        private void OnIdeEvent(dbgEventReason reason, ref dbgExecutionAction executionaction) {
            OnIdeEvent();
        }

        private void OnIdeEvent(EnvDTE.Process newProc, Program newProg, EnvDTE.Thread newThread, EnvDTE.StackFrame newStkFrame) {
            OnIdeEvent();
        }

        private void OnIdeEvent() {
            SetMainWindowTitle(!Options.ChangeWindowTitles);
            if (Options.ChangeSolutionExplorerTitles) {
                DTE.ToolWindows.SolutionExplorer.Parent.Caption = string.Concat("Solution Explorer", currentCaptionSuffix);
            }
        }

        private const string dash = " - ";
        private const string dot = " ● ";

        private void SetMainWindowTitle(bool cleanOnly = false) {

            try {
                var cleanedCurrentCaption = CleanCaption();

                if (cleanOnly) {
                    Application.Current.MainWindow.Title = cleanedCurrentCaption;
                } else {
                    var firstDashIndex = cleanedCurrentCaption.IndexOf(dash);
                    if (firstDashIndex >= 0) {
                        Application.Current.MainWindow.Title = cleanedCurrentCaption.Insert(firstDashIndex, currentCaptionSuffix);
                    } else {
                        Application.Current.MainWindow.Title = string.Concat(cleanedCurrentCaption, currentCaptionSuffix);
                    }
                }
            } catch {
                //ignore errors in title changing - we don't want to break anybody!
            }
        }

        private string CleanCaption() {
            var currentCaption = DTE.MainWindow.Caption;
            var cleanedCurrentCaption = currentCaption;
            int firstDashIndex = 0;
            int firstDotIndex = 0;
            if (currentCaption.Contains(dot)) {
                cleanedCurrentCaption = currentCaption.Substring(0, currentCaption.IndexOf(dot));
                firstDashIndex = currentCaption.IndexOf(dash);
                if (firstDashIndex >= 0) {
                    cleanedCurrentCaption += currentCaption.Substring(firstDashIndex);
                }
            }
            firstDashIndex = currentCaption.IndexOf(dash);
            firstDotIndex = currentCaption.IndexOf(dot);
            var firstDashOrDotIndex = firstDashIndex;
            if (firstDotIndex >= 0) {
                firstDashOrDotIndex = Math.Min(firstDashIndex, firstDotIndex);
            }
            if (firstDashOrDotIndex >= 0 && !string.IsNullOrEmpty(DTE.Solution.FileName)) {
                var solutionName = Path.GetFileNameWithoutExtension(DTE.Solution.FileName);
                cleanedCurrentCaption = string.Concat(solutionName, cleanedCurrentCaption.Substring(firstDashOrDotIndex));
            }
            return cleanedCurrentCaption;
        }

        private Bitmap ConvertToBitmap(BitmapSource bitmapSource) {
            var width = bitmapSource.PixelWidth;
            var height = bitmapSource.PixelHeight;
            var stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
            var memoryBlockPointer = Marshal.AllocHGlobal(height * stride);
            bitmapSource.CopyPixels(new Int32Rect(0, 0, width, height), memoryBlockPointer, height * stride, stride);
            var bitmap = new Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, memoryBlockPointer);
            return bitmap;
        }

        #endregion

        #region Fix bug in Windows API Code Pack

        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessage", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage([MarshalAs(UnmanagedType.LPWStr)] string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr ChangeWindowMessageFilter(uint message, uint dwFlag);

        private const uint MSGFLT_ADD = 1;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_SYSCOMMAND = 0x112;
        private const uint WM_ACTIVATE = 0x0006;

        /// <summary>
        /// Specifies that the taskbar-related windows messages should
        /// pass through the Windows UIPI mechanism even if the process is
        /// running elevated. Calling this method is not required unless the
        /// process is running elevated.
        /// </summary>
        private static void AllowTaskbarWindowMessagesThroughUIPI() {
            uint WM_TaskbarButtonCreated = RegisterWindowMessage("TaskbarButtonCreated");

            ChangeWindowMessageFilter(WM_TaskbarButtonCreated, MSGFLT_ADD);
            ChangeWindowMessageFilter(WM_COMMAND, MSGFLT_ADD);
            ChangeWindowMessageFilter(WM_SYSCOMMAND, MSGFLT_ADD);
            ChangeWindowMessageFilter(WM_ACTIVATE, MSGFLT_ADD);
        }

        #endregion Fix bug in Windows API Code Pack

    }
}
