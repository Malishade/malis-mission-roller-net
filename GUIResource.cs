using AOSharp.Core.UI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

public enum Format_e : int
{
    Unknown = 0,
    Unk1 = 1,
    ARGB1555 = 2,
    Unk2 = 3,
}


[StructLayout(LayoutKind.Explicit)]
public unsafe struct SpriteInfo_t
{
    [FieldOffset(0x00)] public byte* PixelData;
    [FieldOffset(0x04)] public int Id;
    [FieldOffset(0x08)] public int Height;
    [FieldOffset(0x0C)] public int Width;
    [FieldOffset(0x10)] public int Unk10;
    [FieldOffset(0x14)] public int Unk14;
    [FieldOffset(0x18)] public float Unk18;
    [FieldOffset(0x1C)] public int Unk1C;
    [FieldOffset(0x20)] public int Stride;
    [FieldOffset(0x24)] public int Unk24;
    [FieldOffset(0x28)] public int Unk28;
    [FieldOffset(0x2C)] public int Unk2C;
    [FieldOffset(0x30)] public int Unk30;
    [FieldOffset(0x34)] public int Unk34;
    [FieldOffset(0x38)] public int Unk38;
    [FieldOffset(0x3C)] public int Unk3C;
    [FieldOffset(0x40)] public void* Unk40Ptr;
    [FieldOffset(0x44)] public int Unk44;
    [FieldOffset(0x48)] public int Unk48;
    [FieldOffset(0x4C)] public int Unk4C;
    [FieldOffset(0x50)] public void* SelfRef0;
    [FieldOffset(0x54)] public void* SelfRef1;
    [FieldOffset(0x58)] public void* SelfRef2;
    [FieldOffset(0x5C)] public int Unk5C;
    [FieldOffset(0x60)] public int Unk60;
    [FieldOffset(0x64)] public int Unk64;

    public static unsafe SpriteInfo_t* FromPointer(IntPtr ptr) => (SpriteInfo_t*)ptr.ToPointer();

    public unsafe Image<Rgba32> ToImage(bool skipFallback = true)
    {
        int width = Width;
        int height = Height;
        int total = width * height;
        int magentaCount = 0;

        var image = new Image<Rgba32>(width, height);

        for (int y = 0; y < height; y++)
        {
            ushort* row = (ushort*)((byte*)PixelData + y * Stride);
            for (int x = 0; x < width; x++)
            {
                ushort raw = row[x];

                int a = (raw >> 15) & 0x1;
                int r = (raw >> 10) & 0x1F;
                int g = (raw >> 5) & 0x1F;
                int b = raw & 0x1F;

                bool isMagenta = r > 25 && b > 25 && g < 5;
                if (isMagenta) magentaCount++;

                r = (r << 3) | (r >> 2);
                g = (g << 3) | (g >> 2);
                b = (b << 3) | (b >> 2);

                image[x, y] = a == 0
                    ? new Rgba32(0, 0, 0, 0)
                    : new Rgba32((byte)r, (byte)g, (byte)b, 255);
            }
        }

        if (skipFallback && magentaCount == total)
        {
            image.Dispose();
            return null;
        }

        return image;
    }

    public byte[] ToPngBytes(bool skipFallback = true)
    {
        using var image = ToImage(skipFallback);
        if (image == null) return null;
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public void SaveTo(string path, string textureId, bool skipFallback = true)
    {
        using var image = ToImage(skipFallback);
        if (image == null) return;
        image.SaveAsPng(Path.Combine(path, $"{textureId}.png"));
    }

    public override string ToString() => $"SpriteInfo_t {{ Id={Id}, W={Width}, H={Height}, Stride={Stride} }}";
}

public class GuiResourceManager_t
{
    [DllImport("Interfaces.dll", EntryPoint = "?GetInstance@GuiResourceManager_t@@SAPAV1@XZ", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetInstance();

    [DllImport("Interfaces.dll", EntryPoint = "?GetRDBTexture@GuiResourceManager_t@@QAEPAVSpriteInfo_t@@HW4Format_e@@@Z",
        CallingConvention = CallingConvention.ThisCall)]
    public static extern IntPtr GetRDBTexture(IntPtr thisPtr, int id, int format);
}

public class GuiResourceManager
{
    public static unsafe SpriteInfo_t* GetTexture(int id, Format_e format = Format_e.ARGB1555)
    {
        IntPtr instance = GuiResourceManager_t.GetInstance();
        IntPtr raw = GuiResourceManager_t.GetRDBTexture(instance, id, (int)format);

        if (raw == IntPtr.Zero)
        {
            Chat.WriteLine($"[GuiResourceManager] GetTexture({id}) returned null");
            return null;
        }

        return (SpriteInfo_t*)raw.ToPointer();
    }
}