#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(ItemManager))]
public class ItemManagerEditor : Editor
{
    private SerializedProperty definitionsProperty;
    private ReorderableList definitionsList;

    private static readonly GUIContent[] FieldLabels =
    {
        new GUIContent("Item Selection"),
        new GUIContent("Icon"),
        new GUIContent("Prefab"),
        new GUIContent("Hand Prefab"),
        new GUIContent("Alternate Prefab"),
        new GUIContent("Spawn Point")
    };

    private static readonly string[] FieldNames =
    {
        "debugSelection",
        "icon",
        "prefab",
        "handPrefab",
        "alternatePrefab",
        "spawnPoint"
    };

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
            if (target is ItemManager manager)
            {
                manager.SyncItemsFromDefinitions();
                EditorUtility.SetDirty(manager);
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
        float height = EditorGUIUtility.singleLineHeight + ExtraSpacing;

        if (!element.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing + ExtraSpacing;

        for (int i = 0; i < FieldNames.Length; i++)
        {
            SerializedProperty child = element.FindPropertyRelative(FieldNames[i]);
            if (child == null)
            {
                continue;
            }

            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        height += ExtraSpacing;
        return height;
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (definitionsProperty == null || index < 0 || index >= definitionsProperty.arraySize)
        {
            return;
        }

        SerializedProperty element = definitionsProperty.GetArrayElementAtIndex(index);

        rect.height = EditorGUIUtility.singleLineHeight;
        element.isExpanded = EditorGUI.Foldout(rect, element.isExpanded, $"Element {index}", true);

        if (!element.isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        float y = rect.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + ExtraSpacing;

        for (int i = 0; i < FieldNames.Length; i++)
        {
            SerializedProperty child = element.FindPropertyRelative(FieldNames[i]);
            if (child == null)
            {
                continue;
            }

            float childHeight = EditorGUI.GetPropertyHeight(child, true);
            Rect fieldRect = new Rect(rect.x, y, rect.width, childHeight);
            EditorGUI.PropertyField(fieldRect, child, FieldLabels[i], true);
            y += childHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        EditorGUI.indentLevel--;
    }
}
#endif



