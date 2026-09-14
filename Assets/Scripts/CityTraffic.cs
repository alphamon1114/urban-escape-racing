using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UrbanEscape
{
    public sealed class CityTraffic : MonoBehaviour
    {
        public const float Limit = 60f / 3.6f;
        public readonly List<TrafficCar> Cars = new List<TrafficCar>();
        readonly List<Renderer> signals = new List<Renderer>();
        readonly List<bool> signalAxes = new List<bool>();
        Material[] paints;
        Material dark, white;
        float nextSpawn;
        int spawnLane;
        public float Clock => Time.timeSinceLevelLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if(scene.name=="World01_Metro" && !FindAnyObjectByType<CityTraffic>())
                new GameObject("CITY TRAFFIC | 60 kmh | right hand").AddComponent<CityTraffic>();
        }
        public static int Phase(bool northSouth, float time)
        {
            float t = Mathf.Repeat(time + (northSouth ? 0f : 16f),32f);
            return t < 10f ? 0 : t < 13f ? 1 : 2; // green, amber, red; all-red clearance
        }
        void Start()
        {
            dark=Make(new Color(.055f,.07f,.09f));white=Make(new Color(.9f,.9f,.83f));
            paints=new[]{Make(new Color(.72f,.16f,.1f)),Make(new Color(.68f,.71f,.73f)),Make(new Color(.11f,.23f,.32f)),Make(new Color(.85f,.74f,.4f))};
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if(r.name=="Signal green") r.enabled=false;
            for(int x=0;x<5;x++) for(int z=0;z<5;z++) for(int d=0;d<4;d++)
            {
                Vector3 f=Direction(d),right=Vector3.Cross(Vector3.up,f);
                Vector3 p=new Vector3(-360+x*180,0,-360+z*180)-f*14+right*8.5f;
                Part("Traffic light pole",transform,p+Vector3.up*3,new Vector3(.2f,6,.2f),dark);
                var lamp=Part("Working traffic light",transform,p+Vector3.up*5.8f,new Vector3(.9f,.9f,.9f),white);
                signals.Add(lamp.GetComponent<Renderer>());signalAxes.Add(d%2==0);
            }
            // Two cars per directional road, deliberately outside intersection boxes.
            for(int lane=0;lane<20;lane++) { Spawn(lane,-275); Spawn(lane,85); }
            nextSpawn=Clock+2f;
        }
        public static Vector3 Direction(int d) => d==0?Vector3.forward:d==1?Vector3.right:d==2?Vector3.back:Vector3.left;
        public static Vector3 LanePosition(int lane,float progress)
        {
            int d=lane%4;float road=-360+(lane/4)*180;
            Vector3 f=Direction(d),right=Vector3.Cross(Vector3.up,f);
            return (d%2==0 ? new Vector3(road,0,0):new Vector3(0,0,road))+f*progress+right*CityRoadLayout.TrafficOffset+Vector3.up*.7f;
        }
        void Update()
        {
            var block=new MaterialPropertyBlock();
            for(int i=0;i<signals.Count;i++)
            {
                int phase=Phase(signalAxes[i],Clock);
                block.SetColor("_Color",phase==0?Color.green:phase==1?new Color(1,.6f,0):Color.red);
                signals[i].SetPropertyBlock(block);
            }
            Cars.RemoveAll(c=>!c);
            if(Clock>=nextSpawn)
            {
                nextSpawn=Clock+.5f;
                if(Cars.Count<40) Spawn(spawnLane++%20,-410);
            }
        }
        void Spawn(int lane,float progress)
        {
            Vector3 p=LanePosition(lane,progress);
            // Do not spawn on a player, stopped traffic, or scenery.
            if(Physics.CheckBox(p,new Vector3(1.2f,.5f,7),Quaternion.LookRotation(Direction(lane%4)),~0,QueryTriggerInteraction.Ignore)) return;
            var root=new GameObject("Traffic sedan");root.transform.SetParent(transform);
            root.transform.SetPositionAndRotation(p,Quaternion.LookRotation(Direction(lane%4)));
            var box=root.AddComponent<BoxCollider>();box.size=new Vector3(1.85f,1.2f,4.4f);box.center=new Vector3(0,.15f,0);
            var rb=root.AddComponent<Rigidbody>();rb.mass=1450f;rb.useGravity=false;
            rb.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            Part("Body",root.transform,Vector3.zero,new Vector3(1.85f,.55f,4.4f),paints[lane%paints.Length]);
            Part("Cabin",root.transform,new Vector3(0,.55f,-.25f),new Vector3(1.55f,.65f,2.2f),dark);
            for(int i=0;i<4;i++) Part("Wheel",root.transform,new Vector3(i%2==0?-.94f:.94f,-.3f,i<2?1.4f:-1.4f),new Vector3(.23f,.65f,.65f),dark);
            Part("Headlights",root.transform,new Vector3(0,.1f,2.21f),new Vector3(1.5f,.15f,.05f),white);
            var car=root.AddComponent<TrafficCar>();car.Initialize(this,lane,progress);Cars.Add(car);
        }
        static Material Make(Color color) { var m=new Material(Shader.Find("Standard"));m.color=color;m.enableInstancing=true;return m; }
        static GameObject Part(string name,Transform parent,Vector3 p,Vector3 scale,Material mat)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.localScale=scale;
            Destroy(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=mat;return o;
        }
        void OnDestroy()
        {
            if(paints!=null) foreach(var m in paints) Destroy(m);
            if(dark) Destroy(dark);if(white) Destroy(white);
        }
    }
}
