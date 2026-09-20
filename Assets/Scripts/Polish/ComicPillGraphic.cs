using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ComicPillGraphic : MaskableGraphic
{
    public bool on;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        Capsule(vh,r,new Color32(22,22,19,255));
        var inner=new Rect(r.x+3,r.y+3,r.width-6,r.height-6);
        Capsule(vh,inner,on?new Color32(255,173,22,255):new Color32(38,38,33,255));
        float radius=r.height*.5f-6;
        Circle(vh,new Vector2(on?r.xMax-r.height*.5f:r.xMin+r.height*.5f,r.center.y),radius+2,Color.black);
        Circle(vh,new Vector2(on?r.xMax-r.height*.5f:r.xMin+r.height*.5f,r.center.y),radius,new Color32(255,226,175,255));
    }
    static void Capsule(VertexHelper v,Rect r,Color c)
    {
        float rad=r.height*.5f;int start=v.currentVertCount;
        v.AddVert(r.center,c,Vector2.zero);
        for(int i=0;i<=16;i++){float a=(-90+i*180f/16)*Mathf.Deg2Rad;v.AddVert(new Vector2(r.xMax-rad+Mathf.Cos(a)*rad,r.center.y+Mathf.Sin(a)*rad),c,Vector2.zero);}
        for(int i=0;i<=16;i++){float a=(90+i*180f/16)*Mathf.Deg2Rad;v.AddVert(new Vector2(r.xMin+rad+Mathf.Cos(a)*rad,r.center.y+Mathf.Sin(a)*rad),c,Vector2.zero);}
        for(int i=0;i<34;i++)v.AddTriangle(start,start+1+i,start+1+(i+1)%34);
    }
    static void Circle(VertexHelper v,Vector2 p,float radius,Color c)
    {
        int start=v.currentVertCount;v.AddVert(p,c,Vector2.zero);
        for(int i=0;i<32;i++){float a=i*Mathf.PI/16;v.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);}
        for(int i=0;i<32;i++)v.AddTriangle(start,start+1+i,start+1+(i+1)%32);
    }
}
