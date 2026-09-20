using System.Collections.Generic;
using UnityEngine;

public static class ComicSafeSpawn
{
    struct Reserved { public Vector3 point; public float until; }
    static readonly List<Reserved> reserved = new List<Reserved>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { reserved.Clear(); }
    public static bool TryFind(Transform anchor, GameObject prefab, out Vector3 position)
    {
        position=anchor.position;
        var controller=prefab.GetComponent<CharacterController>();
        if(!controller)return false;
        var scale=prefab.transform.lossyScale;
        float radius=controller.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
        float height=Mathf.Max(radius*2,controller.height*Mathf.Abs(scale.y));
        Vector3 center=anchor.rotation*Vector3.Scale(controller.center,scale);
        float expectedFloor=anchor.position.y+center.y-height*.5f;
        reserved.RemoveAll(r=>r.until<Time.unscaledTime);
        Physics.SyncTransforms();
        for(int ring=0;ring<=6;ring++)
        for(int step=0;step<(ring==0?1:ring*12);step++)
        {
            float angle=step*Mathf.PI*2/Mathf.Max(1,ring*12);
            Vector3 trial=anchor.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*(radius*2+.3f);
            // Keep candidates on the same floor; shelf tops are not spawn floors.
            if(!Physics.Raycast(new Vector3(trial.x,expectedFloor+.45f,trial.z),Vector3.down,out var floor,.9f,Physics.AllLayers,QueryTriggerInteraction.Ignore))continue;
            if(floor.normal.y<.85f || floor.collider.GetComponentInParent<BookItem>() || floor.collider.GetComponentInParent<NetworkPlayerSetup>())continue;
            trial.y=floor.point.y+height*.5f-center.y+.04f;
            Vector3 c=trial+center;
            Vector3 a=c+Vector3.up*(height*.5f-radius),b=c-Vector3.up*(height*.5f-radius);
            if(Physics.CheckCapsule(a,b,radius+.01f,Physics.AllLayers,QueryTriggerInteraction.Ignore))continue;
            bool busy=false;
            foreach(var r in reserved)if(Vector3.Distance(r.point,trial)<radius*2+.2f){busy=true;break;}
            if(busy)continue;
            reserved.Add(new Reserved{point=trial,until=Time.unscaledTime+5f});position=trial;return true;
        }
        return false;
    }
}
