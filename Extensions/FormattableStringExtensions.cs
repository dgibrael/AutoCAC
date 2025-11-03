using Dapper;

namespace AutoCAC.Extensions
{
    public static class FormattableStringExtensions
    {
        public static (string Query, DynamicParameters Params) ToSqlAndParams(
            this FormattableString sql)
        {
            var args = sql.GetArguments();
            var cmdText = sql.Format;
            var dp = new DynamicParameters();

            for (var i = 0; i < args.Length; i++)
            {
                var name = "p" + i;
                cmdText = cmdText.Replace("{" + i + "}", "@" + name);
                dp.Add(name, args[i]);
            }

            return (cmdText, dp);
        }
    }

}
