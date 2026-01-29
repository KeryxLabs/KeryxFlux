using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Abstractions
{
    public class MonitorInfoEventArgs(Docket Docket, DocketInfo DocketInfo) : EventArgs;
    public class MonitorErrorEventArgs(Error Error) : EventArgs;

}
