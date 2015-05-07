// ===============================================
// DESCRIPTION
// ===============================================
// This format is pretty similar to IPS.
// The main difference is, that also 32 MB
// offsets are supported. That increases speed
// as a WORD is read and not 3 single bytes. 
// It also does not save every single 0xFF byte
// if you expanded your ROM. It saves a boolean
// which indicates whether the ROM is expanded.
// If yes, 16 MB are appended and then the normal
// process of patching is going on. UPS patches
// are sometimes >16 MB because of the expansion.
// But a PPS patch will be 16 MB smaller!

// ===============================================
// DOCUMENTATION
// ===============================================
// BYTE 0x00-0x0D: POKEPATCHSYS identifier string
// BYTE 0x0E: VERSION of the PPS patch
// BYTE 0x0F: [01 = 32 MB expanded ROM]
// BYTE 0x10: start of the actual data
//      > byte 0-4: Offset of the data
//      > byte 4-8: Length of the data
//      > byte 8-X: The actual data

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Pokepatch
{
    /// <summary>
    /// Provides methods for applying
    /// patches and creating patches.
    /// </summary>
    public class Patchingsystem
    {
        const int HEADER_SIZE = 0x10;
        const int CURRENT_VERSION = 1;
        const string HEADER = "POKEPTCHRSYSM";
        const string ERROR1 = "The header of this file is incorrect. "
            + "It is not a PPS patch and can therefore not be applied";
        const string ERROR2 = "The version number of this PPS patch "
            + "is not supported in this program version. Please download "
            + "a newer version on the site where you downloaded this program.";
        const string ERROR3 = "This PPS patch contained invalid data. "
            + "Please contact the provider of this PPS patch.";
        const string ERROR4 = "The unmodified and modified file are the same. "
            + "Please try another input file or hack the ROM at least once.";
        const string ERROR5 = "The modified file seems to be broken.";

        /// <summary>
        /// Applies a patch to a clean ROM. Returns null
        /// if success, otherwise the error message.
        /// </summary>
        public string Apply(string rom, string patch)
        {
            Stream stream_r = new FileStream(rom, FileMode.Open);
            Stream stream_p = new FileStream(patch, FileMode.Open);
            BinaryWriter bw_r = new BinaryWriter(stream_r);
            BinaryReader br_p = new BinaryReader(stream_p);

            // Reads the header of the pokepatch file
            // and checks whether it is valid or not.
            if (ReadASCII(br_p, 13) != HEADER)
            {
                bw_r.Dispose();
                br_p.Dispose();
                stream_r.Dispose();
                stream_p.Dispose();
                return ERROR1;
            }

            // Reads the version number and
            // checks whether it is supported.
            if (br_p.ReadByte() == CURRENT_VERSION)
            {
                // Attempts to resize the ROM to 32MB
                // in case the determining byte is 1.
                if (br_p.ReadByte() == 1)
                {
                    stream_r.SetLength(1024*1024*32);
                    stream_r.Position = 1024*1024*16;
                    for (int i = 0; i < 1024*1024*16; i++)
                    {
                        stream_r.WriteByte(0xFF);
                    }
                }

                // Now reads the actual byte data
                // and interrupts if an invalid offset
                // has been read or if file end is reached.
                long length = stream_p.Length;
                long romlength = stream_r.Length;
                while (stream_p.Position < length)
                {
                    // Gets the offset where the data is at.
                    uint offset = br_p.ReadUInt32();
                    if (offset > romlength - 1)
                    {
                        bw_r.Dispose();
                        br_p.Dispose();
                        stream_r.Dispose();
                        stream_p.Dispose();
                        return ERROR3;
                    }

                    // Gets the length of the following data.
                    int size = br_p.ReadInt32();
                    if (offset + size > romlength - 1)
                    {
                        bw_r.Dispose();
                        br_p.Dispose();
                        stream_r.Dispose();
                        stream_p.Dispose();
                        return ERROR3;
                    }

                    // After error-checking, get data
                    // and write it to the ROM file.
                    byte[] data = br_p.ReadBytes(size);
                    stream_r.Position = offset;
                    bw_r.Write(data);
                }

                // Returns null on success
                bw_r.Flush();
                bw_r.Dispose();
                br_p.Dispose();
                stream_r.Dispose();
                stream_p.Dispose();
                return null;
            }
            else
            {
                bw_r.Dispose();
                br_p.Dispose();
                stream_r.Dispose();
                stream_p.Dispose();
                return ERROR2;
            }
        }

        /// <summary>
        /// Creates a patch based on the differences
        /// of the unmodified and the modified file.
        /// </summary>
        public byte[] Create(string unmodified, string modified, out string msg)
        {
            // Finds the differences between two files and
            // stores them in a readable list of structures.
            Stream stream_u = new FileStream(unmodified, FileMode.Open);
            Stream stream_m = new FileStream(modified, FileMode.Open);
            List<Difference> diffs = FindDifferences(stream_u, stream_m);

            // Checks if the modified file is valid.
            if (diffs == null)
            {
                msg = ERROR5;
                return null;
            }
            // Checks if any differences exist
            if (diffs.Count == 0)
            {
                msg = ERROR4;
                return null;
            }

            // Gets the total data length.
            int count = diffs.Count;
            long length = HEADER_SIZE;
            for (int i = 0; i < count; i++)
            {
                length += diffs[i].Size;
            }

            // Creates the patch based on the
            // found differences and returns
            // it as an array of raw bytes.
            Byte[] patch = new Byte[length];
            MemoryStream ms = new MemoryStream(patch);
            BinaryWriter bw = new BinaryWriter(ms);

            // Writes the header.
            WriteASCII(bw, HEADER);
            bw.Write(CURRENT_VERSION);
            bw.Write(stream_m.Length > stream_u.Length);

            // Writes the actual data.
            for (int i = 0; i < count; i++)
            {
                bw.Write(BitConverter.GetBytes(diffs[i].Offset));
                bw.Write(BitConverter.GetBytes(diffs[i].Size));
                bw.Write(diffs[i].Data.ToArray());
            }

            bw.Dispose();
            msg = null;
            return patch;
        }

        /// <summary>
        /// Finds all differences between to binary files
        /// and returns a list of all the information.
        /// </summary>
        private List<Difference> FindDifferences(Stream stream_u, Stream stream_m)
        {
            // Creates two binary readers for faster access.
            BinaryReader br_u = new BinaryReader(stream_u);
            BinaryReader br_m = new BinaryReader(stream_m);
            List<Difference> diff = new List<Difference>();

            // Checks if the modified file
            // is not smaller than the original.
            long length_u = stream_u.Length;
            long length_m = stream_m.Length;
            if (length_m < length_u)
            {
                return null;
            }

            // Reads all the differences which are
            // in range of the unmodified stream length.
            bool append = false;
            int currentpos = -1;
            while (stream_u.Position < length_u)
            {
                byte dat_u = br_u.ReadByte();
                byte dat_m = br_m.ReadByte();
                if (dat_u != dat_m)
                {
                    // Checks if the byte before
                    // was also different or not.
                    if (append)
                    {
                        diff[currentpos].Data.Add(dat_m);
                        diff[currentpos].Size += 1;
                    }
                    else
                    {
                        // Creates a new difference entry
                        // and increases list position by one.
                        var entry = new Difference();
                        {
                            currentpos++;
                            entry.Size = 1;
                            entry.Offset = (uint)stream_u.Position;
                            entry.Data = new List<Byte>() { dat_m };
                            diff.Add(entry);
                            append = true;
                        }
                    }
                }
                else
                {
                    append = false;
                }
            }

            // Then reads all the non-FF data
            // in the modified stream area.
            append = false;
            stream_m.Position = length_u;
            while (stream_m.Position < length_m)
            {
                byte dat_m = br_m.ReadByte();
                if (dat_m != 0xFF)
                {
                    if (append)
                    {
                        diff[currentpos].Data.Add(dat_m);
                        diff[currentpos].Size += 1;
                    }
                    else
                    {
                        var entry = new Difference();
                        {
                            currentpos++;
                            entry.Size = 1;
                            entry.Offset = (uint)stream_m.Position;
                            entry.Data = new List<Byte>() { dat_m };
                            diff.Add(entry);
                            append = true;
                        }
                    }
                }
                else
                {
                    append = false;
                }
            }

            // Returns all the found differences.
            return diff;
        }

        /// <summary>
        /// Reads the specified amount of ASCII chars
        /// and converts them into a string object.
        /// </summary>
        private string ReadASCII(BinaryReader br, int count)
        {
            byte[] chars = br.ReadBytes(count);
            var encoding = Encoding.GetEncoding(1252);
            return encoding.GetString(chars);
        }

        private void WriteASCII(BinaryWriter bw, string str)
        {
            var encoding = Encoding.GetEncoding(1252);
            byte[] chars = encoding.GetBytes(str);
            bw.Write(chars);
        }
    }

    /// <summary>
    /// Defines a structure which holds information
    /// on differences between two binary files.
    /// </summary>
    public class Difference
    {
        public List<Byte> Data;
        public UInt32 Offset;
        public Int32 Size;
    }
}