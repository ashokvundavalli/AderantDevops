namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild MessageTask element.
    /// </summary>
    public class Message : Element {

        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="text">The message.</param>
        public Message(string text) {
            Text = text;
        }

        /// <summary>
        /// Gets the text of the message
        /// </summary>
        public string Text {
            get;
            private set;
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}