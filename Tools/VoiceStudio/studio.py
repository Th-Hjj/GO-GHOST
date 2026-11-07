"""Voice Studio worker. Preview in Library; publish exactly the reviewed bytes."""

import hashlib
import json
import os
import re
import sys
import tempfile
import traceback
from contextlib import contextmanager
from pathlib import Path

import audio
import numpy as np

ROOT = audio.ROOT
RES = ROOT / "Assets/_Game/Levels/L01_Duel/Resources"
CAT = RES / "VoiceStudioCatalog.json"
AUTHOR = ROOT / "VoiceAuthoring"
JOBS = audio.CACHE / "jobs"
HISTORY = AUTHOR / "last-adoption.json"
JOURNAL = audio.CACHE / "pending.json"
EXTRA_TONE = dict(happySub=.42, happyDrive=1.9, bossElectronic=0., bossNearDb=0., bossSpaceDb=0.)
HAPPY_TONE = ("electronic", "nearDb", "spaceDb", "happySub", "happyDrive")
BOSS_TONE = ("bossSub", "bossDrive", "bossElectronic", "bossNearDb", "bossSpaceDb")
PROFILE_SETTINGS = {
    "masterDb": ("总音量", " dB"), "matchTarget": ("让各句音量接近", ""),
    "targetDb": ("统一音量", " dB"), "happyDb": ("Happy · 角色音量", " dB"),
    "happySpeed": ("Happy · 常规语速", " 倍"), "electronic": ("Happy · 电子感", "%"),
    "nearDb": ("Happy · 贴耳回声", " dB"), "spaceDb": ("Happy · 空灵回声", " dB"),
    "openingDelay": ("开场等待", " 秒"), "greetingTemplate": ("昵称问候台词", ""),
    "bossDb": ("Final Boss · 角色音量", " dB"), "bossSpeed": ("Final Boss · 常规语速", " 倍"),
    "bossSub": ("Final Boss · 低沉声叠加量", ""), "bossDrive": ("Final Boss · 粗粝感", ""),
    "happySub": ("Happy · 低沉声叠加量", ""), "happyDrive": ("Happy · 粗粝感", ""),
    "bossElectronic": ("Final Boss · 电子感", "%"), "bossNearDb": ("Final Boss · 贴耳回声", " dB"),
    "bossSpaceDb": ("Final Boss · 空灵回声", " dB"),
}


def display_value(value, unit):
    if isinstance(value, bool):
        return "开启" if value else "关闭"
    if isinstance(value, str):
        return value
    return f"{value * (100 if unit == '%' else 1):.7g}{unit}"


def changed_settings(profile, installed):
    changed = []
    for field in PROFILE_SETTINGS:
        old, new = installed[field], profile[field]
        if isinstance(old, (int, float)) and abs(old - new) <= 1e-6:
            continue
        if old != new:
            changed.append(field)
    return changed


def needs_render(req, entry, recipe, changed):
    if entry["locked"] or recipe["independent"]:
        return False
    boss = entry["key"].startswith("FinalBossVoice/")
    fields = {"bossSpeed" if boss else "happySpeed", "matchTarget"}
    if req["catalog"]["matchTarget"]:
        fields.add("targetDb")
    if not recipe.get("overrideTone"):
        fields.update(BOSS_TONE if boss else HAPPY_TONE)
    return should_revoice(req, entry, recipe) or not fields.isdisjoint(changed)


def read_json(p):
    return json.loads(p.read_text(encoding="utf-8-sig"))


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def file_hash(p):
    return digest(p.read_bytes()) if p.exists() else ""


def atomic(p, data):
    p.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=p.parent, delete=False) as f:
        tmp = Path(f.name)
        f.write(data)
    try:
        os.replace(tmp, p)
    finally:
        tmp.unlink(missing_ok=True)


def archive(data, suffix):
    path = AUTHOR / "media" / (digest(data) + suffix)
    if not path.exists():
        atomic(path, data)
    return path.relative_to(ROOT).as_posix()


@contextmanager
def writer():
    audio.CACHE.mkdir(parents=True, exist_ok=True)
    with (audio.CACHE / "write.lock").open("a+b") as lock:
        lock.seek(0)
        if os.name == "nt":
            import msvcrt

            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl

            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if JOURNAL.exists():
            for row in read_json(JOURNAL):
                restore(row)
            JOURNAL.unlink()
        yield


def restore(row):
    target = tracked_path(row["path"])
    if row["old"]:
        atomic(target, archived(row["old"]))
    else:
        target.unlink(missing_ok=True)


def tracked_path(relative):
    target = (ROOT / relative).resolve()
    if target in (CAT.resolve(), HISTORY.resolve()):
        return target
    if target.is_relative_to(RES.resolve()) and target.suffix == ".wav":
        return target
    if target.is_relative_to((AUTHOR / "recipes").resolve()) and target.suffix == ".json":
        return target
    raise ValueError("Unexpected file in the voice adoption history.")


def archived(relative):
    p = (ROOT / relative).resolve()
    if p.parent != (AUTHOR / "media").resolve():
        raise ValueError("Invalid archived voice file.")
    data = p.read_bytes()
    if digest(data) != p.stem:
        raise ValueError("Archived file content changed.")
    return data


def publish(changes):
    rows = []
    for path, new in changes.items():
        if file_hash(path) == (digest(new) if new is not None else ""):
            continue
        old = archive(path.read_bytes(), path.suffix) if path.exists() else ""
        rows.append(
            {
                "path": path.relative_to(ROOT).as_posix(),
                "old": old,
                "after": digest(new) if new is not None else "",
            }
        )
    if not rows:
        return []
    previous = archive(HISTORY.read_bytes(), ".json") if HISTORY.exists() else ""
    atomic(JOURNAL, encode(rows + [{"path": HISTORY.relative_to(ROOT).as_posix(), "old": previous}]))
    try:
        for row in rows:
            path = ROOT / row["path"]
            if changes[path] is None:
                path.unlink(missing_ok=True)
            else:
                atomic(path, changes[path])
        atomic(HISTORY, encode(rows))
        JOURNAL.unlink()
    except BaseException:
        for row in read_json(JOURNAL):
            restore(row)
        JOURNAL.unlink()
        raise
    return rows


def default_recipe(key, text):
    edits = []
    if key == "Handler/WhoAreYou":
        for word, a, b in [("Who", 0.045, 0.455), ("are", 0.506, 0.843), ("you", 0.892, 1.10)]:
            edits.append(
                {"label": word, "start": a, "end": b, "stretch": 1.0, "pitch": 0.0, "gain": 0.0, "gapMs": 0}
            )
    return {
        "mode": "Master",
        "source": "",
        "reference": "",
        "text": text,
        "pronunciations": "",
        "speed": 1.0,
        "pitch": 0.0,
        "gain": 0.0,
        "melody": 0.0,
        "stress": 0.0,
        "timing": 0.0,
        "edits": edits,
        "independent": False,
        "overrideTone": False,
        "electronic": 0.15,
        "nearDb": 0.0,
        "spaceDb": 0.0,
        "bossSub": 0.42,
        "bossDrive": 1.9,
    }


def paths(key):
    if not re.fullmatch(r"(Handler|FinalBossVoice)/[A-Za-z0-9]+", key):
        raise ValueError("Invalid voice resource name.")
    return RES / (key + ".wav"), AUTHOR / "recipes" / (key.replace("/", "-") + ".json")


def load_line(line):
    wav, state = paths(line["key"])
    recipe = read_json(state) if state.exists() else default_recipe(line["key"], line["text"])
    return EXTRA_TONE | recipe, wav


def suggest(text, x):
    """Editable estimates, never a phoneme-forced transcript."""
    words = text.split()
    if not words:
        return []
    times, amp, _ = audio.features(x)
    active = np.flatnonzero(amp > max(0.0005, amp.max() * 0.055))
    if len(active) < len(words):
        return []
    first, last = active[[0, -1]]
    mass = np.cumsum(np.maximum(amp[first : last + 1], amp.max() * 0.01) ** 0.6)
    weights = np.asarray([max(2, len(w.strip(".,!?"))) for w in words])
    estimates = np.interp(np.cumsum(weights)[:-1] / sum(weights), mass / mass[-1], times[first : last + 1])
    edges = [times[first]]
    anchors = [*estimates, times[last]]
    for i, anchor in enumerate(estimates):
        window = min(0.08, (anchor - edges[-1]) * 0.4, (anchors[i + 1] - anchor) * 0.4)
        nearby = np.flatnonzero(abs(times - anchor) <= window)
        edges.append(float(times[nearby[np.argmin(amp[nearby])]]) if len(nearby) else float(anchor))
    edges.append(min(len(x) / audio.RATE, times[last] + 0.01))
    return [
        {"label": word, "start": a, "end": b, "stretch": 1.0, "pitch": 0.0, "gain": 0.0, "gapMs": 0}
        for word, a, b in zip(words, edges, edges[1:])
    ]


def synth_source(recipe, boss, audition_folder=None):
    speed = 1.30 if boss else 1.05
    pronunciations = recipe.get("pronunciations") or ""
    spoken = recipe["text"].replace("“", "").replace("”", "")
    ident = digest(encode([spoken, pronunciations, speed, boss, "kokoro-v1-studio1"]))
    if audition_folder is not None:
        ident = "audition-" + ident
    path = audio.CACHE / "dry" / (ident + ".wav")
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=path.parent, suffix=".wav", delete=False) as f:
            tmp = Path(f.name)
        try:
            audio.write(tmp, audio.synth(spoken, boss, speed, pronunciations, audition_folder))
            os.replace(tmp, path)
        finally:
            tmp.unlink(missing_ok=True)
    return path


def cross_tone(y, tone, boss):
    if boss:
        y = audio.adjust_finished_happy(y, .15 + .85 * tone["bossElectronic"])
        return audio.adjust_finished_room(y, tone["bossNearDb"], tone["bossSpaceDb"], boss=True)
    return audio.happy_body(y, tone["happySub"], tone["happyDrive"])


def master_voice(dry, tone, boss):
    if boss:
        y = audio.boss_master(dry, tone["bossSub"], tone["bossDrive"])
    else:
        y = audio.render_happy_voice.master(dry, tone["electronic"], tone["nearDb"], tone["spaceDb"]) / 32768
    return cross_tone(y, tone, boss)


def match_level(y, profile):
    active = y[np.abs(y) > 0.015]
    if len(active):
        y *= min(0.98 / np.abs(y).max(), 10 ** (profile["targetDb"] / 20) / np.sqrt(np.mean(active**2)))


def render(line, recipe, profile, folder):
    key = line["key"]
    wav, state = paths(key)
    initial = (file_hash(wav), file_hash(state))
    boss = key.startswith("FinalBossVoice/")
    mode = recipe["mode"]
    source = ROOT / recipe["source"] if recipe["source"] else wav
    prior = load_line(line)[0]
    if mode == "Synthesize":
        if (
            prior["mode"] == "Synthesize"
            and prior["text"] == recipe["text"]
            and prior["source"]
            and prior.get("pronunciations") == recipe.get("pronunciations")
        ):
            source = ROOT / prior["source"]
        else:
            source = synth_source(recipe, boss)
    elif mode == "Dry" and not recipe["source"]:
        raise ValueError("Choose a dry recording.")
    if mode not in ("Dry", "Master", "Synthesize"):
        raise ValueError("Unknown source mode.")
    if recipe["text"] != line["text"] and mode != "Synthesize" and source in (wav, ROOT / prior["source"]):
        raise ValueError("Synthesize the new wording or import its recording before adopting different text.")
    x = audio.read(source)
    ref = audio.read(ROOT / recipe["reference"]) if recipe["reference"] else None
    speed = (
        1
        if line["locked"] or recipe["independent"]
        else profile["bossSpeed" if boss else "happySpeed"] / (1.30 if boss else 1.05)
    )
    performance = x
    if mode == "Master" and (not line["locked"] or recipe.get("overrideTone")):
        if not recipe["independent"] and not recipe.get("overrideTone"):
            recipe = dict(recipe, **{f: profile[f] for f in (BOSS_TONE if boss else HAPPY_TONE)})
        if boss:
            performance = audio.adjust_finished_boss(x, recipe["bossSub"], recipe["bossDrive"])
        else:
            performance = audio.adjust_finished_happy(x, recipe["electronic"])
            performance = audio.adjust_finished_room(performance, recipe["nearDb"], recipe["spaceDb"])
        performance = cross_tone(performance, recipe, boss)
    # Whole-line volume follows mastering/level matching so normalization cannot cancel the slider.
    y, mapped = audio.edit(performance.copy(), dict(recipe, gain=0), speed, ref)
    if mode != "Master":
        tone = recipe if recipe.get("overrideTone") else profile
        dry = y.mean(axis=1)
        y = master_voice(dry, tone, boss)
        if boss:
            for mark in mapped:
                mark["start"] /= 0.86
                mark["end"] /= 0.86
    if profile.get("matchTarget") and not line["locked"] and not recipe["independent"]:
        match_level(y, profile)
    if recipe["gain"]:
        if np.max(abs(y)) > 1:
            y *= 0.99 / np.max(abs(y))
        y *= 10 ** (recipe["gain"] / 20)
        peaks = abs(y) > 0.96
        y[peaks] = np.sign(y[peaks]) * (0.96 + 0.039 * np.tanh((abs(y[peaks]) - 0.96) / 0.039))
    folder.mkdir(parents=True, exist_ok=True)
    audio.write(folder / "draft.wav", y)
    audio.write(folder / "source.wav", x)
    # Snapshot inputs now. Apply must remain valid even if external files change later.
    if ref is not None:
        audio.write(folder / "reference.wav", ref)
    if initial != (file_hash(wav), file_hash(state)):
        raise ValueError("Voice changed during render. Reload and retry.")
    return {
        "key": key,
        "recipe": recipe,
        "folder": str(folder),
        "wavHash": initial[0],
        "stateHash": initial[1],
        "mapped": mapped,
        "draftHash": file_hash(folder / "draft.wav"),
    }


def worker(req, job):
    action = req["action"]
    if action == "textPreview":
        boss = req["previewBoss"]
        profile = req["catalog"]
        recipe = default_recipe("", req["auditionText"])
        source = audio.read(synth_source(recipe, boss, job))
        speed = profile["bossSpeed"] / 1.30 if boss else profile["happySpeed"] / 1.05
        timed, _ = audio.edit(source, recipe, speed)
        y = master_voice(timed[:, 0], profile, boss)
        if profile["matchTarget"]:
            match_level(y, profile)
        audio.write(job / "audition.wav", y)
        return {"message": "试听就绪"}
    with writer():
        catalog = EXTRA_TONE | read_json(CAT)
        version = file_hash(CAT)
        if action == "undo":
            if not HISTORY.exists():
                raise ValueError("No saved adoption to restore.")
            rows = read_json(HISTORY)
            if any(file_hash(tracked_path(r["path"])) != r["after"] for r in rows):
                raise ValueError("Files changed since adoption. Reload before restoring.")
            publish({tracked_path(r["path"]): archived(r["old"]) if r["old"] else None for r in rows})
            return {"message": "Previous version restored."}
        if action == "apply":
            preview = Path(req["review"]).resolve()
            if preview.parent != JOBS.resolve():
                raise ValueError("Invalid preview.")
            receipt = read_json(preview / "receipt.json")
            if receipt["version"] != version:
                raise ValueError("Catalog changed. Reload and preview again.")
            changes = {}
            valid_keys = {entry["key"] for entry in catalog["lines"]}
            for row in receipt["rows"]:
                key = row["key"]
                path, state = paths(key)
                folder = Path(row["folder"])
                if key not in valid_keys:
                    raise ValueError("Unknown voice cue.")
                if (
                    file_hash(path) != row["wavHash"]
                    or file_hash(state) != row["stateHash"]
                    or file_hash(folder / "draft.wav") != row["draftHash"]
                ):
                    raise ValueError("The reviewed take changed. Reload and preview again.")
                recipe = row["recipe"]
                recipe["source"] = archive((folder / "source.wav").read_bytes(), ".wav")
                if recipe["reference"]:
                    recipe["reference"] = archive((folder / "reference.wav").read_bytes(), ".wav")
                changes[path] = (folder / "draft.wav").read_bytes()
                changes[state] = encode(recipe)
            changes[CAT] = encode(receipt["catalog"])
            publish(changes)
            return {"message": "Adopted reviewed version."}
    entries = {line["key"]: line for line in catalog["lines"]}
    key = req["key"]
    line = entries[key]
    recipe, wav = load_line(line)
    stamp = file_hash(wav) + file_hash(paths(key)[1])
    if action == "load":
        source = ROOT / recipe["source"] if recipe["source"] else wav
        x = audio.read(source)
        audio.write(job / "source.wav", x)
        if not paths(key)[1].exists() and not recipe["edits"]:
            recipe["edits"] = suggest(recipe["text"], x)
        return {"recipe": recipe, "catalog": catalog, "version": version, "hash": stamp, "message": "Ready."}
    if req["version"] != version or stamp != req["hash"]:
        raise ValueError("Game version changed. Reload before editing.")
    rows = []
    if action == "suggest":
        temp = dict(req["recipe"], edits=[])
        render(line, temp, catalog, job)
        temp["edits"] = suggest(temp["text"], audio.read(job / "source.wav"))
        return {
            "recipe": temp,
            "message": "Estimated word regions. Drag or edit their boundaries before fine adjustments.",
        }
    if action == "monitor":
        if not needs_render(req, line, recipe, changed_settings(req["catalog"], catalog)):
            x = audio.read(wav)
            audio.write(job / "source.wav", x)
            audio.write(job / "draft.wav", x)
            return {"mapped": [], "message": "Protected recording."}
        if should_revoice(req, line, recipe):
            recipe = dict(recipe, mode="Synthesize", source="", edits=[])
        row = render(line, recipe, req["catalog"], job)
        return {"mapped": row["mapped"], "message": "Speaker preview ready."}
    if action == "line":
        recipe = req["recipe"]
        row = render(line, recipe, catalog, job)
        rows.append(row)
        if recipe["text"] != line["text"]:
            line["transcribed"] = False
        line["text"] = recipe["text"]
        message = "Draft ready."
    elif action == "profiles":
        profile = req["catalog"]
        changed = changed_settings(profile, catalog)
        summary = []
        for field in changed:
            label, unit = PROFILE_SETTINGS[field]
            summary.append(f"{label}　{display_value(catalog[field], unit)} → {display_value(profile[field], unit)}")
        if profile["greetingTemplate"].count("{name}") != 1:
            raise ValueError("Include {name} once in the nickname template.")
        words = set(
            re.findall(r"[a-z]+(?:'[a-z]+)?", profile["greetingTemplate"].replace("{name}", "").lower())
        )
        with (audio.BANK / "lexicon-us-en.txt").open(encoding="utf8") as f:
            for word in f:
                words.discard(word.partition(" ")[0])
                if not words:
                    break
        if words:
            raise ValueError("Unknown greeting pronunciation: " + ", ".join(sorted(words)))
        revoiced = set()
        for entry in catalog["lines"]:
            rec, _ = load_line(entry)
            if not needs_render(req, entry, rec, changed):
                continue
            revoice = should_revoice(req, entry, rec)
            if revoice:
                revoiced.add("Final Boss" if entry["key"].startswith("FinalBossVoice/") else "Happy")
                rec = dict(rec, mode="Synthesize", source="", edits=[])
            rows.append(render(entry, rec, profile, job / entry["key"].replace("/", "-")))
        for speaker in sorted(revoiced):
            summary.append(f"{speaker} · 按台词重新配音")
        catalog.update({f: profile[f] for f in changed})
        message = (
            "Affected WAVs: " + (", ".join(r["key"] for r in rows) or "none") + ". Opening master excluded."
        )
    else:
        raise ValueError("Unknown operation.")
    atomic(job / "receipt.json", encode({"version": version, "rows": rows, "catalog": catalog}))
    return {"message": message, "review": str(job), "mapped": rows[0]["mapped"] if action == "line" else [],
            "summary": summary if action == "profiles" else [],
            "canApply": action == "line" or bool(changed or rows)}


def should_revoice(req, line, recipe):
    flag = "revoiceBoss" if line["key"].startswith("FinalBossVoice/") else "revoiceHappy"
    return (
        req.get(flag, False)
        and recipe["mode"] == "Master"
        and not line["locked"]
        and not recipe["independent"]
    )


if __name__ == "__main__":
    job = Path(sys.argv[1]).resolve()
    try:
        result = worker(read_json(job / "request.json"), job)
        result["ok"] = True
    except Exception as error:  # noqa: BLE001 - the Editor receives one structured job result on failure.
        traceback.print_exc()
        result = {"ok": False, "message": str(error)}
    atomic(job / "result.json", encode(result))
