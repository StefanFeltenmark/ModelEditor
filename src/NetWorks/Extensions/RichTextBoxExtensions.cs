namespace ModelEditorApp.Extensions
{
    public static class RichTextBoxExtensions
    {
        public static int GetFirstVisibleLineIndex(this RichTextBox rtb)
        {
            int firstCharIndex = rtb.GetCharIndexFromPosition(new Point(0, 0));
            return rtb.GetLineFromCharIndex(firstCharIndex);
        }
    }
}