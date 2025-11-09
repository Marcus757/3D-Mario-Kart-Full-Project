using UnityEditor;
using UnityEngine;
using System.Reflection;

public class CopyAllComponentsEditor : EditorWindow
{
    private static GameObject sourceObject;

    [MenuItem("GameObject/Copy All Components", false, 0)]
    private static void CopyAllComponents()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("No GameObject selected to copy components from.");
            return;
        }

        sourceObject = Selection.activeGameObject;
        Debug.Log($"Copied components from: {sourceObject.name}");
    }

    [MenuItem("GameObject/Paste All Components", false, 1)]
    private static void PasteAllComponents()
    {
        if (sourceObject == null)
        {
            Debug.LogWarning("No source GameObject. Use 'Copy All Components' first.");
            return;
        }

        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("No GameObject selected to paste components onto.");
            return;
        }

        GameObject target = Selection.activeGameObject;
        CopyComponents(sourceObject, target);
        Debug.Log($"Pasted components from '{sourceObject.name}' to '{target.name}'");
    }

    private static void CopyComponents(GameObject source, GameObject destination)
    {
        foreach (var sourceComp in source.GetComponents<Component>())
        {
            if (sourceComp is Transform) continue; // skip Transform

            // Add component if not already present
            var destComp = destination.GetComponent(sourceComp.GetType());
            if (destComp == null)
                destComp = destination.AddComponent(sourceComp.GetType());

            // Copy all field values (including private ones)
            var type = sourceComp.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                field.SetValue(destComp, field.GetValue(sourceComp));
            }
        }
    }
}
