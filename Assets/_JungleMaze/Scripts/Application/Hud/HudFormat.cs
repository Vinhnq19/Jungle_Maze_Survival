using System;

namespace JungleMaze.App.Hud
{
    /// <summary>
    /// Allocation-free HUD number formatting into caller-owned char buffers (the view then calls
    /// TMP_Text.SetCharArray). Every method writes from index 0 and returns the length written.
    /// Rounding of durability, adrenaline seconds and zone time is "round up" (G-13), so 0 only shows when empty.
    /// </summary>
    public static class HudFormat
    {
        /// <summary>Buffer size large enough for every format here.</summary>
        public const int BufferSize = 24;
        /// <summary>Largest value shown by <see cref="MinutesSeconds"/>: 99:59.</summary>
        public const int MaxClockSeconds = 99 * 60 + 59;
        private const float CeilEpsilon = 1e-4f;

        /// <summary>Rounds up (G-13) with a tiny tolerance for float noise; NaN = 0; saturates at int range.</summary>
        public static int CeilToInt(float value)
        {
            if (float.IsNaN(value)) return 0;
            var ceiled = Math.Ceiling(value - CeilEpsilon);
            if (ceiled >= int.MaxValue) return int.MaxValue;
            if (ceiled <= int.MinValue) return int.MinValue;
            return (int)ceiled;
        }

        /// <summary>"72".</summary>
        public static int Integer(char[] buffer, int value) => WriteInt(buffer, 0, value);

        /// <summary>"/100" (no spaces), used next to the HP number.</summary>
        public static int SlashInteger(char[] buffer, int value)
        {
            buffer[0] = '/';
            return WriteInt(buffer, 1, value);
        }

        /// <summary>"/ 30", used for reserve ammo.</summary>
        public static int SlashSpaceInteger(char[] buffer, int value)
        {
            buffer[0] = '/';
            buffer[1] = ' ';
            return WriteInt(buffer, 2, value);
        }

        /// <summary>"7/8".</summary>
        public static int Fraction(char[] buffer, int numerator, int denominator)
        {
            var index = WriteInt(buffer, 0, numerator);
            buffer[index++] = '/';
            return WriteInt(buffer, index, denominator);
        }

        /// <summary>"×6" for consumable counts.</summary>
        public static int Count(char[] buffer, int count)
        {
            buffer[0] = '×';
            return WriteInt(buffer, 1, count);
        }

        /// <summary>"5s": seconds rounded up (G-13); negative or NaN = "0s".</summary>
        public static int Seconds(char[] buffer, float seconds)
        {
            var value = CeilToInt(seconds);
            var index = WriteInt(buffer, 0, value < 0 ? 0 : value);
            buffer[index++] = 's';
            return index;
        }

        /// <summary>"01:24": seconds rounded up, negative/NaN = "00:00", capped at "99:59".</summary>
        public static int MinutesSeconds(char[] buffer, float seconds)
        {
            var total = CeilToInt(seconds);
            if (total < 0) total = 0;
            if (total > MaxClockSeconds) total = MaxClockSeconds;
            var minutes = total / 60;
            var rest = total % 60;
            buffer[0] = (char)('0' + minutes / 10);
            buffer[1] = (char)('0' + minutes % 10);
            buffer[2] = ':';
            buffer[3] = (char)('0' + rest / 10);
            buffer[4] = (char)('0' + rest % 10);
            return 5;
        }

        /// <summary>"--:--" before the zone countdown is known.</summary>
        public static int ClockPlaceholder(char[] buffer) => Copy(buffer, "--:--");

        /// <summary>"-/-" before the alive count is known.</summary>
        public static int FractionPlaceholder(char[] buffer) => Copy(buffer, "-/-");

        /// <summary>Writes a decimal integer at <paramref name="start"/>; returns the index after the last digit.</summary>
        public static int WriteInt(char[] buffer, int start, int value)
        {
            var index = start;
            long magnitude = value;
            if (magnitude < 0)
            {
                buffer[index++] = '-';
                magnitude = -magnitude;
            }
            var digitsStart = index;
            do
            {
                buffer[index++] = (char)('0' + (int)(magnitude % 10));
                magnitude /= 10;
            } while (magnitude > 0);
            // Digits were written least-significant first: reverse them in place.
            for (int i = digitsStart, j = index - 1; i < j; i++, j--)
            {
                var swap = buffer[i];
                buffer[i] = buffer[j];
                buffer[j] = swap;
            }
            return index;
        }

        private static int Copy(char[] buffer, string text)
        {
            for (var i = 0; i < text.Length; i++) buffer[i] = text[i];
            return text.Length;
        }
    }
}
