#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Test Room Focused Smoke")]
    public static void RunTrainingRoom() => Start("training-room", false);
    public static void RunTrainingThreat() => Start("training-threat", false);
    public static void RunTrainingPatrol() => Start("training-patrol", false);

    static IEnumerator TrainingPatrol()
    {
        yield return TrainingFresh(null);
        var trainer = game.Bosses[0];
        Check("training starts with 99999 HP and one-round magazine",
            trainer.Health.CurrentHealth == 99999 && trainer.Health.MaxHealth == 99999 && trainer.Profile.Magazine == 1);
        // Keep the player on the existing low platform, clear of the walking lane and above the gun.
        OnFloor(game.Player.gameObject, trainer.Navigation, -5.5f);
        yield return Frames(5);
        heldKeys = new[] { Key.Space }; yield return Frames(4); heldKeys = Array.Empty<Key>();
        yield return Seconds(1);
        Check("fixture player physically lands above patrol lane", game.Player.GetComponent<PlayerMovement>().IsGrounded
            && Mathf.Abs(game.Player.GetComponent<Collider2D>().bounds.min.y - LayoutSupports()[1].bounds.max.y) < .06f);
        var playerBody = game.Player.GetComponent<Collider2D>();
        Check("smaller trainer clears platform feet with solid body collision",
            !Physics2D.GetIgnoreCollision(playerBody, trainer.Body)
            && playerBody.bounds.min.y - trainer.Body.bounds.max.y > .5f);
        float firstX = trainer.Position.x, minX = firstX, maxX = firstX;
        bool right = false, leftAgain = false, horizontal = true, forward = true, spaced = true, single = true;
        int shots = 0, lastAttack = -1;
        float lastShot = -100;
        var seen = new System.Collections.Generic.HashSet<Projectile>();
        float until = Time.time + 48;
        while (Time.time < until && !leftAgain)
        {
            minX = Mathf.Min(minX, trainer.Position.x); maxX = Mathf.Max(maxX, trainer.Position.x);
            right |= trainer.Facing > 0;
            leftAgain |= right && trainer.Facing < 0;
            foreach (var shot in Projectile.Active)
            {
                if (shot.Owner != trainer.gameObject || !seen.Add(shot)) continue;
                var velocity = shot.GetComponent<Rigidbody2D>().linearVelocity;
                horizontal &= Mathf.Abs(velocity.y) < .001f && Mathf.Abs(Mathf.Abs(velocity.x) - 10) < .01f;
                forward &= Mathf.Sign(velocity.x) == trainer.Facing;
                spaced &= Time.time - lastShot >= 3.3f;
                single &= lastAttack != trainer.Actions.AttackId;
                lastShot = Time.time; lastAttack = trainer.Actions.AttackId; shots++;
            }
            yield return null;
        }
        Check("physical patrol reaches both arena ends and reverses", right && leftAgain
            && minX < game.ArenaBounds.min.x + .6f && maxX > game.ArenaBounds.max.x - .6f,
            "range=" + minX + ".." + maxX);
        Check("intermittent single forward horizontal bullets ignore elevated player", shots >= 6
            && horizontal && forward && spaced && single, "shots=" + shots);
        Check("no complex abilities or jump", trainer.Actions.Started.Where((v,i) => i != (int)BossAbility.Burst).All(v => v == 0)
            && trainer.Motor.Grounded);
        MouseAt(trainer.Position, true); yield return Seconds(.8f); mouseHeld = false; yield return Seconds(1);
        Check("99999 HP remains damageable by real player bullets", trainer.Health.CurrentHealth < 99999 && trainer.IsAlive);
        checkingPause = true; game.SetPause(true);
        var position = trainer.Position; int attack = trainer.Actions.AttackId; float clock = Time.time;
        yield return RealSeconds(.2);
        Check("pause freezes patrol and firing", trainer.Position == position && trainer.Actions.AttackId == attack && Time.time == clock);
        game.SetPause(false); checkingPause = false;
        yield return TrainingCapture("patrol");
        game.BackFromTraining(); yield return Frames(3);
        Check("Back cleans training actors and shots", game.Phase == DuelPhase.Configure && game.Bosses.Count == 0 && Projectile.Active.Count == 0);
        game.StartConfiguredEncounter(); yield return Frames(5);
        Check("re-entry restores training health and initial left direction", game.Bosses[0].Health.CurrentHealth == 99999 && game.Bosses[0].Facing < 0);
        game.ContinueFromTraining(); game.StartConfiguredEncounter(); yield return Seconds(5);
        Check("Continue starts normal W1 profile", game.Phase == DuelPhase.Fight && !game.Bosses[0].IsTrainingEnemy
            && game.Bosses[0].Health.MaxHealth == game.Bosses[0].Profile.Health && game.Bosses[0].Health.MaxHealth != 99999);
        for (int wave = 2; wave <= 3; wave++)
        {
            game.ToggleDeveloperTools(); game.JumpToDeveloperWave(wave); yield return Seconds(5);
            Check("short W" + wave + " retains normal Boss policies/profiles", game.Phase == DuelPhase.Fight
                && game.Bosses.All(b => !b.IsTrainingEnemy && b.Health.MaxHealth == b.Profile.Health && b.Health.MaxHealth != 99999));
        }
    }

    static IEnumerator TrainingThreat()
    {
        yield return TrainingFresh(d=>V2Rule(d,new ConditionalRule{Condition=53,IncomingAttack=IncomingAttackOption.Bullet,Action="D01"}));
        bool incoming=false;float until=Time.time+8;
        while(Time.time<until)
        {
            incoming|=Feedback(4).Visual.Matched;
            if(game.Rules.AcceptedDodges>0)break;
            yield return null;
        }
        Check("Incoming Attack (Bullet) uses real live shot/threat and accepts Dodge",incoming&&game.Rules.AcceptedDodges>0,
            "matched="+incoming+" accepted="+game.Rules.AcceptedDodges+" attacks="+game.Bosses[0].Actions.AttackId+" hp="+game.Player.CurrentHealth);
    }

    static Rect TrainingRect(RectTransform transform)
    {
        var corners = new Vector3[4]; transform.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    static IEnumerator TrainingCapture(string label)
    {
        Directory.CreateDirectory(Output);
        ScreenCapture.CaptureScreenshot(Output + "training-" + label + ".png");
        yield return Frames(3);
    }
    static void TrainingLayout(int width)
    {
        var back = TrainingRect((RectTransform)ConfigButtonNamed("← Back").transform);
        var next = TrainingRect((RectTransform)ConfigButtonNamed("Continue →").transform);
        var sidebar = TrainingRect(CardsRect("BattleRules"));
        var playerBar = TrainingRect(Private<TMPro.TMP_Text>(CardsUI(), "resources").rectTransform);
        Check(width + " training controls on-screen, separate from Rules/ammo", back.xMin >= 0 && back.yMax <= Screen.height + 1
            && next.xMax <= Screen.width && next.yMin >= 0 && !back.Overlaps(sidebar) && !next.Overlaps(sidebar)
            && !next.Overlaps(playerBar) && ConfigButtonNamed("Continue →").interactable);
        CardsLayout(true,5,width+" training cards");
        TextFits(width + " training UI");
    }
    static IEnumerator TrainingFresh(Action<ConditionalLoadout> configure)
    {
        mouseHeld = false; heldKeys = Array.Empty<Key>();
        game.NewGame();
        if(configure != null) game.EditLoadout(configure);
        game.StartConfiguredEncounter(); yield return Frames(5);
    }
    static IEnumerator TrainingRoom()
    {
        Record("Test Room: normal UI callbacks, virtual keyboard/mouse, real projectiles and rule evaluation. Controlled lethal damage only for player recovery and short wave progression; no balance/hardware-input claim.");
        Check("V2 catalog unchanged", ConditionalCatalog.Conditions.Length == 26 && ConditionalCatalog.Responses.Length == 21 && ConditionalCatalog.MaxRulesTotal == 5);
        game.NewGame(); SetAlphaGameSize(1280,720); yield return Frames(6);
        Check("New Game remains empty/manual, first button enters training", game.DraftLoadout.TotalRules == 0
            && ConfigButtonNamed("StartFight").GetComponentInChildren<TMPro.TMP_Text>().text == "Enter Test Room");
        yield return PointerConfig("StartFight");
        Check("normal UI enters Training immediately, never Wave 0", game.Phase == DuelPhase.Training && game.Wave == 1
            && game.Player.GetComponent<PlayerInputReader>().GameplayInputEnabled && game.Rules != null);
        var formal = Private<Collider2D[]>(game,"platforms");
        var trainer = game.Bosses[0];
        Check("training reuses shared arena with one stage platform", formal.Length==6 && formal[0].gameObject.activeInHierarchy
            && formal.Skip(1).All(c=>!c.gameObject.activeInHierarchy) && LayoutSupports().Length==2
            && trainer.Navigation.Nodes.All(n=>Mathf.Abs(n.Position.x)<=15) && game.GetComponentInChildren<DuoCoordinator>()==null
            && GameObject.Find("TestRoom")==null && game.ArenaBounds==Private<Bounds>(game,"arenaBounds"));
        Check("training profile is independent and single-shot/low-damage", !AssetDatabase.Contains(trainer.Profile)
            && trainer.IsTrainingEnemy && trainer.Profile.Magazine==1 && trainer.Profile.Burst.Damage==4 && trainer.Profile.BulletSpeed==10);
        float x=game.Player.transform.position.x; heldKeys=new[]{Key.D};yield return Seconds(.3f);heldKeys=Array.Empty<Key>();yield return Frames(3);
        Check("empty-loadout manual movement",game.Player.transform.position.x>x+1);
        heldKeys=new[]{Key.Space};yield return Frames(4);heldKeys=Array.Empty<Key>();yield return Seconds(.15f);
        Check("manual Jump uses real physics",game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>0);
        heldKeys=new[]{Key.LeftShift};yield return Frames(3);heldKeys=Array.Empty<Key>();
        Check("manual Dodge uses original executor",game.Dodge.CooldownRemaining>0);
        yield return Seconds(1.2f);
        // The patrol fires horizontally; the preceding jump can leave the player above its lane.
        bool trainingShot=false;float shotDeadline=Time.time+5;
        while(Time.time<shotDeadline)
        {
            trainingShot|=Projectile.Active.Any(p=>p!=null&&p.Owner==trainer.gameObject);
            yield return null;
        }
        Check("live training patrol emits real projectiles",trainingShot&&trainer.Actions.Activated[(int)BossAbility.Burst]>0);
        Check("training policy starts no complex Boss abilities",trainer.Actions.Started.Where((v,i)=>i!=(int)BossAbility.Burst).All(v=>v==0));
        // Controlled start under the low platform; the jump/landing itself uses unmodified input/physics.
        OnFloor(game.Player.gameObject,trainer.Navigation,-5.5f);yield return Frames(5);
        heldKeys=new[]{Key.Space};yield return Frames(4);heldKeys=Array.Empty<Key>();yield return Seconds(1);
        Check("low one-way platform accepts a normal jump and physical landing",game.Player.GetComponent<PlayerMovement>().IsGrounded
            &&Mathf.Abs(game.Player.GetComponent<Collider2D>().bounds.min.y-LayoutSupports()[1].bounds.max.y)<.06f);
        var oldPlayer=game.Player;var oldRules=game.Rules;
        yield return PointerConfig("← Back");yield return Frames(3);
        Check("Back clears actors/projectiles/controller and retains arena",game.Phase==DuelPhase.Configure&&game.Rules==null
            &&oldPlayer==null&&Projectile.Active.Count==0&&formal.All(c=>c.gameObject.activeInHierarchy)&&Private<bool>(oldRules,"disposed"));
        yield return AddEditorRule(59,"M04");
        ClickConfig("RuleParameter1");yield return Frames(2); // Current Move requires explicit Back before commit.
        yield return AddEditorRule(13,"F01");
        ClickConfig("UseAction");yield return Frames(2);
        game.EditLoadout(d=>{
            V2Rule(d,new ConditionalRule{Condition=18,Action="Q06"});
            V2Rule(d,new ConditionalRule{Condition=53,IncomingAttack=IncomingAttackOption.Bullet,Action="D01"});
            V2Rule(d,new ConditionalRule{Condition=3,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Grounded,Action="J01"});
        });
        string saved=JsonUtility.ToJson(game.DraftLoadout);
        yield return TrainingCapture("setup-1280");
        yield return PointerConfig("StartFight");yield return Frames(5);
        trainer=game.Bosses[0];int enemyHP=trainer.Health.CurrentHealth;
        Check("fresh training does not invent Landed events",game.Rules.AcceptedJumps==0);
        MouseAt(trainer.Position,false);yield return Frames(4);
        Check("Fine-Tune sees the real training enemy",game.Rules.Ownership.AimOwned&&Feedback(2).Visual.Matched);
        MouseAt(trainer.Position,true);yield return Frames(3);mouseHeld=false;
        bool step=false,hit=false,actorFeedback=false,matched=false;
        float until=Time.time+2.4f;
        while(Time.time<until)
        {
            MouseAt(game.Bosses[0].Position,false);
            step|=Feedback(0).Executing;hit|=game.Rules.AutomaticShots>0;
            matched|=Feedback(0).Visual.Matched;
            actorFeedback|=game.Rules.VisualFeedback(ControlOperation.Movement).Executing;
            yield return null;
        }
        Check("real Shot Fired -> Move Back and Hit Enemy -> Fire 1 Shot",step&&hit&&trainer.Health.CurrentHealth<enemyHP);
        Check("complete-match HUD and real actor feedback share live controller",matched&&actorFeedback
            &&Private<ConditionalController>(CardsUI(),"hudRules")==game.Rules);
        heldKeys=new[]{Key.Space};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Seconds(1.8f);
        Check("real Landed + Grounded -> Jump Once chain",game.Rules.AcceptedJumps>=1);
        for(int i=0;i<2;i++)
        {
            int width=i==0?1280:1920;SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            TrainingLayout(width);yield return TrainingCapture(width.ToString());
        }
        heldKeys=Array.Empty<Key>();mouseHeld=false;
        // Bound the high-HP fixture, then finish with paid player bullets to verify replacement/subscriptions.
        until=Time.time+7;var killed=game.Bosses[0];
        killed.Health.SetRuntimeHealth(99999, 48);
        while(Time.time<until&&killed.IsAlive){MouseAt(killed.Position,true);yield return null;}
        mouseHeld=false;yield return Until(()=>game.Bosses[0]!=killed,2);
        Check("enemy real bullet death and delayed fresh respawn",killed==null&&game.Bosses[0].IsAlive&&game.Bosses[0].Health.CurrentHealth==99999);
        Check("respawn preserves loadout without duplicate rule subscriptions",JsonUtility.ToJson(game.DraftLoadout)==saved
            &&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==1
            &&((Delegate)typeof(PlayerShooting).GetField("EnemyHit",PrivateInstance).GetValue(game.Rifle)).GetInvocationList().Length==1);
        oldPlayer=game.Player;oldRules=game.Rules;
        game.Player.TakeDamage(game.Player.MaxHealth,game.Bosses[0].gameObject);
        Check("training death never opens Defeat and navigation remains available",game.Phase==DuelPhase.Training&&ConfigButtonNamed("Continue →").interactable);
        yield return Until(()=>game.Player!=oldPlayer,2);yield return Frames(4);
        Check("player recovery creates fresh resources/controller",oldPlayer==null&&game.Phase==DuelPhase.Training&&game.Player.CurrentHealth==game.Player.MaxHealth
            &&game.Rifle.AmmoInMagazine==game.Rifle.MagazineSize&&game.Dodge.CooldownRemaining==0&&Private<bool>(oldRules,"disposed"));
        checkingPause=true;game.SetPause(true);float clock=game.FightClock;yield return RealSeconds(.2);
        Check("pause freezes training and keeps Back/Continue",game.FightClock==clock&&ConfigButtonNamed("← Back").interactable&&ConfigButtonNamed("Continue →").interactable);
        game.SetPause(false);checkingPause=false;
        game.ToggleDeveloperTools();Check("F8 pauses training",game.Paused&&game.DeveloperToolsOpen);game.ToggleDeveloperTools();
        Check("F8 restores training pause state",!game.Paused);
        var trainingCameraRect=Camera.main.rect;float trainingCameraSize=Camera.main.orthographicSize;
        var trainingCameraPosition=Camera.main.transform.position;
        game.Player.SetRuntimeHealth(game.Player.MaxHealth,10);oldRules=game.Rules;
        yield return PointerConfig("Continue →");yield return Frames(3);
        Check("Continue opens editable preserved setup without creating W1",game.Phase==DuelPhase.Configure
            &&game.Player==null&&game.Rules==null&&game.Bosses.Count==0&&Projectile.Active.Count==0
            &&Private<bool>(oldRules,"disposed")&&JsonUtility.ToJson(game.DraftLoadout)==saved
            &&ConfigButtonNamed("StartFight").GetComponentInChildren<TMPro.TMP_Text>().text=="Start Fight");
        yield return Seconds(1.5f);
        Check("post-training setup waits for player confirmation",game.Phase==DuelPhase.Configure&&game.Player==null);
        game.EditLoadout(d=>d.Operations[0].Rules[0].Action="M02");
        saved=JsonUtility.ToJson(game.DraftLoadout);
        yield return TrainingCapture("post-setup-1920");
        yield return PointerConfig("StartFight");yield return Frames(3);
        Check("Start Fight uses latest edited configuration",game.Rules.RuleAt(ControlOperation.Movement,0).Action=="M02");
        Check("training and W1 use identical camera framing with same rule count",Camera.main.rect==trainingCameraRect
            &&Mathf.Approximately(Camera.main.orthographicSize,trainingCameraSize)&&Camera.main.transform.position==trainingCameraPosition);
        Check("Start Fight creates fresh normal W1 preparation",game.Phase==DuelPhase.Prepare&&game.Wave==1&&!game.Bosses[0].IsTrainingEnemy
            &&game.Player.CurrentHealth==game.Player.MaxHealth&&game.Rifle.AmmoInMagazine==8&&game.Dodge.CooldownRemaining==0
            &&Private<bool>(oldRules,"disposed")&&Projectile.Active.Count==0&&JsonUtility.ToJson(game.DraftLoadout)==saved
            &&Mathf.Abs(Camera.main.transform.position.x)<1&&LayoutSupports().Length==8
            &&formal.Skip(1).All(c=>!c.gameObject.activeInHierarchy));
        var geometry=Private<ConditionalThreatGeometry>(game.Rules,"geometry");
        Check("formal threat prediction retains original arena bounds",Private<float>(geometry,"leftEdge")==-15&&Private<float>(geometry,"rightEdge")==15);
        for(int wave=1;wave<=3;wave++)
        {
            yield return Until(()=>game.Phase==DuelPhase.Fight,4);yield return Seconds(1.5f);
            Check("normal W"+wave+" enters with expected Bosses",game.Phase==DuelPhase.Fight&&game.Wave==wave&&game.Bosses.Count==(wave==3?2:1));
            foreach(var boss in game.Bosses)boss.Health.TakeDamage(boss.Health.MaxHealth,game.Player.gameObject);
            yield return Until(()=>game.Phase==(wave==3?DuelPhase.Complete:DuelPhase.Configure),2);
            if(wave<3)
            {
                Check("subsequent setup remains Start Fight",ConfigButtonNamed("StartFight").GetComponentInChildren<TMPro.TMP_Text>().text=="Start Fight");
                yield return PointerConfig("StartFight");
            }
        }
        Check("three-wave progress still completes",game.Phase==DuelPhase.Complete);
        yield return TrainingThreat();
        yield return TrainingFresh(d=>V2Rule(d,new ConditionalRule{Condition=25,Action="D01"}));
        yield return Until(()=>game.Rules.AcceptedDodges>0,5);
        Check("Enemy Preparing an Attack uses real public preparation",game.Rules.AcceptedDodges>0);
        yield return TrainingFresh(d=>V2Rule(d,new ConditionalRule{Condition=52,Distance=EnemyDistanceOption.Close,Action="M02"}));
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,game.Player.transform.position.x+3);
        yield return Until(()=>Feedback(0).Executing,3);
        Check("Enemy Distance Close -> Retreat through normal arbitration",Feedback(0).Executing);
        for(int i=0;i<3;i++){game.BackFromTraining();yield return Frames(2);game.StartConfiguredEncounter();yield return Frames(3);}
        Check("repeated Back/re-entry has one player/enemy and preserved rule",Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Length==1
            &&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==1&&game.DraftLoadout.TotalRules==1);
        game.Player.TakeDamage(game.Player.MaxHealth,game.Bosses[0].gameObject);game.ContinueFromTraining();yield return Frames(3);
        yield return Seconds(1.5f);
        Check("Continue during death delay cancels respawn and stays in setup",game.Phase==DuelPhase.Configure&&game.Player==null&&game.Rules==null&&game.Bosses.Count==0);
        game.MainMenu();yield return Frames(4);
        Check("menu cleanup restores arena/camera and removes transient training state",Projectile.Active.Count==0
            &&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0&&game.Rules==null
            &&formal.All(c=>c.gameObject.activeInHierarchy)&&Mathf.Abs(Camera.main.transform.position.x)<1);
        Record("NOT TESTED: physical input, full fights/balance, long-session stress, standalone. Actual Game View screenshots: training-1280/1920.png.");
    }
}
#endif
