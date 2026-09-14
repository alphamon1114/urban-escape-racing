using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanEscape;

public static class PrototypeSetup
{
    [MenuItem("Urban Escape/Create Driving Test Scene")]
    public static void CreateScene()
    {
        const string path = "Assets/Scenes/DrivingTest.unity";
        if (File.Exists(path)) { Debug.Log("Existing DrivingTest scene preserved."); ValidatePhysics(path); return; }
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var asphalt = Material("Asphalt", new Color(0.22f, 0.25f, 0.29f));
        var paint = Material("SedanBlue", new Color(0.05f, 0.38f, 0.72f));
        var glass = Material("Glass", new Color(0.055f, 0.10f, 0.15f));
        var tire = Material("Tires", new Color(0.035f, 0.04f, 0.05f));
        var white = Material("Markings", new Color(0.8f, 0.87f, 0.87f));
        var orange = Material("Orange", new Color(1f, 0.42f, 0.08f));
        Box("Test ground 600 x 600 m", null, new Vector3(0,-0.25f,0), new Vector3(600,0.5f,600), asphalt, true);
        for (int i = -280; i <= 280; i += 20)
        {
            Box("Grid X", null, new Vector3(i,0.006f,0), new Vector3(0.08f,0.01f,580), white);
            Box("Grid Z", null, new Vector3(0,0.006f,i), new Vector3(580,0.01f,0.08f), white);
        }
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48;
            Box("60 m radius corner marker", null, new Vector3(60 * Mathf.Sin(a),0.2f,60 * Mathf.Cos(a)), new Vector3(0.6f,0.4f,0.6f), orange);
        }
        Box("North boundary", null, new Vector3(0,0.6f,300), new Vector3(600,1.2f,1), orange, true);
        Box("South boundary", null, new Vector3(0,0.6f,-300), new Vector3(600,1.2f,1), orange, true);
        Box("East boundary", null, new Vector3(300,0.6f,0), new Vector3(1,1.2f,600), orange, true);
        Box("West boundary", null, new Vector3(-300,0.6f,0), new Vector3(1,1.2f,600), orange, true);
        var root = new GameObject("AWD Sport Sedan");
        root.transform.position = new Vector3(0,1,-80);
        root.AddComponent<Rigidbody>();
        var car = root.AddComponent<SedanController>();
        Box("Body", root.transform, Vector3.zero, new Vector3(1.85f,0.48f,4.5f), paint, true);
        Box("Cabin", root.transform, new Vector3(0,0.5f,-0.18f), new Vector3(1.55f,0.65f,2.2f), glass, true);
        Box("Roof", root.transform, new Vector3(0,0.85f,-0.3f), new Vector3(1.5f,0.08f,1.6f), paint);
        Box("Hood", root.transform, new Vector3(0,0.28f,1.55f), new Vector3(1.8f,0.1f,1.3f), paint);
        foreach (float x in new[] {-0.65f,0.65f})
        {
            Box("Headlight", root.transform, new Vector3(x,0.06f,2.26f), new Vector3(0.43f,0.13f,0.04f), white);
            Box("Tail light", root.transform, new Vector3(x,0.06f,-2.26f), new Vector3(0.43f,0.13f,0.04f), orange);
        }
        car.wheels = new WheelCollider[4]; car.wheelMeshes = new Transform[4];
        for (int i=0; i<4; i++)
        {
            var w = new GameObject("Wheel " + i); w.transform.SetParent(root.transform, false);
            w.transform.localPosition = new Vector3(i%2==0 ? -0.91f : 0.91f,-0.1f,i<2 ? 1.43f : -1.43f);
            var wc=w.AddComponent<WheelCollider>(); wc.radius=0.36f; wc.mass=23; wc.suspensionDistance=0.22f;
            wc.suspensionSpring=new JointSpring { spring=38000, damper=5200, targetPosition=0.5f };
            wc.forceAppPointDistance=0.15f; wc.wheelDampingRate=1.3f;
            var f=wc.sidewaysFriction; f.extremumSlip=0.22f; f.extremumValue=1; f.asymptoteSlip=0.65f; f.asymptoteValue=0.75f; wc.sidewaysFriction=f;
            car.wheels[i]=wc;
            var pivot=new GameObject("Wheel visual " + i); pivot.transform.SetParent(root.transform,false); car.wheelMeshes[i]=pivot.transform;
            var mesh=GameObject.CreatePrimitive(PrimitiveType.Cylinder); mesh.name="Tire"; mesh.transform.SetParent(pivot.transform,false);
            mesh.transform.localRotation=Quaternion.Euler(0,0,90); mesh.transform.localScale=new Vector3(0.72f,0.13f,0.72f);
            UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>()); mesh.GetComponent<Renderer>().sharedMaterial=tire;
        }
        var camera=new GameObject("Main Camera"); camera.tag="MainCamera"; camera.AddComponent<Camera>().farClipPlane=1000; camera.AddComponent<AudioListener>();
        camera.transform.position=new Vector3(0,4.6f,-88); camera.AddComponent<ChaseCamera>().target=car;
        camera.AddComponent<DrivingHud>().car=car;
        var light=new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.2f; light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientLight=new Color(0.6f,0.65f,0.72f);
        PlayerSettings.companyName="UrbanEscape"; PlayerSettings.productName="Urban Escape Racing";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
        EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(path,true)};
        AssetDatabase.SaveAssets();
        Debug.Log("PROTOTYPE_SETUP_OK: scene saved; four driven wheels; ground and camera ready.");
        ValidatePhysics(path);
    }
    static void ValidatePhysics(string path)
    {
        EditorSceneManager.OpenScene(path);
        var car=UnityEngine.Object.FindAnyObjectByType<SedanController>();
        var flags=System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(SedanController).GetMethod("Awake",flags).Invoke(car,null);
        var tick=typeof(SedanController).GetMethod("FixedUpdate",flags);
        var rb=car.GetComponent<Rigidbody>();
        var oldMode=Physics.simulationMode; Physics.simulationMode=SimulationMode.Script;
        void InputValue(string name,object value) => typeof(SedanController).GetField(name,flags).SetValue(car,value);
        void Step(int count) { for(int i=0;i<count;i++) { tick.Invoke(car,null); Physics.Simulate(Time.fixedDeltaTime); } }
        try
        {
            Physics.SyncTransforms(); Step(200);
            if(!car.wheels[0].isGrounded || !car.wheels[3].isGrounded) throw new Exception("Wheels failed to settle on ground.");
            InputValue("throttle",1f); Step(600); float speed=car.SpeedKph;
            if(speed<35f) throw new Exception("Acceleration failed: "+speed);
            InputValue("throttle",0f); InputValue("braking",true); Step(200); float slowed=car.SpeedKph;
            if(slowed>=speed) throw new Exception("Braking failed.");
            Step(500); float reverse=Vector3.Dot(rb.linearVelocity,car.transform.forward);
            if(reverse>=-1f) throw new Exception("Reverse failed.");
            InputValue("braking",false); car.ResetVehicle(); Physics.SyncTransforms(); Step(100);
            InputValue("steerInput",1f); Step(1); float first=car.SteeringAngle;
            if(first<=0 || first>=5f) throw new Exception("Steering ramp failed.");
            Step(80); float low=car.SteeringAngle;
            InputValue("steerInput",0f); car.ResetVehicle(); Physics.SyncTransforms(); Step(100);
            InputValue("steerInput",1f);
            rb.linearVelocity=car.transform.forward*40f; Step(20); float high=car.SteeringAngle;
            if(low<31f || high>=low*0.6f || high<6f) throw new Exception("Speed-sensitive steering failed.");
            float rearSlip=0f, frontSlip=0f;
            for(int frame=0; frame<30; frame++)
            {
                Step(1);
                for(int i=0;i<4;i++) if(car.wheels[i].GetGroundHit(out var hit))
                {
                    if(i<2) frontSlip=Mathf.Max(frontSlip,Mathf.Abs(hit.sidewaysSlip));
                    else rearSlip=Mathf.Max(rearSlip,Mathf.Abs(hit.sidewaysSlip));
                }
            }
            if(frontSlip<=rearSlip) throw new Exception("Fast corner did not produce front-led slip.");
            Debug.Log($"CORNER_CHECK rearPeakSlip={rearSlip:F3} frontPeakSlip={frontSlip:F3}");
            InputValue("handbrake",true); Step(1);
            if(car.wheels[2].brakeTorque<4000 || car.wheels[2].motorTorque!=0) throw new Exception("Handbrake failed.");
            car.ResetVehicle();
            if(rb.linearVelocity.sqrMagnitude>0.001f || rb.angularVelocity.sqrMagnitude>0.001f) throw new Exception("Reset failed.");
            float RecoveryYaw(float assist)
            {
                InputValue("handbrake",false); InputValue("steerInput",0f);
                car.ResetVehicle(); Physics.SyncTransforms(); Step(150);
                car.countersteerAssist=assist;
                rb.linearVelocity=new Vector3(-8f,0f,20f);
                rb.angularVelocity=Vector3.up*1.2f;
                InputValue("steerInput",-1f); Step(10);
                return Mathf.Abs(Vector3.Dot(rb.angularVelocity,car.transform.up));
            }
            float unassisted=RecoveryYaw(0f), assisted=RecoveryYaw(2.8f);
            if(assisted>=unassisted) throw new Exception("Countersteer assist did not reduce spin.");
            Debug.Log($"RECOVERY_CHECK yawWithout={unassisted:F3} yawWith={assisted:F3}");
            InputValue("steerInput",0f); car.ResetVehicle(); Physics.SyncTransforms(); Step(150);
            rb.linearVelocity=Vector3.forward*20f;
            InputValue("throttle",1f); InputValue("handbrake",true); InputValue("steerInput",1f); Step(25);
            float handbrakeYaw=Vector3.Dot(rb.angularVelocity,car.transform.up);
            if(car.wheels[0].motorTorque<=0 || car.wheels[2].motorTorque!=0 || handbrakeYaw<0.1f)
                throw new Exception("Handbrake steering/front drive failed.");
            InputValue("steerInput",-1f); Step(25);
            float counterYaw=Vector3.Dot(rb.angularVelocity,car.transform.up);
            if(counterYaw>=handbrakeYaw) throw new Exception("Handbrake countersteering failed.");
            InputValue("handbrake",false); Step(1);
            float releaseGrip=car.wheels[2].sidewaysFriction.stiffness;
            if(releaseGrip>=1.35f || car.wheels[2].brakeTorque!=0) throw new Exception("Handbrake release failed.");
            Step(30);
            if(Mathf.Abs(car.wheels[2].sidewaysFriction.stiffness-1.35f)>0.01f) throw new Exception("Rear grip recovery failed.");
            Debug.Log($"HANDBRAKE_CHECK yaw={handbrakeYaw:F3} counterYaw={counterYaw:F3} releaseGrip={releaseGrip:F3}; front drive and smooth recovery passed.");
            Debug.Log($"PHYSICS_CHECK_OK acceleration={speed:F1}km/h braking={slowed:F1}km/h reverse={reverse:F1}m/s steeringFirst={first:F2} low={low:F1} high={high:F1}; ground, handbrake, reset passed.");
        }
        finally { Physics.simulationMode=oldMode; EditorSceneManager.OpenScene(path); }
    }
    static Material Material(string name, Color color)
    {
        string path="Assets/Materials/"+name+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing) return existing;
        var m=new Material(Shader.Find("Standard")); m.color=color; AssetDatabase.CreateAsset(m,path); return m;
    }
    static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material mat,bool collider=false)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent,false); o.transform.localPosition=position; o.transform.localScale=scale;
        o.GetComponent<Renderer>().sharedMaterial=mat; if(!collider) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>()); return o;
    }
}
