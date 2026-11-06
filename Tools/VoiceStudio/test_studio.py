"""Exercise adoption and undo against copied game assets. Never write to the real catalog."""

import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from uuid import uuid4

import audio
import numpy as np
import studio


class AdoptionTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        resources = root / "Resources"
        resources.mkdir()
        catalog = studio.read_json(studio.CAT)
        shutil.copyfile(studio.CAT, resources / studio.CAT.name)
        for entry in catalog["lines"]:
            target = resources / (entry["key"] + ".wav")
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(studio.RES / (entry["key"] + ".wav"), target)
        author = root / "VoiceAuthoring"
        cache = root / "Library"
        settings = {
            "ROOT": root,
            "RES": resources,
            "CAT": resources / "VoiceStudioCatalog.json",
            "AUTHOR": author,
            "HISTORY": author / "last-adoption.json",
            "JOBS": cache / "jobs",
            "JOURNAL": cache / "pending.json",
        }
        for obj, values in ((studio, settings), (audio, {"CACHE": cache})):
            change = patch.multiple(obj, **values)
            change.start()
            self.addCleanup(change.stop)

    def request(self, action, **params):
        job = studio.JOBS / uuid4().hex
        job.mkdir(parents=True)
        return studio.worker(dict(action=action, **params), job)

    def current(self, key):
        response = self.request("load", key=key)
        return {k: response[k] for k in ("recipe", "catalog", "hash", "version")} | {"key": key}

    def test_opening_gap_adoption_and_stale_write(self):
        key = "Handler/WhoAreYou"
        target, state = studio.paths(key)
        approved = target.read_bytes()
        request = self.current(key)
        request["recipe"]["edits"][1]["gapMs"] = -10
        first = self.request("line", **request)["review"]
        second = self.request("line", **request)["review"]
        self.assertEqual(len(audio.read(target)) - len(audio.read(Path(first) / "draft.wav")), 240)
        self.request("apply", review=first)
        self.assertEqual(target.read_bytes(), (Path(first) / "draft.wav").read_bytes())
        self.assertEqual(studio.read_json(state)["edits"][1]["gapMs"], -10)
        with self.assertRaises(ValueError):
            self.request("apply", review=second)
        self.request("undo")
        self.assertEqual(approved, target.read_bytes())
        self.assertFalse(state.exists())

    def test_finished_happy_electronic_monitor_matches_adoption(self):
        key = "Handler/TutorialIntro"
        target, _ = studio.paths(key)
        original = audio.read(target)
        opening, _ = studio.paths("Handler/WhoAreYou")
        protected = opening.read_bytes()
        request = self.current(key)
        for amount in (0.15, 0, 0.8):
            request["catalog"]["electronic"] = amount
            monitor = studio.JOBS / uuid4().hex
            monitor.mkdir()
            studio.worker(dict(request, action="monitor"), monitor)
            heard = audio.read(monitor / "draft.wav")
            self.assertEqual(heard.shape, original.shape)
            if amount == 0.15:
                np.testing.assert_array_equal(heard, original)
            else:
                self.assertGreater(np.linalg.norm(heard - original) / np.linalg.norm(original), 0.05)
        review = Path(self.request("profiles", **request)["review"])
        rows = studio.read_json(review / "receipt.json")["rows"]
        self.assertEqual({row["key"] for row in rows}, {
            line["key"] for line in request["catalog"]["lines"] if line["key"].startswith("Handler/") and not line["locked"]
        })
        self.request("apply", review=str(review))
        np.testing.assert_array_equal(audio.read(target), heard)
        self.assertEqual(opening.read_bytes(), protected)
        request = self.current(key)
        request["catalog"]["electronic"] = 0.15
        review = self.request("profiles", **request)["review"]
        self.request("apply", review=review)
        np.testing.assert_array_equal(audio.read(target), original)

    def test_batch_and_recovery(self):
        request = self.current("Handler/WhoAreYou")
        original = studio.CAT.read_bytes()
        request["catalog"]["happySpeed"] = 1.15
        review = self.request("profiles", **request)["review"]
        rows = studio.read_json(Path(review) / "receipt.json")["rows"]
        self.assertEqual(len(rows), 6)
        self.assertTrue(
            all(row["key"].startswith("Handler/") and row["key"] != request["key"] for row in rows)
        )
        self.request("apply", review=review)
        self.request("undo")
        self.assertEqual(original, studio.CAT.read_bytes())
        # Simulate interruption after a replacement and before transaction completion.
        old = studio.archive(original, ".json")
        studio.atomic(
            studio.JOURNAL, studio.encode([{"path": "Resources/VoiceStudioCatalog.json", "old": old}])
        )
        studio.atomic(studio.CAT, b"incomplete")
        self.current(request["key"])
        self.assertEqual(studio.CAT.read_bytes(), original)

    def test_each_profile_control_review_and_save(self):
        cases = {
            "masterDb": -9, "happyDb": -7, "bossDb": -10, "openingDelay": 2.5,
            "greetingTemplate": "Hello, {name}.", "happySpeed": 1.20, "bossSpeed": 1.5,
            "matchTarget": True, "targetDb": -32, "electronic": 0.75,
            "nearDb": -12, "spaceDb": 8, "bossDrive": 2.9, "bossSub": 0.05,
            "happySub": .95, "happyDrive": 2.9, "bossElectronic": .85, "bossNearDb": 7, "bossSpaceDb": 7,
        }
        self.assertEqual(set(cases), set(studio.PROFILE_SETTINGS))
        for field, value in cases.items():
            with self.subTest(field=field):
                boss = field.startswith("boss")
                key = "FinalBossVoice/Line1" if boss else "Handler/TutorialIntro"
                request = self.current(key)
                if field == "targetDb":
                    request["catalog"]["matchTarget"] = True
                    self.request("apply", review=self.request("profiles", **request)["review"])
                    request = self.current(key)
                request["catalog"][field] = value
                target, _ = studio.paths(key)
                baseline = audio.read(target)
                monitor = studio.JOBS / uuid4().hex
                monitor.mkdir()
                studio.worker(dict(request, action="monitor"), monitor)
                heard = audio.read(monitor / "draft.wav")
                response = self.request("profiles", **request)
                self.assertTrue(response["canApply"])
                self.assertTrue(any(studio.PROFILE_SETTINGS[field][0] in change and " → " in change
                                    for change in response["summary"]))
                review = Path(response["review"])
                rows = studio.read_json(review / "receipt.json")["rows"]
                if field in ("masterDb", "happyDb", "bossDb", "openingDelay", "greetingTemplate"):
                    self.assertFalse(rows)
                    np.testing.assert_array_equal(heard, baseline)
                else:
                    self.assertIn(key, [row["key"] for row in rows])
                    np.testing.assert_array_equal(heard, audio.read(review / key.replace("/", "-") / "draft.wav"))
                    if len(heard) == len(baseline):
                        self.assertGreater(np.linalg.norm(heard - baseline) / np.linalg.norm(baseline), .01)
                    else:
                        self.assertIn(field, ("happySpeed", "bossSpeed"))
                self.request("apply", review=str(review))
                self.assertEqual(studio.read_json(studio.CAT)[field], value)
                np.testing.assert_array_equal(audio.read(target), heard)
                self.request("undo")
                np.testing.assert_array_equal(audio.read(target), baseline)
                if field == "targetDb":
                    reset = self.current(key)
                    reset["catalog"]["matchTarget"] = False
                    self.request("apply", review=self.request("profiles", **reset)["review"])
        response = self.request("profiles", **self.current("Handler/Greeting"))
        self.assertFalse(response["canApply"])
        self.assertEqual(response["summary"], [])

    def test_free_text_preview(self):
        installed = studio.read_json(studio.CAT)
        def preview(text, boss, **settings):
            job = studio.JOBS / uuid4().hex
            job.mkdir(parents=True)
            studio.worker(dict(action="textPreview", auditionText=text, previewBoss=boss,
                               catalog=dict(installed, **settings)), job)
            self.assertFalse((job / "receipt.json").exists())
            return audio.read(job / "audition.wav")
        for boss in (False, True):
            original = preview("阿西吧，hello!", boss)
            change = {"bossElectronic": .8} if boss else {"happySub": .95}
            tuned = preview("阿西吧，hello!", boss, **change)
            self.assertGreater(np.linalg.norm(original - tuned) / np.linalg.norm(original), .03)
            longer = preview("This is a different sentence. Can you hear the change?", boss)
            self.assertGreater(len(longer), len(original))
        self.assertEqual(studio.read_json(studio.CAT), installed)

    def test_single_line_audible_controls(self):
        request = self.current("Handler/Greeting")
        before = audio.read(studio.paths(request["key"])[0])
        for field, value in (("speed", 1.3), ("pitch", 3), ("gain", -6), ("nearDb", 7),
                             ("spaceDb", -12), ("electronic", .9), ("stretch", 1.5),
                             ("wordPitch", -4), ("wordGain", -8), ("gapMs", 90)):
            with self.subTest(field=field):
                recipe = dict(request["recipe"], edits=[dict(e) for e in request["recipe"]["edits"]])
                if field in ("stretch", "wordPitch", "wordGain", "gapMs"):
                    recipe["edits"][0][{"wordPitch": "pitch", "wordGain": "gain"}.get(field, field)] = value
                else:
                    recipe[field] = value
                    recipe["overrideTone"] = True
                response = self.request("line", **dict(request, recipe=recipe))
                after = audio.read(Path(response["review"]) / "draft.wav")
                if before.shape == after.shape:
                    self.assertGreater(np.linalg.norm(after - before) / np.linalg.norm(before), .005)

    def test_reference_and_dry_recording_controls(self):
        request = self.current("Handler/Greeting")
        recipe = request["recipe"]
        recipe.update(mode="Dry", source=str(audio.ROOT / "Docs/Game/VoiceReferences/Happy-Inner-Dry.wav"),
                      reference=str(studio.paths("Handler/TutorialGoodbye")[0]), edits=[])
        def samples(**changes):
            result = self.request("line", **dict(request, recipe=dict(recipe, **changes)))
            return audio.read(Path(result["review"]) / "draft.wav")
        original = samples()
        for field in ("melody", "timing", "stress"):
            with self.subTest(field=field):
                take = samples(**{field: .75})
                if len(take) == len(original):
                    self.assertGreater(np.linalg.norm(take - original) / np.linalg.norm(original), .01)
        # A line-level gain must survive the Boss mastering stage, which peak-normalizes internally.
        request = self.current("FinalBossVoice/Line1")
        recipe.update(text=request["recipe"]["text"])
        original = samples()
        quiet = samples(gain=-6)
        ratio = np.linalg.norm(quiet) / np.linalg.norm(original)
        self.assertAlmostEqual(ratio, 10 ** (-6 / 20), places=3)

    def test_review_multiple_settings_with_per_line_override(self):
        request = self.current("Handler/Greeting")
        request["recipe"].update(overrideTone=True, spaceDb=3)
        self.request("apply", review=self.request("line", **request)["review"])
        request = self.current("Handler/Greeting")
        request["catalog"].update(nearDb=5, spaceDb=-7, bossDrive=2.5, masterDb=-3)
        result = self.request("profiles", **request)
        self.assertEqual(len(result["summary"]), 4)
        rows = studio.read_json(Path(result["review"]) / "receipt.json")["rows"]
        self.assertEqual(len(rows), 12)
        self.assertFalse(any(row["key"] in ("Handler/Greeting", "Handler/WhoAreYou") for row in rows))

    def test_revised_subtitle_requires_revised_performance(self):
        request = self.current("FinalBossVoice/Line1")
        request["recipe"]["text"] = "Who are you?"
        with self.assertRaisesRegex(ValueError, "new wording"):
            self.request("line", **request)
        request["recipe"].update(mode="Synthesize", edits=[])
        review = self.request("line", **request)["review"]
        self.request("apply", review=review)
        adopted = self.current("FinalBossVoice/Line1")
        self.assertEqual(adopted["recipe"]["text"], "Who are you?")
        self.assertTrue((studio.ROOT / adopted["recipe"]["source"]).is_file())


if __name__ == "__main__":
    unittest.main()
