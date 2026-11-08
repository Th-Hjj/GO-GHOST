using UnityEngine;

namespace GoGhost.Duel
{
    // Rainbow body and phase transition. Phase two retains the original axe and boxer poses.
    public sealed partial class BossSilhouetteView
    {
        const float AxeShaft = 1.0f, AxeShaftWidth = .075f, AxeGrip = .14f;
        static readonly Vector2 AxeHeadSize = new Vector2(.26f, .30f);
        static readonly Part[] AxeBodyParts = { Part.Torso, Part.Head, Part.BackThigh, Part.BackCalf, Part.FrontThigh,
            Part.FrontCalf, Part.BackUpperArm, Part.BackForearm, Part.FrontUpperArm, Part.FrontForearm };
        Material rainbowMaterial;
        Material[] axeBodyMaterials;
        Transform redEye;
        Mesh eyeMesh;
        TrailRenderer eyeTrail;
        float gazeAt = float.PositiveInfinity;
        Material chargedWeapon, originalWeaponMaterial;
        Collider2D plantedSupport;
        Vector2 plantedPoint;
        float chargeAt = float.PositiveInfinity;
        internal const float PlantedChargeSeconds = 5.10f, WaveSpacing = .70f;
        float drainFrom = -1, drainSeconds = 1, shockwaveAt = -1;
        public const float ShockwaveSeconds = 2.40f;
        public const float ShockwaveStartRadius = .4f;
        float shockwaveReach, releasedAt = float.NegativeInfinity;
        Vector2 shockwaveOrigin;
        SpriteRenderer[] empEdges;
        static readonly Color EyeRed = new Color(1f, .16f, .18f, 1f);
        float eyesLitAt = float.NegativeInfinity;
        // Five straight wakes per front. The other slots draw the rings and the burst at the blade.
        static readonly int[] WaveWakeSlots = {0,1,3,4,5,13,14,15,16,17,21,22,23,24,25};
        bool rising;
        float kneel;
        public void RiseFromKneel() => rising = true;
        public bool EyesRed { get; private set; }
        bool FinalAxe => actor != null && actor.Axe != null;

        void ConfigureFinalBossLook()
        {
            if (!FinalAxe) return;
            if (axeBodyMaterials == null)
            {
                axeBodyMaterials = new Material[AxeBodyParts.Length];
                for (int i = 0; i < AxeBodyParts.Length; i++)
                    axeBodyMaterials[i] = parts[(int)AxeBodyParts[i]] != null ? parts[(int)AxeBodyParts[i]].sharedMaterial : null;
            }
            if (rainbowMaterial == null)
            {
                var shader = Resources.Load<Shader>("FinalBossRainbow");
                if (shader != null) rainbowMaterial = new Material(shader) { name = "Final Boss rainbow (runtime)", hideFlags = HideFlags.DontSave };
                if (rainbowMaterial != null) { rainbowMaterial.SetFloat("_RainbowAmount", 1); rainbowMaterial.SetColor("_BaseColor", NeuralPalette.Emp); }
            }
            ApplyFinalBossBody(false);
        }

        public void SetFinalBossPhaseTwo()
        {
            drainFrom = -1;
            if (empEdges != null)
                foreach (var edge in empEdges) edge.enabled = false;
            ApplyFinalBossBody(true);
        }

        void ApplyFinalBossBody(bool phaseTwo)
        {
            if (axeBodyMaterials == null) return;
            for (int i = 0; i < AxeBodyParts.Length; i++)
            {
                var part = parts[(int)AxeBodyParts[i]];
                if (part != null) part.sharedMaterial = phaseTwo || rainbowMaterial == null ? axeBodyMaterials[i] : rainbowMaterial;
            }
            // The final shader colour matches the ordinary sprites on direct phase-two retry.
            bodyColor = phaseTwo ? NeuralPalette.Emp : Color.white;
            weaponColor = NeuralPalette.Primary;
        }

        void UpdateFinalBossLook()
        {
            if (!FinalAxe) return;
            if (chargeAt <= Time.time && releasedAt < chargeAt)
                DrawPlantedCharge();
            UpdateShockwave();
            UpdateFlyingAxe();
            if (rainbowMaterial == null) return;
            float height = actor.Motor.Size.y;
            rainbowMaterial.SetFloat("_RainbowBottom", actor.Position.y - height * .5f);
            rainbowMaterial.SetFloat("_RainbowHeight", height + .1f);
            if (drainFrom >= 0)
            {
                float drained = Mathf.Clamp01((clock - drainFrom) / drainSeconds);
                rainbowMaterial.SetFloat("_RainbowAmount", 1 - drained);
                if (drained >= 1) SetFinalBossPhaseTwo();
            }
        }

        // Phase-change presentation, driven by the scripted transition.
        internal Vector2 PlantedContact => plantedSupport != null
            ? (Vector2)plantedSupport.transform.TransformPoint(plantedPoint) : plantedPoint;

        internal void PlantAxe()
        {
            actor.Navigation.TrySupport(actor.Position, actor.Motor.Size, out plantedSupport);
            float x = actor.Position.x + actor.Facing * .54f;
            if (plantedSupport == null)
            {
                plantedPoint = new Vector2(x, actor.Body.bounds.min.y);
                return;
            }
            var surface = plantedSupport.bounds;
            x = Mathf.Clamp(x,surface.min.x+.05f,surface.max.x-.05f);
            plantedPoint = plantedSupport.transform.InverseTransformPoint(new Vector2(x,surface.max.y));
        }

        internal void BeginPlantedCharge()
        {
            chargeAt = Time.time;
            if (rainbowMaterial == null) return;
            var weapon = parts[(int)Part.Weapon];
            originalWeaponMaterial = weapon.sharedMaterial;
            chargedWeapon = new Material(rainbowMaterial) {name="Charged EMP axe",hideFlags=HideFlags.DontSave};
            weapon.sharedMaterial = chargedWeapon;
        }

        internal void LiftGaze() => gazeAt = clock;
        public void DrainRainbow(float seconds) { drainFrom = clock; drainSeconds = Mathf.Max(.05f, seconds); }
        internal void Shockwave(Vector2 origin, float reach)
        {
            shockwaveOrigin = origin;
            shockwaveReach = reach;
            shockwaveAt = releasedAt = Time.time;
            empEdges = new SpriteRenderer[AxeBodyParts.Length];
            int backOrder = int.MaxValue;
            foreach (var part in AxeBodyParts)
                backOrder = Mathf.Min(backOrder, parts[(int)part].sortingOrder);
            for (int i = 0; i < AxeBodyParts.Length; i++)
                empEdges[i] = EmpOutline.Create(parts[(int)AxeBodyParts[i]], axeBodyMaterials[i], backOrder - 1, .055f);
        }
        public void SetEyesRed(bool value, bool immediate = false)
        {
            EyesRed = value;
            eyesLitAt = immediate ? float.NegativeInfinity : clock;
        }

        void UpdateShockwave()
        {
            if (empEdges != null && empEdges[0].enabled)
            {
                float age = Time.time - releasedAt;
                float rim = Mathf.Lerp(.96f, .35f, Mathf.SmoothStep(0, 1, age / .5f));
                if (drainFrom >= 0) rim *= 1 - Mathf.Clamp01((clock - drainFrom) / drainSeconds);
                var tint = NeuralPalette.Alpha(NeuralPalette.Emp, rim);
                foreach (var edge in empEdges) edge.color = tint;
            }
            if (shockwaveAt < 0) return;
            float waveElapsed = Time.time - shockwaveAt;
            if (waveElapsed >= ShockwaveSeconds + 2 * WaveSpacing) { shockwaveAt = -1; return; }
            for (int i = 0; i < 3; i++)
            {
                float t = (waveElapsed - i * WaveSpacing) / ShockwaveSeconds;
                if (t < 0 || t >= 1) continue;
                float radius = Mathf.Lerp(ShockwaveStartRadius, shockwaveReach, t);
                float strength = (1 - .16f * i) * (1 - Mathf.SmoothStep(0, 1, (t - .62f) / .38f));
                DrawEmpRing(18 + i, shockwaveOrigin, radius, Mathf.Lerp(i==1?.40f:i==2?.13f:.26f, i==1?.20f:.075f, t),
                    NeuralPalette.Alpha(NeuralPalette.Emp, strength));
                float wakeLength=(i==1?1.25f:.85f)*Mathf.Sin(t*Mathf.PI);
                for(int j=0;j<5;j++)
                {
                    Vector2 dir=Angle(12+j*39+i*4);
                    Vector2 leading=shockwaveOrigin+dir*radius;
                    SetLine(AxeLine(WaveWakeSlots[i*5+j],"EMP wave streak",2),leading-dir*wakeLength,leading,
                        .034f,NeuralPalette.Alpha(NeuralPalette.Emp,strength*.58f));
                }
                float age = t * ShockwaveSeconds;
                if (age < .30f)
                    for (int j=0; j<7; j++)
                    {
                        Vector2 dir=Angle(6+j*28);
                        float length=(1-age/.30f)*(i==0?2.1f:1.25f);
                        SetLine(AxeLine(6+j,"EMP release",2),shockwaveOrigin+dir*.10f,
                            shockwaveOrigin+dir*(.11f+length),.045f,NeuralPalette.Alpha(NeuralPalette.Emp,1-age/.30f));
                    }
                if (i == 0)
                    DrawEmpRing(2, shockwaveOrigin, radius, .044f, NeuralPalette.Alpha(NeuralPalette.Primary, strength * .85f));
            }
        }

        void DrawEmpRing(int slot,Vector2 origin,float radius,float width,Color color)
        {
            const int points=96;
            var line=AxeLine(slot,"EMP wave",points);
            line.loop=true; line.startWidth=line.endWidth=width; line.startColor=line.endColor=color;
            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2;
                line.SetPosition(i, origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        // One side-facing slit. Three flat polygons keep the graphite lid and red core readable at arena scale.
        void PlaceEyes(Pose p)
        {
            if (!EyesRed) { if (redEye != null) redEye.gameObject.SetActive(false); return; }
            if (redEye == null) CreateRedEye();
            redEye.gameObject.SetActive(true);
            redEye.localPosition=p.Head+new Vector2(.005f,.074f);
            redEye.localRotation=Quaternion.Euler(0,0,-8);
            float opening=Ease((clock-eyesLitAt)/.18f);
            redEye.localScale=new Vector3(1,opening,1);
            bool streak=opening>=1 && actor.Velocity.sqrMagnitude>4 && actor.Running;
            if(!streak || (eyeTrail.emitting && Mathf.Sign(actor.Velocity.x)!=actor.Facing))eyeTrail.Clear();
            eyeTrail.emitting=streak;
        }

        void CreateRedEye()
        {
            redEye=new GameObject("Red eye").transform;
            redEye.SetParent(transform,false);
            var head=parts[(int)Part.Head];
            var filter=redEye.gameObject.AddComponent<MeshFilter>();
            var ink=redEye.gameObject.AddComponent<MeshRenderer>();
            ink.sharedMaterial=tellMaterial;
            ink.sortingLayerID=head.sortingLayerID; ink.sortingOrder=head.sortingOrder+1;
            eyeMesh=new Mesh {name="Final Boss eye slit"};
            eyeMesh.vertices=new Vector3[] {
                new(-.042f,.035f), new(.170f,.035f), new(.184f,.006f), new(.130f,-.048f), new(.008f,-.039f),
                new(-.022f,.006f), new(.168f,.006f), new(.125f,-.041f), new(.014f,-.032f),
                new(.050f,-.006f), new(.151f,-.006f), new(.124f,-.023f), new(.057f,-.023f) };
            var lid=new Color32(37,33,48,255); var red=new Color32(210,37,53,255); var hot=new Color32(255,94,97,255);
            eyeMesh.colors32=new[]{lid,lid,lid,lid,lid,red,red,red,red,hot,hot,hot,hot};
            eyeMesh.triangles=new[]{0,1,2,0,2,3,0,3,4,5,6,7,5,7,8,9,10,11,9,11,12};
            filter.sharedMesh=eyeMesh;
            var corner=new GameObject("Eye streak").transform;corner.SetParent(redEye,false);
            eyeTrail=corner.gameObject.AddComponent<TrailRenderer>();
            eyeTrail.emitting=false;eyeTrail.time=.09f;eyeTrail.minVertexDistance=.035f;
            eyeTrail.startWidth=.032f;eyeTrail.endWidth=0;eyeTrail.sharedMaterial=glowMaterial;
            eyeTrail.startColor=new Color(.63f,.06f,.09f,.7f);eyeTrail.endColor=NeuralPalette.Alpha(EyeRed,0);
            eyeTrail.sortingLayerID=head.sortingLayerID;eyeTrail.sortingOrder=head.sortingOrder;
        }

        void OnDestroy()
        {
            if (eyeMesh != null) Destroy(eyeMesh);
            if (chargedWeapon != null) Destroy(chargedWeapon);
            if (rainbowMaterial != null) Destroy(rainbowMaterial);
        }

        void AxePose(ref Pose p, float wave, float motion)
        {
            var axe = actor.Axe;
            float t = Ease(axe.PhaseProgress);
            bool prepare = axe.Phase == FinalBossAxe.Stage.Prepare, locked = axe.Phase == FinalBossAxe.Stage.Locked;
            bool active = axe.Phase == FinalBossAxe.Stage.Active, recover = axe.Phase == FinalBossAxe.Stage.Recovery;
            float settle = recover ? 1 - t : 1;
            // Idle: the axe rests upright in the leading hand.
            p.WeaponAngle = 72;
            p.FrontHand = new Vector2(.22f + wave * motion * .06f, .02f);
            p.BackHand = new Vector2(-.04f - wave * motion * .10f, -.05f);
            kneel = Mathf.MoveTowards(kneel, axe.Transitioning && !rising && (actor.Motor.Grounded || kneel>0) ? 1 : 0, Time.deltaTime * 2.4f);
            if (kneel > 0)
            {
                p.Hip = Vector2.Lerp(p.Hip, new Vector2(-.05f, -.43f), kneel);
                p.Chest = Vector2.Lerp(p.Chest, new Vector2(.08f, -.04f), kneel);
                float gaze = Ease((clock-gazeAt)/.24f);
                p.Head = Vector2.Lerp(p.Head, new Vector2(.16f+.035f*gaze,.11f+.095f*gaze), kneel);
                float floor = -actor.Motor.Size.y * .5f + .06f;
                p.FrontFoot = Vector2.Lerp(p.FrontFoot, new Vector2(.44f, floor), kneel);
                p.BackFoot = Vector2.Lerp(p.BackFoot, new Vector2(-.48f, floor), kneel);
                const float plantAngle=-67;
                var forward=Angle(plantAngle);
                var across=new Vector2(forward.y,-forward.x);
                // Authored axe: from the grip (96,192) to the blade corner (257,579), at 299 px/unit.
                var grip=LocalPoint(PlantedContact)-forward*(387f/AxePixelsPerUnit)-across*(161f/AxePixelsPerUnit);
                p.FrontHand=Vector2.Lerp(p.FrontHand,grip+forward*.30f,kneel);
                p.BackHand=Vector2.Lerp(p.BackHand,grip+forward*.03f,kneel);
                p.WeaponAngle=Mathf.LerpAngle(p.WeaponAngle,plantAngle,kneel);
                return;
            }
            if (axe.Invading)
            {
                float age=axe.InvasionAge, lift=Ease(Mathf.InverseLerp(.30f,.88f,age));
                float dip=Ease(age/.30f)*(1-lift), reach=lift*(1-Ease(axe.InvasionRecovery));
                p.WeaponAngle=72-108*dip+36*reach;
                p.FrontHand=Vector2.Lerp(p.FrontHand,new Vector2(.40f,-.30f),dip);
                p.FrontHand=Vector2.Lerp(p.FrontHand,new Vector2(-.14f,.88f),reach);
                p.BackHand=Vector2.Lerp(p.BackHand,new Vector2(-.58f,.08f),reach);
                p.Hip+=new Vector2(-.09f,-.20f)*dip;
                p.Chest+=new Vector2(.16f,-.21f)*dip+new Vector2(-.13f,.10f)*reach;
                p.Head+=new Vector2(.12f,-.21f)*dip+new Vector2(-.12f,.10f)*reach;
                p.FrontFoot.x+=.18f*(dip+reach); p.BackFoot.x-=.21f*(dip+reach);
                return;
            }
            if (!axe.PhaseTwo)
            {
                PhaseOneAxePose(ref p, axe, wave, motion);
                return;
            }
            switch (axe.Current)
            {
                case FinalBossAxe.Move.Chop:
                    p.FrontFoot.x = .34f; p.BackFoot.x = -.38f;
                    if (prepare || locked)
                    {
                        float raise = prepare ? t : 1;
                        p.WeaponAngle = Mathf.Lerp(72, 158, raise);
                        p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(-.02f, .66f), raise);
                        p.Chest.x -= .06f * raise; p.Head.x -= .04f * raise;
                    }
                    else
                    {
                        float cut = active ? t : 1;
                        p.WeaponAngle = Mathf.Lerp(158, -38, cut);
                        p.FrontHand = Vector2.Lerp(new Vector2(-.02f, .66f), new Vector2(.40f, .02f), cut);
                        p.Chest.x += .12f * settle; p.Head.x += .09f * settle; p.Hip.y -= .07f * settle;
                        if (recover)
                        {
                            p.WeaponAngle = Mathf.Lerp(-38, 72, t);
                        }
                    }
                    p.BackHand = p.FrontHand + new Vector2(-.12f, -.06f);
                    break;
                case FinalBossAxe.Move.Spin:
                    p.FrontFoot.x = .36f; p.BackFoot.x = -.36f;
                    p.Hip.y -= .08f; p.Chest.y -= .05f;
                    if (active)
                    {
                        // The whole axe sweeps a full circle around the body at arm's length.
                        p.WeaponAngle = axe.AxeSpin;
                        p.FrontHand = new Vector2(Mathf.Cos(axe.AxeSpin * Mathf.Deg2Rad), Mathf.Sin(axe.AxeSpin * Mathf.Deg2Rad) * .4f + .25f) * .42f;
                    }
                    else
                    {
                        float out_ = prepare ? t : locked ? 1 : settle;
                        p.WeaponAngle = Mathf.Lerp(72, 180, out_);
                        p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(-.40f, .22f), out_);
                    }
                    p.BackHand = new Vector2(.30f, .20f);
                    break;
            }
        }

        // Called after the shared pose placement: the axe replaces the sword geometry.
        void ApplyAxe(Pose p)
        {
            PlaceEyes(p);
            if (actor.Axe.AxeFlying) { Hide(Part.Weapon); Hide(Part.Sheath); return; }
            PlaceHeldAxe(p);
        }

        void PlaceHeldAxe(Pose p)
        {
            if (!FinalAxe || !actor.Axe.PhaseTwo)
            {
                PlacePhaseOneAxe(p);
                return;
            }
            var weapon = parts[(int)Part.Weapon];
            if (originalWeaponSprite != null) weapon.sprite = originalWeaponSprite;
            Vector2 direction = Angle(p.WeaponAngle);
            Place(Part.Weapon, p.FrontHand + direction * (AxeShaft * .5f - AxeGrip), new Vector2(AxeShaft, AxeShaftWidth), p.WeaponAngle);
            Vector2 blade = new Vector2(direction.y, -direction.x);
            Place(Part.Sheath, p.FrontHand + direction * (AxeShaft - AxeGrip - .08f) + blade * .10f, AxeHeadSize, p.WeaponAngle);
            if (parts[(int)Part.Sheath] != null) { var color = weaponColor; color.a = alpha; parts[(int)Part.Sheath].color = color; }
        }

        internal Vector2 InvasionChargePoint => phaseOneSocket + Vector2.up * .23f;

        void InvasionSignal(FinalBossAxe axe)
        {
            float age=axe.InvasionAge;
            if(age<FinalBossAxe.ChargeAt) return;
            if(age<FinalBossAxe.BurstAt)
            {
                DrawCharge(age);
                return;
            }
            if(axe.WaveRadius<=0) return;
            float fade=1-Ease(axe.InvasionRecovery);
            DrawEmpRing(2,axe.WaveOrigin,axe.WaveRadius,.105f,NeuralPalette.Alpha(NeuralPalette.Emp,fade));
            float burst=Mathf.Clamp01((age-FinalBossAxe.BurstAt)/.22f);
            if(burst<1)
            {
                for(int i=0;i<8;i++)
                {
                    Vector2 dir=Angle(45*i+8*Mathf.Sin(i));
                    SetLine(AxeLine(8+i,"EMP burst spark",2),axe.WaveOrigin+dir*(.04f+.25f*burst),
                        axe.WaveOrigin+dir*(.50f+.36f*burst+.1f*(i%3)),.054f*(1-burst),
                        NeuralPalette.Alpha(i%2==0?NeuralPalette.Emp:NeuralPalette.Primary,1-burst));
                }
            }
        }

        void DrawCharge(float age)
        {
            Vector2 center=InvasionChargePoint;
            float charge=Ease(Mathf.InverseLerp(FinalBossAxe.ChargeAt,FinalBossAxe.PinchAt,age));
            float pinch=Ease(Mathf.InverseLerp(FinalBossAxe.PinchAt,FinalBossAxe.BurstAt,age));
            DrawAbsorption(center,age-FinalBossAxe.ChargeAt,charge,pinch,false);
        }

        void DrawPlantedCharge()
        {
            float age=Time.time-chargeAt;
            float charge=Ease(age/4.8f),pinch=Ease((age-4.8f)/.30f);
            DrawAbsorption(PlantedContact,age,charge,pinch,true);
        }

        static readonly float[] LiftedLanes={25,74,133,172};
        static readonly float[] GroundLanes={0,180,35,147,68,112,91};

        void DrawAbsorption(Vector2 center,float age,float charge,float pinch,bool planted)
        {
            var lanes=planted?GroundLanes:LiftedLanes;
            for(int i=0;i<lanes.Length;i++)
            {
                float elapsed=age-i*(planted?.32f:.12f);
                if(elapsed<=0)continue;
                Vector2 dir=Angle(lanes[i]);
                float span=Mathf.Lerp(planted?2.4f:1.4f,planted?10.5f:3.6f,charge)*(1-(i%3)*.075f);
                float travel=Mathf.Repeat(elapsed/(planted?1.27f:1.03f),1);
                float far=(1-travel*travel)*span*(1-pinch);
                float near=Mathf.Max(0,far-span*(.22f+.28f*charge));
                float strength=Mathf.Clamp01(elapsed/.25f)*(1-pinch);
                SetLine(AxeLine(i,"EMP intake lane",2),center,center+dir*span*(1-pinch),.019f,
                    NeuralPalette.Alpha(NeuralPalette.Emp,strength*.23f));
                SetLine(AxeLine(8+i,"EMP incoming current",2),center+dir*near,center+dir*far,.039f+.027f*charge,
                    NeuralPalette.Alpha(NeuralPalette.Emp,strength*Mathf.Sin(Mathf.PI*travel)));
                SetLine(AxeLine(18+i,"EMP bright tip",2),center+dir*near,center+dir*Mathf.Lerp(near,far,.22f),.028f,
                    NeuralPalette.Alpha(NeuralPalette.Primary,strength*Mathf.Sin(Mathf.PI*travel)));
            }
            float size=Mathf.Lerp(.04f,planted?.31f:.18f,charge)*(1-.62f*pinch);
            var core=AxeLine(27,"Square EMP core",2);
            core.numCapVertices=0;
            SetLine(core,center-Vector2.right*size*.5f,center+Vector2.right*size*.5f,size,NeuralPalette.Emp);
            var middle=AxeLine(28,"Square EMP centre",2);middle.numCapVertices=0;
            SetLine(middle,center-Vector2.right*size*.22f,center+Vector2.right*size*.22f,size*.44f,NeuralPalette.Primary);
        }

        internal void DrawInvasionContact(FinalBossAxe axe)
        {
            // Draw the contact frame immediately, even if time stops before the next LateUpdate.
            UpdateTells();
            DrawEmpRing(24,axe.WaveContact,.20f,.07f,NeuralPalette.Primary);
        }


        Color AxeTellColor(bool active, bool locked)
        {
            Color color = NeuralPalette.Primary;
            color.a = active ? .95f : locked ? .8f : .3f;
            return color;
        }

        void AxeTells()
        {
            var axe = actor.Axe;
            if (axe.PhaseTwo) return;
            if (axe.Invading) { InvasionSignal(axe); return; }
            DrawPhaseOneAttack(axe);
        }
    }
}
