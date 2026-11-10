// Run with script eval --file in the saved Farm scene, Edit Mode.
using System;
using System.Linq;
using GrowWhatever;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

class SettingsAuthoring
{
    static TMP_FontAsset font;
    static Color ink = new Color32(237,231,202,255), muted = new Color32(179,190,162,255);
    static Color dark = new Color32(22,38,32,255), green = new Color32(74,103,72,255);

    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Farm.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Farm scene in Edit Mode first.");
        var canvas = GameObject.Find("Farm Canvas");
        var session = UnityEngine.Object.FindFirstObjectByType<FarmSession>();
        if (canvas == null || session == null || canvas.transform.Find("Player Settings") != null)
            throw new InvalidOperationException("Expected farm scene without an existing settings panel.");
        font = canvas.GetComponentsInChildren<TMP_Text>(true).First().font;
        var backgrounds = canvas.transform.Cast<Transform>().Select(x =>
        {
            var group = x.GetComponent<CanvasGroup>();
            return group != null ? group : x.gameObject.AddComponent<CanvasGroup>();
        }).ToArray();
        var serviceRoot = new GameObject("Player Settings Service");
        var service = serviceRoot.AddComponent<PlayerSettingsService>();
        Ref(UnityEngine.Object.FindFirstObjectByType<FarmAudio>(), "playerSettings", service);
        var view = canvas.AddComponent<PlayerSettingsView>();
        Ref(view,"settings",service); Ref(view,"session",session); Ref(view,"hud",canvas.GetComponent<FarmHudView>());
        Ref(view,"tradeDialog",canvas.GetComponentsInChildren<TradeQuantityView>(true).Single());
        Ref(view,"openButton",Button("SettingsButton",canvas.transform.Find("Garden"),"Settings",new Vector2(114,268),new Vector2(112,28),14));

        var overlay = Box("Player Settings", canvas.transform, Vector2.zero, Vector2.zero, new Color(0,0,0,0.8f));
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Ref(view,"panel",overlay.gameObject);
        var dialog = Box("Dialog",overlay,Vector2.zero,new Vector2(760,730),new Color32(35,57,48,255));
        var content = dialog.gameObject.AddComponent<CanvasGroup>(); Ref(view,"content",content);
        Label("Title",dialog,"SETTINGS",new Vector2(0,315),new Vector2(620,44),30,ink);
        Label("Subtitle",dialog,"Your farm keeps growing while settings are open.",new Vector2(0,274),new Vector2(680,30),17,muted);
        Label("DisplayTitle",dialog,"DISPLAY",new Vector2(-265,226),new Vector2(160,30),19,ink);
        Label("ModeTitle",dialog,"Window mode",new Vector2(-225,179),new Vector2(230,34),19,ink);
        var mode = Button("ModeButton",dialog,"Windowed",new Vector2(140,179),new Vector2(350,42),19);
        Ref(view,"modeButton",mode); Ref(view,"modeLabel",mode.GetComponentInChildren<TMP_Text>());
        Label("ResolutionTitle",dialog,"Resolution",new Vector2(-225,124),new Vector2(230,34),19,ink);
        Ref(view,"previousButton",Button("PreviousResolution",dialog,"<",new Vector2(-10,124),new Vector2(48,42),24));
        Ref(view,"resolutionLabel",Label("Resolution",dialog,"1280 x 720",new Vector2(140,124),new Vector2(240,40),19,ink));
        Ref(view,"nextButton",Button("NextResolution",dialog,">",new Vector2(290,124),new Vector2(48,42),24));
        Ref(view,"resolutionHint",Label("ResolutionHint",dialog,"",new Vector2(0,79),new Vector2(680,30),16,muted));
        Label("AudioTitle",dialog,"AUDIO",new Vector2(-265,28),new Vector2(160,30),19,ink);
        Slider(view,dialog,"Master",-23,"masterSlider","masterLabel");
        Slider(view,dialog,"Music",-84,"musicSlider","musicLabel");
        Ref(view,"musicHint",Label("MusicHint",dialog,"No background music yet.",new Vector2(80,-117),new Vector2(450,24),14,muted));
        Slider(view,dialog,"Effects",-155,"effectsSlider","effectsLabel");
        Ref(view,"noticeLabel",Label("Notice",dialog,"",new Vector2(0,-213),new Vector2(680,58),16,new Color32(242,209,123,255)));
        Ref(view,"defaultsButton",Button("DefaultsButton",dialog,"Restore defaults",new Vector2(-225,-287),new Vector2(210,48),18));
        Ref(view,"applyButton",Button("ApplyButton",dialog,"Apply display",new Vector2(0,-287),new Vector2(210,48),18));
        Ref(view,"closeButton",Button("CloseButton",dialog,"Close / Esc",new Vector2(225,-287),new Vector2(210,48),18));
        Label("SaveHint",dialog,"Volume saves immediately. Display changes require confirmation.",new Vector2(0,-335),new Vector2(700,24),15,muted);

        var confirmation = Box("Confirmation",overlay,Vector2.zero,Vector2.zero,new Color(0,0,0,0.88f));
        confirmation.anchorMin = Vector2.zero; confirmation.anchorMax = Vector2.one; confirmation.offsetMin = confirmation.offsetMax = Vector2.zero;
        Ref(view,"confirmation",confirmation.gameObject);
        var card = Box("Dialog",confirmation,Vector2.zero,new Vector2(660,280),new Color32(35,57,48,255));
        Ref(view,"confirmationText",Label("Message",card,"",new Vector2(0,40),new Vector2(600,115),23,ink));
        var keep = Button("KeepButton",card,"Keep changes",new Vector2(-150,-80),new Vector2(275,50),20);
        var revert = Button("RevertButton",card,"Revert",new Vector2(150,-80),new Vector2(275,50),20);
        Ref(view,"keepButton",keep); Ref(view,"revertButton",revert);
        Ref(view,"keepLabel",keep.GetComponentInChildren<TMP_Text>()); Ref(view,"revertLabel",revert.GetComponentInChildren<TMP_Text>());
        var serialized = new SerializedObject(view); var groups = serialized.FindProperty("backgroundGroups"); groups.arraySize = backgrounds.Length;
        for (int i=0;i<backgrounds.Length;i++) groups.GetArrayElementAtIndex(i).objectReferenceValue = backgrounds[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        confirmation.gameObject.SetActive(false); overlay.gameObject.SetActive(false);
        PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.resizableWindow=true; PlayerSettings.runInBackground=true;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return new { scene = scene.path, savedUI = true, sliders = 3, resolutions = 5 };
    }

    static void Slider(PlayerSettingsView view,Transform parent,string title,float y,string sliderField,string labelField)
    {
        Label(title+"Title",parent,title,new Vector2(-250,y),new Vector2(180,34),19,ink);
        var track = Box(title+"Slider",parent,new Vector2(60,y),new Vector2(340,34),dark);
        var fillArea = Rect("Fill Area",track,Vector2.zero,new Vector2(320,12));
        var fill = Box("Fill",fillArea,Vector2.zero,Vector2.zero,green);
        fill.anchorMin=Vector2.zero; fill.anchorMax=Vector2.one; fill.offsetMin=fill.offsetMax=Vector2.zero;
        var handleArea = Rect("Handle Area",track,Vector2.zero,new Vector2(320,34));
        var handle = Box("Handle",handleArea,Vector2.zero,new Vector2(20,32),ink);
        var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.minValue=0; slider.maxValue=1;
        slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
        handle.sizeDelta=new Vector2(20,0);
        Ref(view,sliderField,slider);
        Ref(view,labelField,Label(title+"Value",parent,"70%",new Vector2(285,y),new Vector2(80,34),19,ink));
    }
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false);
        var rect=(RectTransform)obj.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);
        rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
    }
    static RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    { var rect=Rect(name,parent,position,size); rect.gameObject.AddComponent<UnityEngine.UI.Image>().color=color; return rect; }
    static TMP_Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,float sizeText,Color color)
    {
        var rect=Rect(name,parent,position,size); var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font; label.text=text; label.fontSize=sizeText; label.color=color; label.alignment=TextAlignmentOptions.Center;
        label.raycastTarget=false; return label;
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size,float fontSize)
    {
        var rect=Box(name,parent,position,size,green); var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
        var label=Label("Label",rect,text,Vector2.zero,size,fontSize,ink);
        label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one; label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
        return button;
    }
    static void Ref(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var data=new SerializedObject(target); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
}
return SettingsAuthoring.Apply();
