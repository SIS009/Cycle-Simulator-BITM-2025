using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using SBPScripts;

/// <summary>
/// Editor tool that places a configured <see cref="CycleSoundManager"/> in each of the
/// four levels, so the sounds do not have to be wired up by hand four times.
///
/// Run it from: Tools > Cycle Simulator > Set Up Cycle Sounds In All Levels
///
/// The tool is safe to run more than once. A level that already has a manager is
/// updated in place instead of gaining a second one.
/// </summary>
public static class CycleSoundSetup
{
    private const string SoundObjectName = "Cycle Sounds";
    private const string ScenesFolder = "Assets/_CycleSimulator/Level/Scenes";
    private const string AudioFolder = "Assets/_CycleSimulator/Audio/Sound";

    /// <summary>Per-level rolling sound settings. All four share the one concrete clip
    /// that exists today, pitched differently so the levels do not sound identical.</summary>
    private struct LevelSetup
    {
        public string sceneName;
        public string surfaceName;
        public float pitchScale;
        public float volumeScale;
    }

    private static readonly LevelSetup[] Levels =
    {
        new LevelSetup { sceneName = "Earth",  surfaceName = "Concrete",    pitchScale = 1.00f, volumeScale = 1.00f },
        new LevelSetup { sceneName = "Mars",   surfaceName = "Regolith",    pitchScale = 0.90f, volumeScale = 0.85f },
        new LevelSetup { sceneName = "Europa", surfaceName = "Ice",         pitchScale = 1.15f, volumeScale = 0.90f },
        new LevelSetup { sceneName = "Titan",  surfaceName = "Frozen Sand", pitchScale = 1.05f, volumeScale = 0.90f }
    };

    [MenuItem("Tools/Cycle Simulator/Set Up Cycle Sounds In All Levels")]
    private static void SetUpAllLevels()
    {
        // Never discard unsaved work in the scene the user is currently looking at.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        AudioClip rollingClip = LoadClip("cycle_moving_on_concrete.wav");
        AudioClip accidentClip = LoadClip("accident.mp3");
        AudioClip hornClip = LoadClip("car_horn_single.wav");

        string sceneToRestore = EditorSceneManager.GetActiveScene().path;
        List<string> report = new List<string>();

        try
        {
            for (int i = 0; i < Levels.Length; i++)
            {
                LevelSetup level = Levels[i];
                string scenePath = $"{ScenesFolder}/{level.sceneName}.unity";

                EditorUtility.DisplayProgressBar(
                    "Cycle Sound Setup",
                    $"Setting up {level.sceneName}...",
                    (float)i / Levels.Length);

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    report.Add($"{level.sceneName}: SKIPPED, scene not found at {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                string result = ConfigureScene(
                    level, rollingClip, accidentClip, hornClip);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                report.Add($"{level.sceneName}: {result}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (!string.IsNullOrEmpty(sceneToRestore))
            {
                EditorSceneManager.OpenScene(sceneToRestore, OpenSceneMode.Single);
            }
        }

        Debug.Log("Cycle sound setup finished.\n" + string.Join("\n", report));
    }

    private static string ConfigureScene(
        LevelSetup level,
        AudioClip rollingClip,
        AudioClip accidentClip,
        AudioClip hornClip)
    {
        CycleSoundManager manager = Object.FindObjectOfType<CycleSoundManager>();
        bool created = false;

        if (manager == null)
        {
            GameObject holder = new GameObject(SoundObjectName);
            manager = holder.AddComponent<CycleSoundManager>();
            created = true;
        }

        SerializedObject serializedManager = new SerializedObject(manager);

        serializedManager.FindProperty("accidentClip").objectReferenceValue = accidentClip;
        serializedManager.FindProperty("hornClip").objectReferenceValue = hornClip;

        // Assign the bicycle directly where one exists. Automatic lookup stays on as
        // a fallback for bicycles that are spawned at run time.
        BicycleController bicycle = Object.FindObjectOfType<BicycleController>();
        serializedManager.FindProperty("bicycleController").objectReferenceValue = bicycle;
        serializedManager.FindProperty("findBicycleAutomatically").boolValue = true;

        // One fallback surface entry per level, tuned so the four levels differ.
        SerializedProperty surfaces = serializedManager.FindProperty("surfaces");
        surfaces.arraySize = 1;

        SerializedProperty defaultSurface = surfaces.GetArrayElementAtIndex(0);
        defaultSurface.FindPropertyRelative("name").stringValue = level.surfaceName;
        defaultSurface.FindPropertyRelative("groundTag").stringValue = string.Empty;
        defaultSurface.FindPropertyRelative("rollingClip").objectReferenceValue = rollingClip;
        defaultSurface.FindPropertyRelative("volumeScale").floatValue = level.volumeScale;
        defaultSurface.FindPropertyRelative("pitchScale").floatValue = level.pitchScale;

        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        string accidentResult = WireAccidentSound(manager);

        return created
            ? $"created '{SoundObjectName}' ({level.surfaceName}), {accidentResult}"
            : $"updated existing manager ({level.surfaceName}), {accidentResult}";
    }

    /// <summary>
    /// Points the bicycle's accident event at the sound manager, so a crash plays
    /// accident.mp3 without any manual inspector work.
    /// </summary>
    private static string WireAccidentSound(CycleSoundManager manager)
    {
        BicycleAccidentReset accidentReset = Object.FindObjectOfType<BicycleAccidentReset>();

        if (accidentReset == null)
        {
            return "no BicycleAccidentReset in this scene";
        }

        if (accidentReset.onAccident == null)
        {
            accidentReset.onAccident = new UnityEvent();
        }

        // Do not add the same listener twice when the tool is run again.
        for (int i = 0; i < accidentReset.onAccident.GetPersistentEventCount(); i++)
        {
            if (accidentReset.onAccident.GetPersistentTarget(i) == manager &&
                accidentReset.onAccident.GetPersistentMethodName(i) == nameof(CycleSoundManager.PlayAccident))
            {
                return "accident sound already wired";
            }
        }

        UnityEventTools.AddPersistentListener(
            accidentReset.onAccident,
            new UnityAction(manager.PlayAccident));

        EditorUtility.SetDirty(accidentReset);

        return "accident sound wired";
    }

    private static AudioClip LoadClip(string fileName)
    {
        string path = $"{AudioFolder}/{fileName}";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"Cycle sound setup could not find the clip at {path}.");
        }

        return clip;
    }
}
