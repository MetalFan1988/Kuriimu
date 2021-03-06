using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kontract.Compression;
using Kontract.Interface;
using Kontract.IO;

namespace archive_3ds_lz
{
    public class _3DSLZ
    {
        public List<_3dslzFileInfo> Files = new List<_3dslzFileInfo>();
        public bool UseFixedOffsets = false;
        private Stream _stream = null;

        private const string Magic = "3DS-LZ\r\n";
        private const int _alignment = 0x40;

        private List<int> _fileOffsets = new List<int>();

        public _3DSLZ(Stream input)
        {
            _stream = input;
            using (var br = new BinaryReaderX(input, true))
            {
                var fileExts = new List<string>();
                var fileSizes = new List<int>();

                // File Indecies and Extensions
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    var dddHeader = br.ReadString(Magic.Length);
                    if (dddHeader == Magic)
                    {
                        _fileOffsets.Add((int)br.BaseStream.Position);

                        // Get Extension
                        br.BaseStream.Position += 5;
                        var ext = br.ReadString(4);
                        if (!Regex.IsMatch(ext, "[A-Za-z]"))
                            ext = "BIN";
                        fileExts.Add(Regex.Replace(ext, "[^A-Za-z]", ""));
                        br.BaseStream.Position -= 4;
                        br.BaseStream.Position -= 5;
                    }
                    br.BaseStream.Position -= Magic.Length;
                    br.BaseStream.Position += _alignment;
                }

                // FileSizes
                for (var i = 0; i < _fileOffsets.Count; i++)
                {
                    if (i == _fileOffsets.Count - 1)
                        fileSizes.Add((int)br.BaseStream.Length - _fileOffsets[i]);
                    else
                        fileSizes.Add(_fileOffsets[i + 1] - _fileOffsets[i] - Magic.Length);
                }

                // Create AFIs
                for (var i = 0; i < _fileOffsets.Count; i++)
                {
                    Files.Add(new _3dslzFileInfo
                    {
                        Size = fileSizes[i],
                        FileName = $@"File_{i:000000}.{fileExts[i]}",
                        State = ArchiveFileState.Archived,
                        FileData = new SubStream(input, _fileOffsets[i], fileSizes[i])
                    });
                }
            }
        }

        public void Save(Stream output)
        {
            using (var bw = new BinaryWriterX(output))
            {
                for (var i = 0; i < Files.Count; i++)
                {
                    var afi = Files[i];
                    if (UseFixedOffsets)
                        bw.BaseStream.Position = _fileOffsets[i] - Magic.Length;
                    bw.WriteASCII(Magic);
                    if (afi.State == ArchiveFileState.Archived)
                        afi.CompressedFileData.CopyTo(bw.BaseStream);
                    else
                        bw.Write(Nintendo.Compress(afi.FileData, Nintendo.Method.LZ11));
                    if (afi != Files.Last())
                        bw.WriteAlignment(_alignment);
                }
            }
        }

        public void Close()
        {
            _stream?.Dispose();
            foreach (var afi in Files)
                if (afi.State != ArchiveFileState.Archived)
                    afi.FileData?.Dispose();
            _stream = null;
        }
    }
}
