using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    // Analytics is a paused F8 page. No independent overlay or per-frame text allocation.
    public sealed partial class DuelUI
    {
        int analyticsTab, analyticsPage;
        static string MetricWave(int wave) => wave == DuelGame.BonusWave ? "BONUS" : "W" + wave;
        static string MetricNumber(double value) => value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        static string MetricPercent(bool valid, float value) => valid ? MetricNumber(value) + "%" : "N/A";
        void AnalyticsText(string value, float x, float y, float width, int size = 23, float height = 44)
            => Text(value,page,new Vector2(x,y),new Vector2(width,height),size,TextAlignmentOptions.Left);
        void BuildAnalytics()
        {
            string[] tabs = { "Fight", "Run", "Actions", "History" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                Button(tabs[i],page,new Vector2(-495+i*330,225),new Vector2(300,48),
                    ()=>{analyticsTab=tab;analyticsPage=0;Show();},analyticsTab==i);
            }
            var run = PlayMetrics.Run;
            var fight = PlayMetrics.Current;
            if (fight == null && run != null && run.attempts.Count > 0) fight = run.attempts[run.attempts.Count-1];
            if (run == null) AnalyticsText("Start New Game to begin a named record.",0,90,1300,28);
            else if (analyticsTab == 3) BuildAnalyticsHistory(run);
            else if (analyticsTab == 1) BuildRunSummary(run);
            else
            {
                bool total = analyticsTab == 1;
                var operations = total ? PlayMetrics.RunOperations(true) : fight?.operations;
                float seconds = total ? run.combatSeconds + (PlayMetrics.Current?.seconds ?? 0) : fight?.seconds ?? 0;
                string title = total ? "RUN" : fight == null ? "FIGHT" : MetricWave(fight.wave)+"  "+PlayMetrics.BossName(fight.wave)+"  "+fight.result.Replace('_',' ').ToUpperInvariant();
                bool test = total ? run.testData || (PlayMetrics.Current?.testData ?? false) : fight?.testData ?? false;
                AnalyticsText(title+"    "+MetricNumber(seconds)+"s"+(test?"    TEST DATA":""),0,155,1300,27);
                if (operations == null) AnalyticsText("Start a fight to collect action data.",0,65,1300);
                else if (analyticsTab == 2) BuildAnalyticsActions(operations);
                else
                {
                    PlayMetrics.Calculate(operations,out bool valid,out float percent);
                    AnalyticsText("Recorded operations",-390,95,520,27);
                    AnalyticsText("Deaths  W1: "+run.deathsByWave[1]+"   W2: "+run.deathsByWave[2],270,95,700);
                    string[] headings={"Operation","Automatic","Manual","Auto %"};
                    float[] xs={-470,-100,225,510}; float[] widths={360,300,300,240};
                    for(int c=0;c<4;c++)AnalyticsText(headings[c],xs[c],35,widths[c]);
                    for(int i=0;i<5;i++)
                    {
                        var op=operations[i]; bool timed=op.unit=="seconds"; float y=-17-i*48;
                        AnalyticsText(i==0?"Move":op.operation,xs[0],y,widths[0]);
                        AnalyticsText(timed?MetricNumber(op.automaticSeconds)+" s":op.automaticCount.ToString(),xs[1],y,widths[1]);
                        AnalyticsText(timed?MetricNumber(op.manualSeconds)+" s":op.manualCount.ToString(),xs[2],y,widths[2]);
                        AnalyticsText(MetricPercent(op.hasData,op.automaticPercent),xs[3],y,widths[3]);
                    }
                    AnalyticsText("Aim Assist: "+MetricNumber(operations[2].assistedSeconds)+"s  (separate from Auto Aim)"
                        +"    |    Drops  Auto: "+operations[1].automaticDrops+"  Manual: "+operations[1].manualDrops,0,-265,1300,21);
                    if(fight!=null)AnalyticsText("Remaining HP  You: "+fight.playerHealth+" / "+fight.playerMaxHealth
                        +(fight.gunnerMaxHealth>0?"    Gunner: "+fight.gunnerHealth+" / "+fight.gunnerMaxHealth:"")
                        +(fight.swordMaxHealth>0?"    Swordsman: "+fight.swordHealth+" / "+fight.swordMaxHealth:""),0,-318,1300,21);
                    if(total&&run.levelCleared)AnalyticsText("Level clear: "+MetricNumber(run.levelClearSeconds)+" combat seconds",0,-370,1300,22);
                }
            }
            var open=Button("Open data folder",page,new Vector2(500,-400),new Vector2(300,48),
                ()=>Application.OpenURL(new System.Uri(PlayMetrics.DirectoryPath).AbsoluteUri));
            open.interactable=Directory.Exists(PlayMetrics.DirectoryPath);
            if(!string.IsNullOrEmpty(PlayMetrics.LastSaveError))
                AnalyticsText("Save failed: "+PlayMetrics.LastSaveError,-200,-400,900,18,44);
        }
        void BuildRunSummary(PlayMetrics.RunData run)
        {
            var name=Text(run.playerName,page,new Vector2(0,155),new Vector2(1300,44),30,TextAlignmentOptions.Left,CharacterNameFont());
            name.richText=false;
            string[] rows={
                "Total play: "+MetricNumber(run.playSeconds)+"s    Combat: "+MetricNumber(run.combatSeconds+(PlayMetrics.Current?.seconds??0))+"s",
                "Character: "+MetricNumber(run.creationSeconds)+"s    Configuration: "+MetricNumber(run.configurationSeconds)+"s",
                "Training: "+MetricNumber(run.trainingSeconds)+"s    Paused: "+MetricNumber(run.pauseSeconds)+"s",
                "Rules  Added: "+run.rulesAdded+"    Edited: "+run.rulesEdited+"    Removed: "+run.rulesRemoved+"    Reordered: "+run.rulesReordered,
                "Most rules equipped: "+run.maxRulesEquipped+"    Help opened: "+run.helpOpened,
                "First rule: "+(run.firstRuleAtSeconds<0?"N/A":MetricNumber(run.firstRuleAtSeconds)+"s")+"    First fight: "+(run.firstFightAtSeconds<0?"N/A":MetricNumber(run.firstFightAtSeconds)+"s"),
                "Deaths  Training: "+run.trainingDeaths+"    W1: "+run.deathsByWave[1]+"    W2: "+run.deathsByWave[2],
                "Wave 1 skipped: "+(run.waveOneSkipped?"Yes":"No")+"    "+(run.finished?"Ended: "+run.endReason:"In progress")+(run.editor?"    EDITOR":"")+(run.testData?"    TEST DATA":"")
            };
            for(int i=0;i<rows.Length;i++)AnalyticsText(rows[i],0,95-i*52,1300,23);
        }
        void BuildAnalyticsActions(PlayMetrics.OperationStats[] operations)
        {
            var rows=new List<string>();
            for(int i=0;i<operations.Length;i++)
            {
                foreach(var a in operations[i].actions)
                {
                    string label=a.action=="Manual"?"Manual":a.action+" "+ConditionalCatalog.Response(a.action).Label;
                    rows.Add((i==0?"Move":operations[i].operation)+" / "+label+"\n"
                        +(operations[i].unit=="seconds"?MetricNumber(a.seconds)+"s":a.count+" actions")+(a.drops>0?"    Drops: "+a.drops:""));
                }
            }
            AnalyticsText("Current / last fight: executed actions only",0,95,1300,22);
            if(rows.Count==0)AnalyticsText("No actions recorded.",0,20,1300);
            int pages=Mathf.Max(1,(rows.Count+3)/4);analyticsPage=Mathf.Clamp(analyticsPage,0,pages-1);
            for(int i=0;i<4&&analyticsPage*4+i<rows.Count;i++)AnalyticsText(rows[analyticsPage*4+i],0,15-i*80,1300,23,72);
            AnalyticsPager(pages);
        }
        void BuildAnalyticsHistory(PlayMetrics.RunData run)
        {
            AnalyticsText("Finished / abandoned fights in this run",0,155,1300,27);
            int pages=Mathf.Max(1,(run.attempts.Count+4)/5);analyticsPage=Mathf.Clamp(analyticsPage,0,pages-1);
            for(int i=0;i<5;i++)
            {
                int index=run.attempts.Count-1-analyticsPage*5-i;if(index<0)break;
                var a=run.attempts[index];
                AnalyticsText("#"+(index+1)+"    "+MetricWave(a.wave)+" "+PlayMetrics.BossName(a.wave)+"    "+a.result.ToUpperInvariant()
                    +"    "+MetricNumber(a.seconds)+"s    Auto "+MetricPercent(a.hasAutomationData,a.automaticPercent)+(a.testData?"    TEST":""),0,75-i*65,1300,23,58);
            }
            if(run.attempts.Count==0)AnalyticsText("No completed fights yet.",0,55,1300);
            AnalyticsPager(pages);
        }
        void AnalyticsPager(int pages)
        {
            var back=Button("Previous",page,new Vector2(-350,-320),new Vector2(250,44),()=>{analyticsPage--;Show();});
            back.interactable=analyticsPage>0;
            Text((analyticsPage+1)+" / "+pages,page,new Vector2(0,-320),new Vector2(240,44),22);
            var next=Button("Next",page,new Vector2(350,-320),new Vector2(250,44),()=>{analyticsPage++;Show();});
            next.interactable=analyticsPage+1<pages;
        }
    }
}
