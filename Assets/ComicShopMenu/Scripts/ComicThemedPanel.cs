using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace ComicShop {
public sealed class ComicThemedPanel : MonoBehaviour {
    public enum Page { Settings, Credits, Pause }
    public Page page;
    public MainMenuController menu;
    public ComicPauseMenu pause;
    public Button firstButton;
    RectTransform board, content;
    static readonly Color Ink=new Color32(29,18,32,255), Paper=new Color32(255,221,163,255), Orange=new Color32(240,116,27,255);
    void Awake() { Build(); }
    void OnEnable() { if(firstButton && EventSystem.current) EventSystem.current.SetSelectedGameObject(firstButton.gameObject); }
    public void Build() {
        if(board) return;
        var overlay=GetComponent<Image>(); if(!overlay) overlay=gameObject.AddComponent<Image>();
        overlay.color=new Color(0.025f,.008f,.025f,.84f);
        board=Node("ComicBoard",transform,Vector2.zero,new Vector2(800,700));
        var graphic=board.gameObject.AddComponent<ComicPanelGraphic>(); graphic.color=Ink;
        Label(board,"COMIC SHOP  /  TIDY UP TOGETHER",new Vector2(0,298),19,Paper);
        var banner=Node("TitleBand",board,new Vector2(0,221),new Vector2(724,96));
        var band=banner.gameObject.AddComponent<ComicPanelGraphic>();band.color=Orange;band.dots=false;
        Label(banner,page==Page.Settings?"SETTINGS":page==Page.Pause?"TAKE A BREAK":"CREDITS",Vector2.zero,48,Ink);
        content=Node("Content",board,new Vector2(0,-2),new Vector2(680,340));
        if(page==Page.Settings) {
            firstButton=Button(board,"SOUND",new Vector2(-231,141),new Vector2(215,48),()=>ShowTab(0));
            Button(board,"DISPLAY",new Vector2(0,141),new Vector2(215,48),()=>ShowTab(1));
            Button(board,"CONTROLS",new Vector2(231,141),new Vector2(215,48),()=>ShowTab(2));
            ShowTab(0);
        } else if(page==Page.Credits) {
            Label(content,"COMIC SHOP:",new Vector2(0,78),30,Paper);
            Label(content,"TIDY UP TOGETHER",new Vector2(0,28),40,Orange);
            Label(content,"MENU MUSIC",new Vector2(0,-58),18,Paper);
            Label(content,"Sunday Morning Vinyl",new Vector2(0,-94),27,Paper);
        } else {
            firstButton=Button(content,"RESUME",new Vector2(0,68),new Vector2(540,76),()=>pause.Resume());
            Button(content,"SETTINGS",new Vector2(0,-27),new Vector2(540,76),()=>pause.OpenSettings());
            Button(content,"MAIN MENU",new Vector2(0,-122),new Vector2(540,76),()=>pause.ReturnToMenu());
        }
        var back=Button(board,page==Page.Pause?"RESUME  /  ESC":"BACK  /  ESC",new Vector2(0,-269),new Vector2(380,64),Close);
        if(!firstButton) firstButton=back;
        Label(board,"CHANGES SAVE AUTOMATICALLY",new Vector2(0,-318),15,Paper);
        if(page!=Page.Settings) board.Find("CHANGES SAVE AUTOMATICALLY").gameObject.SetActive(false);
    }
    void Close() { ComicPreferences.Save(); if(pause) pause.Back(); else if(menu) menu.ClosePanels(); }
    void ShowTab(int index) {
        for(int i=content.childCount-1;i>=0;i--) { content.GetChild(i).gameObject.SetActive(false); Destroy(content.GetChild(i).gameObject); }
        if(index==0) {
            Row("MASTER",110,0,1,ComicPreferences.Master,"cs_master_volume",false);
            Row("MUSIC",0,0,1,ComicPreferences.Music,"cs_music_volume",false);
            Row("EFFECTS",-110,0,1,ComicPreferences.Sfx,"cs_sfx_volume",false);
        } else if(index==1) {
            Row("FIELD OF VIEW",98,60,110,ComicPreferences.Fov,"cs_camera_fov",true);
            var b=Button(content,Screen.fullScreen?"FULLSCREEN: ON":"FULLSCREEN: OFF",new Vector2(0,-48),new Vector2(600,64),null);
            b.onClick.AddListener(()=> { bool next=!Screen.fullScreen; Screen.fullScreen=next; PlayerPrefs.SetInt("cs_fullscreen",next?1:0); ComicPreferences.Save(); b.GetComponentInChildren<Text>().text=next?"FULLSCREEN: ON":"FULLSCREEN: OFF"; });
        } else {
            Row("LOOK SENSITIVITY",100,.2f,3,ComicPreferences.Sensitivity,"cs_look_multiplier",false,true);
            Label(content,"ESC   PAUSE / BACK",new Vector2(0,-40),23,Paper);
            Label(content,"MOUSE / ARROW KEYS   NAVIGATE",new Vector2(0,-86),21,Paper);
            Label(content,"ENTER   SELECT",new Vector2(0,-126),21,Paper);
        }
    }
    void Row(string title,float y,float min,float max,float value,string key,bool whole,bool multiplier=false) {
        var label=Label(content,title,new Vector2(-80,y+22),23,Paper); label.alignment=TextAnchor.MiddleLeft;label.rectTransform.sizeDelta=new Vector2(490,34);
        var number=Label(content,"",new Vector2(270,y+22),24,Orange);number.rectTransform.sizeDelta=new Vector2(100,34);
        Action<float> show=v=>number.text=multiplier?v.ToString("0.00")+"x":whole?v.ToString("0")+"°":Mathf.RoundToInt(v*100)+"%";
        show(value);
        var root=Node(title+"Slider",content,new Vector2(0,y-17),new Vector2(646,32));
        var bg=Node("Track",root,Vector2.zero,new Vector2(646,12));bg.gameObject.AddComponent<Image>().color=new Color32(84,55,63,255);
        var fillArea=Node("FillArea",root,Vector2.zero,new Vector2(622,12));
        var fill=Node("Fill",fillArea,Vector2.zero,Vector2.zero);fill.gameObject.AddComponent<Image>().color=Orange;
        var handleArea=Node("HandleArea",root,Vector2.zero,new Vector2(622,32));
        var handle=Node("Handle",handleArea,Vector2.zero,new Vector2(24,32));var hi=handle.gameObject.AddComponent<Image>();hi.color=Paper;
        var s=root.gameObject.AddComponent<Slider>();s.fillRect=fill;s.handleRect=handle;s.targetGraphic=hi;s.minValue=min;s.maxValue=max;s.wholeNumbers=whole;s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(v=>{show(v);ComicPreferences.Set(key,v);});
    }
    static RectTransform Node(string name,Transform parent,Vector2 pos,Vector2 size) {
        var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    static Text Label(Transform parent,string text,Vector2 pos,int size,Color color) {
        var r=Node(text,parent,pos,new Vector2(700,58));var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.fontStyle=FontStyle.BoldAndItalic;t.alignment=TextAnchor.MiddleCenter;t.color=color;t.raycastTarget=false;return t;
    }
    static Button Button(Transform parent,string text,Vector2 pos,Vector2 size,Action action) {
        var r=Node(text,parent,pos,size);var g=r.gameObject.AddComponent<ComicPanelGraphic>();g.color=Paper;g.dots=false;
        var label=Label(r,text,Vector2.zero,size.y>60?28:21,Ink);label.rectTransform.sizeDelta=size-new Vector2(18,8);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=g;b.transition=Selectable.Transition.None;
        var fx=r.gameObject.AddComponent<ComicMenuButton>();fx.normalColor=Paper;fx.hoverColor=Orange;fx.hoverOffset=new Vector2(-3,0);fx.hoverScale=1.035f;fx.hoverTilt=-.6f;fx.fallbackPointer=false;
        if(action!=null)b.onClick.AddListener(()=>action());return b;
    }
}}
