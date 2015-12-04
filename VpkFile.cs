namespace Ensage.VPK
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class VpkFile : IDisposable
    {
        /// <summary>
        /// The path and the file name of the object.
        /// </summary>
        public readonly string Filename;

        private const uint Magic = 0x55AA1234;

        private List<VpkNode> nodes;

        private Stream fileStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="VpkFile"/> class. 
        /// </summary>
        /// <param name="filename">
        /// Path to the VPK file that is to be opened
        /// </param>
        public VpkFile(string filename)
        {
            this.Filename = filename;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VpkFile"/> class. 
        /// Opens the primary VPK file associated with the DotA files
        /// </summary>
        public VpkFile() : this(DirectoryHelper.GetParent(Directory.GetCurrentDirectory(), 2) + @"\dota\pak01_dir.vpk")
        {
        }

        /// <summary>
        /// Length of the VP
        /// </summary>
        public uint TreeLength { get; private set; }

        public uint Version { get; private set; }

        public uint DataOffset
        {
            get
            {
                switch (this.Version)
                {
                    case 1:
                        return sizeof(uint) * 3;
                    case 2:
                        return (sizeof(uint) * 4) + (sizeof(int) * 3);
                    default:
                        throw new InvalidOperationException("Called DataOffset on a VpkFile with unknown version");
                }
            }
        }

        public void Open()
        {
            this.fileStream = File.OpenRead(this.Filename);
            this.fileStream.Seek(0, SeekOrigin.Begin);

            using (var reader = new BinaryReader(this.fileStream, Encoding.UTF8, true))
            {
                this.Load(reader);
            }

            /*Console.WriteLine(this.nodes.Count);
            Console.WriteLine();

            foreach (var t in this.nodes)
            {
                if (t.Name == "png")
                {
                    foreach (var q in t.Children)
                    {
                        Console.WriteLine("- " + q.Name + " -");
                        if (q.Name == @"resource/flash3/images/spellicons")
                        {
                            Console.WriteLine(q.Children.Length);
                            Console.WriteLine();

                            foreach (var r in q.Children)
                            {
                                Console.WriteLine(r.Name);
                                Console.WriteLine();
                            }
                        }
                    }
                }
            }*/
        }

        public void Close()
        {
            if (this.fileStream == null)
            {
                return;
            }

            this.fileStream.Close();
            this.fileStream.Dispose();
        }
        public VpkNode GetFile(string name)
        {
            var dotPos = name.LastIndexOf(".", StringComparison.Ordinal);
            var slashPos = name.LastIndexOf("/", StringComparison.Ordinal);
            if (dotPos == -1 || slashPos == -1)
            {
                return null;
            }

            var extension = name.Substring(dotPos + 1);
            var path = name.Substring(0, slashPos);
            var actualName = name.Substring(slashPos + 1, dotPos - slashPos - 1);

            return this.nodes.Where(ext => ext.Name == extension).SelectMany(ext => ext.Children.Where(source => source.Name == path).SelectMany(source => source.Children.Where(node => node.Name == actualName))).FirstOrDefault();
        }
        public void Dispose()
        {
            this.Close();
        }

        private static List<VpkNode> LoadRootNodes(BinaryReader reader, VpkFile file)
        {
            var nodes = new List<VpkNode>();

            VpkNode newNode = null;
            while (newNode == null || !string.IsNullOrEmpty(newNode.Name))
            {
                newNode = new VpkNode(file);
                newNode.Load(reader);
                if (string.IsNullOrEmpty(newNode.Name))
                {
                    continue;
                }

                nodes.Add(newNode);
                newNode.Children = LoadNodeChildren(reader, newNode, file);
            }

            return nodes;
        }

        private static VpkNode[] LoadNodeChildren(BinaryReader reader, VpkNode parent, VpkFile file)
        {
            var nodes = new List<VpkNode>();

            VpkNode newNode = null;
            while (newNode == null || !string.IsNullOrEmpty(newNode.Name))
            {
                newNode = new VpkNode(parent, file);
                newNode.Load(reader);
                if (string.IsNullOrEmpty(newNode.Name))
                {
                    continue;
                }

                nodes.Add(newNode);
                newNode.Children = LoadNodeFileChildren(reader, newNode, file);
            }

            return nodes.ToArray();
        }

        private static VpkNode[] LoadNodeFileChildren(BinaryReader reader, VpkNode parent, VpkFile file)
        {
            var nodes = new List<VpkNode>();

            VpkNode newNode = null;

            while (newNode == null || !string.IsNullOrEmpty(newNode.Name))
            {
                newNode = new VpkNode(parent, file);
                newNode.LoadFileInfo(reader);
                if (!string.IsNullOrEmpty(newNode.Name))
                {
                    nodes.Add(newNode);
                }
            }

            return nodes.ToArray();
        }

        private void Load(BinaryReader reader)
        {
            var signature = reader.ReadUInt32();
            if (signature != Magic)
            {
                throw new InvalidDataException("Incorrect magic");
            }

            this.Version = reader.ReadUInt32();

            if (this.Version < 1 || this.Version > 2)
            {
                throw new InvalidDataException("Unknown version");
            }

            switch (this.Version)
            {
                case 1:
                    this.LoadVersion1Header(reader);
                    break;
                case 2:
                    this.LoadVersion2Header(reader);
                    break;
                default:
                    throw new InvalidOperationException("I got lost.");
            }

            this.nodes = LoadRootNodes(reader, this);
        }

        private void LoadVersion1Header(BinaryReader reader)
        {
            this.TreeLength = reader.ReadUInt32();
        }

        private void LoadVersion2Header(BinaryReader reader)
        {
            this.TreeLength = reader.ReadUInt32();
        }

        private static class DirectoryHelper
        {
            public static string GetParent(string dir, int times = 1)
            {
                if (times == 0)
                {
                    return dir;
                }
                while (true)
                {
                    var parent = Directory.GetParent(dir).FullName;
                    if (times == 1)
                    {
                        return parent;
                    }
                    dir = parent;
                    times = times - 1;
                }
            }
        }
    }
}