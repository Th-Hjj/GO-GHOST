using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One TextAsset, stable IDs, plain records. No execution commands or story conditions in text.</summary>
public sealed class NarrativeContent
{
    [Serializable] private sealed class Document
    {
        public int schemaVersion;
        public string contentRevision, language;
        public Speaker[] speakers;
        public Entry[] entries;
        public Sequence[] sequences;
    }
    [Serializable] private sealed class Speaker { public string id, displayName; }
    [Serializable] private sealed class Entry { public string id, speakerId, text, status, notes; }
    [Serializable] private sealed class Sequence { public string id; public string[] lineIds; }
    private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private readonly Dictionary<string, Sequence> sequences = new Dictionary<string, Sequence>();

    public NarrativeContent(TextAsset source)
    {
        if (source == null) throw new ArgumentException("Narrative TextAsset is missing.");
        var doc = JsonUtility.FromJson<Document>(source.text);
        if (doc == null || doc.schemaVersion != 1) throw new ArgumentException("Unknown narrative schemaVersion; expected 1.");
        if (string.IsNullOrWhiteSpace(doc.contentRevision) || string.IsNullOrWhiteSpace(doc.language)
            || doc.speakers == null || doc.entries == null || doc.sequences == null)
            throw new ArgumentException("Narrative document fields are missing.");
        var speakers = new HashSet<string>();
        foreach (var speaker in doc.speakers)
            if (speaker == null || string.IsNullOrWhiteSpace(speaker.id) || !speakers.Add(speaker.id))
                throw new ArgumentException("Invalid or duplicate narrative speaker ID.");
        foreach (var entry in doc.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entries.ContainsKey(entry.id)
                || !speakers.Contains(entry.speakerId ?? "") || (entry.status != "approved" && entry.status != "draft"))
                throw new ArgumentException("Invalid narrative entry, speaker reference or status.");
            entries.Add(entry.id, entry);
        }
        foreach (var sequence in doc.sequences)
        {
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.id) || sequences.ContainsKey(sequence.id) || sequence.lineIds == null)
                throw new ArgumentException("Invalid or duplicate narrative sequence.");
            foreach (string id in sequence.lineIds)
                if (id == null || !entries.ContainsKey(id)) throw new ArgumentException("Missing narrative line: " + id);
            sequences.Add(sequence.id, sequence);
        }
    }

    public string[] ApprovedSequence(string id) => SequenceText(id, false);

    public string EntryText(string id, bool allowDraft = false)
    {
        if (!entries.TryGetValue(id, out var entry) || string.IsNullOrWhiteSpace(entry.text)
            || (entry.status != "approved" && !allowDraft))
            throw new ArgumentException("Missing, empty or unaccepted narrative entry: " + id);
        return entry.text;
    }

    public string[] SequenceText(string id, bool allowDraft = false)
    {
        if (!sequences.TryGetValue(id, out var sequence) || sequence.lineIds.Length == 0)
            throw new ArgumentException("Missing or empty narrative sequence: " + id);
        var lines = new string[sequence.lineIds.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            var entry = entries[sequence.lineIds[i]];
            if ((!allowDraft && entry.status != "approved") || string.IsNullOrWhiteSpace(entry.text))
                throw new ArgumentException("Unapproved or empty connected narrative line: " + entry.id);
            lines[i] = entry.text;
        }
        return lines;
    }
}
