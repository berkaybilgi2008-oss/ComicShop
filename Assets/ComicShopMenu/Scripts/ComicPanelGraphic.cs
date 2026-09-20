using UnityEngine;
using UnityEngine.UI;
namespace ComicShop {
// Native UI geometry: crisp ink border, clipped corners and subtle printed dots.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ComicPanelGraphic : MaskableGraphic {
    public bool dots = true;
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear(); var r = rectTransform.rect;
        Polygon(vh, r, new Color32(7,5,9,255), 18);
        Polygon(vh, Inset(r,5), new Color32(248,193,103,255), 15);
        Polygon(vh, Inset(r,8), color, 13);
        if (!dots) return;
        for(float y=r.yMin+16;y<r.yMax-16;y+=18)
            for(float x=r.xMin+16;x<r.xMax-16;x+=18) {
                float a=Mathf.InverseLerp(r.xMin,r.xMax,x)*0.14f;
                Quad(vh,new Rect(x,y,3,3),new Color(1,.65f,.22f,a));
            }
    }
    static Rect Inset(Rect r,float n) => new Rect(r.x+n,r.y+n,r.width-2*n,r.height-2*n);
    static void Quad(VertexHelper vh,Rect r,Color c) {
        int i=vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero); vh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero); vh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    static void Polygon(VertexHelper vh,Rect r,Color c,float cut) {
        int i=vh.currentVertCount; vh.AddVert(r.center,c,Vector2.zero);
        Vector2[] p={new Vector2(r.xMin+cut,r.yMin),new Vector2(r.xMin,r.yMin+cut),new Vector2(r.xMin,r.yMax-cut),new Vector2(r.xMin+cut,r.yMax),new Vector2(r.xMax-cut,r.yMax),new Vector2(r.xMax,r.yMax-cut),new Vector2(r.xMax,r.yMin+cut),new Vector2(r.xMax-cut,r.yMin)};
        foreach(var v in p) vh.AddVert(v,c,Vector2.zero);
        for(int n=0;n<8;n++) vh.AddTriangle(i,i+1+n,i+1+(n+1)%8);
    }
}}
