using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using AutoCAC.Extensions;

namespace AutoCAC.Models;

public enum DataTypeEnum
{
    STR,
    INT,
    FLOAT,
    DATE,
    DATETIME,
    SUBFILE,
    POINTERPARENT,
    POINTERCHILD,
    PROMPTANSWER,
    CHARONLY
}

public partial class FilemanPrintTemplateItem
{
    // Typed view over the string column.
    [NotMapped]
    public DataTypeEnum DataTypeEnum
    {
        get
        {
            if (Enum.TryParse<DataTypeEnum>(DataType, ignoreCase: true, out var dt))
                return dt;

            // pick a safe default if DB contains unexpected value
            return DataTypeEnum.STR;
        }
        set => DataType = value.ToString();
    }

    [NotMapped]
    public bool NoColumnName => DataTypeEnum is (DataTypeEnum.POINTERCHILD or DataTypeEnum.PROMPTANSWER or DataTypeEnum.CHARONLY);
    public bool StandardField => DataTypeEnum is (DataTypeEnum.STR or DataTypeEnum.INT or DataTypeEnum.FLOAT or DataTypeEnum.DATE or DataTypeEnum.DATETIME);

    [NotMapped]
    public string ColumnNameFormatted
    {
        get
        {
            if (NoColumnName) return null;

            var disp = DataTypeEnum switch
            {
                DataTypeEnum.STR or DataTypeEnum.POINTERPARENT => ColumnName,
                _ => $"<{DataTypeEnum}>{ColumnName}"
            };

            var endStr = DataTypeEnum switch
            {
                DataTypeEnum.SUBFILE => "[",
                _ => "_$C(31)",
            };

            return $"$C(31)_\"{disp}\"_$C(31)_\":\"{endStr};X";
        }
    }

    [NotMapped]
    public string FieldFormatted => DataTypeEnum switch
    {
        DataTypeEnum.SUBFILE or DataTypeEnum.POINTERPARENT => $"{Field}:;X",
        DataTypeEnum.PROMPTANSWER => Field,
        DataTypeEnum.CHARONLY => $"\"{Field}\";X",
        _ => $"{Field};X"
    };

    [NotMapped]
    public string FullStr
    {
        get
        {
            var sb = new StringBuilder();
            if (!NoColumnName) sb.AppendLine(ColumnNameFormatted);
            sb.AppendLine(FieldFormatted);

            if (DataTypeEnum == DataTypeEnum.POINTERCHILD)
                sb.AppendLine("");

            if (DataTypeEnum is not (DataTypeEnum.POINTERPARENT or DataTypeEnum.PROMPTANSWER or DataTypeEnum.CHARONLY))
                sb.AppendLine("$C(31);X");

            return sb.ToString();
        }
    }

    public void DataTypeChange()
    {
        // Old: if (value == PROMPTANSWER) Field = "YES";
        //      else if (value == CHARONLY) Field = ",";
        if (DataTypeEnum == DataTypeEnum.PROMPTANSWER)
            Field = "YES";
        else if (DataTypeEnum == DataTypeEnum.CHARONLY)
            Field = ",";
        if (NoColumnName) ColumnName = "";
    }

    public void FieldChange()
    {
        if (!NoColumnName)
            ColumnName = Field.ToPascalCase();
    }

}
