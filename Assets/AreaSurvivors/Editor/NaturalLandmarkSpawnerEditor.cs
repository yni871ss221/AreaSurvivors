using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    [CustomEditor(typeof(NaturalLandmarkSpawner))]
    public sealed class NaturalLandmarkSpawnerEditor : UnityEditor.Editor
    {
        SerializedProperty seed;
        SerializedProperty randomizeSeedEachRun;
        SerializedProperty edgePaddingCells;
        SerializedProperty separationCells;
        SerializedProperty maxPlacementAttemptsPerObject;
        SerializedProperty addOutline;
        SerializedProperty outlineColor;
        SerializedProperty outlineThickness;
        SerializedProperty landmarks;
        SerializedProperty placementBands;

        bool showPlacementRules = true;
        bool showSpawnSettings;
        bool showLandmarkDefinitions;
        bool showVisualSettings;

        void OnEnable()
        {
            seed = serializedObject.FindProperty("seed");
            randomizeSeedEachRun = serializedObject.FindProperty("randomizeSeedEachRun");
            edgePaddingCells = serializedObject.FindProperty("edgePaddingCells");
            separationCells = serializedObject.FindProperty("separationCells");
            maxPlacementAttemptsPerObject = serializedObject.FindProperty("maxPlacementAttemptsPerObject");
            addOutline = serializedObject.FindProperty("addOutline");
            outlineColor = serializedObject.FindProperty("outlineColor");
            outlineThickness = serializedObject.FindProperty("outlineThickness");
            landmarks = serializedObject.FindProperty("landmarks");
            placementBands = serializedObject.FindProperty("placementBands");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPlacementRules();
            EditorGUILayout.Space(8f);
            DrawSpawnSettings();
            DrawVisualSettings();
            DrawLandmarkDefinitions();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawPlacementRules()
        {
            showPlacementRules = EditorGUILayout.Foldout(showPlacementRules, "Placement Count Rules", true);
            if (!showPlacementRules) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("中心塔からの距離区間ごとの配置数", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(separationCells, new GUIContent("Gap Cells", "自然物同士が触れ合わないように空けるセル数"));
                EditorGUILayout.Space(4f);

                for (int i = 0; i < placementBands.arraySize; i++)
                {
                    DrawPlacementBand(placementBands.GetArrayElementAtIndex(i), i);
                    EditorGUILayout.Space(4f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Distance Band"))
                    {
                        int index = placementBands.arraySize;
                        placementBands.InsertArrayElementAtIndex(index);
                        var band = placementBands.GetArrayElementAtIndex(index);
                        band.FindPropertyRelative("name").stringValue = "New Band";
                        band.FindPropertyRelative("minDistanceCells").intValue = 0;
                        band.FindPropertyRelative("maxDistanceCells").intValue = 10;
                        band.FindPropertyRelative("entries").arraySize = 0;
                    }
                }
            }
        }

        void DrawPlacementBand(SerializedProperty band, int bandIndex)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var name = band.FindPropertyRelative("name");
                var minDistance = band.FindPropertyRelative("minDistanceCells");
                var maxDistance = band.FindPropertyRelative("maxDistanceCells");
                var entries = band.FindPropertyRelative("entries");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(name, GUIContent.none);
                    if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                    {
                        placementBands.DeleteArrayElementAtIndex(bandIndex);
                        return;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(minDistance, new GUIContent("Min"), GUILayout.MinWidth(90f));
                    EditorGUILayout.PropertyField(maxDistance, new GUIContent("Max"), GUILayout.MinWidth(90f));
                }

                EditorGUILayout.LabelField("配置数", EditorStyles.miniBoldLabel);
                for (int i = 0; i < entries.arraySize; i++)
                {
                    DrawPlacementEntry(entries, i);
                }

                if (GUILayout.Button("Add Object Count"))
                {
                    int index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                    var entry = entries.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("landmarkName").stringValue = FirstLandmarkName();
                    entry.FindPropertyRelative("count").intValue = 1;
                }
            }
        }

        void DrawPlacementEntry(SerializedProperty entries, int index)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            var landmarkName = entry.FindPropertyRelative("landmarkName");
            var count = entry.FindPropertyRelative("count");

            using (new EditorGUILayout.HorizontalScope())
            {
                string[] names = LandmarkNames();
                if (names.Length > 0)
                {
                    int current = Mathf.Max(0, System.Array.IndexOf(names, landmarkName.stringValue));
                    int selected = EditorGUILayout.Popup(current, names);
                    landmarkName.stringValue = names[selected];
                }
                else
                {
                    EditorGUILayout.PropertyField(landmarkName, GUIContent.none);
                }

                count.intValue = Mathf.Max(0, EditorGUILayout.IntField(count.intValue, GUILayout.Width(56f)));
                GUILayout.Label("個", GUILayout.Width(18f));

                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    entries.DeleteArrayElementAtIndex(index);
                }
            }
        }

        void DrawSpawnSettings()
        {
            showSpawnSettings = EditorGUILayout.Foldout(showSpawnSettings, "Spawn Settings", true);
            if (!showSpawnSettings) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(seed);
                EditorGUILayout.PropertyField(randomizeSeedEachRun);
                EditorGUILayout.PropertyField(edgePaddingCells);
                EditorGUILayout.PropertyField(maxPlacementAttemptsPerObject);
            }
        }

        void DrawVisualSettings()
        {
            showVisualSettings = EditorGUILayout.Foldout(showVisualSettings, "Visual Settings", true);
            if (!showVisualSettings) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(addOutline);
                using (new EditorGUI.DisabledScope(!addOutline.boolValue))
                {
                    EditorGUILayout.PropertyField(outlineColor);
                    EditorGUILayout.PropertyField(outlineThickness);
                }
            }
        }

        void DrawLandmarkDefinitions()
        {
            showLandmarkDefinitions = EditorGUILayout.Foldout(showLandmarkDefinitions, "Advanced Landmark Definitions", true);
            if (!showLandmarkDefinitions) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(landmarks, true);
            }
        }

        string[] LandmarkNames()
        {
            if (landmarks == null || landmarks.arraySize == 0) return System.Array.Empty<string>();

            var names = new string[landmarks.arraySize];
            for (int i = 0; i < landmarks.arraySize; i++)
            {
                var element = landmarks.GetArrayElementAtIndex(i);
                names[i] = element.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(names[i])) names[i] = $"Landmark {i}";
            }

            return names;
        }

        string FirstLandmarkName()
        {
            var names = LandmarkNames();
            return names.Length > 0 ? names[0] : string.Empty;
        }
    }
}
