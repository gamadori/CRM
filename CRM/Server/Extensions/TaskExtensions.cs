namespace CRM.Server.Extensions
{
    public static class TaskExtensions
    {
        public static async Task<IEnumerable<TResult>> SelectInSequenceAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> asyncSelector)
        {
            var result = new List<TResult>();
            foreach (var s in source)
            {
                result.Add(await asyncSelector(s));
            }

            return result;
        }
    }
}
