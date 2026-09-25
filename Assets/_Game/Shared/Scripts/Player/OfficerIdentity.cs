using System;
using UnityEngine;

/// <summary>Assigned once to a new actor instance. Combat receives only the immutable deployment.</summary>
public sealed class OfficerIdentity : MonoBehaviour
{
    private IAttemptAugmentation[] augmentations = System.Array.Empty<IAttemptAugmentation>();
    private bool attemptEnded;
    public DeploymentSnapshot Deployment { get; private set; }
    public int OfficerIndex => Deployment != null ? Deployment.OfficerIndex : 0;

    public void Assign(DeploymentSnapshot snapshot)
    {
        if (Deployment != null) throw new InvalidOperationException("An officer identity cannot be reused.");
        Deployment = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        gameObject.name = "Officer #" + OfficerIndex;
        GetComponent<OfficerVisual>()?.Apply(snapshot);
        var hooks = new System.Collections.Generic.List<IAttemptAugmentation>();
        foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
            if (component is IAttemptAugmentation augmentation) hooks.Add(augmentation);
        augmentations = hooks.ToArray();
        foreach (var augmentation in augmentations) augmentation.ApplyDeployment(snapshot);
    }

    public void EndAttempt()
    {
        if (attemptEnded) return;
        attemptEnded = true;
        foreach (var augmentation in augmentations) augmentation.CleanupAttempt();
    }

    private void OnDisable() => EndAttempt();
}
