using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Journal;

/// <summary>
/// Win32 clipboard paste for Ctrl+V file/image paste, used because
/// Avalonia 11.3's IClipboard has no GetFilesAsync.
/// Fixed 2026-10-27 (v1.0.1) — why the old code did nothing on the
/// user's PC:
///   1. The old paste path was deferred (Task.Run + Dispatcher invoke)
///      and routed through Window.OnKeyDown, which the focused
///      TextBox's own Ctrl+V handling can swallow first, so the
///      window handler never ran for a focused input.
///   2. CF_DIB handling rejected DIBV5 screenshots (124-byte header),
///      which is what many screenshot tools actually put on the
///      clipboard, so a pasted screenshot produced no file.
///   3. OpenClipboard was tried once only; if another app briefly
///      held the clipboard open it failed silently.
/// The fix: paste is checked SYNCHRONOUSLY on the UI thread, in a
/// window-level KeyDown handler registered with handledEventsToo,
/// so it runs even when the TextBox handled Ctrl+V first. If the
/// clipboard holds files/images they are attached and the key event
/// is marked handled (so the TextBox does not also paste text);
/// if it holds text only, the event is left unhandled so the
/// TextBox pastes text normally. No credential is involved here.
/// </summary>
public static class ClipboardFiles
{
    private const uint CF_HDROP = 15;
    private const uint CF_DIB = 8;
    private const uint CF_DIBV5 = 17;

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr hMem);

    /// <summary>True when the clipboard currently holds copied files
    /// (Explorer) or a copied image. Synchronous, UI-thread only.</summary>
    public static bool HasFileOrImage()
    {
        try
        {
            if (!TryOpenClipboard()) return false;
            try
            {
                return IsClipboardFormatAvailable(CF_HDROP)
                    || IsClipboardFormatAvailable(CF_DIB)
                    || IsClipboardFormatAvailable(CF_DIBV5);
            }
            finally { CloseClipboard(); }
        }
        catch { return false; }
    }

    public static List<string> TryGetPastedFiles()
    {
        var result = new List<string>();
        try
        {
            if (!TryOpenClipboard()) return result;
            try
            {
                if (IsClipboardFormatAvailable(CF_HDROP))
                {
                    var hDrop = GetClipboardData(CF_HDROP);
                    if (hDrop != IntPtr.Zero)
                    {
                        var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                        for (uint i = 0; i < count; i++)
                        {
                            var sb = new StringBuilder(1024);
                            if (DragQueryFile(hDrop, i, sb, (uint)sb.Capacity) > 0)
                                result.Add(sb.ToString());
                        }
                    }
                }
                else if (IsClipboardFormatAvailable(CF_DIB) || IsClipboardFormatAvailable(CF_DIBV5))
                {
                    var format = IsClipboardFormatAvailable(CF_DIB) ? CF_DIB : CF_DIBV5;
                    var hDib = GetClipboardData(format);
                    if (hDib != IntPtr.Zero)
                    {
                        var ptr = GlobalLock(hDib);
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                var size = (int)GlobalSize(hDib);
                                if (size > 0)
                                {
                                    var dib = new byte[size];
                                    Marshal.Copy(ptr, dib, 0, size);
                                    var png = WritePng(dib);
                                    if (png != null) result.Add(png);
                                }
                            }
                            finally { GlobalUnlock(hDib); }
                        }
                    }
                }
            }
            finally { CloseClipboard(); }
        }
        catch { }
        return result;
    }

    private static bool TryOpenClipboard()
    {
        // Another app (or the TextBox's own paste) can briefly hold
        // the clipboard open; retry instead of failing silently once.
        for (var i = 0; i < 5; i++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    // v1.0.3: pasted screenshots are converted to PNG on paste.
    // Why: v1.0.2 wrote the clipboard DIB out as a .bmp file
    // (pasted-image.bmp). That BMP — especially a DIBV5 screenshot
    // with a 124-byte header — did not reliably decode into an
    // Avalonia Bitmap, so the file chip fell back to a generic icon
    // and showed no photo thumbnail, while the other app (which
    // encodes the same paste as PNG) did show one. PNG is decoded
    // reliably by Avalonia, by the chip thumbnail, by PreviewWindow
    // and by Notion (image/png). This encoder is dependency-free:
    // it decodes the DIB pixels itself and writes a standard PNG
    // (8-bit RGB, filter 0, zlib-compressed) with a small CRC32.
    // Alpha is dropped on purpose: 32bpp clipboard bitmaps often
    // carry alpha 0 for opaque pixels, which would make a PNG
    // thumbnail look blank/transparent.
    private static string? WritePng(byte[] dib)
    {
        try
        {
            if (dib.Length < 40) return null;
            var headerSize = BitConverter.ToInt32(dib, 0);
            if (headerSize < 40 || headerSize > dib.Length) return null;
            var width = BitConverter.ToInt32(dib, 4);
            var heightRaw = BitConverter.ToInt32(dib, 8);
            var bitCount = BitConverter.ToUInt16(dib, 14);
            var compression = BitConverter.ToUInt32(dib, 16);
            if (width <= 0 || width > 16384 || heightRaw == 0 || Math.Abs(heightRaw) > 16384) return null;
            if (bitCount != 24 && bitCount != 32) return null;
            if (compression != 0 && compression != 3) return null; // BI_RGB or BI_BITFIELDS only
            var height = Math.Abs(heightRaw);
            var topDown = heightRaw < 0;
            var bytesPerPixel = bitCount / 8;
            var stride = ((width * bitCount + 31) / 32) * 4;
            // Pixel data starts after the header, plus 12 bytes of
            // masks when a 40-byte header carries BI_BITFIELDS masks
            // after it (a 124-byte V5 header already contains them).
            var pixelOffset = headerSize + (compression == 3 && headerSize == 40 ? 12 : 0);
            if ((long)stride * height > dib.Length - pixelOffset) return null;

            // Bitfield masks for BI_BITFIELDS (and the BI_RGB defaults).
            uint rMask, gMask, bMask;
            if (compression == 3)
            {
                rMask = BitConverter.ToUInt32(dib, 40);
                gMask = BitConverter.ToUInt32(dib, 44);
                bMask = BitConverter.ToUInt32(dib, 48);
            }
            else { rMask = 0x00FF0000; gMask = 0x0000FF00; bMask = 0x000000FF; }

            // Raw scanlines: one filter byte (0 = none) + RGB per pixel.
            using var raw = new MemoryStream();
            for (var y = 0; y < height; y++)
            {
                var srcRow = topDown ? y : height - 1 - y;
                raw.WriteByte(0);
                for (var x = 0; x < width; x++)
                {
                    var o = pixelOffset + srcRow * stride + x * bytesPerPixel;
                    byte r, g, b;
                    if (bitCount == 24)
                    {
                        b = dib[o]; g = dib[o + 1]; r = dib[o + 2];
                    }
                    else
                    {
                        var v = BitConverter.ToUInt32(dib, o);
                        r = ExtractChannel(v, rMask);
                        g = ExtractChannel(v, gMask);
                        b = ExtractChannel(v, bMask);
                    }
                    raw.WriteByte(r); raw.WriteByte(g); raw.WriteByte(b);
                }
            }

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var zs = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    raw.Position = 0;
                    raw.CopyTo(zs);
                }
                compressed = ms.ToArray();
            }

            Directory.CreateDirectory(Storage.AttachmentsDir);
            var path = Path.Combine(Storage.AttachmentsDir,
                Storage.UniqueCleanName("pasted-image.png"));
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            WriteChunk(bw, "IHDR", w =>
            {
                WriteBe32(w, (uint)width); WriteBe32(w, (uint)height);
                w.Write((byte)8); // bit depth
                w.Write((byte)2); // colour type: truecolour RGB
                w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
            });
            WriteChunk(bw, "IDAT", w => w.Write(compressed));
            WriteChunk(bw, "IEND", _ => { });
            return path;
        }
        catch { return null; }
    }

    private static byte ExtractChannel(uint value, uint mask)
    {
        if (mask == 0) return 0;
        var shift = 0;
        while (((mask >> shift) & 1) == 0) shift++;
        var bits = 0;
        while (((mask >> (shift + bits)) & 1) == 1) bits++;
        if (bits == 0) return 0;
        var v = (value & mask) >> shift;
        var max = (1u << bits) - 1;
        return (byte)(v * 255 / max);
    }

    private static void WriteBe32(BinaryWriter w, uint v)
    {
        w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16));
        w.Write((byte)(v >> 8)); w.Write((byte)v);
    }

    private static void WriteChunk(BinaryWriter w, string type, Action<BinaryWriter> writeData)
    {
        using var ms = new MemoryStream();
        using var dw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        writeData(dw);
        dw.Flush();
        var data = ms.ToArray();
        var typeBytes = Encoding.ASCII.GetBytes(type);
        WriteBe32(w, (uint)data.Length);
        w.Write(typeBytes);
        w.Write(data);
        // CRC over chunk type + data.
        var crcData = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcData, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcData, typeBytes.Length, data.Length);
        WriteBe32(w, Crc32(crcData));
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
