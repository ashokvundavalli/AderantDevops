using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using Aderant.Build.Logging;
using Version = System.Version;

namespace Aderant.Build.Tasks.BuildTime {
    public class BuildException : Exception {
        public BuildException(string message)
            : base(message) {
        }
    }

    internal sealed class BuildMasterPSHost : PSHost {
        private readonly ILogger log;
        private readonly Guid UniqueId = Guid.NewGuid();
        private PSHostUserInterface ui;

        //private readonly BuildMasterPSHostUserInterface ui = new BuildMasterPSHostUserInterface();

        public BuildMasterPSHost(ILogger log) {
            this.log = log;
            //  this.ui.MessageLogged += this.Ui_MessageLogged;
        }

        //public event EventHandler<LogMessageEventArgs> MessageLogged;
        //public event EventHandler<ShouldExitEventArgs> ShouldExit;

        public override PSHostUserInterface UI {
            get { return ui ?? (ui = new Interface(log)); }
        }

        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId => UniqueId;
        public override string Name => "BuildMaster";

        public override Version Version => typeof(BuildMasterPSHost).Assembly.GetName().Version;
        //public override PSHostUserInterface UI => this.ui;

        public override void EnterNestedPrompt() {
        }

        public override void ExitNestedPrompt() {
        }

        public override void NotifyBeginApplication() {
        }

        public override void NotifyEndApplication() {
        }

        public override void SetShouldExit(int exitCode) {
            //var handler = this.ShouldExit;
            //if (handler != null) {
        }

        //     handler(this, new ShouldExitEventArgs(exitCode));
    }

    internal class Interface : PSHostUserInterface {
        private readonly ILogger log;
        private PSHostRawUserInterface myraw = new RawRawUserInterface();

        public Interface(ILogger log) {
            this.log = log;
        }

        public override string ReadLine() {
            return null;
        }

        public override SecureString ReadLineAsSecureString() {
            return null;
        }

        public override void Write(string value) {
            log.Info(value);
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) {
            log.Info(value);
        }

        public override void WriteLine(string value) {
            log.Info(value);
        }

        public override void WriteErrorLine(string value) {
            log.Error(value);
        }

        public override void WriteDebugLine(string message) {
            log.Debug(message);
        }

        public override void WriteProgress(long sourceId, ProgressRecord record) {
        }

        public override void WriteVerboseLine(string message) {
            log.Debug(message);
        }

        public override void WriteWarningLine(string message) {
            log.Warning(message);
        }

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions) {
            return null;
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName) {
            return null;
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options) {
            return null;
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice) {
            return 0;
        }

        public override PSHostRawUserInterface RawUI {
            get { return myraw; }
        }
    }

    internal class RawRawUserInterface : PSHostRawUserInterface {
        public override ConsoleColor BackgroundColor { get; set; }
        public override Size BufferSize { get; set; }
        public override Coordinates CursorPosition { get; set; }
        public override int CursorSize { get; set; }
        public override ConsoleColor ForegroundColor { get; set; }
        public override bool KeyAvailable => false;
        public override Size MaxPhysicalWindowSize => new Size(1000, 1000);
        public override Size MaxWindowSize => new Size(1000, 1000);
        public override Coordinates WindowPosition { get; set; }
        public override Size WindowSize { get; set; } = new Size(100, 100);
        public override string WindowTitle { get; set; }

        public override void FlushInputBuffer() {
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle) {
            return new BufferCell[0, 0];
        }

        public override KeyInfo ReadKey(ReadKeyOptions options) {
            throw new NotImplementedException();
        }

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill) {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill) {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) {
            throw new NotImplementedException();
        }
    }
}