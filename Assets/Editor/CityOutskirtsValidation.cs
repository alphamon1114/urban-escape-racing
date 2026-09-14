using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanEscape;

public static class CityOutskirtsValidation
{
    const string ScenePath = "Assets/Scenes/CityOutskirts.unity";

    [MenuItem("Urban Escape/Validate City Outskirts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before validation.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath);
        var objects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (var item in objects)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                throw new Exception("Missing script: " + item.name);
        }
        var cars = UnityEngine.Object.FindObjectsByType<SedanController>(FindObjectsSortMode.None);
        if (cars.Length != 1 || cars[0].wheels.Length != 4 || cars[0].wheels.Any(w => !w)
            || cars[0].wheelMeshes.Length != 4 || cars[0].wheelMeshes.Any(w => !w))
            throw new Exception("Vehicle or wheel references are invalid.");
        if (!Camera.main || Camera.main.GetComponent<ChaseCamera>().target != cars[0]
            || Camera.main.GetComponent<DrivingHud>().car != cars[0])
            throw new Exception("Camera/HUD references are invalid.");
        Debug.Log($"CITY_SCENE_OK objects={objects.Length}; vehicle, wheels, camera, HUD and scripts valid.");

        // Reuse the current vehicle checks on the new map.
        // The validator reloads the scene in finally; simulated state is never saved.
        try
        {
            typeof(PrototypeSetup).GetMethod("ValidatePhysics", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { ScenePath });
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
        Debug.Log("CITY_OUTSKIRTS_VALIDATION_OK (editor physics; keyboard driving and rendering require Play mode).");
    }
}
