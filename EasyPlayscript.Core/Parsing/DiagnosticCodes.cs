namespace EasyPlayscript.Parsing;

public static class DiagnosticCodes
{
    public const string UnexpectedToken = "SCPT002";
    public const string MismatchedInput = "SCPT003";
    public const string DuplicateScriptName = "SCPT004";
    public const string UndeclaredConsumerCall = "SCPT005";
    public const string DuplicateInterfaceSignature = "SCPT006";
    public const string ArgumentTypeMismatch = "SCPT007";
    public const string ArgumentCountMismatch = "SCPT008";
    public const string MissingImplementation = "SCPT009";
    public const string DuplicateImplementation = "SCPT010";
    public const string UnusedImplementation = "SCPT011";

    public const string UnexpectedTokenFormat = "{0}";
    public const string MismatchedInputFormat = "{0}";
    public const string DuplicateScriptNameFormat = "Duplicate {0} name \"{1}\"";
    public const string UndeclaredConsumerCallFormat = "Consumer call \"{0}\" is not declared in any interface";
    public const string DuplicateInterfaceSignatureFormat = "Duplicate interface signature \"{0}\"";
    public const string ArgumentTypeMismatchFormat = "Argument {0} of \"{1}\": cannot convert from {2} to {3}{4}";
    public const string ArgumentCountMismatchFormat = "\"{0}\" does not match any overload with {1} argument(s){2}";
    public const string MissingImplementationFormat = "Interface \"{0}\" has no [Implementation] method";
    public const string DuplicateImplementationFormat = "Duplicate [Implementation] for \"{0}\" with {1} parameter(s) in {2}";
    public const string UnusedImplementationFormat = "[Implementation] method \"{0}.{1}\" is not used by any playscript";
}
