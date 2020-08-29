using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

public class FaultyFinderWindow : EditorWindow
{
    public class Fault
    {
        public string Message;
        public System.Type Type;
    }

    public class FaultyPrefab
    {
        public GameObject GameObject;
        public List<Fault> Faults = new List<Fault>();
        public bool IsExpanded = false;
    }

    public const float LeftPanelWidth = 256;

    [MenuItem("Window/Faulty Finder")]
    public static void ShowWindow()
    {
        var window = GetWindow(typeof(FaultyFinderWindow));
        window.titleContent = new GUIContent("Faulty Finder", Resources.Load<Texture>("FaultyFindexIcon"));
        window.minSize = new Vector2(512, 512);
        window.Show();
    }

    private List<FaultyPrefab> FoundGameObjects = new List<FaultyPrefab>();
    private Vector2 CurrentScroll;
    private string TitleLabelText = string.Empty;

    public void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(LeftPanelWidth))) {

                EditorGUILayout.LabelField("Fault to find", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                using (var scrollView = new EditorGUILayout.ScrollViewScope(Vector2.zero)) {
                    if (GUILayout.Button("Removed scripts")) {
                        TitleLabelText = "Objects with removed scripts";
                        FoundGameObjects = FindObjectsWithMissingScripts(FindAllProjectPrefabs());
                    }

                    if (GUILayout.Button("Missing required components")) {
                        TitleLabelText = "Objects with missing required components";
                        FoundGameObjects = FindObjectsWithMissingRequiredComponents(FindAllProjectPrefabs());
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                EditorGUILayout.LabelField(TitleLabelText, EditorStyles.boldLabel);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(CurrentScroll)) {
                    CurrentScroll = scrollView.scrollPosition;

                    if (FoundGameObjects == null || FoundGameObjects.Count == 0) {
                        return;
                    }

                    var foldoutStyle = new GUIStyle(EditorStyles.foldout) {
                        fontStyle = FontStyle.Bold
                    };

                    var imageFoldoutStyle = new GUIStyle(EditorStyles.label) {
                        margin = new RectOffset(0, 0, 0, 0),
                        padding = new RectOffset(0, 0, 0, 0)
                    };

                    foreach (var faultyPrefab in FoundGameObjects) {

                        using (new EditorGUILayout.HorizontalScope()) {
                            faultyPrefab.IsExpanded = EditorGUILayout.Foldout(faultyPrefab.IsExpanded, faultyPrefab.GameObject.name, foldoutStyle);

                            if (GUILayout.Button(new GUIContent(Resources.Load<Texture>("ShowInProjectIcon"), "Open in project view"), EditorStyles.label, GUILayout.MaxWidth(18))) {
                                ProjectWindowUtil.ShowCreatedAsset(faultyPrefab.GameObject);
                                EditorGUIUtility.PingObject(faultyPrefab.GameObject);
                            }
                        }

                        if (faultyPrefab.IsExpanded) {
                            using (new EditorGUI.IndentLevelScope(increment: 1)) {
                                foreach (var fault in faultyPrefab.Faults) {
                                    using (new EditorGUILayout.HorizontalScope()) {
                                        EditorGUILayout.LabelField(new GUIContent(EditorGUIUtility.ObjectContent(null, fault.Type).image), imageFoldoutStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(32));
                                        EditorGUILayout.LabelField(fault.Message);
                                    }
                                }
                                EditorGUILayout.Space();
                            }
                        }
                    }
                }
            }
        }
    }

    private List<GameObject> FindAllProjectPrefabs()
    {
        return AssetDatabase.GetAllAssetPaths()
            .Where(p => p.Contains(".prefab"))
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
            .ToList();
    }

    private List<FaultyPrefab> FindObjectsWithMissingScripts(List<GameObject> gameObjects)
    {
        var result = new List<FaultyPrefab>();
        
        foreach (var gameObject in gameObjects) {
            var missingComponents = GetMissingComponent(gameObject);
            if(missingComponents.Count > 0) { 
                result.Add(new FaultyPrefab { GameObject = gameObject, Faults = missingComponents });
            }
        }

        return result;
    }

    private List<Fault> GetMissingComponent(GameObject gameObject)
    {
        var result = new List<Fault>();
        foreach (var component in gameObject.GetComponents<Component>()) {
            if (component == null) {
                result.Add(new Fault { Message = $"{gameObject.name} has component of removed script", Type = typeof(Component)});
            }
        }

        foreach (Transform child in gameObject.transform) {
            result.AddRange(GetMissingComponent(child.gameObject));
        }

        return result;
    }

    private List<FaultyPrefab> FindObjectsWithMissingRequiredComponents(List<GameObject> gameObjects)
    {
        var result = new List<FaultyPrefab>();

        foreach (var gameObject in gameObjects) {
            var missingComponents = GetMissingRequiredComponents(gameObject);
            if(missingComponents.Count > 0) {
                result.Add(new FaultyPrefab { GameObject = gameObject, Faults = missingComponents });
            }      
        }

        return result;
    }

    private List<Fault> GetMissingRequiredComponents(GameObject gameObject)
    {
        var result = new List<Fault>();

        var requiredComponents = GetRequiredComponentsForGameObject(gameObject);
        var components = gameObject.GetComponents<Component>()
            .Where(c => c != null)
            .Select(c => c.GetType())
            .ToList();

        foreach(var requiredComponent in requiredComponents) {
            bool found = false;
            foreach (var component in components) {
                if (requiredComponent.IsAssignableFrom(component)) {
                    found = true;
                }
            }

            if (!found) {
                result.Add(new Fault { Message = $"{gameObject.name} is missing {requiredComponent.Name}", Type = requiredComponent });
            }
        }

        foreach (Transform child in gameObject.transform) {
            result.AddRange(GetMissingRequiredComponents(child.gameObject));
            
        }

        return result;
    }

    private List<System.Type> GetRequiredComponentsForGameObject(GameObject gameObject)
    {
        return gameObject.GetComponents<MonoBehaviour>()
            .SelectMany(c => GetRequiredComponentsForComponent(c))
            .ToList();
    }

    private List<System.Type> GetRequiredComponentsForComponent(MonoBehaviour monoBehaviour)
    {
        if(monoBehaviour == null) {
            return new List<System.Type>();
        }

        return monoBehaviour.GetType()
            .GetCustomAttributes(typeof(RequireComponent), true)
            .Select(o => (RequireComponent)o)
            .SelectMany(o => new List<System.Type> { o.m_Type0, o.m_Type1, o.m_Type2 })
            .Where(o => o != null)
            .Distinct()
            .ToList();
    }
}