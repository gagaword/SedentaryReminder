using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// 一次性图标生成器：渲染多分辨率「起身伸展的小人」徽章并打包为 .ico
// 用法: dotnet run --project tools/iconrgen

string outPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "app.ico");
outPath = Path.GetFullPath(outPath);

int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var frames = new List<byte[]>();
foreach (var s in sizes)
{
    using var bmp = RenderIcon(s);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    frames.Add(ms.ToArray());
}
WriteIco(outPath, sizes, frames);
Console.WriteLine($"Wrote {outPath} ({sizes.Length} sizes).");

// 预览图（仅用于人工查看，可删）
using (var preview = RenderIcon(256))
{
    var pv = Path.Combine(Path.GetDirectoryName(outPath)!, "app_preview.png");
    preview.Save(pv, ImageFormat.Png);
    Console.WriteLine($"Preview: {pv}");
}

static Bitmap RenderIcon(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    // 统一在 256 逻辑画布上绘制，再缩放到目标尺寸
    float k = size / 256f;
    g.ScaleTransform(k, k);

    // 圆角方形背景 + 青绿竖向渐变
    var rect = new RectangleF(8, 8, 240, 240);
    using (var path = RoundedRect(rect, 56))
    using (var bg = new LinearGradientBrush(
        new PointF(0, 8), new PointF(0, 248),
        Color.FromArgb(0x3D, 0xDC, 0x97), Color.FromArgb(0x0E, 0x9E, 0x83)))
    {
        g.FillPath(bg, path);
    }

    // 白色「伸展的小人」
    using var white = new SolidBrush(Color.White);
    using var limb = new Pen(Color.White, 24f)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round,
    };

    // 头
    float hr = 23f;
    g.FillEllipse(white, 128 - hr, 58 - hr, hr * 2, hr * 2);
    // 躯干
    using var torso = new Pen(Color.White, 28f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
    g.DrawLine(torso, 128, 96, 128, 166);
    // 高举的手臂（V 形伸展）
    g.DrawLine(limb, 128, 112, 84, 72);
    g.DrawLine(limb, 128, 112, 172, 72);
    // 双腿
    g.DrawLine(limb, 128, 166, 98, 210);
    g.DrawLine(limb, 128, 166, 158, 210);

    return bmp;
}

static GraphicsPath RoundedRect(RectangleF r, float radius)
{
    float d = radius * 2f;
    var p = new GraphicsPath();
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

static void WriteIco(string path, int[] sizes, List<byte[]> frames)
{
    using var fs = new FileStream(path, FileMode.Create);
    using var w = new BinaryWriter(fs);
    w.Write((short)0);              // reserved
    w.Write((short)1);              // type = icon
    w.Write((short)sizes.Length);   // image count

    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int s = sizes[i];
        w.Write((byte)(s >= 256 ? 0 : s)); // width  (0 = 256)
        w.Write((byte)(s >= 256 ? 0 : s)); // height (0 = 256)
        w.Write((byte)0);                  // palette
        w.Write((byte)0);                  // reserved
        w.Write((short)1);                 // color planes
        w.Write((short)32);                // bits per pixel
        w.Write(frames[i].Length);         // bytes in resource
        w.Write(offset);                   // image offset
        offset += frames[i].Length;
    }
    foreach (var f in frames) w.Write(f);
}
