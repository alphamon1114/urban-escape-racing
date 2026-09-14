using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanEscape;

// Deterministic tire/steering comparisons on a flat test surface, not a course autopilot.
public static class HandlingValidation
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static SedanController car;
    static Rigidbody body;
    static readonly MethodInfo Tick = typeof(SedanController).GetMethod("FixedUpdate", Flags);

    struct Result
    {
        public float yaw, peakYaw, frontSlip, rearSlip, slipAngle, speed, heading;
    }

    static void Input(string name, object value) => typeof(SedanController).GetField(name, Flags).SetValue(car, value);
    static void Step(int count)
    {
        for (int i = 0; i < count; i++) { Tick.Invoke(car, null); Physics.Simulate(Time.fixedDeltaTime); }
    }

    static void Prepare(float speed)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DrivingTest.unity");
        car = UnityEngine.Object.FindAnyObjectByType<SedanController>();
        body = car.GetComponent<Rigidbody>();
        // Remove test markers and perimeter collisions from this in-memory fixture only.
        foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (!collider.transform.IsChildOf(car.transform) && collider.name != "Test ground 600 x 600 m")
                collider.enabled = false;
        typeof(SedanController).GetMethod("Awake", Flags).Invoke(car, null);
        Physics.SyncTransforms(); Step(150);
        foreach (var wheel in car.wheels)
            if (!wheel.isGrounded) throw new Exception("Handling fixture did not settle.");
        body.linearVelocity = car.transform.forward * speed;
        Input("throttle", 1f);
    }

    static Result Turn(float speed, float direction, bool handbrake, bool counter = false)
    {
        Prepare(speed);
        Input("steerInput", direction);
        Input("handbrake", handbrake);
        Result result = new Result();
        int frames = Mathf.RoundToInt(2f / Time.fixedDeltaTime);
        for (int frame = 0; frame < frames; frame++)
        {
            // A short handbrake entry followed by release and optional countersteer.
            if (handbrake && frame == Mathf.RoundToInt(0.5f / Time.fixedDeltaTime))
            {
                Input("handbrake", false);
                if (counter) Input("steerInput", -direction);
            }
            if (!handbrake && counter && frame == Mathf.RoundToInt(1f / Time.fixedDeltaTime))
                Input("steerInput", -direction);
            Step(1);
            result.yaw = Vector3.Dot(body.angularVelocity, Vector3.up) * direction;
            result.peakYaw = Mathf.Max(result.peakYaw, Mathf.Abs(result.yaw));
            float forward = Vector3.Dot(body.linearVelocity, car.transform.forward);
            float lateral = Vector3.Dot(body.linearVelocity, car.transform.right);
            result.slipAngle = Mathf.Max(result.slipAngle, Mathf.Abs(Mathf.Atan2(lateral, forward) * Mathf.Rad2Deg));
            for (int i = 0; i < 4; i++) if (car.wheels[i].GetGroundHit(out var hit))
            {
                if (i < 2) result.frontSlip = Mathf.Max(result.frontSlip, Mathf.Abs(hit.sidewaysSlip));
                else result.rearSlip = Mathf.Max(result.rearSlip, Mathf.Abs(hit.sidewaysSlip));
            }
            if (Vector3.Dot(car.transform.up, Vector3.up) < 0.8f || car.transform.position.y < -1f)
                throw new Exception("Vehicle tipped or left ground.");
        }
        result.speed = car.SpeedKph;
        result.heading = Mathf.Abs(Mathf.DeltaAngle(0f, car.transform.eulerAngles.y));
        Debug.Log($"HANDLING speedIn={speed * 3.6f:F0} direction={direction} handbrake={handbrake} counter={counter} peakYaw={result.peakYaw:F3} endYaw={result.yaw:F3} frontSlip={result.frontSlip:F3} rearSlip={result.rearSlip:F3} bodySlip={result.slipAngle:F1} heading={result.heading:F1} speedOut={result.speed:F1}");
        return result;
    }

    public static void MeasureBaseline() => Execute(false);
    [MenuItem("Urban Escape/Validate Handling")]
    public static void Run() => Execute(true);

    static void Execute(bool assertHandling)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var previousMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            foreach (float direction in new[] { -1f, 1f })
            {
                var normal = Turn(20f, direction, false);
                var fast = Turn(40f, direction, false);
                var entry = Turn(20f, direction, true);
                var recover = Turn(20f, direction, true, true);
                var reverse = Turn(40f, direction, false, true);
                var fastEntry = Turn(40f, direction, true);
                var fastRecover = Turn(40f, direction, true, true);
                if (!assertHandling) continue;
                if (fast.frontSlip <= fast.rearSlip || fast.slipAngle > 20f || fast.peakYaw > 0.9f)
                    throw new Exception("High speed turn did not remain stable with front-led slip.");
                if (entry.rearSlip <= normal.rearSlip || entry.peakYaw <= normal.peakYaw)
                    throw new Exception("Handbrake did not produce a distinct rear slide.");
                if (recover.heading >= entry.heading || recover.slipAngle > 60f)
                    throw new Exception("Countersteer did not arrest the entry rotation.");
                if (reverse.slipAngle > 25f || reverse.peakYaw > 1.2f)
                    throw new Exception("High speed steering reversal became unstable.");
                if (fastEntry.rearSlip <= fast.rearSlip || fastRecover.heading >= fastEntry.heading
                    || fastRecover.slipAngle > 60f)
                    throw new Exception("High speed handbrake/countersteer failed.");
            }
            Debug.Log(assertHandling ? "HANDLING_VALIDATION_OK" : "HANDLING_BASELINE_RECORDED");
        }
        finally
        {
            Physics.simulationMode = previousMode;
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/DrivingTest.unity");
            else EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
