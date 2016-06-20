using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Aderant.DeveloperTools.XamlAdorner {
    /// <summary>
    /// Establishes an <see cref="ICompletionSourceProvider"/> for images.
    /// </summary>
    // Bug: Disabled CompletionSourceProvider (MEF attributes!) as ReSharper overwrites the VS Intellisense popup with its own and doesn't respect added CompletionSource items.
    // VS doesn't show Intellisense in XAML when using '+' for nested static classes (which we use with ExpertImages) so disabling the ReSharper Intellisense for XAML won't help.
    //[Export(typeof(ICompletionSourceProvider))] 
    [ContentType("text")]
    [Name("Image Completion")]
    public class ImageCompletionProvider : ICompletionSourceProvider {

        //[Import]
        public ITextStructureNavigatorSelectorService TextStructureNavigatorSelector;

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) {
            return new ImageCompletionSource(textBuffer, TextStructureNavigatorSelector.GetTextStructureNavigator(textBuffer));
        }
    }
}
