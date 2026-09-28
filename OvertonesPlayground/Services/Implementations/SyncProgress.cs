namespace OvertonesPlayground.Services.Implementations;

///<summary>
///An <see cref="IProgress{T}"/> that runs its callback on whichever thread reports, unlike <see cref="Progress{T}"/>,
///which posts each report to a thread pool thread when it wasn't created on a UI thread and so can deliver them out of
///order.
///</summary>
internal sealed class SyncProgress(Action<double> onReport) : IProgress<double>
{
    public void Report(double value) => onReport(value);
}
