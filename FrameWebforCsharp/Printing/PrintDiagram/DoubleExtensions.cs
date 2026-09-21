using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PDF_Manager.Printing
{

    static class DoubleExtensions
    {
        /// <summary>
        /// double型の数値を文字列に変換する(整数や小数点以下1桁までの値（小数点以下2桁以降が0の値）の場合は小数第1位まで。それ以外の場合は、小数点以下3桁未満を四捨五入した後、小数点以下3桁目が'0'なら取り除く)
        /// </summary>
        /// <param name="value">変換対象の数値</param>
        /// <returns>変換後の文字列</returns>
        public static string ToStringF1(this double value)
        {
            var pvalue10 = Math.Abs(value) * 10; // VBAのInt関数とC#のdouble→int型変換とでは挙動が違うので絶対値を使う必要はないが、念のため
            return pvalue10 == (int)pvalue10 ? value.ToString("F1") : value.ToStringF2();
        }

        /// <summary>
        /// double型の数値を文字列に変換する(小数点以下3桁未満を四捨五入した後、小数点以下3桁目が'0'なら取り除く)
        /// </summary>
        /// <param name="value">変換対象の数値</param>
        /// <returns>変換後の文字列</returns>
        public static string ToStringF2(this double value) => Regex.Replace(value.ToString("F3"), "0$", "");

        /// <summary>
        /// double型数値の比較。差の絶対値が <paramref name="threshold"/> 以下なら等しいとみなす
        /// </summary>
        /// <param name="value1">比較対象の値</param>
        /// <param name="value2">比較対象の値</param>
        /// <param name="threshold">閾値</param>
        /// <returns>比較結果</returns>
        public static bool NearlyEqualTo(this double value1, double value2, double threshold = 1e-5) => Math.Abs(value1 - value2) <= threshold;

        /// <summary>
        /// double型数値のコレクションから重複(差の絶対値が <paramref name="threshold"/> 以下の場合に重複しているとみなす)を取り除く。
        /// 重複を取り除く際は、コレクションの先頭に近い方を残して遠い方を削除する
        /// </summary>
        /// <param name="values">double型数値のコレクション</param>
        /// <param name="threshold">閾値</param>
        /// <returns>重複を取り除いた数値のコレクション</returns>
        public static IEnumerable<double> DistinctLoosely(this IEnumerable<double> values, double threshold = 1e-5)
        {
            var list = values.ToList();
            for (var i = 0; i < list.Count - 1; i++)
            {
                for (var j = list.Count - 1; j > i; j--)
                {
                    if (NearlyEqualTo(list[i], list[j], threshold))
                    {
                        list.RemoveAt(j);
                    }
                }
            }
            return list.AsEnumerable();
        }
    }
}
