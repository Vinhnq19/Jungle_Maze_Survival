using System.Linq;
using _Codebase_.Entity._Abstract_;
using UnityEngine;

namespace _Codebase_.Utils
{
    public static class RarityExtensions
    {
        public static int RandomIndexWithRarity<T>(this T[] items)
            where T : class, IHasRarity
        {
            var totalRarity = items.Where(item => item is not null).Sum(item => item.Rarity);

            var randomValue = Random.Range(0f, totalRarity);

            var cumulativeChance = 0f;

            for (var i = 0; i < items.Length; i++)
            {
                cumulativeChance += items[i].Rarity;

                if (randomValue < cumulativeChance)
                {
                    return i;
                }
            }

            return -1;
        }

        public static T RandomWithRarity<T>(this T[] items)
            where T : class, IHasRarity
        {
            var index = items.RandomIndexWithRarity();

            return index == -1 ? null : items[index];
        }
    }
}