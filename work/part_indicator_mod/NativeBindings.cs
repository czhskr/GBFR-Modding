using System.Runtime.InteropServices;
namespace PartIndicator;
public sealed class NativeBindings
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void V1(nint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void V2(nint a,nint b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void V3(nint a,nint b,nint c);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void V4(nint a,nint b,nint c,nint d);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate nint P1(nint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate nint P2(nint a,nint b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte B1(nint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int I1(nint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate nint P0();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte AtlasRequest(nint context,nint path,uint hash,byte flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte AtlasStatus(nint path,nuint length);
    public static readonly IReadOnlyDictionary<string,(int Rva,string Prefix)> Specs = new Dictionary<string,(int,string)>
    {
        ["object"] = (0x26181c0, "564883ec204889ce8b050a909f046548"),
        ["canvas"] = (0xbdf3a0, "555657534883ec78488d6c247048c745"),
        ["insert"] = (0x2520180, "5541574156415541545657534883ec48"),
        ["delete"] = (0x261cd70, "5541574156415541545657534883ec38"),
        ["releaseObject"] = (0x2618380, "5657534883ec20488b324885f6747549"),
        ["dtor"] = (0x2135180, "554157415641545657534883ec40488d"),
        ["canvasDelete"] = (0xbdf5e0, "4c89c04c8b024d85c00f84f300000048"),
        ["service"] = (0x1cbd6f, "b960000000e87784540448b901000000"),
        ["ui"] = (0x2136fa0, "554157415641554154565753b8081400"),
        ["render"] = (0x1a97d0, "554157415641554154565753b8281700"),
        ["resource"] = (0x8d78d0, "5541574156415541545657534881ecf8"),
        ["try"] = (0x47142b0, "4883ec38488364242000488d54242083"),
        ["unlock"] = (0x47142d0, "4883ec2883694c01750e834948ff4883"),
        ["image"] = (0x3340dc0, "564883ec204889d68b050a04cd036548"),
        ["attach"] = (0x2618da0, "5541574156415541545657534883ec48"),
        ["imageInit"] = (0x3345d60, "415741565657534883ec304889cee8bd"),
        ["imageDtor"] = (0x3342db0, "55564883ec28488d6c242048c74500fe"),
        ["imageRelease"] = (0x3342cf0, "5657534883ec20488b324885f6747549"),
        ["imageRender"] = (0x3342fb0, "5657534881ecc0000000c5f829b424b0"),
        ["parent"] = (0x2618aa0, "41574156415541545657534883ec2048"),
        ["activeSetter"] = (0x261a620, "55415741565657534883ec38488d6c24"),
        ["imageCommand"] = (0x3345060, "554157415641545657534881ecb00000"),
        ["commandConsumer"] = (0x43ef740, "554157415641554154565753b8781600"),
        ["controller"] = (0x26939d0, "5657534883ec40c5f8297424304889ce"),
        ["atlasRequest"] = (0x25374a0, "5541574156415541545657534881ec88"),
        ["atlasStatus"] = (0x2539460, "55564883ec38488d6c243048c74500fe"),
    };
    public nint ModuleBase { get; }
    public nint At(string name) => ModuleBase + Specs[name].Rva;
    public readonly V1 CreateObject, Unlock;
    public readonly V2 CreateCanvas, ParentLink, SetActive, Delete, AttachImage;
    public readonly V3 InsertOwnerRelease, ReleaseImage;
    public readonly V4 Insert;
    public readonly P2 CreateImage;
    public readonly B1 InitImage;
    public readonly I1 TryLock;
    public readonly AtlasStatus GetAtlasStatus;
    private readonly ExecutableBuffer _tlsCode;
    public readonly P0 TlsArray;
    public NativeBindings(nint moduleBase)
    {
        ModuleBase=moduleBase;
        foreach(var (name,(rva,prefix)) in Specs)
        {
            var bytes=Convert.FromHexString(prefix);
            for(int i=0;i<bytes.Length;i++)
                if(ProcessMemory.Read<byte>(moduleBase+rva+i)!=bytes[i]) throw new IOException("Native prefix mismatch: "+name);
        }
        CreateObject=Bind<V1>("object"); CreateCanvas=Bind<V2>("canvas");
        ParentLink=Bind<V2>("parent"); SetActive=Bind<V2>("activeSetter"); Delete=Bind<V2>("delete");
        CreateImage=Bind<P2>("image"); AttachImage=Bind<V2>("attach"); InitImage=Bind<B1>("imageInit");
        Insert=Bind<V4>("insert"); InsertOwnerRelease=Bind<V3>("releaseObject"); ReleaseImage=Bind<V3>("imageRelease");
        TryLock=Bind<I1>("try"); Unlock=Bind<V1>("unlock");
        GetAtlasStatus=Bind<AtlasStatus>("atlasStatus");
        _tlsCode=new ExecutableBuffer(Convert.FromHexString("65488B042558000000C3"));
        TlsArray=Marshal.GetDelegateForFunctionPointer<P0>(_tlsCode.Address);
    }
    private T Bind<T>(string name) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(At(name));
}
