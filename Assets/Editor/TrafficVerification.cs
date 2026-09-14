using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanEscape;

[InitializeOnLoad]
public static class TrafficVerification
{
    static TrafficCar victim;
    static bool hitSent;
    static float hitTime=-1,crashTime=-1;
    static Vector3 crashPosition;
    static float displacement;
    static int samples,stopped;
    static TrafficVerification() { EditorApplication.update+=Tick; }
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/World01_Metro.unity");
        SessionState.SetBool("TrafficVerification",true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool("TrafficVerification",false) || !EditorApplication.isPlaying)return;
        try
        {
            var traffic=UnityEngine.Object.FindAnyObjectByType<CityTraffic>();
            if(!traffic) { if(Time.timeSinceLevelLoad>5)throw new Exception("Traffic manager missing");return; }
            foreach(var car in traffic.Cars)
            {
                if(!car)continue;
                if(!car.Crashed && car.Speed>CityTraffic.Limit+.001f)throw new Exception("Speed exceeded 60 km/h");
                if(!car.Crashed && Vector3.Distance(car.transform.position,CityTraffic.LanePosition(car.Lane,car.Progress))>1f)throw new Exception("Lane deviation");
                if(car.Speed<.1f && !car.Crashed) stopped++;
                samples++;
            }
            if(Time.timeSinceLevelLoad>34 && !hitSent)
            {
                if(traffic.Cars.Count<20 || stopped==0)throw new Exception("Population/signal stop check failed");
                victim=traffic.Cars.Find(c=>c && !c.Crashed);
                var player=UnityEngine.Object.FindAnyObjectByType<SedanController>().GetComponent<Rigidbody>();
                Vector3 right=victim.transform.right;
                player.position=victim.transform.position+right*3f;
                player.rotation=victim.transform.rotation;
                player.linearVelocity=-right*12f+victim.transform.forward*victim.Speed;
                player.angularVelocity=Vector3.zero;
                hitTime=Time.timeSinceLevelLoad;hitSent=true;
            }
            if(hitSent && crashTime<0)
            {
                if(victim && victim.Crashed) { crashTime=Time.timeSinceLevelLoad;crashPosition=victim.transform.position; }
                else if(Time.timeSinceLevelLoad-hitTime>3)throw new Exception("Actual player collision did not stop traffic");
            }
            if(crashTime>=0)
            {
                float elapsed=Time.timeSinceLevelLoad-crashTime;
                if(victim)
                {
                    displacement=Mathf.Max(displacement,Vector3.ProjectOnPlane(victim.transform.position-crashPosition,Vector3.up).magnitude);
                    if(victim.GetComponent<Rigidbody>().isKinematic)throw new Exception("Crashed car is immovable");
                }
                if(!victim && elapsed<4.9f)throw new Exception("Car disappeared too early");
                if(elapsed>5.2f)
                {
                    if(victim)throw new Exception("Car did not disappear after 5 seconds");
                    if(displacement<.5f)throw new Exception("Impact did not push traffic");
                    Debug.Log($"TRAFFIC_PLAY_OK samples={samples} stoppedSamples={stopped} impactDisplacement={displacement:F2}m; right lanes, speed cap, physical impact and 5-second removal passed.");
                    Finish(0);
                }
            }
        }
        catch(Exception ex) { Debug.LogException(ex);Finish(1); }
    }
    static void Finish(int code) { SessionState.SetBool("TrafficVerification",false);EditorApplication.Exit(code); }
}
