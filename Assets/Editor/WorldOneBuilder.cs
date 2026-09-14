using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanEscape;

public static class WorldOneBuilder
{
    const string ScenePath = "Assets/Scenes/World01_Metro.unity";
    const string ArtPath = "Assets/World01";
    static Transform city;
    static Material road, pavement, ink, white, cyan, green, bark, grass;
    static Material[] facades;
    static readonly float[] Streets = { -360, -180, 0, 180, 360 };

    [MenuItem("Urban Escape/Create World 1")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) { Debug.Log("World 1 already exists; preserving scene."); return; }
        Directory.CreateDirectory(ArtPath);
        EditorSceneManager.OpenScene("Assets/Scenes/DrivingTest.unity");
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (!root.GetComponent<SedanController>() && !root.GetComponent<Camera>() && !root.GetComponent<Light>())
                UnityEngine.Object.DestroyImmediate(root);
        city = new GameObject("WORLD 01 | METRO DISTRICT").transform;
        road = Mat("Road", new Color(.10f,.13f,.17f));
        pavement = Mat("Pavement", new Color(.50f,.55f,.58f));
        ink = Mat("Charcoal", new Color(.06f,.09f,.13f));
        white = Mat("Lane paint", new Color(.88f,.88f,.78f));
        cyan = Mat("Metro teal", new Color(.02f,.65f,.70f));
        green = Mat("Tree crowns", new Color(.18f,.40f,.29f));
        bark = Mat("Tree trunks", new Color(.23f,.18f,.14f));
        grass = Mat("Park lawn", new Color(.30f,.48f,.34f));
        facades = new[] { Mat("Blue glass",new Color(.18f,.32f,.42f)), Mat("Silver glass",new Color(.43f,.55f,.59f)),
            Mat("Warm concrete",new Color(.61f,.57f,.51f)), Mat("Midnight glass",new Color(.12f,.22f,.29f)) };
        Box("City foundation",new Vector3(0,-.3f,0),new Vector3(860,.6f,860),road,true);
        for(int x=0;x<4;x++) for(int z=0;z<4;z++) Block(x,z);
        MarkRoads();
        // Wide outer connection completes the street loops; walls sit beyond all road ends.
        foreach(float s in new[]{-428f,428f})
        {
            Box("Perimeter barrier",new Vector3(s,1,0),new Vector3(1,2,860),pavement,true);
            Box("Perimeter barrier",new Vector3(0,1,s),new Vector3(860,2,1),pavement,true);
        }
        var car=UnityEngine.Object.FindAnyObjectByType<SedanController>();
        car.transform.SetPositionAndRotation(new Vector3(0,1,-80),Quaternion.identity);
        var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
        camera.farClipPlane=1600;
        camera.transform.SetPositionAndRotation(new Vector3(0,5,-90),Quaternion.Euler(12,0,0));
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(.63f,.74f,.80f);
        RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.00125f;
        RenderSettings.ambientLight=new Color(.64f,.70f,.76f);
        var sun=UnityEngine.Object.FindAnyObjectByType<Light>();
        sun.transform.rotation=Quaternion.Euler(38,-32,0); sun.intensity=1.3f; sun.color=new Color(1,.91f,.78f);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}
            .Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
        AssetDatabase.SaveAssets();
        Validate();
    }

    static void MarkRoads()
    {
        foreach(float s in Streets)
        {
            for(float p=-408;p<410;p+=12)
            {
                if (Streets.Any(c=>Mathf.Abs(c-p)<18)) continue;
                Box("Lane dash",new Vector3(s,.015f,p),new Vector3(.18f,.02f,5),white);
                Box("Lane dash",new Vector3(p,.015f,s),new Vector3(5,.02f,.18f),white);
                foreach(float offset in new[]{-CityRoadLayout.LaneDivider,CityRoadLayout.LaneDivider})
                {
                    Box("Lane divider",new Vector3(s+offset,.015f,p),new Vector3(.12f,.02f,4),white);
                    Box("Lane divider",new Vector3(p,.015f,s+offset),new Vector3(4,.02f,.12f),white);
                }
            }
            foreach(float t in Streets) Intersection(s,t);
        }
    }

    public static void ResizeRoads()
    {
        EditorSceneManager.OpenScene(ScenePath);
        city=GameObject.Find("WORLD 01 | METRO DISTRICT").transform;
        if(city.Find("Road layout 14m")) { Validate();return; }
        white=AssetDatabase.LoadAssetAtPath<Material>(ArtPath+"/Lane paint.mat");
        ink=AssetDatabase.LoadAssetAtPath<Material>(ArtPath+"/Charcoal.mat");
        cyan=AssetDatabase.LoadAssetAtPath<Material>(ArtPath+"/Metro teal.mat");
        var children=city.Cast<Transform>().ToArray();
        string[] parts={"Tower","Retail podium","Roof cap","Rooftop plant","Facade band","Window mullion"};
        foreach(var tower in children.Where(t=>t.name=="Tower"))
        {
            Vector3 p=tower.position;
            float bx=-270+Mathf.Round((p.x+270)/180)*180,bz=-270+Mathf.Round((p.z+270)/180)*180;
            if(Mathf.Abs(Mathf.Abs(p.x-bx)-34)>1 || Mathf.Abs(Mathf.Abs(p.z-bz)-34)>1)continue;
            float w=tower.localScale.x,d=tower.localScale.z;
            Vector3 delta=new Vector3(bx+Mathf.Sign(p.x-bx)*(79.5f-(w+3)/2)-p.x,0,bz+Mathf.Sign(p.z-bz)*(79.5f-(d+3)/2)-p.z);
            var group=children.Where(t=>parts.Contains(t.name) && Mathf.Abs(t.position.x-p.x)<=w/2+2 && Mathf.Abs(t.position.z-p.z)<=d/2+2).ToArray();
            foreach(var t in group)t.position+=delta;
        }
        foreach(var t in children)
        {
            if(t.name=="Sidewalk block")t.localScale=new Vector3(166,.16f,166);
            if(t.name=="Tree trunk" || t.name=="Tree crown")
            {
                float bz=-270+Mathf.Round((t.position.z+270)/180)*180;
                if(Mathf.Abs(Mathf.Abs(t.position.z-bz)-64)<.1f)t.position+=Vector3.forward*Mathf.Sign(t.position.z-bz)*17;
            }
            if(t.name=="Shop sign" || t.GetComponent<TextMesh>())
            {
                float bz=-270+Mathf.Round((t.position.z+270)/180)*180;
                if(Mathf.Abs(t.position.z-bz+55)<1)t.position+=new Vector3(-22,0,-25);
            }
            if(new[]{"Lane dash","Lane divider","Zebra crossing","Stop line","Signal pole","Signal arm","Traffic signal","Signal green"}.Contains(t.name))
                UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        MarkRoads();new GameObject("Road layout 14m").transform.SetParent(city,false);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Validate();
        // Verify both outer traffic lanes with the width of a sedan, including curb clearance.
        for(int lane=0;lane<20;lane++)for(float p=-350;p<=350;p+=10)
        {
            Vector3 pos=CityTraffic.LanePosition(lane,p);
            if(Physics.CheckBox(pos,new Vector3(1,.4f,2.3f),Quaternion.LookRotation(CityTraffic.Direction(lane%4)),1<<8))
                throw new Exception("Narrow traffic lane obstructed: "+pos);
        }
        Debug.Log("ROAD_RESIZE_OK width=14 lane=3.25 sidewalk=3.5; 1420 traffic lane samples clear; existing scene objects preserved.");
    }

    static void Block(int ix,int iz)
    {
        float x=-270+ix*180,z=-270+iz*180;
        Box("Sidewalk block",new Vector3(x,.08f,z),new Vector3(166,.16f,166),pavement,true);
        if(ix==1 && iz==2)
        {
            Box("Central park lawn",new Vector3(x,.18f,z),new Vector3(120,.1f,120),grass);
            Box("Park promenade",new Vector3(x,.24f,z),new Vector3(14,.04f,132),white);
            for(int a=-2;a<=2;a++) for(int b=-2;b<=2;b++) if(a!=0) Tree(x+a*23,z+b*23);
            Sign("MIDORI PARK",new Vector3(x,4,z-65),0,cyan);
            return;
        }
        if(ix==2 && iz==1)
        {
            // Open plaza is drivable, with a clearly visible landmark on its far edge.
            Box("Metro plaza",new Vector3(x,.18f,z),new Vector3(130,.06f,130),facades[1]);
            Building(x+35,z+30,24,28,150,3);
            Box("Landmark crown",new Vector3(x+35,153,z+30),new Vector3(27,5,31),cyan);
            Sign("METRO / 01",new Vector3(x,6,z-62),0,cyan);
            return;
        }
        for(int a=0;a<2;a++) for(int b=0;b<2;b++)
        {
            int seed=ix*17+iz*11+a*7+b*3;
            float h=28+(seed%7)*12;
            if(ix>=2 && iz>=2) h+=38;
            float w=38+seed%9,d=36+seed%7;
            Building(x+(a==0?-1:1)*(79.5f-(w+3)/2),z+(b==0?-1:1)*(79.5f-(d+3)/2),w,d,h,seed%4);
        }
        // Cross-block alleys remain open between the four buildings.
        for(int i=-1;i<=1;i++) { Tree(x+i*42,z-81); Tree(x+i*42,z+81); }
        string[] names={"AOI HOTEL","METRO MART","HIKARI","NOVA BANK","TOKI CAFE","EAST GATE"};
        Sign(names[(ix*4+iz)%names.Length],new Vector3(x-56,7,z-80),0,(ix+iz)%2==0?cyan:ink);
    }

    static void Building(float x,float z,float w,float d,float h,int style)
    {
        Box("Tower",new Vector3(x,h/2+.16f,z),new Vector3(w,h,d),facades[style],true);
        Box("Retail podium",new Vector3(x,3,z),new Vector3(w+3,6,d+3),ink,true);
        Box("Roof cap",new Vector3(x,h+.6f,z),new Vector3(w+1,1,d+1),pavement);
        Box("Rooftop plant",new Vector3(x,h+2.8f,z),new Vector3(w*.35f,4,d*.35f),ink);
        for(float y=9;y<h-2;y+=5)
        {
            Box("Facade band",new Vector3(x,y,z-d/2-.04f),new Vector3(w,.22f,.12f),pavement);
            Box("Facade band",new Vector3(x,y,z+d/2+.04f),new Vector3(w,.22f,.12f),pavement);
            Box("Facade band",new Vector3(x-w/2-.04f,y,z),new Vector3(.12f,.22f,d),pavement);
            Box("Facade band",new Vector3(x+w/2+.04f,y,z),new Vector3(.12f,.22f,d),pavement);
        }
        for(float a=-w/2+4;a<w/2;a+=7)
            foreach(float side in new[]{-1f,1f}) Box("Window mullion",new Vector3(x+a,h/2,z+side*(d/2+.1f)),new Vector3(.18f,h,.14f),ink);
        for(float a=-d/2+4;a<d/2;a+=7)
            foreach(float side in new[]{-1f,1f}) Box("Window mullion",new Vector3(x+side*(w/2+.1f),h/2,z+a),new Vector3(.14f,h,.18f),ink);
    }

    static void Intersection(float x,float z)
    {
        for(int i=-4;i<=4;i++) foreach(float side in new[]{-1f,1f})
        {
            Box("Zebra crossing",new Vector3(x+i*1.5f,.02f,z+side*11),new Vector3(.75f,.02f,3),white);
            Box("Zebra crossing",new Vector3(x+side*11,.02f,z+i*1.5f),new Vector3(3,.02f,.75f),white);
        }
        foreach(float side in new[]{-1f,1f})
        {
            Box("Stop line",new Vector3(x+side*3.5f,.02f,z-side*14.5f),new Vector3(6.5f,.02f,.25f),white);
            Box("Stop line",new Vector3(x-side*14.5f,.02f,z-side*3.5f),new Vector3(.25f,.02f,6.5f),white);
            float px=x+side*9,pz=z+side*9;
            Box("Signal pole",new Vector3(px,3.5f,pz),new Vector3(.25f,7,.25f),ink,true);
            Box("Signal arm",new Vector3(px-side*4,6.7f,pz),new Vector3(8,.2f,.2f),ink);
            Box("Traffic signal",new Vector3(px-side*7,6.4f,pz),new Vector3(2,.65f,.4f),ink);
            Box("Signal green",new Vector3(px-side*7,6.4f,pz-.23f),new Vector3(.35f,.35f,.06f),cyan);
        }
    }
    static void Tree(float x,float z)
    {
        Box("Tree trunk",new Vector3(x,1.8f,z),new Vector3(.5f,3.6f,.5f),bark,true);
        var o=GameObject.CreatePrimitive(PrimitiveType.Sphere); o.name="Tree crown"; o.transform.SetParent(city,false);
        o.transform.position=new Vector3(x,5,z); o.transform.localScale=new Vector3(6,6,6);
        o.GetComponent<Renderer>().sharedMaterial=green; UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>()); o.isStatic=true;
    }
    static void Sign(string text,Vector3 p,float angle,Material mat)
    {
        Box("Shop sign",p,new Vector3(23,4,.4f),mat);
        var o=new GameObject(text); o.transform.SetParent(city,false); o.transform.position=p+Vector3.back*.25f;
        o.transform.rotation=Quaternion.Euler(0,180+angle,0);
        var t=o.AddComponent<TextMesh>(); t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
        t.fontSize=64;t.characterSize=.38f;t.color=Color.white;
    }
    static Material Mat(string name,Color color)
    {
        string path=ArtPath+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat) return mat;
        mat=new Material(Shader.Find("Standard"));mat.color=color; mat.enableInstancing=true;
        AssetDatabase.CreateAsset(mat,path);return mat;
    }
    static GameObject Box(string name,Vector3 pos,Vector3 scale,Material mat,bool solid=false)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(city,false);
        o.transform.position=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;o.isStatic=true;
        if(!solid) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
        else if(name!="City foundation" && name!="Sidewalk block") o.layer=8;
        return o;
    }
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath); Physics.SyncTransforms();
        int samples=0;
        foreach(float s in Streets) for(float t=-400;t<=400;t+=10)
            foreach(var p in new[]{new Vector3(s,1,t),new Vector3(t,1,s)})
            {
                if(Physics.CheckBox(p,new Vector3(3,.5f,3),Quaternion.identity,1<<8)) throw new Exception("Road obstructed: "+p);
                if(!Physics.Raycast(p,Vector3.down,2)) throw new Exception("Road has no ground: "+p);
                samples++;
            }
        if(UnityEngine.Object.FindObjectsByType<SedanController>(FindObjectsSortMode.None).Length!=1) throw new Exception("Expected one car.");
        Debug.Log($"WORLD01_OK roadSamples={samples}; 25 intersections; 16 blocks; one preserved AWD car.");
    }
    public static void Preview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
        camera.transform.position=new Vector3(-115,135,-180); camera.transform.LookAt(new Vector3(70,20,70));
        var rt=new RenderTexture(1600,900,24); camera.targetTexture=rt; camera.Render();
        RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("world01-preview.png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("WORLD01_PREVIEW_OK");
    }
}
