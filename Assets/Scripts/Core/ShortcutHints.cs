namespace Game.Core
{
    /// <summary>
    /// Display names of the global shortcut keys (quick save/load, debug panel) for help texts.
    /// The owner of the keys (DebugPanel) publishes the keys it actually listens to - they differ per
    /// platform (WebGL: the browser reserves F1/F5) and per keyboard layout.
    /// </summary>
    public static class ShortcutHints
    {
        public static string QuickSave { get; set; } = "F5";
        public static string QuickLoad { get; set; } = "F9";
        public static string DebugPanel { get; set; } = "F1";
    }
}
