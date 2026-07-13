using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lin.Runtime.Helper
{
    public static class TaskHelper
    {
        public static void ThrowIfCancellationRequested(this CancellationTokenSource self)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            if (self.IsCancellationRequested)
                throw new TaskCanceledException();
        }

        public static int Translate2Milliseconds(this float self) => (int)(self * 1000);
    }
}
