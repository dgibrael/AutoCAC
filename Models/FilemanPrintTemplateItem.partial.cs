using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using AutoCAC.Extensions;

namespace AutoCAC.Models;

public enum FmDataTypeEnum
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
    public FmDataTypeEnum FmDataTypeEnum
    {
        get
        {
            if (Enum.TryParse<FmDataTypeEnum>(DataType, ignoreCase: true, out var dt))
                return dt;

            // pick a safe default if DB contains unexpected value
            return FmDataTypeEnum.STR;
        }
        set => DataType = value.ToString();
    }

    [NotMapped]
    public bool NoColumnName => FmDataTypeEnum is (FmDataTypeEnum.POINTERCHILD or FmDataTypeEnum.PROMPTANSWER or FmDataTypeEnum.CHARONLY);
    public bool StandardField => FmDataTypeEnum is (FmDataTypeEnum.STR or FmDataTypeEnum.INT or FmDataTypeEnum.FLOAT or FmDataTypeEnum.DATE or FmDataTypeEnum.DATETIME);

    [NotMapped]
    public string ColumnNameFormatted
    {
        get
        {
            if (NoColumnName) return null;

            var disp = FmDataTypeEnum switch
            {
                FmDataTypeEnum.STR or FmDataTypeEnum.POINTERPARENT => ColumnName,
                _ => $"<{FmDataTypeEnum}>{ColumnName}"
            };

            var endStr = FmDataTypeEnum switch
            {
                FmDataTypeEnum.SUBFILE => "[",
                _ => "_$C(31)",
            };

            return $"$C(31)_\"{disp}\"_$C(31)_\":\"{endStr};X";
        }
    }

    [NotMapped]
    public string FieldFormatted => FmDataTypeEnum switch
    {
        FmDataTypeEnum.SUBFILE or FmDataTypeEnum.POINTERPARENT => $"{Field}:;X",
        FmDataTypeEnum.PROMPTANSWER => Field,
        FmDataTypeEnum.CHARONLY => $"\"{Field}\";X",
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

            if (FmDataTypeEnum == FmDataTypeEnum.POINTERCHILD)
                sb.AppendLine("");

            if (FmDataTypeEnum is not (FmDataTypeEnum.POINTERPARENT or FmDataTypeEnum.PROMPTANSWER or FmDataTypeEnum.CHARONLY))
                sb.AppendLine("$C(31);X");

            return sb.ToString();
        }
    }

    public void DataTypeChange()
    {
        // Old: if (value == PROMPTANSWER) Field = "YES";
        //      else if (value == CHARONLY) Field = ",";
        if (FmDataTypeEnum == FmDataTypeEnum.PROMPTANSWER)
            Field = "YES";
        else if (FmDataTypeEnum == FmDataTypeEnum.CHARONLY)
            Field = ",";
        if (NoColumnName) ColumnName = "";
    }

    public void FieldChange()
    {
        if (!NoColumnName)
            ColumnName = Field.ToPascalCase();
    }

}
