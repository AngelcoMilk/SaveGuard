namespace System.Runtime.CompilerServices;

// Polyfill for netstandard2.1 which doesn't include IsExternalInit.
// Required for `init` property accessors with LangVersion >= 9.
internal static class IsExternalInit { }
