// <copyright file="VpkNode.cs" company="EnsageSharp">
//    Copyright (c) 2015 EnsageSharp.
// 
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
// 
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
// 
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see http://www.gnu.org/licenses/
// </copyright>


namespace Ensage.VPK
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// VPK node class. Provides easy operations on VPK nodes.
    /// </summary>
    [DebuggerDisplay("Name = {Name}")]
    public sealed class VpkNode
    {

        private const uint Terminator = 0xFFFF;

        /// <summary>
        /// Initializes a new instance of the <see cref="VpkNode"/> class. 
        /// </summary>
        /// <param name="file">
        /// VPK file that the node to be initialized belongs to.
        /// </param>
        public VpkNode(VpkFile file)
        {
            this.File = file;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VpkNode"/> class. 
        /// </summary>
        /// <param name="parent">
        /// Parent node of the node to be initialized.
        /// </param>
        /// <param name="file">
        /// VPK file that the node to be initialized belongs to.
        /// </param>
        public VpkNode(VpkNode parent, VpkFile file)
        {
            this.Parent = parent;
            this.File = file;
        }

        /// <summary>
        /// Gets the parent node of the node.
        /// </summary>
        public VpkNode Parent { get; private set; }

        /// <summary>
        /// Gets the VPK file that the node belongs to.
        /// </summary>
        public VpkFile File { get; private set; }

        /// <summary>
        /// Gets the name of the node.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the CRC of the node.
        /// </summary>
        public uint Crc { get; private set; }

        /// <summary>
        /// Gets the absolute filepath of the node.
        /// </summary>
        public string FilePath
        {
            get
            {
                if (this.Parent != null && this.Parent.Parent != null)
                {
                    return null;
                }
                var fileName = this.Name;
                var path = this.Parent.Name;
                var extension = this.Parent.Parent.Name;

                return string.Format("{0}/{1}.{2}", path, fileName, extension);
            }
        }

        internal short PreloadBytes { get; private set; }

        internal short ArchiveIndex { get; private set; }

        internal uint EntryOffset { get; private set; }

        internal uint EntryLength { get; private set; }

        internal byte[] PreloadData { get; private set; }

        internal VpkNode[] Children { get; set; }

        /// <summary>
        /// Gets an input stream which streams the file associated with the node.
        /// </summary>
        /// <returns> Input stream of the file associated with the node</returns>
        public Stream GetInputStream()
        {
            if (this.EntryLength == 0 && this.PreloadBytes > 0)
            {
                return new MemoryStream(this.PreloadData);
            }

            if (this.PreloadBytes != 0)
            {
                throw new NotSupportedException(
                    "Unable to get entry data: Both EntryLength and PreloadBytes specified.");
            }

            var prefix = new string(Enumerable.Repeat('0', 3 - this.ArchiveIndex.ToString().Length).ToArray());
            var dataPakFilename = this.File.Filename.Replace("_dir.vpk", "_" + prefix + this.ArchiveIndex + ".vpk");

            var fsin = new FileStream(dataPakFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
            fsin.Seek(this.EntryOffset, SeekOrigin.Begin);
            return fsin;
        }

        internal void Load(BinaryReader reader)
        {
            var builder = new StringBuilder();
            char nextChar;
            do
            {
                nextChar = reader.ReadChar();
                builder.Append(nextChar);
            }
            while (nextChar != '\0');

            this.Name = builder.ToString().TrimEnd('\0');
        }

        internal void LoadFileInfo(BinaryReader reader)
        {
            this.Load(reader);
            if (string.IsNullOrEmpty(this.Name))
            {
                return;
            }

            this.Crc = reader.ReadUInt32();
            this.PreloadBytes = reader.ReadInt16();
            this.ArchiveIndex = reader.ReadInt16();
            this.EntryOffset = reader.ReadUInt32();
            this.EntryLength = reader.ReadUInt32();

            var terminator = reader.ReadUInt16();

            if (terminator != Terminator)
            {
                throw new InvalidDataException("Error: VPK entry did not end with correct terminator");
            }

            if (this.PreloadBytes > 0)
            {
                this.PreloadData = reader.ReadBytes(this.PreloadBytes);
            }
        }
    }
}