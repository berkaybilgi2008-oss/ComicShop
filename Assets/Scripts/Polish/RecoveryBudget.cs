// Pure policy: repeated failures back off; a book is never abandoned permanently.
public sealed class RecoveryBudget
{
    public int Attempts { get; private set; }
    public double RetryAt { get; private set; }
    int safeObservations;
    public bool CanAttempt(double now) => now >= RetryAt;
    public void ObserveSafe()
    {
        if (++safeObservations < 2) return;
        Attempts = 0;
        RetryAt = 0;
    }
    public bool Attempt(double now, int limit, double backoff)
    {
        safeObservations = 0;
        if (!CanAttempt(now)) return false;
        Attempts++;
        if (Attempts >= System.Math.Max(1, limit))
        {
            RetryAt = now + System.Math.Max(1, backoff);
            Attempts = 0;
        }
        return true;
    }
}
