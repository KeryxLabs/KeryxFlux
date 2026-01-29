using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Abstractions
{
    public class MonitorInfoEventArgs : EventArgs
    {
        public Docket Docket { get; }
        public DocketInfo DocketInfo { get; }

        public MonitorInfoEventArgs(Docket docket, DocketInfo docketInfo)
        {
            Docket = docket;
            DocketInfo = docketInfo;
        }
    }

    public class MonitorErrorEventArgs : EventArgs
    {
        public Error Error { get; }

        public MonitorErrorEventArgs(Error error)
        {
            Error = error;
        }
    }
}

