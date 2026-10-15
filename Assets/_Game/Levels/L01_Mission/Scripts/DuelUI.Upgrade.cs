using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// UPGRADE SCREEN, built in code like the rest of DuelUI (this is a partial class, so it can use
    /// DuelUI's private helpers Rect/Text/Image/Button). It replaces what the Configure phase used to draw.
    ///
    /// Layout: left = clickable stickman blueprint (head, chest, arms, legs) + "AI Abilities" tab;
    ///         right = list of abilities for whatever is selected; top = AI level buttons and open-slot counts.
    ///
    /// Rules enforced: level only goes up, abilities are only added, a filled body slot never changes,
    /// every commit asks for confirmation then autosaves immediately.
    ///
    /// Functions:
    ///  ShowUpgrade()  draws the whole screen (called from DuelUI.Show() in the Configure phase).
    ///  PartButton()   one clickable body region, tinted with your OfficerVisual.SlotColor palette.
    ///  UpgradeRow()   one ability in the right-hand list.
    ///  AskLevel / CommitBody / CommitAi   open the Yes/No confirm; Yes saves and redraws.
    /// </summary>
    public sealed partial class DuelUI
    {
        LoadoutSlot? upgradePart;
        bool upgradeShowAi = true;
        string confirmMessage;
        System.Action confirmAction;

        SaveData Progress => game.Save;
        int OpenAi => AssistRules.TotalAiSlots(Progress.aiLevel) - Progress.aiAbilityIds.Count;
        int OpenBody => AssistRules.TotalBodySlots(Progress.aiLevel) - Progress.bodyCommits.Count;

        void ShowUpgrade()
        {
            var draft = LoadoutBridge.BuildDraft(Progress);

            Text("UPGRADE  -  WAVE " + game.Wave, page, new Vector2(0, 400), new Vector2(900, 65), 42);
            Text($"AI slots open: {OpenAi}     Body slots open: {OpenBody}", page, new Vector2(0, 345), new Vector2(1000, 40), 24);

            for (int i = 1; i <= AssistRules.MaxLevel; i++)
            {
                int level = i;
                Button("AI Level " + level, page, new Vector2(-250 + (i - 1) * 250, 285), new Vector2(220, 55),
                    () => AskLevel(level), level <= Progress.aiLevel);
            }

            // Blueprint (front view, screen-left = the character's left)
            PartButton(LoadoutSlot.Brain,    "Head",  new Vector2(-480, 150),  new Vector2(90, 90),  draft);
            PartButton(LoadoutSlot.Torso,    "Chest", new Vector2(-480, 35),   new Vector2(100, 140), draft);
            PartButton(LoadoutSlot.LeftArm,  "L Arm", new Vector2(-565, 45),   new Vector2(50, 140), draft);
            PartButton(LoadoutSlot.RightArm, "R Arm", new Vector2(-395, 45),   new Vector2(50, 140), draft);
            PartButton(LoadoutSlot.LeftLeg,  "L Leg", new Vector2(-515, -130), new Vector2(50, 170), draft);
            PartButton(LoadoutSlot.RightLeg, "R Leg", new Vector2(-445, -130), new Vector2(50, 170), draft);
            Button("AI Abilities", page, new Vector2(-480, -270), new Vector2(260, 58),
                () => { upgradeShowAi = true; upgradePart = null; Show(); }, upgradeShowAi);

            // Right-hand list
            int row = 0;
            if (upgradeShowAi)
            {
                Text("AI Abilities", page, new Vector2(170, 230), new Vector2(560, 40), 30, TextAlignmentOptions.Left);
                foreach (var info in AiAbilityCatalog.All)
                {
                    var captured = info;
                    bool owned = Progress.aiAbilityIds.Contains(info.Id);
                    UpgradeRow(row++, info.Label, info.Description, owned, !owned && OpenAi > 0, () => CommitAi(captured));
                }
                if (row == 0) Text("No AI abilities yet.", page, new Vector2(170, 150), new Vector2(560, 40), 24);
            }
            else if (upgradePart.HasValue)
            {
                var slot = upgradePart.Value;
                Text(slot == LoadoutSlot.Brain ? "Head: Brain Abilities" : slot == LoadoutSlot.Torso ? "Chest: Core Abilities"
                    : LoadoutRules.SlotLabel(slot) + " Abilities", page, new Vector2(170, 230), new Vector2(560, 40), 30, TextAlignmentOptions.Left);

                string equippedHere = draft.GetSlot(slot).ItemId;
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var def in LoadoutRules.PrototypeCatalog)
                {
                    if (!def.Occupies(slot) || !LoadoutRules.IsOffered(slot, def.ItemId) || !seen.Add(def.ItemId)) continue;
                    string id = def.ItemId;
                    bool owned = equippedHere == id;
                    bool can = !owned && OpenBody > 0 && LoadoutBridge.CanCommit(Progress, slot, id);
                    UpgradeRow(row++, LoadoutRules.ItemLabel(id), BodyAbilityText.Description(id), owned, can, () => CommitBody(slot, id));
                }
                if (row == 0) Text("Nothing available for this part.", page, new Vector2(170, 150), new Vector2(560, 40), 24);
            }

            Button("Start Fight", page, new Vector2(0, -360), new Vector2(360, 65), game.StartFight);

            if (confirmMessage != null) ShowConfirm();
        }

        void PartButton(LoadoutSlot slot, string label, Vector2 pos, Vector2 size, LoadoutDraft draft)
        {
            var r = Rect("Part_" + slot, page, pos, size);
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            Color c = OfficerVisual.SlotColor(slot);
            bool selected = !upgradeShowAi && upgradePart == slot;
            bool committed = !string.IsNullOrEmpty(draft.GetSlot(slot).ItemId);
            c.a = selected ? 1f : committed ? .75f : .35f;
            img.color = c; img.raycastTarget = true;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => { upgradeShowAi = false; upgradePart = slot; Show(); });
            Text(label, r, Vector2.zero, size, 18).color = Color.black;
        }

        void UpgradeRow(int index, string label, string desc, bool owned, bool canChoose, UnityEngine.Events.UnityAction click)
        {
            float y = 150 - index * 95;
            string title = owned ? label + "  (equipped)" : canChoose ? label : label + "  (unavailable)";
            Button(title, page, new Vector2(170, y), new Vector2(560, 58), canChoose ? click : () => { }, owned);
            if (!string.IsNullOrEmpty(desc)) Text(desc, page, new Vector2(170, y - 42), new Vector2(560, 30), 20);
        }

        // ---------- Commits (each asks first, then saves) ----------

        void Ask(string message, System.Action onYes) { confirmMessage = message; confirmAction = onYes; Show(); }

        void AskLevel(int level)
        {
            if (level <= Progress.aiLevel) return;   // never downgrade
            Ask($"Raise AI assistance to Level {level}? This can't be undone.", () => Progress.aiLevel = level);
        }

        void CommitBody(LoadoutSlot slot, string itemId)
        {
            if (OpenBody <= 0 || !LoadoutBridge.CanCommit(Progress, slot, itemId)) return;
            Ask($"Commit to \"{LoadoutRules.ItemLabel(itemId)}\" on {LoadoutRules.SlotLabel(slot)}? This is permanent.",
                () => Progress.bodyCommits.Add(new BodyCommit { slot = (int)slot, itemId = itemId }));
        }

        void CommitAi(AiAbilityInfo info)
        {
            if (OpenAi <= 0 || Progress.aiAbilityIds.Contains(info.Id)) return;
            Ask($"Commit to \"{info.Label}\"? This is permanent.", () => Progress.aiAbilityIds.Add(info.Id));
        }

        void ShowConfirm()
        {
            // Full-screen blocker so clicks can't reach the screen underneath
            var blocker = Rect("Blocker", page, Vector2.zero, new Vector2(4000, 4000));
            var bi = blocker.gameObject.AddComponent<UnityEngine.UI.Image>();
            bi.color = new Color(0, 0, 0, .8f); bi.raycastTarget = true;

            Text(confirmMessage, page, new Vector2(0, 60), new Vector2(900, 120), 32);
            Button("Yes", page, new Vector2(-130, -70), new Vector2(200, 60), () =>
            {
                var action = confirmAction;
                confirmMessage = null; confirmAction = null;
                action?.Invoke();
                SaveSystem.Save(Progress);   // autosave right after every commitment
                Show();
            });
            Button("No", page, new Vector2(130, -70), new Vector2(200, 60), () =>
            {
                confirmMessage = null; confirmAction = null;
                Show();
            });
        }
    }
}
