using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
namespace ComicShop {
[DefaultExecutionOrder(10010)]
public sealed class ComicPauseMenu : MonoBehaviour {
    public GameObject pausePanel, settingsPanel;
    public string menuSceneName="MainMenu";
    [Tooltip("For multiplayer, connect your existing disconnect-and-return method. When assigned, that method owns scene loading.")]
    public UnityEngine.Events.UnityEvent returnToMenuRequested = new UnityEngine.Events.UnityEvent();
    [Tooltip("Only the LOCAL player's look, movement and interaction scripts. Never assign network managers or this menu.")]
    public Behaviour[] localInputScripts;
    [Tooltip("Leave OFF for multiplayer. Controls are still blocked through Local Input Scripts.")]
    public bool pauseWorldTime=false;
    public bool lockCursorOnResume=true;
    bool opened, loading; bool[] previousEnabled; float previousTime;
    CursorLockMode previousLock; bool previousVisible;
    public static bool IsOpen { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsOpen=false; }
    void Start() { if(pausePanel) pausePanel.SetActive(false);if(settingsPanel)settingsPanel.SetActive(false); }
    void Update() {
        if(loading) return;
        if(InputCompat.EscapePressed()) { if(!opened) Open();else Back(); }
    }
    public void Open() {
        if(opened || loading) return;
        MenuBootstrap.EnsureEventSystem();
        previousTime=Time.timeScale;previousLock=Cursor.lockState;previousVisible=Cursor.visible;
        opened=true;IsOpen=true;
        previousEnabled=new bool[localInputScripts==null?0:localInputScripts.Length];
        for(int i=0;i<previousEnabled.Length;i++) {
            var script=localInputScripts[i]; if(!script || script==this) continue;
            previousEnabled[i]=script.enabled;script.enabled=false;
        }
        if(pauseWorldTime)Time.timeScale=0;
        if(pausePanel)pausePanel.SetActive(true);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void OpenSettings() { if(!opened)Open(); if(pausePanel)pausePanel.SetActive(false);if(settingsPanel)settingsPanel.SetActive(true); }
    public void Back() {
        if(settingsPanel && settingsPanel.activeSelf) {
            ComicPreferences.Save();settingsPanel.SetActive(false);if(pausePanel)pausePanel.SetActive(true);
        } else Resume();
    }
    public void Resume() {
        if(!opened)return;
        if(settingsPanel)settingsPanel.SetActive(false);if(pausePanel)pausePanel.SetActive(false);
        if(pauseWorldTime)Time.timeScale=previousTime;
        for(int i=0;previousEnabled!=null && localInputScripts!=null && i<previousEnabled.Length && i<localInputScripts.Length;i++)
            if(localInputScripts[i] && localInputScripts[i]!=this)localInputScripts[i].enabled=previousEnabled[i];
        opened=false;IsOpen=false;ComicPreferences.Save();
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState=lockCursorOnResume?CursorLockMode.Locked:previousLock;
        Cursor.visible=lockCursorOnResume?false:previousVisible;
    }
    public void ReturnToMenu() {
        if(returnToMenuRequested.GetPersistentEventCount()>0) { Resume();loading=true;returnToMenuRequested.Invoke();return; }
        if(!Application.CanStreamedLevelBeLoaded(menuSceneName)) {Debug.LogError("[ComicShop] Main menu scene is not in the build scene list.");return;}
        Resume();loading=true;Time.timeScale=1;SceneManager.LoadScene(menuSceneName);
    }
    void LateUpdate() { if(opened) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; } }
    void OnDisable() { Resume(); }
    void OnApplicationFocus(bool focus) { if(!focus && !opened && !loading)Open(); }
}}
