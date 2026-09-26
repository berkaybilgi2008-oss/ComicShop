using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using Object = UnityEngine.Object;

public sealed partial class ShopFrontEnd
{
    static readonly Color ComicOrange=new Color32(255,170,22,255);
    static readonly Color ComicPaper=new Color32(255,223,168,255);
    static readonly Color ComicInk=new Color32(24,21,18,255);
    RectTransform comicCursor;
    void PrepareComicFrame()
    {
        card.GetComponent<Image>().color=Paper;
        var originalOutline=card.GetComponent<Outline>();if(originalOutline)originalOutline.enabled=true;
        card.sizeDelta=(page==Page.Settings || page==Page.Credits)?new Vector2(1120,660):new Vector2(1080,760);
        if(comicCursor)return;
        var texture=Resources.Load<Texture2D>("ComicShopMenu/cursor_comic");
        if(!texture)return;
        var go=new GameObject("Comic cursor",typeof(RectTransform),typeof(RawImage));
        go.transform.SetParent(canvas.transform,false);
        comicCursor=(RectTransform)go.transform;comicCursor.pivot=new Vector2(.06f,.94f);comicCursor.sizeDelta=new Vector2(40,51);
        go.GetComponent<RawImage>().texture=texture;go.AddComponent<ComicProjectCursor>();
    }
    void ApplyComicTheme()
    {
        if(page==Page.Settings)return;
        if(page==Page.Credits){BuildComicCredits();return;}
        var backdrop=new GameObject("Comic print frame",typeof(RectTransform),typeof(CanvasRenderer),typeof(ComicShop.ComicPanelGraphic));
        backdrop.transform.SetParent(card,false);Stretch((RectTransform)backdrop.transform);
        var graphic=backdrop.GetComponent<ComicShop.ComicPanelGraphic>();graphic.color=Paper;graphic.raycastTarget=false;
        backdrop.transform.SetAsFirstSibling();
    }
    RectTransform ComicBox(Transform parent,string name,float x,float y,float w,float h,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(ComicShop.ComicPanelGraphic));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;Rect(r,x,y,w,h);go.GetComponent<ComicShop.ComicPanelGraphic>().color=color;return r;
    }
    Text ComicText(Transform parent,string value,float x,float y,float w,float h,int size,bool heading=false)
    {
        var text=Label(parent,value,x,y,w,h,size,ComicInk);
        // Dile uygun font (Latin/Kiril/CJK). Basliklar menu fontuyla; uzun ceviriler kutuya sigacak kadar kuculur.
        text.font=heading?Loc.Display(font):Loc.Body(font);text.fontStyle=FontStyle.Normal;
        text.resizeTextForBestFit=true;text.resizeTextMaxSize=size;text.resizeTextMinSize=Mathf.Max(10,Mathf.RoundToInt(size*.55f));
        return text;
    }
    void ComicRule(Transform parent,float y) {var line=Panel(parent,"Ink rule",ComicInk);Rect(line,24,y,798,2);line.GetComponent<Image>().raycastTarget=false;}
    Button ComicButton(Transform parent,string title,float x,float y,float w,float h,Action click,bool active=false,int fontSize=27)
    {
        var r=ComicBox(parent,title,x,y,w,h,active?ComicOrange:ComicPaper);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=r.GetComponent<ComicShop.ComicPanelGraphic>();button.transition=Selectable.Transition.None;
        r.gameObject.AddComponent<ComicShopMenuFeedback>();
        var t=ComicText(r,title,12,5,w-24,h-10,fontSize,true);t.alignment=TextAnchor.MiddleCenter;
        button.onClick.AddListener(()=>{ShopAudio.Play(ShopCue.Click,Vector3.zero,false);click();});return button;
    }
    void ComicToggle(Transform parent,string title,float y,bool on,Action<bool> change)
    {
        ComicText(parent,title,30,y,510,54,27);
        var go=new GameObject(title+" switch",typeof(RectTransform),typeof(CanvasRenderer),typeof(ComicPillGraphic),typeof(Button));go.transform.SetParent(parent,false);
        Rect((RectTransform)go.transform,604,y+2,215,50);var pill=go.GetComponent<ComicPillGraphic>();pill.on=on;
        var button=go.GetComponent<Button>();button.targetGraphic=pill;button.transition=Selectable.Transition.None;
        var text=ComicText(go.transform,on?Loc.T("toggle.on"):Loc.T("toggle.off"),on?8:48,4,155,42,24,true);text.alignment=TextAnchor.MiddleCenter;text.color=on?ComicInk:ComicPaper;
        button.onClick.AddListener(()=>{change(!on);ShopSettings.Apply();Build();});
        ComicRule(parent,y+65);
    }
    void ComicSlider(Transform parent,string title,float y,float min,float max,float value,Action<float> change,bool percent=false)
    {
        var text=ComicText(parent,title,30,y,540,35,26);
        var number=ComicText(parent,percent?Mathf.RoundToInt(value*100)+"%":value.ToString("0.0"),680,y,130,35,26);number.alignment=TextAnchor.MiddleRight;
        var root=Panel(parent,"Slider",ComicInk);Rect(root,32,y+48,780,12);
        var fill=Panel(root,"Fill",ComicOrange);Stretch(fill);
        var handle=Panel(root,"Handle",ComicPaper);handle.sizeDelta=new Vector2(22,24);
        var slider=root.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v=>{change(v);number.text=percent?Mathf.RoundToInt(v*100)+"%":v.ToString("0.0");ShopSettings.Apply();});
    }
    void BuildComicSettings()
    {
        // Build() created the legacy rail. Hide it before composing the reference layout.
        foreach(Transform child in card)child.gameObject.SetActive(false);
        var outline=card.GetComponent<Outline>();if(outline)outline.enabled=false;
        card.GetComponent<Image>().color=Color.clear;
        var frame=ComicBox(card,"Settings frame",0,0,1120,660,new Color(.08f,.04f,.08f));
        string[] titles={Loc.T("settings.tab.general"),Loc.T("settings.tab.display"),Loc.T("settings.tab.audio"),Loc.T("settings.tab.controls"),Loc.T("settings.tab.access")};
        tab=Mathf.Clamp(tab,0,4);
        for(int i=0;i<titles.Length;i++){int selected=i;ComicButton(frame,titles[i],12,18+i*104,236,94,()=>{tab=selected;waitingKey=null;Build();},tab==i,i==4?21:28);}
        var sheet=ComicBox(frame,"Paper settings",255,10,854,542,ComicPaper);
        ComicText(sheet,titles[tab],30,20,788,67,52,true);
        string[] intros={Loc.T("settings.intro.general"),Loc.T("settings.intro.display"),Loc.T("settings.intro.audio"),Loc.T("settings.intro.controls"),Loc.T("settings.intro.access")};
        var intro=ComicText(sheet,intros[tab],32,87,790,32,22);intro.fontStyle=FontStyle.Italic;
        ComicRule(sheet,130);
        var p=ShopSettings.Current;
        if(tab==0)
        {
            ComicToggle(sheet,Loc.T("settings.hints"),151,p.hints,v=>p.hints=v);
            ComicToggle(sheet,Loc.T("settings.crosshair"),233,ComicInterfaceOptions.CrosshairVisible,v=>ComicInterfaceOptions.CrosshairVisible=v);
            // Dil secimi: her dil kendi adiyla ve kendi yazisiyla. Kitap/yayinci adlari cevrilmez.
            ComicText(sheet,Loc.T("settings.language")+"  ·  LANGUAGE",30,314,788,34,24,true);
            for(int i=0;i<Loc.Count;i++)
            {
                var language=(GameLanguage)i;
                var chip=ComicButton(sheet,Loc.NativeNames[i],30+(i%5)*159,356+(i/5)*46,151,40,()=>Loc.Set(language),Loc.Current==language,19);
                var label=chip.GetComponentInChildren<Text>();if(label)label.font=Loc.NativeFont(language,font);
            }
        }
        else if(tab==1)
        {
            ComicSlider(sheet,Loc.T("settings.fov"),150,55,105,p.fov,v=>{p.fov=v;p.fovChosen=true;});
            ComicToggle(sheet,"VSync",231,p.vsync,v=>p.vsync=v);
            ComicButton(sheet,Loc.T("settings.fps",p.fps),30,311,382,52,()=>{p.fps=p.fps<60?60:p.fps<120?120:p.fps<144?144:p.fps<240?240:30;ShopSettings.Apply();Build();},false,22);
            ComicButton(sheet,Screen.width+" × "+Screen.height,435,311,382,52,CycleResolution,false,22);
            ComicButton(sheet,Screen.fullScreenMode==FullScreenMode.Windowed?Loc.T("settings.windowed"):Loc.T("settings.borderless"),30,384,787,52,
                ()=>PreviewDisplay(Screen.width,Screen.height,Screen.fullScreenMode==FullScreenMode.Windowed?1:0),false,24);
            ComicText(sheet,Application.isEditor?Loc.T("settings.display_note_editor"):Loc.T("settings.display_note"),32,462,782,44,18);
        }
        else if(tab==2)
        {
            ComicSlider(sheet,Loc.T("settings.master"),151,0,1,p.master,v=>p.master=v,true);
            ComicSlider(sheet,Loc.T("settings.effects"),240,0,1,p.effects,v=>p.effects=v,true);
            ComicSlider(sheet,Loc.T("settings.ambience"),329,0,1,p.ambience,v=>p.ambience=v,true);
            ComicSlider(sheet,Loc.T("settings.music"),418,0,1,p.music,v=>p.music=v,true);
        }
        else if(tab==3)
        {
            ComicSlider(sheet,Loc.T("settings.sensitivity"),145,.1f,10,p.sensitivity,v=>p.sensitivity=v);
            ComicToggle(sheet,Loc.T("settings.invert"),220,p.invertY,v=>p.invertY=v);
            for(int i=0;i<10;i++) {var action=(ShopAction)i;
                // Eylem adi solda okunakli govde fontuyla, tus sagda menu fontuyla: uzun ceviriler tusu ezmez.
                var bind=ComicButton(sheet,"",28+(i%2)*403,296+(i/2)*41,394,38,
                    ()=>{waitingKey=action;listenAfterFrame=Time.frameCount+1;if(statusText)statusText.text=Loc.T("settings.press_key");},false,17);
                var actionText=ComicText(bind.transform,ShopSettings.Label(action),26,4,242,30,19);actionText.alignment=TextAnchor.MiddleLeft;
                var keyText=ComicText(bind.transform,ShopHud.KeyName(ShopSettings.Key(action)),270,4,98,30,21,true);keyText.alignment=TextAnchor.MiddleRight;
            }
        }
        else
        {
            ComicToggle(sheet,Loc.T("settings.large_cursor"),151,ComicInterfaceOptions.LargeCursor,v=>ComicInterfaceOptions.LargeCursor=v);
            ComicToggle(sheet,Loc.T("settings.reduce_motion"),233,ComicInterfaceOptions.ReducedMotion,v=>ComicInterfaceOptions.ReducedMotion=v);
        }
        statusText=ComicText(sheet,"",30,508,790,25,17);
        ComicButton(frame,Loc.T("lobby.back"),12,570,302,76,ComicSettingsBack,false,27);
        ComicButton(frame,Loc.T("settings.defaults"),326,570,450,76,()=>{ShopSettings.ResetDefaults();ComicInterfaceOptions.Reset();PlayerPrefs.Save();Build();},false,27);
        ComicButton(frame,Loc.T("settings.apply"),789,570,319,76,()=>{ShopSettings.Apply();ShopSettings.Save();PlayerPrefs.Save();statusText.text=Loc.T("settings.saved");},true,38);
    }
    void BuildComicCredits()
    {
        foreach(Transform child in card) child.gameObject.SetActive(false);
        var outline=card.GetComponent<Outline>();if(outline)outline.enabled=false;
        card.GetComponent<Image>().color=Color.clear;
        var frame=ComicBox(card,"Credits frame",0,0,1120,660,new Color(.08f,.04f,.08f));
        ComicButton(frame,Loc.T("credits.title"),12,18,236,94,()=>{},true,32);
        var rail=ComicBox(frame,"Credits sidebar",12,126,236,426,ComicPaper);
        ComicText(rail,"COMIC\nSHOP",20,28,196,120,43,true);
        ComicText(rail,"TIDY UP\nTOGETHER",20,170,196,100,27,true);
        ComicText(rail,Loc.T("credits.tagline"),20,305,196,95,20);
        var sheet=ComicBox(frame,"Credits paper",255,10,854,542,ComicPaper);
        ComicText(sheet,Loc.T("credits.title"),30,20,788,67,52,true);
        var intro=ComicText(sheet,"COMIC SHOP: TIDY UP TOGETHER",32,87,790,32,24);
        intro.fontStyle=FontStyle.Italic;
        ComicRule(sheet,130);
        ComicText(sheet,Loc.T("credits.music"),32,163,780,38,25,true);
        var music=ComicBox(sheet,"Music credit",26,218,800,104,ComicOrange);
        ComicText(music,"Sunday Morning Vinyl",24,21,750,62,34,true);
        ComicRule(sheet,356);
        ComicText(sheet,Loc.T("credits.thanks"),32,392,780,45,28,true);
        ComicText(sheet,Loc.T("credits.motto"),32,454,780,38,23);
        ComicButton(frame,Loc.T("lobby.back"),12,570,302,76,ComicCreditsBack,false,27);
        ComicButton(frame,Connected?Loc.T("credits.back_session"):Loc.T("credits.main_menu"),326,570,782,76,ComicCreditsBack,true,35);
    }
    void ComicCreditsBack() {page=Connected?Page.Session:Page.Title;Build();}
    void ComicSettingsBack() {waitingKey=null;PlayerPrefs.Save();if(Connected)Resume();else BackFromSettings();}
}

// Remove the duplicate generic UI on each gameplay scene load, including reconnects.
// Do not disable player scripts: the existing controllers already gate input on cursor lock.
public static class ComicProjectMenuOwnership
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindFirstObjectByType<BookSpawner>() == null) return;
        foreach (var pause in Object.FindObjectsByType<ComicShop.ComicPauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            pause.gameObject.SetActive(false);
        foreach (var menu in Object.FindObjectsByType<ComicShop.MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var canvas = menu.GetComponentInParent<Canvas>(true);
            if (canvas != null) canvas.gameObject.SetActive(false);
        }
        // ShopAudio and ShopFrontEnd apply master gain themselves; do not multiply it twice.
        AudioListener.volume = 1f;
    }
}
