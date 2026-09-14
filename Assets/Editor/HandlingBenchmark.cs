using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanEscape;

public static class HandlingBenchmark
{
    public static void Run()
    {
        var oldMode=Physics.simulationMode;
        var oldStep=Time.fixedDeltaTime;
        Physics.simulationMode=SimulationMode.Script; Time.fixedDeltaTime=0.02f;
        Directory.CreateDirectory("HandlingResults");
        try
        {
            foreach(float speed in new[]{60f,90f,120f})
            {
                Corner(speed,false,9f);
                foreach(float angle in new[]{9f,12f,16f}) Corner(speed,true,angle);
            }
            Debug.Log("HANDLING_BENCHMARK_OK");
        }
        finally { Physics.simulationMode=oldMode; Time.fixedDeltaTime=oldStep; EditorSceneManager.OpenScene("Assets/Scenes/DrivingTest.unity"); }
    }
    static void Corner(float entry,bool adaptive,float angle)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DrivingTest.unity");
        var car=UnityEngine.Object.FindAnyObjectByType<SedanController>();
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        typeof(SedanController).GetMethod("Awake",flags).Invoke(car,null);
        var tick=typeof(SedanController).GetMethod("FixedUpdate",flags);
        void Input(string key,object value) => typeof(SedanController).GetField(key,flags).SetValue(car,value);
        void Step() { tick.Invoke(car,null); Physics.Simulate(Time.fixedDeltaTime); }
        var rb=car.GetComponent<Rigidbody>();car.adaptiveSteering=adaptive;car.cornerSlipAngle=angle;
        Physics.SyncTransforms(); for(int i=0;i<150;i++) Step();
        rb.linearVelocity=car.transform.forward*(entry/3.6f);
        Input("throttle",1f);Input("steerInput",1f);
        var start=rb.position; float peakSlip=0, yawTotal=0, path=0, min=entry;
        string label=adaptive?"adaptive"+angle.ToString("0",CultureInfo.InvariantCulture):"baseline";
        using(var csv=new StreamWriter($"HandlingResults/{label}-{entry:0}.csv"))
        {
            csv.WriteLine("time,speedKph,steeringDeg,yawRadSec,frontSlip,rearSlip,drift,x,z");
            int frames=0;
            for(;frames<300;frames++)
            {
                Vector3 previous=rb.position;Step();
                path+=Vector3.Distance(previous,rb.position);
                float yaw=Vector3.Dot(rb.angularVelocity,car.transform.up);
                yawTotal+=yaw*Time.fixedDeltaTime;
                float front=0,rear=0;
                for(int w=0;w<4;w++) if(car.wheels[w].GetGroundHit(out var hit))
                { if(w<2) front+=Mathf.Abs(hit.sidewaysSlip)*.5f; else rear+=Mathf.Abs(hit.sidewaysSlip)*.5f; }
                peakSlip=Mathf.Max(peakSlip,front); min=Mathf.Min(min,car.SpeedKph);
                csv.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:F2},{1:F3},{2:F3},{3:F3},{4:F4},{5:F4},{6:F3},{7:F3},{8:F3}",
                    (frames+1)*.02f,car.SpeedKph,car.SteeringAngle,yaw,front,rear,car.DriftAmount,rb.position.x,rb.position.z));
                if(yawTotal>=Mathf.PI*.5f) break;
            }
            if(yawTotal<Mathf.PI*.5f || car.DriftAmount>0.01f || Vector3.Dot(car.transform.up,Vector3.up)<.7f)
                throw new Exception("Corner failed: "+label+entry);
            Debug.Log($"CORNER_RESULT {label} entry={entry:F0} exit={car.SpeedKph:F1} minimum={min:F1} time={(frames+1)*.02f:F2} radiusApprox={path/yawTotal:F1} peakFrontSlip={peakSlip:F3}");
        }
    }
}
