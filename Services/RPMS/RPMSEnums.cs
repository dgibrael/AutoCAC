namespace AutoCAC.Services;

public enum RPMSTermKey { F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
public static class RPMSTermKeyExtensions
{
    extension(RPMSTermKey key)
    {
        public string Value => key switch
        {
            RPMSTermKey.F1 => "\x1BOP",
            RPMSTermKey.F2 => "\x1BOQ",
            RPMSTermKey.F3 => "\x1BOR",
            RPMSTermKey.F4 => "\x1BOS",
            RPMSTermKey.F5 => "\x1B[15~",
            RPMSTermKey.F6 => "\x1B[17~",
            RPMSTermKey.F7 => "\x1B[18~",
            RPMSTermKey.F8 => "\x1B[19~",
            RPMSTermKey.F9 => "\x1B[20~",
            RPMSTermKey.F10 => "\x1B[21~",
            RPMSTermKey.F11 => "\x1B[23~",
            RPMSTermKey.F12 => "\x1B[24~",
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }
}
public enum RPMSScrollFunc
{
    DeleteRow,
    Exit,
    ExitAndSave
}

public static class RPMSScrollFuncExtensions
{
    extension(RPMSScrollFunc func)
    {
        public string Value => func switch
        {
            RPMSScrollFunc.DeleteRow => "D",
            RPMSScrollFunc.Exit => "Q",
            RPMSScrollFunc.ExitAndSave => "E",
            _ => throw new ArgumentOutOfRangeException(nameof(func))
        };
    }
}

public enum RPMSMode
{
    Disconnected,
    DefaultInput,
    DefaultReceive,
    Access,
    Verify,
    ScrollWrite,
    Report,
    ReportPrompt
}

public static class RPMSModeExtensions
{
    extension(RPMSMode mode)
    {
        public bool SignedIn => mode switch
        {
            RPMSMode.Disconnected or RPMSMode.Access
            or RPMSMode.Verify => false,
            _ => true
        };
    }
}

public enum RPMSReport
{
    OrderDialog
}

public static class RPMSReportExtensions
{
    extension(RPMSReport value)
    {
        public string FullName => value switch
        {
            RPMSReport.OrderDialog => "Order Dialog Update App",
            _ => throw new Exception("menu option not found")
        };

    }
}