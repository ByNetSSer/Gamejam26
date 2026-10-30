using System.IO;
using MenuUIBase.Audio;
using UnityEditor;
using UnityEngine;

namespace MenuUIBase.Editor
{
    [CustomEditor(typeof(MusicPlaylist))]
    public sealed class MusicPlaylistEditor : UnityEditor.Editor
    {
        AudioClip clipToAdd;
        const string CueFolder = "Assets/Proyect/Assets/Audio/Music";
        const string PlaylistFolder = "Assets/Proyect/Assets/Audio/Resources/Music/";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Arrastra una canción al campo inferior y pulsa Añadir. También puedes editar las pistas existentes.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tracks"), new GUIContent("Canciones (Audio Cues)"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crossfadeSeconds"), new GUIContent("Transición (segundos)"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            clipToAdd = (AudioClip)EditorGUILayout.ObjectField("Canción nueva", clipToAdd, typeof(AudioClip), false);
            using (new EditorGUI.DisabledScope(clipToAdd == null))
                if (GUILayout.Button("Añadir canción a esta lista")) AddClip();
        }

        void AddClip()
        {
            serializedObject.Update();
            var tracks = serializedObject.FindProperty("tracks");
            for (int i = 0; i < tracks.arraySize; i++)
            {
                var existing = tracks.GetArrayElementAtIndex(i).objectReferenceValue as AudioCue;
                if (existing != null && existing.Clip == clipToAdd)
                {
                    EditorUtility.DisplayDialog("Ya está en la lista", "Esta canción ya pertenece a esta lista.", "Aceptar");
                    return;
                }
            }
            AudioCue cue = null;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioCue"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<AudioCue>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.Channel == AudioChannel.Music && candidate.Clip == clipToAdd)
                { cue = candidate; break; }
            }
            if (cue == null)
            {
                if (!AssetDatabase.IsValidFolder(CueFolder))
                {
                    EditorUtility.DisplayDialog("Carpeta no encontrada", "No existe la carpeta de cues: " + CueFolder, "Aceptar");
                    return;
                }
                cue = CreateInstance<AudioCue>();
                var data = new SerializedObject(cue);
                data.FindProperty("clip").objectReferenceValue = clipToAdd;
                data.FindProperty("channel").enumValueIndex = (int)AudioChannel.Music;
                data.FindProperty("loop").boolValue = false;
                data.FindProperty("fadeDuration").floatValue = 4f;
                data.ApplyModifiedPropertiesWithoutUndo();
                string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(CueFolder, clipToAdd.name + " Music.asset").Replace('\\', '/'));
                AssetDatabase.CreateAsset(cue, path);
            }
            Undo.RecordObject(target, "Añadir canción a la lista");
            int index = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(index);
            tracks.GetArrayElementAtIndex(index).objectReferenceValue = cue;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            clipToAdd = null;
        }

        static void SelectPlaylist(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MusicPlaylist>(PlaylistFolder + name + ".asset");
            if (asset == null) { Debug.LogWarning("No se encontró la lista " + name); return; }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        [MenuItem("Tools/Runner/Música/Menú")] static void Menu() => SelectPlaylist("MenuPlaylist");
        [MenuItem("Tools/Runner/Música/Salas seguras")] static void Safe() => SelectPlaylist("SafeRoomsPlaylist");
        [MenuItem("Tools/Runner/Música/Gameplay tranquilo")] static void Calm() => SelectPlaylist("CalmPlaylist");
        [MenuItem("Tools/Runner/Música/Gameplay intenso")] static void Intense() => SelectPlaylist("IntensePlaylist");
        [MenuItem("Tools/Runner/Música/Último minuto")] static void Urgent() => SelectPlaylist("LastMinutePlaylist");
    }
}
