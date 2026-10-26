using UnityEditor;
using UnityEngine;

namespace ZincWhiskers
{
    static class LookMenu
    {
        [MenuItem("Zinc Whiskers/World Look Settings")]
        static void SelectWorldLookSettings()
        {
            var settings = Resources.Load<WorldLookSettings>("WorldLookSettings");
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
