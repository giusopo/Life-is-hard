using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneColliderTools
{
    [MenuItem("Tools/Physics/Add Mesh Colliders/Active Scene")]
    private static void AddMeshCollidersToActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogWarning("No active loaded scene was found.");
            return;
        }

        MeshColliderReport report = AddMeshColliders(activeScene.GetRootGameObjects());
        EditorSceneManager.MarkSceneDirty(activeScene);
        LogReport("Active Scene", report);
    }

    [MenuItem("Tools/Physics/Add Mesh Colliders/Selection")]
    private static void AddMeshCollidersToSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("Select one or more root objects in the Hierarchy first.");
            return;
        }

        MeshColliderReport report = AddMeshColliders(Selection.gameObjects);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        LogReport("Selection", report);
    }

    [MenuItem("Tools/Physics/Add Mesh Colliders/Active Scene", true)]
    [MenuItem("Tools/Physics/Add Mesh Colliders/Selection", true)]
    private static bool ValidateSceneCommands()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && activeScene.isLoaded;
    }

    private static MeshColliderReport AddMeshColliders(IEnumerable<GameObject> rootObjects)
    {
        MeshColliderReport report = new MeshColliderReport();
        HashSet<Transform> visited = new HashSet<Transform>();

        foreach (GameObject rootObject in rootObjects)
        {
            if (rootObject == null)
            {
                continue;
            }

            foreach (MeshFilter meshFilter in rootObject.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!visited.Add(meshFilter.transform))
                {
                    continue;
                }

                if (meshFilter.sharedMesh == null)
                {
                    report.missingMesh++;
                    continue;
                }

                if (meshFilter.GetComponent<Collider>() != null)
                {
                    report.alreadyHasCollider++;
                    continue;
                }

                if (meshFilter.GetComponentInParent<Rigidbody>() != null)
                {
                    report.skippedDynamicObjects++;
                    continue;
                }

                if (meshFilter.GetComponentInParent<Animator>() != null)
                {
                    report.skippedAnimatedObjects++;
                    continue;
                }

                Undo.AddComponent<MeshCollider>(meshFilter.gameObject).sharedMesh = meshFilter.sharedMesh;
                report.added++;
            }
        }

        return report;
    }

    private static void LogReport(string scope, MeshColliderReport report)
    {
        Debug.Log(
            $"[{nameof(SceneColliderTools)}] {scope}: added {report.added} MeshCollider, " +
            $"already present {report.alreadyHasCollider}, " +
            $"skipped animated {report.skippedAnimatedObjects}, " +
            $"skipped dynamic {report.skippedDynamicObjects}, " +
            $"missing mesh {report.missingMesh}."
        );
    }

    private struct MeshColliderReport
    {
        public int added;
        public int alreadyHasCollider;
        public int skippedAnimatedObjects;
        public int skippedDynamicObjects;
        public int missingMesh;
    }
}
