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
        Physics.SyncTransforms();
        // The authored marker can float above the floor. Do not assume its
        // height is already the character's final standing height.
        if (!FindFloor(anchor.position, out var referenceFloor)) return false;
        float floorY = referenceFloor.point.y;
        reserved.RemoveAll(r=>r.until<Time.unscaledTime);
        for(int ring=0;ring<=6;ring++)
        for(int step=0;step<(ring==0?1:ring*12);step++)
        {
            float angle=step*Mathf.PI*2/Mathf.Max(1,ring*12);
            Vector3 trial=anchor.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*(radius*2+.3f);
            // Look through books/players to find the structural floor, then
            // reject occupied standing capsules below. Never spawn on a shelf.
            if(!FindFloor(new Vector3(trial.x, floorY + .3f, trial.z),out var floor))continue;
            if(Mathf.Abs(floor.point.y-floorY)>.2f)continue;
            trial.y=floor.point.y+height*.5f-center.y+Mathf.Max(.04f,controller.skinWidth*Mathf.Abs(scale.y));
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

    static bool FindFloor(Vector3 origin, out RaycastHit floor)
    {
        floor = default;
        float closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(origin + Vector3.up * .2f,
            Vector3.down, 8f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < .85f || hit.collider.attachedRigidbody != null ||
                hit.collider.GetComponentInParent<BookItem>() != null ||
                hit.collider.GetComponentInParent<NetworkPlayerSetup>() != null ||
                hit.collider.GetComponentInParent<ShelfSlot>() != null ||
                hit.distance >= closest) continue;
            closest = hit.distance;
            floor = hit;
        }
        return closest < float.PositiveInfinity;
    }
}
