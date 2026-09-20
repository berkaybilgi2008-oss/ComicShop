using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class ComicInterfaceOptions
{
    public static bool CrosshairVisible { get => PlayerPrefs.GetInt("ComicShop.UI.Crosshair",1)!=0; set => PlayerPrefs.SetInt("ComicShop.UI.Crosshair",value?1:0); }
    public static bool LargeCursor { get => PlayerPrefs.GetInt("ComicShop.UI.LargeCursor",0)!=0; set => PlayerPrefs.SetInt("ComicShop.UI.LargeCursor",value?1:0); }
    public static bool ReducedMotion { get => PlayerPrefs.GetInt("ComicShop.UI.ReducedMotion",0)!=0; set => PlayerPrefs.SetInt("ComicShop.UI.ReducedMotion",value?1:0); }
    public static void Reset() {CrosshairVisible=true;LargeCursor=false;ReducedMotion=false;}
}

