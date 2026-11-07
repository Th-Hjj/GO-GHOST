# Voice direction

Use **Tools → GO GHOST → Voice Studio** to audition, adopt and restore line edits in Unity. The [Voice Studio guide](VoiceStudio.md) covers the approved opening, editable speaker settings, reference performances and nickname previews.

Thomas's decision on 2026-11-05 establishes two production references for new or revised English dialogue. The WAVs below are the actual audition takes, stored outside Unity's Assets folder. Existing game recordings and the live nickname synthesizer have their own production history.

| Speaker | Decision | Listening reference |
|---|---|---|
| Happy | **Approved.** Use the v0.8 stereo inner voice for every new or revised Happy line. It supersedes the v0.4 B-light master. | [Happy inner voice · 6.41 s](VoiceReferences/Happy-Inner-v08.wav) |
| Final Boss | **Provisional.** Use the faster, more inflected v0.3 take with the game's deep B treatment until Thomas revises it. | [Final Boss B · 7.19 s](VoiceReferences/FinalBoss-B.wav) |

Thomas owns character, dialogue, motivation and emotional direction. Most existing lines are placeholders or first passes. Leave unspecified direction blank. The audition text demonstrates sound and timing only. Michael remains reserved for the player; give additional speakers their own approved reference before authoring lines for them.

## Making the next line

Register every new fixed Happy or Final Boss visit line in Voice Studio as part of delivery. Give the catalog entry, initial PCM WAV and gameplay cue the same stable resource key. For revisions, edit the existing entry and retain its key and WAV GUID. The [line intake instructions](VoiceStudio.md#录入新台词) cover the required fields, file placement, visit-page order and audition/save check.

1. Identify the existing gameplay cue or describe the new trigger. Record what the player has just done and what they can do while the line plays.
2. Get the text and any delivery notes from Thomas. Keep the speaker's established treatment when wording changes. Synthesize a whole thought together so adjacent words, names and UI labels such as *Add Rule* keep their natural connections.
3. Choose speed, emphasis and pauses separately. Record a word-level exception only when a particular word needs emphasis or a deliberate break. Listen to the revised take beside the speaker's reference at matched loudness.
4. For back-to-back lines, measure the gap between audible speech boundaries. The two references use **1.00 s** after “Wave two is complete” and **1.30 s** after “There is a bonus challenge.” Use these as audition anchors when a similar break is wanted. Each new line can have its own timing. Subtract the existing tail and head silence from the target before inserting silence. The audition used 10 ms RMS windows at −40 dBFS as a measurement aid. Listen for quiet consonants and effect tails before cutting.
5. Keep the selected take, its dry source, the cue ID, text revision and render settings together. On the target page, check normal playback, an early click, pause/resume and exit/re-entry. Record which take Thomas accepted.

### Happy production recipe

Use Kokoro multilingual v1.0 INT8 Heart with the US English lexicon. Thomas selected **1.05× synthesis speed** for routine fixed lines and named greetings. The character-creation question has its own delivery. The archived v0.8 take at 1.10× remains the timbre and mastering reference. Heart is speaker 3 in the 54-voice production bank and 0 in the game's two-voice desktop bank. Work with synchronized 24 kHz mono audio.

**Character creation · “Who are you?” · Approved 2026-11-06.** Use the [approved opening WAV](../../Assets/_Game/Levels/L01_Duel/Resources/Handler/WhoAreYou.wav) as delivered. Its timing is baked in. Exclude this cue from routine 1.05× Happy renders and play it at normal playback speed. Thomas’s performance sets the rhythm for this line: *who* rises into a pause, *are* comes in lower with a little more weight, and *you* follows after a shorter gap and trails gently downward. The master is 24 kHz stereo PCM16, 40,595 frames including the tail. SHA-256 `2f62bab00054bffbc94c1545a387a5d1eca4b0cae2bf0af2def7daf85fc01d35`.

1. Make the full B version from the dry Heart take. Use a 24-band spectral-envelope vocoder (85 Hz–10.5 kHz, 1024-sample FFT, 128-sample hop, square-root Hann window). A steady 182 Hz harmonic carrier supplies voiced energy. Blend back the original high-frequency speech from 3.3 to 5 kHz to keep consonants intelligible.
2. Match the vocoder layer's RMS to the dry take. Mix 0.40 dry plus 0.65 vocoded, then match the result to the dry RMS. The reference level-matching steps cap peak amplitude at 0.91 full scale.
3. Mix **15% of the finished B take and 85% synchronized dry Heart** with linear gains. Use the v0.4 listening level for level matching. Keep the direct voice centered.
4. Apply the v0.8 short stereo returns at word onset (mid −9 dB, side −6.5 dB) and the longer diffuse returns (mid −12.5 dB, side −13 dB). Allow 650 ms of tail. Export stereo at 24 kHz and preserve both channels through import and playback.

**Production command** (Python 3 with NumPy, from the repository root):

```text
python Tools/render_happy_voice.py dry-heart.wav happy-v08.wav
```

Keep the dry take separately. The [offline renderer](../../Tools/render_happy_voice.py) accepts a mono 24 kHz PCM16 WAV. For a regression check, render the [reference dry take](VoiceReferences/Happy-Inner-Dry.wav) and compare it with the approved stereo WAV. The archived master has SHA-256 `8ee63ce5e50de4a8950217e9eaab4875c0130ad44497bd01135d92e5973d3156`. Review new lines at matched loudness on headphones and in mono.

The seven active Happy WAVs use this master, including the opening question and fallback greeting. Named greetings apply [the corresponding stereo master](../../Assets/_Game/Levels/L01_Duel/Scripts/HappyVoiceMaster.cs) on the synthesis worker before creating the Unity clip. Its four little-endian float32 impulse responses in StreamingAssets are the offline diffuse kernels in order: long mid, long side, near mid, near side. Compare a named greeting with the fixed fallback when checking playback.

### Final Boss production recipe

Use the original game's B processing chain. Historical notes call it “Lewis B”. The actual production command uses **speaker 26** with the GB English lexicon in the full bank (`bm_george` in model metadata). Speaker 27 is a different source. For the faster reference, the three TTS inputs use **1.32×, 1.31× and 1.27×** respectively. Their combined audible spans are about 4.47 s. Choose the speed for a revised sentence by listening alongside this take.

Apply any author-directed pitch and gain contour to the dry line before the effect. The reference has an accented first statement, a mid-line lift in the second sentence and a rising final question. These are readings of those three lines. For a new delivery Thomas wants to perform, start from his dry recording, preserve his stressed syllables and align the processed result to his intended timing.

| B processing stage | Reference settings |
|---|---|
| Time and pitch | Read dry audio at a 0.86 time step and allow a 350 ms tail. Sum two granular pitch layers at ratios 0.68 and 0.49, using 100 and 140 ms windows with quarter-window hops. |
| Tone and saturation | Combine 0.77 low layer, 0.42 sub layer, 0.62 of the low layer's 145 Hz band (Q 0.8), 0.34 of the slowed source's 3 kHz band (Q 0.85), and 0.11 slowed source. Apply tanh with 1.9 drive. |
| Reflections and level | Add 49 / 93 ms taps at 0.19 / 0.10 linear gain. Normalize each rendered line to 0.87 peak. Account for the 0.86 time step when matching a performed take. |

## Dialogue and progression

The [voice catalog](../../Assets/_Game/Levels/L01_Duel/Resources/VoiceStudioCatalog.json) identifies fixed cues by their stable resource names and supplies the visit dialogue, nickname template, speaker settings and opening delay. Game phase callbacks own progression. Only automatic same-sequence joins use an authored pause. Player-driven Next, Yes and No take effect on input.

| Cue | When it plays | Replay and interruption |
|---|---|---|
| Happy · creation question | One second after the New Game click, while character creation is still open. | Once per New Game. Leaving the page cancels a pending question. |
| Happy · greeting | Upload prepares an offline whole-sentence nickname greeting on desktop. First configuration plays it, with a fixed recording for empty/unsupported input or synthesis failure. | Once per new character. An early Add Rule click allows the greeting to finish. |
| Happy · configuration intro / farewell | After the greeting, play the intro if the rule editor has yet to open. First Add Rule cues the farewell. | Opening early skips the pending intro or interrupts an intro already playing. Leaving the first configuration stops the sequence. |
| Happy · Wave 1 / Wave 2 / Bonus clear | The combat result resolves a genuine victory for the corresponding encounter. | Once at settlement. Result-page input remains available. Testing, defeat and Retry have no new voice trigger. |
| Final Boss · Wave 3 intro / Bonus offer | Enter Wave 3 starts the five provisional pre-fight lines. Wave 3 victory starts the two Bonus lines. | Next replaces the current recording. Fight starts the countdown. The Bonus question offers Yes / No. |

Pause/F8 follows the game's audio pause. Preserve the active phase, input permissions and metrics boundary when changing a recording. Check [the progression and script](BossEncounters.md), [voice playback](../../Assets/_Game/Levels/L01_Duel/Scripts/HandlerVoice.cs), [nickname generation](../../Assets/_Game/Levels/L01_Duel/Scripts/HandlerSpeech.cs) and [Final Boss page routing](../../Assets/_Game/Levels/L01_Duel/Scripts/DuelGame.Progression.cs) for implementation details.

**Cue record to copy:** cue ID and speaker · approved text and revision · fixed WAV or dynamic template and fallback · pronunciation · Thomas's delivery notes · source voice and treatment version · speed, emphasis and target pause · trigger and repeat scope · handling of clicks, pause and exit · chosen recording and approval status.

For example, if Thomas revises only the final question on page 7, keep its page cue and Yes/No routes. Render the revised text against the Final Boss reference, pair it with the revised on-screen line, then test an immediate choice as well as listening to completion.
