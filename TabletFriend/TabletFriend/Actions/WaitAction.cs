using System.Threading.Tasks;

namespace TabletFriend.Actions
{
	using System.Threading;
	public class WaitAction : ButtonAction
	{
		private int _delay;

		public WaitAction(int delay)
		{
			_delay = delay;
		}


		public override Task Invoke() => Invoke(ActionRunner.Token);

		public override Task Invoke(CancellationToken cancellationToken) =>
			Task.Delay(_delay, cancellationToken);
	}
}
