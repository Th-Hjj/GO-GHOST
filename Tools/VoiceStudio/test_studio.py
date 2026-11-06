"""Exercise adoption and undo against copied game assets. Never write to the real catalog."""

import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from uuid import uuid4

import audio
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
