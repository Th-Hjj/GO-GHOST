/// <summary>
/// Optional hook on the officer or its children. Apply once from the locked deployment;
/// clean transient state, subscriptions and spawned objects at attempt end. Cleanup must be idempotent.
/// Future effects use this boundary; no mutable draft or static combat state belongs here.
/// </summary>
public interface IAttemptAugmentation
{
    void ApplyDeployment(DeploymentSnapshot snapshot);
    void CleanupAttempt();
}
