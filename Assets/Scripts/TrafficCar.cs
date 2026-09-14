using UnityEngine;

namespace UrbanEscape
{
    public sealed class TrafficCar : MonoBehaviour
    {
        public float Speed { get; private set; }
        public bool Crashed { get; private set; }
        public int Lane { get; private set; }
        public float Progress { get; private set; }
        public bool Committed { get; private set; }
        CityTraffic owner;
        Rigidbody body;
        int crossing;
        float crashAt;
        readonly RaycastHit[] hits=new RaycastHit[32];
        public void Initialize(CityTraffic traffic,int lane,float progress)
        {
            owner=traffic;Lane=lane;Progress=progress;body=GetComponent<Rigidbody>();
            crossing=0;while(crossing<5 && -360+crossing*180+CityRoadLayout.ClearCenter<progress) crossing++;
        }
        void FixedUpdate()
        {
            if(Crashed) { Speed=body.linearVelocity.magnitude;if(Time.time-crashAt>=5f) Destroy(gameObject);return; }
            if(!owner)return;
            if(Progress>416) { Destroy(gameObject);return; }
            Vector3 f=CityTraffic.Direction(Lane%4);
            Progress=Vector3.Dot(body.position-CityTraffic.LanePosition(Lane,0),f);
            float clearance=60;
            int count=Physics.BoxCastNonAlloc(body.position+f*2.3f,new Vector3(.95f,.45f,.1f),f,hits,body.rotation,60,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++) if(hits[i].collider.attachedRigidbody!=body) clearance=Mathf.Min(clearance,hits[i].distance);
            float desired=Mathf.Min(CityTraffic.Limit,Mathf.Sqrt(2f*6f*Mathf.Max(0,clearance-3f)));
            if(crossing<5)
            {
                float center=-360+crossing*180;
                float toLine=center-CityRoadLayout.StopCenter-Progress;
                int phase=CityTraffic.Phase(Lane%4==0,owner.Clock);
                if(!Committed)
                {
                    bool mayEnter=phase==0 || (phase==1 && toLine < Speed*Speed/12f+1f && Speed>2f);
                    // Reserve enough exit space to avoid queuing across the junction.
                    mayEnter &= clearance > Mathf.Max(5f, center+CityRoadLayout.ClearCenter+2-Progress);
                    if(mayEnter && toLine<=2f) Committed=true;
                    else if(!mayEnter) desired=Mathf.Min(desired,Mathf.Sqrt(12f*Mathf.Max(0,toLine)));
                }
                if(Progress>center+CityRoadLayout.ClearCenter) { crossing++;Committed=false; }
            }
            Speed=Mathf.MoveTowards(Speed,desired,(desired<Speed?6f:2.5f)*Time.fixedDeltaTime);
            float step=Mathf.Min(Speed*Time.fixedDeltaTime,Mathf.Max(0,clearance-1f));
            if(crossing<5 && !Committed)
            {
                float line=-360+crossing*180-CityRoadLayout.StopCenter;
                step=Mathf.Min(step,Mathf.Max(0,line-Progress));
            }
            if(step<Speed*Time.fixedDeltaTime) Speed=step/Time.fixedDeltaTime;
            Progress+=step;
            // Finite-mass motion lets the collision solver push both vehicles on first impact.
            body.linearVelocity=f*(step/Time.fixedDeltaTime);
        }
        void OnCollisionEnter(Collision collision)
        {
            if(collision.collider.GetComponentInParent<SedanController>() || collision.collider.GetComponentInParent<TrafficCar>() || collision.gameObject.layer==8)
                StopAfterCollision();
        }
        public void StopAfterCollision()
        {
            if(Crashed)return;Crashed=true;crashAt=Time.time;
            // Disable AI driving, preserving the solver's impact velocity and rotation.
            body.constraints=RigidbodyConstraints.None;
            body.useGravity=true;
            body.linearDamping=.45f;
            body.angularDamping=1.2f;
            Speed=body.linearVelocity.magnitude;
        }
    }
}
