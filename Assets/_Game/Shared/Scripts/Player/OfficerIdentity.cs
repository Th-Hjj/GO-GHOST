using System;
using UnityEngine;

/// <summary>Assigned once to a new actor instance. Combat receives only the immutable deployment.</summary>
public sealed class OfficerIdentity : MonoBehaviour
{
    public DeploymentSnapshot Deployment { get; private set; }
    public int OfficerIndex => Deployment != null ? Deployment.OfficerIndex : 0;

    public void Assign(DeploymentSnapshot snapshot)
    {
        if (Deployment != null) throw new InvalidOperationException("An officer identity cannot be reused.");
        Deployment = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        gameObject.name = "Officer #" + OfficerIndex;
        GetComponent<OfficerVisual>()?.Apply(snapshot);
    }
}
