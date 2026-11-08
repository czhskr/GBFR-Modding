using PartIndicator;
using System.Runtime.InteropServices;

static unsafe class HeadAssetsTest
{
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint kind, uint protection);
    [DllImport("kernel32")] private static extern bool VirtualFree(nint address, nuint size, uint kind);
    public static void Run()
    {
        static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        foreach (var (bytes, expected) in new[] { ("B800563412C3", 0), ("B801563412C3", 1) })
        {
            using var code = new ExecutableBuffer(Convert.FromHexString(bytes));
            var status = Marshal.GetDelegateForFunctionPointer<NativeBindings.AtlasStatus>(code.Address);
            Check(status(1,20) == expected, "native resource readiness uses AL; upper return bits are unspecified");
        }
        string assets = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../assets"));
        Check(NativeHeadAssets.FilesValid(assets), "built dedicated head resources match their reviewed hashes");
        var temp = Directory.CreateTempSubdirectory("pbi-head-assets-");
        Check(Path.GetRelativePath(Path.GetTempPath(), temp.FullName).Split(Path.DirectorySeparatorChar)[0] != "..", "temporary test path escaped");
        try
        {
            Check(!NativeHeadAssets.FilesValid(temp.FullName), "missing head resources must disable the head request");
            foreach (string path in Directory.GetFiles(assets, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(temp.FullName, Path.GetRelativePath(assets, path));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(path, destination);
            }
            Check(NativeHeadAssets.FilesValid(temp.FullName), "head resource guard must accept a complete copied bundle");
            string metadata = Path.Combine(temp.FullName, "GBFR/data/ui/atlas/pbi_angra_head.tex.texb");
            using (var stream = File.OpenWrite(metadata)) stream.WriteByte(0);
            Check(!NativeHeadAssets.FilesValid(temp.FullName), "changed metadata must not reach the native file loader");
            File.Copy(Path.Combine(assets, "GBFR/data/ui/atlas/pbi_angra_head.tex.texb"), metadata, true);
            File.Delete(Path.Combine(temp.FullName, "GBFR/data/ui/fhd/atlas/pbi_angra_head.wtb"));
            Check(!NativeHeadAssets.FilesValid(temp.FullName), "missing FHD texture must not become a resolution-specific native failure");
        }
        finally { Directory.Delete(temp.FullName, true); }
        nint module = VirtualAlloc(0, 0x8000000, 0x2000, 4), manager = (nint)NativeMemory.AllocZeroed(0x940);
        try
        {
            Check(module != 0 && VirtualAlloc(module+0x7ab3000,0x1000,0x1000,4)!=0 &&
                VirtualAlloc(module+0x7c17000,0x1000,0x1000,4)!=0, "native request fixture allocation");
            nint context=0x12345000;
            *(nint*)(module+0x7ab3bf8)=manager; *(nint*)(module+0x7c17858)=context;
            *(uint*)(manager+0x8e8)=0x102; *(int*)(manager+0x930)=42; *(int*)(manager+0x934)=2;
            Check(NativeHeadAssets.CanRequest(module,context,0,42), "observed caller-owned recursive resource context accepted");
            Check(!NativeHeadAssets.CanRequest(module,context,1,42), "unlocked no-atlas request class must not trigger head loading");
            Check(!NativeHeadAssets.CanRequest(module,context+8,0,42), "foreign native context must not trigger head loading");
            Check(!NativeHeadAssets.CanRequest(module,context,0,43), "another thread's mutex cannot authorize the caller");
            *(int*)(manager+0x934)=0; Check(!NativeHeadAssets.CanRequest(module,context,0,42), "unowned native mutex rejected");
            *(int*)(manager+0x934)=2; *(uint*)(manager+0x8e8)=1;
            Check(!NativeHeadAssets.CanRequest(module,context,0,42), "unsupported mutex type rejected");
            *(uint*)(manager+0x8e8)=0x102; *(nint*)(module+0x7ab3bf8)=0;
            Check(!NativeHeadAssets.CanRequest(module,context,0,42), "missing native resource manager rejected");
            Check(!NativeHeadAssets.CanRequest(1,context,0,42), "unmapped native request context rejected");
        }
        finally { NativeMemory.Free((void*)manager); if(module!=0)VirtualFree(module,0,0x8000); }
        Console.WriteLine("PASS: dedicated head bundle and actual-memory request gate; corrupt/FHD-missing files,foreign context/thread,unowned/wrong mutex rejected");
    }
}
