using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GoGhost.Duel.EditorTools
{
    public sealed partial class VoiceStudioWindow
    {
        void OnGUI()
        {
            if (catalog==null) return;
            if (!File.Exists(Python))
            {
                EditorGUILayout.HelpBox("Use Python 3.12 to install the local audio worker. Synthesis and rendering run on this computer.",MessageType.Info);
                using (new EditorGUI.DisabledScope(busy))
                    if (GUILayout.Button("Install audio environment…")) Setup();
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(busy))
            {
                listScroll=EditorGUILayout.BeginScrollView(listScroll,GUILayout.Width(192));
                foreach (var line in catalog.lines)
                    if (GUILayout.Toggle(key==line.key,line.label,EditorStyles.miniButton) && key!=line.key && CanLeave())
                    { key=line.key; revision++; scope=0; Reload(); }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.BeginVertical();
            using (new EditorGUI.DisabledScope(busy))
            {
                int tab=GUILayout.Toolbar(scope,new[]{"Line","Mix & speakers"});
                if (tab!=scope && CanLeave()) { scope=tab; revision++; Reload(); }
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(publishing))
            {
                if (recipe!=null && scope==0) LinePanel();
                if (recipe!=null && scope==1) Profiles();
            }
            EditorGUILayout.EndScrollView();
            if (recipe!=null) Transport();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(status ?? "",MessageType.None);
        }

        void LinePanel()
        {
            var line=catalog.Find(key);
            GUILayout.Label(line.label,EditorStyles.boldLabel);
            if (line.locked && !fork)
            {
                EditorGUILayout.HelpBox("Approved opening master · excluded from speaker batches",MessageType.Info);
                if (GUILayout.Button("Create a line-specific draft")) fork=true;
            }
            using (new EditorGUI.DisabledScope(line.locked && !fork))
            {
                EditorGUI.BeginChangeCheck();
                string before=recipe.mode;
                int mode=EditorGUILayout.Popup("Source",Mathf.Max(0,Array.IndexOf(Modes,before)),
                    new[]{"Finished recording","Imported dry performance","Synthesize text"});
                recipe.mode=Modes[mode];
                if (recipe.mode!=before) { recipe.source=""; ClearMarks(); }
                if (recipe.mode!="Synthesize")
                {
                    if (GUILayout.Button("Choose recording…"))
                    {
                        string path=EditorUtility.OpenFilePanel("Choose recording","","");
                        if (path.Length>0) { recipe.source=path; ClearMarks(); Changed(); }
                    }
                    if (recipe.mode=="Master") EditorGUILayout.LabelField("Timbre baked into recording",EditorStyles.miniLabel);
                }
                EditorGUILayout.LabelField("Spoken text");
                string text=EditorGUILayout.TextArea(recipe.text??"",GUILayout.MinHeight(36));
                if (text!=recipe.text && recipe.mode=="Synthesize") ClearMarks();
                recipe.text=text;
                if (recipe.mode=="Synthesize")
                {
                    EditorGUILayout.LabelField("Pronunciation overrides · word followed by Kokoro phones");
                    recipe.pronunciations=EditorGUILayout.TextArea(recipe.pronunciations??"");
                }
                if (line.transcribed) EditorGUILayout.LabelField("Transcribed from the placeholder take · review before synthesis",EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                recipe.speed=EditorGUILayout.Slider("Tempo ×",recipe.speed,.55f,1.8f);
                if (GUILayout.Button("+2%",GUILayout.Width(44))) {recipe.speed=Mathf.Min(1.8f,recipe.speed*1.02f);Changed();}
                if (GUILayout.Button("+10%",GUILayout.Width(48))) {recipe.speed=Mathf.Min(1.8f,recipe.speed*1.1f);Changed();}
                EditorGUILayout.EndHorizontal();
                recipe.pitch=EditorGUILayout.Slider("Pitch (semitones)",recipe.pitch,-8,8);
                recipe.gain=EditorGUILayout.Slider("Line gain (dB)",recipe.gain,-18,6);
                if (!line.locked) recipe.independent=EditorGUILayout.Toggle("Exclude from speaker renders",recipe.independent);
                if (recipe.mode!="Master")
                {
                    recipe.overrideTone=EditorGUILayout.Toggle("Override speaker timbre",recipe.overrideTone);
                    if (recipe.overrideTone && key.StartsWith("Handler/"))
                    {
                        recipe.electronic=EditorGUILayout.Slider("Electronic blend",recipe.electronic,0,1);
                        recipe.nearDb=EditorGUILayout.Slider("Near returns (Δ dB)",recipe.nearDb,-18,8);
                        recipe.spaceDb=EditorGUILayout.Slider("Diffuse returns (Δ dB)",recipe.spaceDb,-18,8);
                    }
                    if (recipe.overrideTone && key.StartsWith("FinalBossVoice/"))
                    {
                        recipe.bossSub=EditorGUILayout.Slider("Sub layer",recipe.bossSub,0,1);
                        recipe.bossDrive=EditorGUILayout.Slider("Saturation drive",recipe.bossDrive,.5f,3);
                    }
                }
                if (EditorGUI.EndChangeCheck()) Changed();
                Waveform();
                Regions();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(string.IsNullOrEmpty(recipe.reference) ? "Choose performance reference…" : Path.GetFileName(recipe.reference)))
                {
                    string path=EditorUtility.OpenFilePanel("Performance reference","","");
                    if (path.Length>0) { recipe.reference=path; Changed(); }
                }
                if (!string.IsNullOrEmpty(recipe.reference) && GUILayout.Button("Clear",GUILayout.Width(52)))
                {recipe.reference=""; Changed();}
                EditorGUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(recipe.reference))
                {
                    recipe.timing=EditorGUILayout.Slider("Follow timing",recipe.timing,0,1);
                    recipe.melody=EditorGUILayout.Slider("Follow pitch contour",recipe.melody,0,1);
                    recipe.stress=EditorGUILayout.Slider("Follow emphasis",recipe.stress,0,1);
                }
                if (EditorGUI.EndChangeCheck()) Changed();
            }
        }
        void ClearMarks()
        {
            recipe.edits.Clear(); region=-1; selectFrom=selectTo=0;
        }
        void Waveform()
        {
            if (source==null) return;
            Rect rect=GUILayoutUtility.GetRect(40,88,GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect,new Color(.09f,.11f,.14f));
            if (selectTo>selectFrom)
            {
                float left=rect.x+rect.width*selectFrom/source.Seconds;
                float right=rect.x+rect.width*Mathf.Min(1,selectTo/source.Seconds);
                EditorGUI.DrawRect(new Rect(left,rect.y,right-left,rect.height),new Color(.16f,.28f,.38f));
            }
            if (Event.current.type==EventType.Repaint)
            {
                int frames=source.samples.Length/source.channels;
                int width=Mathf.Max(1,(int)rect.width);
                for (int col=0;col<width;col+=2)
                {
                    int a=(int)((long)col*frames/width),b=Mathf.Min(frames,(int)((long)(col+2)*frames/width));
                    float peak=0;
                    for (int i=a;i<b;i+=Mathf.Max(1,(b-a)/24))
                        peak=Mathf.Max(peak,Mathf.Abs(source.samples[i*source.channels]));
                    float h=Mathf.Clamp(peak*rect.height*1.8f,1,rect.height);
                    EditorGUI.DrawRect(new Rect(rect.x+col,rect.center.y-h/2,2,h),new Color(.49f,.81f,.81f));
                }
            }
            var e=Event.current;
            for (int i=0;i<recipe.edits.Count;i++)
            {
                var mark=recipe.edits[i];
                float left=rect.x+rect.width*Mathf.Clamp01(mark.start/source.Seconds);
                float right=rect.x+rect.width*Mathf.Clamp01(mark.end/source.Seconds);
                var wordRect=new Rect(left,rect.yMax-20,Mathf.Max(1,right-left),20);
                EditorGUI.DrawRect(wordRect,i==region ? new Color(.20f,.42f,.46f) : new Color(.14f,.19f,.22f));
                GUI.Label(wordRect,new GUIContent(mark.label,mark.label),EditorStyles.miniLabel);
                if (GUI.enabled && e.type==EventType.MouseDown && wordRect.Contains(e.mousePosition))
                {
                    region=i;selectFrom=mark.start;selectTo=mark.end;e.Use();Repaint();
                }
            }
            int id=GUIUtility.GetControlID(FocusType.Passive);
            float t=Mathf.Clamp01((e.mousePosition.x-rect.x)/rect.width)*source.Seconds;
            if (e.type==EventType.MouseDown && e.button==0 && rect.Contains(e.mousePosition) && GUI.enabled)
            { GUIUtility.hotControl=id; dragStart=t; selectFrom=selectTo=t; region=-1; e.Use(); }
            if (GUIUtility.hotControl==id && (e.type==EventType.MouseDrag || e.type==EventType.MouseUp))
            {
                selectFrom=Mathf.Min(t,dragStart); selectTo=Mathf.Max(t,dragStart);
                if (e.type==EventType.MouseUp) GUIUtility.hotControl=0;
                Repaint();e.Use();
            }
        }
        void Regions()
        {
            EditorGUILayout.BeginHorizontal();
            string[] labels=new string[recipe.edits.Count+1];
            labels[0]="Waveform selection";
            for(int i=0;i<recipe.edits.Count;i++) labels[i+1]=recipe.edits[i].label;
            int pick=EditorGUILayout.Popup(region+1,labels)-1;
            if (pick!=region)
            {
                region=pick;
                if (pick>=0) {selectFrom=recipe.edits[pick].start; selectTo=recipe.edits[pick].end;}
            }
            using (new EditorGUI.DisabledScope(selectTo-selectFrom<.008f))
                if (GUILayout.Button("Add region",GUILayout.Width(85)))
                {
                    recipe.edits.Add(new Region {start=selectFrom,end=selectTo});region=recipe.edits.Count-1;Changed();
                }
            using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(recipe.text)))
                if (GUILayout.Button("Estimate words",GUILayout.Width(110)) &&
                    (recipe.edits.Count==0 || EditorUtility.DisplayDialog("Replace regions?", "Replace the current region edits with estimated word boundaries?", "Replace", "Cancel")))
                    Run("suggest");
            EditorGUILayout.EndHorizontal();
            if (region<0 || region>=recipe.edits.Count) return;
            var r=recipe.edits[region];
            EditorGUI.BeginChangeCheck();
            r.label=EditorGUILayout.TextField("Word / phrase",r.label);
            EditorGUILayout.BeginHorizontal();
            r.start=EditorGUILayout.FloatField("Start (s)",r.start);
            r.end=EditorGUILayout.FloatField("End (s)",r.end);
            EditorGUILayout.EndHorizontal();
            r.stretch=EditorGUILayout.Slider("Length ×",r.stretch,.35f,2.8f);
            r.pitch=EditorGUILayout.Slider("Pitch (semitones)",r.pitch,-8,8);
            r.gain=EditorGUILayout.Slider("Emphasis (dB)",r.gain,-12,8);
            EditorGUILayout.BeginHorizontal();
            r.gapMs=EditorGUILayout.IntField("Gap after (± ms)",r.gapMs);
            if (GUILayout.Button("−10 ms",GUILayout.Width(62))) {r.gapMs-=10;Changed();}
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { selectFrom=r.start; selectTo=r.end; Changed(); }
            if (GUILayout.Button("Remove region")) {recipe.edits.RemoveAt(region);region=-1;Changed();}
        }

        void Profiles()
        {
            EditorGUI.BeginChangeCheck();
            catalog.masterDb=EditorGUILayout.Slider("Dialogue bus (dB)",catalog.masterDb,-36,0);
            catalog.matchTarget=EditorGUILayout.Toggle("Match active speech RMS",catalog.matchTarget);
            if (catalog.matchTarget) catalog.targetDb=EditorGUILayout.Slider("Target (dBFS)",catalog.targetDb,-34,-16);
            GUILayout.Label("Happy",EditorStyles.boldLabel);
            catalog.happyDb=EditorGUILayout.Slider("Speaker gain (dB)",catalog.happyDb,-24,0);
            catalog.happySpeed=EditorGUILayout.Slider("Default speed ×",catalog.happySpeed,.65f,1.65f);
            catalog.electronic=EditorGUILayout.Slider("Electronic blend",catalog.electronic,0,1);
            catalog.nearDb=EditorGUILayout.Slider("Near returns (Δ dB)",catalog.nearDb,-18,8);
            catalog.spaceDb=EditorGUILayout.Slider("Diffuse returns (Δ dB)",catalog.spaceDb,-18,8);
            catalog.openingDelay=EditorGUILayout.Slider("New Game cue delay (s)",catalog.openingDelay,0,5);
            catalog.greetingTemplate=EditorGUILayout.TextField("Greeting template",catalog.greetingTemplate);
            GUILayout.Label("Final Boss",EditorStyles.boldLabel);
            catalog.bossDb=EditorGUILayout.Slider("Speaker gain (dB)",catalog.bossDb,-24,0);
            catalog.bossSpeed=EditorGUILayout.Slider("Default speed ×",catalog.bossSpeed,.65f,1.8f);
            catalog.bossSub=EditorGUILayout.Slider("Sub layer",catalog.bossSub,0,1);
            catalog.bossDrive=EditorGUILayout.Slider("Saturation drive",catalog.bossDrive,.5f,3);
            revoiceHappy=EditorGUILayout.Toggle("Revoice Happy masters from text",revoiceHappy);
            revoiceBoss=EditorGUILayout.Toggle("Revoice Boss masters from text",revoiceBoss);
            if (revoiceHappy || revoiceBoss)
                EditorGUILayout.HelpBox("Creates new performances for unlocked masters. Review each take, including sentence pauses, before adoption.",MessageType.Info);
            if (EditorGUI.EndChangeCheck()) Changed();
            EditorGUILayout.HelpBox("Synthesized and dry recordings inherit timbre. Existing masters inherit tempo and output gain. Locked lines skip batch rendering.",MessageType.None);
            nickname=EditorGUILayout.TextField("Test nickname",nickname);
            if (GUILayout.Button("Hear greeting / fallback")) ListenName();
            if (!string.IsNullOrEmpty(review))
            {
                foreach (var entry in catalog.lines)
                {
                    string path=Path.Combine(review,entry.key.Replace('/','-'),"draft.wav");
                    if (!File.Exists(path)) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.label);
                    if (GUILayout.Button("A · Game",GUILayout.Width(80)))
                    {
                        var take=StudioTake.Read(ResourceDir+entry.key+".wav");
                        player.Play(take,0,take.Seconds,false,installedCatalog.Volume(entry.key.StartsWith("FinalBossVoice/",StringComparison.Ordinal)),null,0);
                    }
                    if (GUILayout.Button("B · Draft",GUILayout.Width(80)))
                    {
                        var take=StudioTake.Read(path);
                        player.Play(take,0,take.Seconds,false,catalog.Volume(entry.key.StartsWith("FinalBossVoice/",StringComparison.Ordinal)),null,0);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        void Transport()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("A · Game")) Listen(false);
            using (new EditorGUI.DisabledScope(draft==null))
                if (GUILayout.Button("B · Draft")) Listen(true);
            if (GUILayout.Button("Stop")) player.Dispose();
            loop=GUILayout.Toggle(loop,"Loop selection");
            match=GUILayout.Toggle(match,"Match level");
            EditorGUILayout.EndHorizontal();
            if (scope==0)
            {
                EditorGUILayout.BeginHorizontal();
                autoListen=GUILayout.Toggle(autoListen,"Preview edits automatically");
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    sceneAudition=GUILayout.Toggle(sceneAudition,"Listen in scene");
                EditorGUILayout.EndHorizontal();
                if (Application.isPlaying && draft!=null)
                {
                    bool use=EditorGUILayout.Toggle("Use draft at its game trigger",gameDraft!=null);
                    if (use && gameDraft==null)
                    {
                        gameDraft=draft.Clip(0,draft.Seconds,1);
                        HandlerVoice.SetStudioDraft(key,gameDraft);
                    }
                    else if (!use && gameDraft!=null) ClearGameDraft();
                }
                var e=Event.current;
                if (draft!=null && e.keyCode==KeyCode.F2)
                {
                    if(e.type==EventType.KeyDown && !holdingA) {holdingA=true;Listen(false);e.Use();}
                    else if(e.type==EventType.KeyUp) {holdingA=false;Listen(true);e.Use();}
                }
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(busy || (scope==0 && catalog.Find(key).locked && !fork)))
            {
                if (GUILayout.Button(scope==0 ? "Render draft" : "Review affected lines")) Run(scope==0 ? "line" : "profiles");
                using (new EditorGUI.DisabledScope(review==null))
                    if (GUILayout.Button("Adopt to game")) Run("apply");
            }
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Reload") && CanLeave()) Reload();
                if (GUILayout.Button("Restore last adoption") && CanLeave()) Run("undo");
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
