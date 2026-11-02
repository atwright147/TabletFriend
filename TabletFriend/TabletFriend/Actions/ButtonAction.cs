using System;
using System.Threading.Tasks;

namespace TabletFriend.Actions
{
	using System.Threading;
	public abstract class ButtonAction : IDisposable
	{
		public abstract Task Invoke();

		public virtual Task Invoke(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Invoke();
		}

		public virtual void Dispose() {}
	}
}
