using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GoGhost.Duel
{
    [Serializable]
    public sealed class VoiceStudioLine
    {
        public string key, label, text;
        // transition: Final Boss phase-change lines, played by the scripted transition rather than the intro pages.
        public bool locked, transcribed, bonusOffer, transition;
    }

    [Serializable]
    public sealed class VoiceStudioCatalog
    {
        public float masterDb, happyDb, bossDb, happySpeed, bossSpeed, electronic, nearDb, spaceDb, bossSub, bossDrive, openingDelay;
        public string greetingTemplate;
        public bool matchTarget;
        public float happySub=.42f, happyDrive=1.9f, bossElectronic, bossNearDb, bossSpaceDb;
        public float targetDb=-22;
        public VoiceStudioLine[] lines;
        [NonSerialized] VoiceStudioLine[] visit;
        public VoiceStudioLine[] Visit => visit ?? (visit = Array.FindAll(lines, x => x.key.StartsWith("FinalBossVoice/", StringComparison.Ordinal)));
        [NonSerialized] VoiceStudioLine[] intro, offer;
        public VoiceStudioLine[] BossIntro => intro ?? (intro = Array.FindAll(Visit, line => !line.bonusOffer && !line.transition));
        public VoiceStudioLine[] BonusOffer => offer ?? (offer = Array.FindAll(Visit, line => line.bonusOffer));
        static VoiceStudioCatalog current;
        public static VoiceStudioCatalog Current => current ?? (current = JsonUtility.FromJson<VoiceStudioCatalog>(
            Resources.Load<TextAsset>("VoiceStudioCatalog").text));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() => current = null;

        public VoiceStudioLine Find(string key) => Array.Find(lines, line => line.key == key);
        public float Volume(bool boss) => Mathf.Pow(10, (masterDb + (boss ? bossDb : happyDb)) / 20f);

#if UNITY_EDITOR
        // Use the exact desktop nickname tokenizer and synthesis path for author auditions.
        public Task<float[]> PreviewName(string root, string name, CancellationToken token)
        {
            return Task.Run(() => HandlerSpeech.Synthesize(root, name, token, _ => {}, this).Samples, token);
        }
#endif
    }
}
