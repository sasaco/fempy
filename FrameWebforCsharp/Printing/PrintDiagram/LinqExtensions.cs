using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    static class LinqExtensions
    {
        /// <summary>
        /// 要素を <paramref name="chunkSize"/> 個ずつにまとめる
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
        {
            if (chunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));

            while (source.Any())
            {
                yield return source.Take(chunkSize);
                source = source.Skip(chunkSize);
            }
        }

        /// <summary>
        /// Projects each element of a sequence into a tuple that includes the previous
        /// and the next element.
        /// </summary>
        /// <see cref="https://stackoverflow.com/questions/8759849/get-previous-and-next-item-in-a-ienumerable-using-linq"/>
        public static IEnumerable<(T Previous, T Current, T Next)> WithPreviousAndNext<T>(
            this IEnumerable<T> source, T firstPrevious = default, T lastNext = default)
        {
            //ArgumentNullException.ThrowIfNull(source);
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            (T Previous, T Current, bool HasPrevious) queue = (default, firstPrevious, false);
            foreach (var item in source)
            {
                if (queue.HasPrevious)
                    yield return (queue.Previous, queue.Current, item);
                queue = (queue.Current, item, true);
            }
            if (queue.HasPrevious)
                yield return (queue.Previous, queue.Current, lastNext);
        }

        /// <summary>
        /// <paramref name="source"/> が空ではない場合はLINQのMax()を実行した結果を返し、空の場合は <paramref name="Default"/> を返す
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        public static TResult MaxOrDefault<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult Default = default)
            => source.Any() ? source.Max(selector) : Default;

        /// <summary>
        /// シーケンスを <paramref name="predicate"/> が真となる要素から始まる複数のシーケンスに分割します
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> source, Predicate<T> predicate)
        {
            while (source.Any())
            {
                var chunk = source.Take(1).Concat(source.Skip(1).TakeWhile(s => !predicate(s)));
                yield return chunk;
                source = source.Skip(chunk.Count());
            }
        }
    }
}
