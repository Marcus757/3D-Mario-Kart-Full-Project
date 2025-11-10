#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(ItemCatalog))]
public class ItemCatalogEditor : Editor
{
    private SerializedProperty definitionsProperty;
    private ReorderableList definitionsList;

    private const float ExtraSpacing = 4f;

    private void OnEnable()
    {
        definitionsProperty = serializedObject.FindProperty("itemDefinitions");

        if (definitionsProperty != null)
        {
            definitionsList = new ReorderableList(serializedObject, definitionsProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Item Definitions"),
                elementHeightCallback = GetElementHeight,
                drawElementCallback = DrawElement
            };
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (definitionsProperty != null && iterator.propertyPath == definitionsProperty.propertyPath)
            {
                if (definitionsList != null)
                {
                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing + ExtraSpacing);
                    definitionsList.DoLayoutList();
                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            if (target is ItemCatalog catalog)
            {
                catalog.Initialize();
                EditorUtility.SetDirty(catalog);
            }
        }
    }

    private float GetElementHeight(int index)
    {
        if (definitionsProperty == null || index < 0 || index >= definitionsProperty.arraySize)
        {
            return EditorGUIUtility.singleLineHeight + ExtraSpacing;
        }

        SerializedProperty element = definitionsProperty.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, true) + ExtraSpacing;
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (definitionsProperty == null || index < 0 || index >= definitionsProperty.arraySize)
        {
            return;
        }

        SerializedProperty element = definitionsProperty.GetArrayElementAtIndex(index);

        rect.height = EditorGUI.GetPropertyHeight(element, true);
        rect = EditorGUI.IndentedRect(rect);
        EditorGUI.PropertyField(rect, element, GUIContent.none, true);
    }
}
#endif

