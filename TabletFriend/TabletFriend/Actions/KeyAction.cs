using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Events;

namespace TabletFriend.Actions
{
	public class KeyAction : ButtonAction
	{
		private readonly KeyCode[] _keys;
		internal KeyCode[] Keys => (KeyCode[])_keys.Clone();

		public KeyAction(params KeyCode[] keys)
		{
			_keys = keys;
		}

		public override async Task Invoke() =>
			await Simulate.Events().ClickChord(_keys).Invoke();
	}
}
