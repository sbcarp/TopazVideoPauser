namespace TopazVideoPauser
{
	internal static class TaskExtension
	{
		internal static async Task<TV> Then<T, TV>(this Task<T> task, Func<T, TV> then)
		{
			var result = await task;
			return then(result);
		}
		internal static async Task Then<T>(this Task<T> task, Action<T> then)
		{
			var result = await task;
			then(result);
		}
	}
}
