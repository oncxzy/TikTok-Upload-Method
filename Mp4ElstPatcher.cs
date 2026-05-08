using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TikTokUploadMethod;

public static class Mp4ElstPatcher
{
    
    private const uint MagicEntryCount = 0x10000001;

    public sealed record PatchResult(bool Success, string Message);

    public static PatchResult Patch(string filePath)
    {
        if (!File.Exists(filePath))
            return new PatchResult(false, "File not found: " + filePath);

        FileStream? fs = null;
        try
        {
            fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            
            var moov = FindBox(fs, 0, fs.Length, "moov");
            if (moov == null)
                return new PatchResult(false, "moov box not found");

            
            var traks = FindAllBoxes(fs, moov.PayloadStart, moov.PayloadEnd, "trak");
            if (traks.Count == 0)
                return new PatchResult(false, "No trak boxes inside moov");

            int patched = 0;
            var notes = new List<string>();

            byte[] magic = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(magic, MagicEntryCount);

            foreach (var trak in traks)
            {
                var edts = FindBox(fs, trak.PayloadStart, trak.PayloadEnd, "edts");
                if (edts == null)
                {
                    notes.Add("trak with no edts (skipped)");
                    continue;
                }

                var elst = FindBox(fs, edts.PayloadStart, edts.PayloadEnd, "elst");
                if (elst == null)
                {
                    notes.Add("edts with no elst (skipped)");
                    continue;
                }

                long versionFlagsOffset = elst.PayloadStart;
                long entryCountOffset = elst.PayloadStart + 4;

                if (entryCountOffset + 4 > elst.BoxEnd)
                {
                    notes.Add("elst too short to patch (skipped)");
                    continue;
                }

                fs.Seek(versionFlagsOffset, SeekOrigin.Begin);
                fs.Write(magic);

                fs.Seek(entryCountOffset, SeekOrigin.Begin);
                fs.Write(magic);
                patched++;
            }

            fs.Flush();

            if (patched == 0)
                return new PatchResult(false,
                    "No elst boxes were patched. Notes: " + string.Join("; ", notes));

            return new PatchResult(true,
                $"Patched {patched} elst entry_count field(s) to 0x{MagicEntryCount:X8}");
        }
        catch (Exception ex)
        {
            return new PatchResult(false, "Exception during patch: " + ex.Message);
        }
        finally
        {
            try { fs?.Dispose(); } catch { }
        }
    }

    private sealed class Mp4Box
    {
        public string Type { get; init; } = "";
        public long BoxStart { get; init; }
        public long BoxEnd { get; init; }
        public long PayloadStart { get; init; }
        public long PayloadEnd { get; init; }
    }

    private static Mp4Box? FindBox(FileStream fs, long start, long end, string type)
    {
        long pos = start;
        while (pos + 8 <= end)
        {
            var box = ReadBoxHeader(fs, pos, end);
            if (box == null) break;
            if (box.Type == type) return box;
            if (box.BoxEnd <= box.BoxStart) break; 
            pos = box.BoxEnd;
        }
        return null;
    }

    private static List<Mp4Box> FindAllBoxes(FileStream fs, long start, long end, string type)
    {
        var result = new List<Mp4Box>();
        long pos = start;
        while (pos + 8 <= end)
        {
            var box = ReadBoxHeader(fs, pos, end);
            if (box == null) break;
            if (box.Type == type) result.Add(box);
            if (box.BoxEnd <= box.BoxStart) break;
            pos = box.BoxEnd;
        }
        return result;
    }

    private static Mp4Box? ReadBoxHeader(FileStream fs, long offset, long parentEnd)
    {
        try
        {
            if (offset + 8 > parentEnd) return null;

            fs.Seek(offset, SeekOrigin.Begin);
            byte[] hdr = new byte[8];
            int read = fs.Read(hdr);
            if (read < 8) return null;

            uint size = BinaryPrimitives.ReadUInt32BigEndian(hdr.AsSpan(0, 4));
            string type = Encoding.ASCII.GetString(hdr.AsSpan(4, 4));

            long boxSize;
            long payloadStart;

            if (size == 1)
            {
                
                byte[] ext = new byte[8];
                int eRead = fs.Read(ext);
                if (eRead < 8) return null;
                boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(ext);
                payloadStart = offset + 16;
            }
            else if (size == 0)
            {
                
                boxSize = parentEnd - offset;
                payloadStart = offset + 8;
            }
            else
            {
                boxSize = size;
                payloadStart = offset + 8;
            }

            
            long boxEnd = offset + boxSize;
            if (boxEnd > parentEnd) boxEnd = parentEnd;
            if (boxEnd < payloadStart) return null;

            return new Mp4Box
            {
                Type = type,
                BoxStart = offset,
                BoxEnd = boxEnd,
                PayloadStart = payloadStart,
                PayloadEnd = boxEnd,
            };
        }
        catch
        {
            return null;
        }
    }
}
