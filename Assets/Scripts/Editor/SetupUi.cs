using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Dialogs of the setup menus. Skipped (logged instead) while the <see cref="EditorCommandRunner"/>
    /// executes commands, because a modal dialog would block the editor until someone clicks it.
    /// </summary>
    public static class SetupUi
    {
        public static bool Dialog(string title, string message, string ok)
        {
            if (EditorCommandRunner.IsSilent || Application.isBatchMode)
            {
                Debug.Log($"[{title}] {message}");
                return true;
            }

            return EditorUtility.DisplayDialog(title, message, ok);
        }
    }
}
