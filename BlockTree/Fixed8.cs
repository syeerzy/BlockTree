using BlockTree.IO;
using System;
using System.Globalization;
using System.IO;

namespace BlockTree
{
    /// <summary>
    /// Accurate to 10^-10 64-bit fixed-point numbers minimize rounding errors.
    /// By controlling the accuracy of the multiplier, rounding errors can be completely eliminated.
    /// </summary>
    public struct Fixed10 : IComparable<Fixed10>, IEquatable<Fixed10>, IFormattable, ISerializable
    {
        private const long D = 10_000_000_000;
        internal long value;

        public static readonly Fixed10 MaxValue = new Fixed10 { value = long.MaxValue };

        public static readonly Fixed10 MinValue = new Fixed10 { value = long.MinValue };

        public static readonly Fixed10 One = new Fixed10 { value = D };

        public static readonly Fixed10 Satoshi = new Fixed10 { value = 1 };

        public static readonly Fixed10 Zero = default(Fixed10);

        public int Size => sizeof(long);

        public Fixed10(long data)
        {
            this.value = data;
        }

        public Fixed10 Abs()
        {
            if (value >= 0) return this;
            return new Fixed10
            {
                value = -value
            };
        }

        public Fixed10 Ceiling()
        {
            long remainder = value % D;
            if (remainder == 0) return this;
            if (remainder > 0)
                return new Fixed10
                {
                    value = value - remainder + D
                };
            else
                return new Fixed10
                {
                    value = value - remainder
                };
        }

        public int CompareTo(Fixed10 other)
        {
            return value.CompareTo(other.value);
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            value = reader.ReadInt64();
        }

        public bool Equals(Fixed10 other)
        {
            return value.Equals(other.value);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Fixed10)) return false;
            return Equals((Fixed10)obj);
        }

        public static Fixed10 FromDecimal(decimal value)
        {
            value *= D;
            if (value < long.MinValue || value > long.MaxValue)
                throw new OverflowException();
            return new Fixed10
            {
                value = (long)value
            };
        }

        public long GetData() => value;

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static Fixed10 Max(Fixed10 first, params Fixed10[] others)
        {
            foreach (Fixed10 other in others)
            {
                if (first.CompareTo(other) < 0)
                    first = other;
            }
            return first;
        }

        public static Fixed10 Min(Fixed10 first, params Fixed10[] others)
        {
            foreach (Fixed10 other in others)
            {
                if (first.CompareTo(other) > 0)
                    first = other;
            }
            return first;
        }

        public static Fixed10 Parse(string s)
        {
            return FromDecimal(decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override string ToString()
        {
            return ((decimal)this).ToString(CultureInfo.InvariantCulture);
        }

        public string ToString(string format)
        {
            return ((decimal)this).ToString(format);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ((decimal)this).ToString(format, formatProvider);
        }

        public static bool TryParse(string s, out Fixed10 result)
        {
            decimal d;
            if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                result = default(Fixed10);
                return false;
            }
            d *= D;
            if (d < long.MinValue || d > long.MaxValue)
            {
                result = default(Fixed10);
                return false;
            }
            result = new Fixed10
            {
                value = (long)d
            };
            return true;
        }

        public static explicit operator decimal(Fixed10 value)
        {
            return value.value / (decimal)D;
        }

        public static explicit operator long(Fixed10 value)
        {
            return value.value / D;
        }

        public static bool operator ==(Fixed10 x, Fixed10 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Fixed10 x, Fixed10 y)
        {
            return !x.Equals(y);
        }

        public static bool operator >(Fixed10 x, Fixed10 y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator <(Fixed10 x, Fixed10 y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >=(Fixed10 x, Fixed10 y)
        {
            return x.CompareTo(y) >= 0;
        }

        public static bool operator <=(Fixed10 x, Fixed10 y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static Fixed10 operator *(Fixed10 x, Fixed10 y)
        {
            const ulong QUO = (1ul << 63) / (D >> 1);
            const ulong REM = ((1ul << 63) % (D >> 1)) << 1;
            int sign = Math.Sign(x.value) * Math.Sign(y.value);
            ulong ux = (ulong)Math.Abs(x.value);
            ulong uy = (ulong)Math.Abs(y.value);
            ulong xh = ux >> 32;
            ulong xl = ux & 0x00000000fffffffful;
            ulong yh = uy >> 32;
            ulong yl = uy & 0x00000000fffffffful;
            ulong rh = xh * yh;
            ulong rm = xh * yl + xl * yh;
            ulong rl = xl * yl;
            ulong rmh = rm >> 32;
            ulong rml = rm << 32;
            rh += rmh;
            rl += rml;
            if (rl < rml)
                ++rh;
            if (rh >= D)
                throw new OverflowException();
            ulong rd = rh * REM + rl;
            if (rd < rl)
                ++rh;
            ulong r = rh * QUO + rd / D;
            x.value = (long)r * sign;
            return x;
        }

        public static Fixed10 operator *(Fixed10 x, long y)
        {
            x.value *= y;
            return x;
        }

        public static Fixed10 operator /(Fixed10 x, long y)
        {
            x.value /= y;
            return x;
        }

        public static Fixed10 operator +(Fixed10 x, Fixed10 y)
        {
            x.value = checked(x.value + y.value);
            return x;
        }

        public static Fixed10 operator -(Fixed10 x, Fixed10 y)
        {
            x.value = checked(x.value - y.value);
            return x;
        }

        public static Fixed10 operator -(Fixed10 value)
        {
            value.value = -value.value;
            return value;
        }
    }
}
