using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace AutoCAC.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsNumeric(this Type type)
        {
            if (type == null)
                return false;

            type = Nullable.GetUnderlyingType(type) ?? type;

            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte or
                TypeCode.SByte or
                TypeCode.Int16 or
                TypeCode.Int32 or
                TypeCode.Int64 or
                TypeCode.UInt16 or
                TypeCode.UInt32 or
                TypeCode.UInt64 or
                TypeCode.Single or
                TypeCode.Double or
                TypeCode.Decimal => true,

                _ => false
            };
        }
    }
}
