using System.Threading.Tasks;

namespace TabletFriend.Actions
{
	using System.Threading;
	public class BatchAction : ButtonAction
	{
		private ButtonAction[] _actions;

		public BatchAction(ButtonAction[] actions)
		{
			_actions = actions;
		}

		public override Task Invoke() => Invoke(ActionRunner.Token);

		public override async Task Invoke(CancellationToken cancellationToken)
		{
			for (var i = 0; i < _actions.Length; i += 1)
			{
				cancellationToken.ThrowIfCancellationRequested();
				await _actions[i].Invoke(cancellationToken);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			for (var i = 0; i < _actions.Length; i += 1)
			{
				_actions[i].Dispose();
			}
		}
	}
}
