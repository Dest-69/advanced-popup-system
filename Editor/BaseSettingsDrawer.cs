using System.Linq;
using System.Reflection;
using AdvancedPS.Core.System;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    //[CustomPropertyDrawer(typeof(BaseSettings), true)]
    public class BaseSettingsDrawer : PropertyDrawer
    {
         public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = position.y;

            var targetType = fieldInfo.FieldType;
            var fields = targetType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.Name != "OnAnimationEnd")
                .OrderBy(f => f.MetadataToken)
                .ToList();

            var onAnimationEndField = property.FindPropertyRelative("OnAnimationEnd");

            foreach (var field in fields)
            {
                var prop = property.FindPropertyRelative(field.Name);
                if (prop != null)
                {
                    GUIContent labelContent = new GUIContent(ObjectNames.NicifyVariableName(field.Name));

                    Rect labelRect = new Rect(position.x, y, position.width, lineHeight);
                    EditorGUI.LabelField(labelRect, labelContent);
                    y += lineHeight + spacing;

                    float fieldHeight = EditorGUI.GetPropertyHeight(prop, true);
                    Rect fieldRect = new Rect(position.x, y, position.width, fieldHeight);

                    EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
                    y += fieldHeight + spacing;
                }
            }

            if (onAnimationEndField != null)
            {
                GUIContent labelContent = new GUIContent(ObjectNames.NicifyVariableName(onAnimationEndField.name));

                Rect labelRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.LabelField(labelRect, labelContent);
                y += lineHeight + spacing;

                float fieldHeight = EditorGUI.GetPropertyHeight(onAnimationEndField, true);
                Rect fieldRect = new Rect(position.x, y, position.width, fieldHeight);

                EditorGUI.PropertyField(fieldRect, onAnimationEndField, GUIContent.none);
                y += fieldHeight + spacing;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalHeight = 0f;

            var targetType = fieldInfo.FieldType;
            var fields = targetType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.Name != "OnAnimationEnd")
                .OrderBy(f => f.MetadataToken)
                .ToList();

            foreach (var field in fields)
            {
                var prop = property.FindPropertyRelative(field.Name);
                if (prop != null)
                {
                    float fieldHeight = EditorGUI.GetPropertyHeight(prop, true);
                    totalHeight += fieldHeight + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            var onAnimationEndField = property.FindPropertyRelative("OnAnimationEnd");
            if (onAnimationEndField != null)
            {
                float fieldHeight = EditorGUI.GetPropertyHeight(onAnimationEndField, true);
                totalHeight += fieldHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return totalHeight;
        }
    }
}