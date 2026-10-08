#nullable disable
// Generated game assemblies contain stripped compiler attributes. Supply CLR versions
// so closure metadata referencing SyncCore's annotated delegates can be emitted.
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
internal sealed class NullableAttribute : Attribute
{
    public readonly byte[] NullableFlags;
    public NullableAttribute(byte flag) => NullableFlags = new[] { flag };
    public NullableAttribute(byte[] flags) => NullableFlags = flags;
}
