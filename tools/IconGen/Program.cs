using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var winUiAssets = Path.Combine(root, "MirrorDeck.WinUI", "Assets");
var packageImages = Path.Combine(root, "MirrorDeck.Package", "Images");

var preferredSource = Path.Combine(winUiAssets, "MirrorDeck.Logo.png");
var fallbackSource = Path.Combine(winUiAssets, "MirrorDeckLogo.png");
var sourcePngPath = File.Exists(preferredSource) ? preferredSource : fallbackSource;

if (!File.Exists(sourcePngPath))
{
    throw new FileNotFoundException("No source PNG found. Expected MirrorDeck.Logo.png or MirrorDeckLogo.png in MirrorDeck.WinUI/Assets.", sourcePngPath);
}

var sourceBytes = File.ReadAllBytes(sourcePngPath);
using var sourceStream = new MemoryStream(sourceBytes);
using var sourceImage = (Bitmap)Image.FromStream(sourceStream);
using var normalizedSquare = NormalizeToSquare(sourceImage, 1024);
using var icon1024 = ResizeBitmap(normalizedSquare, 1024, 1024);

using var canonicalLogo = ResizeBitmap(normalizedSquare, 512, 512);

// Keep canonical app logo path updated from source PNG.
SavePng(canonicalLogo, Path.Combine(winUiAssets, "MirrorDeckLogo.png"), 512, 512);

SavePng(canonicalLogo, Path.Combine(packageImages, "Logo.png"), 150, 150);
SavePng(canonicalLogo, Path.Combine(packageImages, "SmallLogo.png"), 44, 44);
SavePng(canonicalLogo, Path.Combine(packageImages, "StoreLogo.png"), 50, 50);
// Keep Splash.png user-maintained: do not overwrite customized splash artwork.

var icoSizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var icoBitmaps = new List<Bitmap>();
try
{
    foreach (var size in icoSizes)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        SetupQuality(g);
        g.DrawImage(icon1024, 0, 0, size, size);
        icoBitmaps.Add(bmp);
    }

    SaveIco(icoBitmaps, Path.Combine(winUiAssets, "MirrorDeck.ico"));
}
finally
{
    foreach (var bmp in icoBitmaps)
    {
        bmp.Dispose();
    }
}

var embeddedPngBase64 = Convert.ToBase64String(BitmapToPngBytes(canonicalLogo));
var svg = $"""
<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 512 512' width='512' height='512'>
    <image href='data:image/png;base64,{embeddedPngBase64}' width='512' height='512' preserveAspectRatio='xMidYMid meet' />
</svg>
""";
File.WriteAllText(Path.Combine(winUiAssets, "MirrorDeckLogo.svg"), svg);

Console.WriteLine("MirrorDeck icon assets generated.");

static void SavePng(Bitmap source, string path, int width, int height)
{
    using var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(target);
    SetupQuality(g);
    g.Clear(Color.Transparent);
    g.DrawImage(source, 0, 0, width, height);
    target.Save(path, ImageFormat.Png);
}

static Bitmap ResizeBitmap(Image source, int width, int height)
{
    var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(target);
    SetupQuality(g);
    g.Clear(Color.Transparent);
    g.DrawImage(source, 0, 0, width, height);
    return target;
}

static Bitmap NormalizeToSquare(Image source, int size)
{
    var min = Math.Min(source.Width, source.Height);
    var srcX = (source.Width - min) / 2;
    var srcY = (source.Height - min) / 2;

    var target = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(target);
    SetupQuality(g);
    g.Clear(Color.Transparent);
    g.DrawImage(
        source,
        new Rectangle(0, 0, size, size),
        new Rectangle(srcX, srcY, min, min),
        GraphicsUnit.Pixel);

    return target;
}

static byte[] BitmapToPngBytes(Bitmap source)
{
    using var ms = new MemoryStream();
    source.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static void SaveIco(IReadOnlyList<Bitmap> bitmaps, string path)
{
    using var fs = File.Open(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)bitmaps.Count);

    var pngData = new List<byte[]>();
    foreach (var bmp in bitmaps)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        pngData.Add(ms.ToArray());
    }

    var offset = 6 + (16 * bitmaps.Count);
    for (var i = 0; i < bitmaps.Count; i++)
    {
        var bmp = bitmaps[i];
        var data = pngData[i];

        bw.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));
        bw.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write((uint)data.Length);
        bw.Write((uint)offset);
        offset += data.Length;
    }

    foreach (var data in pngData)
    {
        bw.Write(data);
    }
}

static void SetupQuality(Graphics g)
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.CompositingQuality = CompositingQuality.HighQuality;
}
