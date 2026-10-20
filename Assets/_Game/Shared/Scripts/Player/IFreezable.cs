// FUNCTION SUMMARY
// Freeze() — stuns/freezes whatever implements this for the given duration; your partner's enemy scripts implement this.
public interface IFreezable
{
    /// <param name="duration">How long, in seconds, the target should be frozen/stunned for.</param>
    void Freeze(float duration);
}