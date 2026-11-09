using UnityEngine;

namespace GoGhost.Duel
{
    // Both phases share the rainbow silhouette and axe. The EMP sets the spectrum in motion.
    public sealed partial class BossSilhouetteView
    {
        static readonly Part[] AxeBodyParts = { Part.Torso, Part.Head, Part.BackThigh, Part.BackCalf, Part.FrontThigh,
            Part.FrontCalf, Part.BackUpperArm, Part.BackForearm, Part.FrontUpperArm, Part.FrontForearm };
        Material rainbowMaterial;
        Material[] axeBodyMaterials;
        float gazeAt = float.PositiveInfinity;
        Material chargedWeapon;
        const float RainbowCycleSeconds = 4f;
        internal float RainbowPhase { get; private set; }
        const float AxeFlowCycleSeconds = 2.4f;
        internal const float FlowRampSeconds = 1.2f;
        float flowStart = -1;
        Collider2D plantedSupport;
        Vector2 plantedPoint;
        float chargeAt = float.PositiveInfinity;
        internal const float PlantedChargeSeconds = 5.10f, WaveSpacing = .70f;
        float shockwaveAt = -1;
        public const float ShockwaveSeconds = 2.40f;
        public const float ShockwaveStartRadius = .4f;
        float shockwaveReach, releasedAt = float.NegativeInfinity;
        Vector2 shockwaveOrigin;
        SpriteRenderer[] empEdges;
        // Five straight wakes per front. The other slots draw the rings and the burst at the blade.
        static readonly int[] WaveWakeSlots = {0,1,3,4,5,13,14,15,16,17,21,22,23,24,25};
        bool rising;
        float kneel, plantedGrip;
        float knockdownAt = float.PositiveInfinity;
        const float KneeDownAt = 2.15f;
        const float LookToPlayerAt = KneeDownAt + 1.4f;
        const float LookToPlayerSeconds = .45f;
        internal bool ReadyForKneelingDialogue => clock - knockdownAt >= LookToPlayerAt + LookToPlayerSeconds;
        public void RiseFromKneel() => rising = true;
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
            for (int i=0;i<AxeBodyParts.Length;i++)
            {
                var part=parts[(int)AxeBodyParts[i]];
                if(part!=null && rainbowMaterial!=null)part.sharedMaterial=rainbowMaterial;
            }
            bodyColor=Color.white;
            weaponColor=NeuralPalette.Primary;
        }

        public void SetFinalBossPhaseTwo()
        {
            if (flowStart >= 0) return;
            flowStart = clock;
            UseChargedWeapon();
            if (chargedWeapon != null)
            {
                chargedWeapon.SetFloat("_AxeCharge", 1.08f);
                chargedWeapon.SetFloat("_AxeFlowPhase", 0);
            }
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
            if (flowStart >= 0)
            {
                float age = Mathf.Max(0,clock-flowStart);
                // Integrate the speed ramp so the static colour bands start moving continuously.
                float travel = age < FlowRampSeconds ? .5f*age*age/FlowRampSeconds : age-.5f*FlowRampSeconds;
                RainbowPhase = Mathf.Repeat(travel / RainbowCycleSeconds, 1f);
                rainbowMaterial.SetFloat("_RainbowPhase", RainbowPhase);
                if (chargedWeapon != null)
                    chargedWeapon.SetFloat("_AxeFlowPhase", Mathf.Repeat(age / AxeFlowCycleSeconds, 1f));
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
            knockdownAt = clock;
        }

        internal void BeginPlantedCharge()
        {
            chargeAt = Time.time;
            UseChargedWeapon();
            if(chargedWeapon!=null)chargedWeapon.SetFloat("_AxeCharge",0);
        }

        void UseChargedWeapon()
        {
            if (chargedWeapon!=null || rainbowMaterial==null) return;
            chargedWeapon = new Material(rainbowMaterial) {name="Charged EMP axe",hideFlags=HideFlags.DontSave};
            parts[(int)Part.Weapon].sharedMaterial = chargedWeapon;
            chargedWeapon.SetColor("_AxeShadow", NeuralPalette.EmpShadow);
            chargedWeapon.SetColor("_AxeHighlight", NeuralPalette.EmpHighlight);
        }

        internal void LiftGaze() => gazeAt = clock;
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
        void UpdateShockwave()
        {
            if (empEdges != null && empEdges[0].enabled)
            {
                float age = Time.time - releasedAt;
                float rim = Mathf.Lerp(.96f, .35f, Mathf.SmoothStep(0, 1, age / .5f));
                if (flowStart >= 0) rim *= 1 - Mathf.Clamp01((clock-flowStart)/FlowRampSeconds);
                var tint = NeuralPalette.Alpha(NeuralPalette.Emp, rim);
                foreach (var edge in empEdges) { edge.color = tint; edge.enabled = rim > 0; }
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

        void OnDestroy()
        {
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
            float knockdownElapsed = clock - knockdownAt;
            if (axe.Transitioning && knockdownElapsed >= 0 && (!rising || kneel > 0))
            {
                p.Hip = Vector2.Lerp(p.Hip, new Vector2(-.05f, -.43f), kneel);
                p.Chest = Vector2.Lerp(p.Chest, new Vector2(.08f, -.04f), kneel);
                float gaze = Ease((clock-gazeAt)/.24f);
                float bowed = 1 - Ease((knockdownElapsed - LookToPlayerAt) / LookToPlayerSeconds);
                p.Head = Vector2.Lerp(p.Head, new Vector2(.16f+.035f*gaze,.11f+.095f*gaze-.09f*bowed), kneel);
                float floor = -actor.Motor.Size.y * .5f + .06f;
                p.FrontFoot = Vector2.Lerp(p.FrontFoot, new Vector2(.44f, floor), kneel);
                p.BackFoot = Vector2.Lerp(p.BackFoot, new Vector2(-.48f, floor), kneel);
                const float plantAngle=-67;
                var forward=Angle(plantAngle);
                var across=new Vector2(forward.y,-forward.x);
                // Authored axe: from the grip (96,192) to the blade corner (257,579), at 299 px/unit.
                var grip=LocalPoint(PlantedContact)-forward*(387f/AxePixelsPerUnit)-across*(161f/AxePixelsPerUnit);
                p.FrontHand=Vector2.Lerp(p.FrontHand,grip+forward*.30f,plantedGrip);
                p.BackHand=Vector2.Lerp(p.BackHand,grip+forward*.03f,plantedGrip);
                p.WeaponAngle=Mathf.LerpAngle(p.WeaponAngle,plantAngle,plantedGrip);
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
                if (rising)
                {
                    kneel = Mathf.MoveTowards(kneel, 0, Time.deltaTime * 2.4f);
                    plantedGrip = kneel;
                }
                else
                {
                    // The blade catches his weight before the back knee reaches the floor.
                    float recoil = Ease(knockdownElapsed / .25f) * (1 - Ease((knockdownElapsed - .25f) / .55f));
                    p.Hip += new Vector2(-.08f, -.09f) * recoil;
                    p.Chest += new Vector2(-.17f, -.07f) * recoil;
                    p.Head += new Vector2(-.22f, -.09f) * recoil;
                    p.WeaponAngle += 16 * recoil;
                    plantedGrip = Ease(Mathf.InverseLerp(.25f, 1.1f, knockdownElapsed));
                    kneel = Ease(Mathf.InverseLerp(.85f, KneeDownAt, knockdownElapsed));
                }
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
            if (actor.Axe.AxeFlying) { Hide(Part.Weapon); Hide(Part.Sheath); return; }
            PlaceHeldAxe(p);
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
