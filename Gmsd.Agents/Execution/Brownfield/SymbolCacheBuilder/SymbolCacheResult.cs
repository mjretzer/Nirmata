namespace Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;

/// <summary>
/// Result of a symbol cache build operation.
/// </summary>
public sealed class SymbolCacheResult
{
    /// <summary>
    /// Whether the build completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the build failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The root path of the scanned repository.
    /// </summary>
    public string RepositoryRoot { get; init; } = "";

    /// <summary>
    /// Timestamp when the cache was built.
    /// </summary>
    public DateTimeOffset BuildTimestamp { get; init; }

    /// <summary>
    /// All extracted symbols indexed by their unique identifier.
    /// </summary>
    public IReadOnlyList<SymbolInfo> Symbols { get; init; } = Array.Empty<SymbolInfo>();

    /// <summary>
    /// Statistics about the symbol cache.
    /// </summary>
    public SymbolCacheStatistics Statistics { get; init; } = new();
}

/// <summary>
/// Represents a code symbol (type, method, property, etc.) extracted from source.
/// </summary>
public sealed class SymbolInfo
{
    /// <summary>
    /// Unique identifier for the symbol (fully qualified name).
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// Simple name of the symbol.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Fully qualified name including namespace.
    /// </summary>
    public string FullName { get; init; } = "";

    /// <summary>
    /// Type of symbol (class, interface, method, property, field, enum, etc.).
    /// </summary>
    public SymbolType SymbolType { get; init; }

    /// <summary>
    /// Accessibility modifier (public, private, internal, protected, protected internal, private protected).
    /// </summary>
    public SymbolAccessibility Accessibility { get; init; }

    /// <summary>
    /// Namespace containing this symbol.
    /// </summary>
    public string Namespace { get; init; } = "";

    /// <summary>
    /// Parent symbol ID (for nested types, methods in classes, etc.).
    /// </summary>
    public string? ParentSymbolId { get; init; }

    /// <summary>
    /// Source location where this symbol is defined.
    /// </summary>
    public SymbolLocation Location { get; init; } = new();

    /// <summary>
    /// Documentation comments for this symbol (/// or /** style).
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// For methods: return type; for properties/fields: type; for type members: containing type info.
    /// </summary>
    public string? TypeSignature { get; init; }

    /// <summary>
    /// For methods: parameter list; for generic types: type parameters.
    /// </summary>
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } = Array.Empty<ParameterInfo>();

    /// <summary>
    /// Attributes applied to this symbol.
    /// </summary>
    public IReadOnlyList<string> Attributes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Cross-references: symbols referenced by this symbol.
    /// </summary>
    public IReadOnlyList<string> References { get; init; } = Array.Empty<string>();

    /// <summary>
    /// For types: interfaces implemented.
    /// </summary>
    public IReadOnlyList<string> ImplementedInterfaces { get; init; } = Array.Empty<string>();

    /// <summary>
    /// For types: base type if any.
    /// </summary>
    public string? BaseType { get; init; }

    /// <summary>
    /// Modifiers applied to this symbol (static, abstract, virtual, sealed, etc.).
    /// </summary>
    public IReadOnlyList<string> Modifiers { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents a symbol parameter (for methods, constructors, etc.).
/// </summary>
public sealed class ParameterInfo
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Parameter type name.
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// Whether this is an optional parameter.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Whether this is a params array parameter.
    /// </summary>
    public bool IsParams { get; init; }

    /// <summary>
    /// Whether this is a this/ref parameter.
    /// </summary>
    public bool IsThis { get; init; }

    /// <summary>
    /// Whether this is an out parameter.
    /// </summary>
    public bool IsOut { get; init; }

    /// <summary>
    /// Whether this is a ref parameter.
    /// </summary>
    public bool IsRef { get; init; }

    /// <summary>
    /// Default value if optional.
    /// </summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Represents a symbol's source location.
/// </summary>
public sealed class SymbolLocation
{
    /// <summary>
    /// Full path to the source file.
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>
    /// Relative path from repository root.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// 1-based line number where the symbol is declared.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// 1-based column number where the symbol name starts.
    /// </summary>
    public int ColumnNumber { get; init; }

    /// <summary>
    /// Line span (start and end lines) for multi-line declarations.
    /// </summary>
    public int EndLineNumber { get; init; }

    /// <summary>
    /// Character span in the source text.
    /// </summary>
    public int StartCharacter { get; init; }

    /// <summary>
    /// Character end position.
    /// </summary>
    public int EndCharacter { get; init; }
}

/// <summary>
/// Type of code symbol.
/// </summary>
public enum SymbolType
{
    Unknown,
    Class,
    Interface,
    Struct,
    Enum,
    Delegate,
    Record,
    Method,
    Constructor,
    Property,
    Field,
    Event,
    Indexer,
    Operator,
    ConversionOperator,
    Namespace,
    TypeParameter,
    LocalVariable,
    Parameter
}

/// <summary>
/// Symbol accessibility modifier.
/// </summary>
public enum SymbolAccessibility
{
    Unknown,
    Public,
    Private,
    Internal,
    Protected,
    ProtectedInternal,
    PrivateProtected
}

/// <summary>
/// Statistics about the symbol cache.
/// </summary>
public sealed class SymbolCacheStatistics
{
    /// <summary>
    /// Total number of symbols extracted.
    /// </summary>
    public int TotalSymbols { get; init; }

    /// <summary>
    /// Number of type symbols (classes, interfaces, structs, enums).
    /// </summary>
    public int TypeCount { get; init; }

    /// <summary>
    /// Number of method symbols.
    /// </summary>
    public int MethodCount { get; init; }

    /// <summary>
    /// Number of property symbols.
    /// </summary>
    public int PropertyCount { get; init; }

    /// <summary>
    /// Number of field symbols.
    /// </summary>
    public int FieldCount { get; init; }

    /// <summary>
    /// Number of source files scanned.
    /// </summary>
    public int SourceFileCount { get; init; }

    /// <summary>
    /// Number of cross-references detected.
    /// </summary>
    public int CrossReferenceCount { get; init; }

    /// <summary>
    /// Time taken to build the cache.
    /// </summary>
    public TimeSpan BuildDuration { get; init; }
}
