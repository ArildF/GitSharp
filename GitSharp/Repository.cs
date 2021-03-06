/*
 * Copyright (C) 2007, Dave Watson <dwatson@mimvista.com>
 * Copyright (C) 2008, Robin Rosenberg <robin.rosenberg@dewire.com>
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2008, Kevin Thompson <kevin.thompson@theautomaters.com>
 * Copyright (C) 2009, Henon <meinrad.recheis@gmail.com>
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the Git Development Community nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GitSharp.Util;
using GitSharp.Exceptions;

namespace GitSharp
{
    /**
     * Represents a Git repository. A repository holds all objects and refs used for
     * managing source code (could by any type of file, but source code is what
     * SCM's are typically used for).
     *
     * In Git terms all data is stored in GIT_DIR, typically a directory called
     * .git. A work tree is maintained unless the repository is a bare repository.
     * Typically the .git directory is located at the root of the work dir.
     *
     * <ul>
     * <li>GIT_DIR
     * 	<ul>
     * 		<li>objects/ - objects</li>
     * 		<li>refs/ - tags and heads</li>
     * 		<li>config - configuration</li>
     * 		<li>info/ - more configurations</li>
     * 	</ul>
     * </li>
     * </ul>
     *
     * This implementation only handles a subtly undocumented subset of git features.
     *
     */
    public class Repository
    {
        private RefDatabase _refDb;
        //private List<PackFile> _packs;

        private ObjectDirectory objectDatabase;

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository"/> class. Assumes parent directory is the working directory.
        /// </summary>
        /// <param name="gitDirectory">The git directory.</param>
        public Repository(DirectoryInfo gitDirectory)
            : this(gitDirectory, gitDirectory.Parent)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository"/> class.
        /// </summary>
        /// <param name="gitDirectory">The git directory.</param>
        /// <param name="workingDirectory">The working directory.</param>
        public Repository(DirectoryInfo gitDirectory, DirectoryInfo workingDirectory)
        {
            this.Directory = gitDirectory;
            this.WorkingDirectory = workingDirectory;
            objectDatabase = new ObjectDirectory(FS.resolve(gitDirectory, "objects"));
            _objectsDirs = new List<DirectoryInfo>();
            _objectsDirs = ReadObjectsDirs(Path.Combine(gitDirectory.FullName, "objects"), ref _objectsDirs);

            this.Config = new RepositoryConfig(this);
            _refDb = new RefDatabase(this);
            //_packs = new List<PackFile>();

            bool isExisting = _objectsDirs[0].Exists;
            if (isExisting)
            {
                this.Config.Load();
                string repositoryFormatVersion = this.Config.GetString("core", null, "repositoryFormatVersion");

                if (!"0".Equals(repositoryFormatVersion))
                {
                    throw new IOException("Unknown repository format \""
                            + repositoryFormatVersion + "\"; expected \"0\".");
                }
            }
            else
            {
                this.Config.Create();
            }
            //if (isExisting)
            //    ScanForPacks();
        }

        #region events

        public event EventHandler<RefsChangedEventArgs> RefsChanged;
        internal void OnRefsChanged()
        {
            var handler = this.RefsChanged;
            if (handler != null)
                handler(this, new RefsChangedEventArgs(this));
        }

        public event EventHandler<IndexChangedEventArgs> IndexChanged;
        internal void OnIndexChanged()
        {
            var handler = this.IndexChanged;
            if (handler != null)
                handler(this, new IndexChangedEventArgs(this));
        }
        #endregion

        /**
         * Create a new Git repository initializing the necessary files and
         * directories.
         *
         * @
         */
        public void Create()
        {
            if (this.Directory.Exists)
                throw new GitException("Unable to create repository. Directory already exists.");

            this.Directory.Create();
            this._refDb.Create();

            this._objectsDirs[0].Create();
            new DirectoryInfo(Path.Combine(this._objectsDirs[0].FullName, "pack")).Create();
            new DirectoryInfo(Path.Combine(this._objectsDirs[0].FullName, "info")).Create();

            new DirectoryInfo(Path.Combine(this.Directory.FullName, "branches")).Create();
            new DirectoryInfo(Path.Combine(this.Directory.FullName, "remote")).Create();

            string master = Constants.RefsHeads + Constants.Master;

            this._refDb.Link(Constants.Head, master);

            this.Config.Create();
            this.Config.Save();

        }
        /**
         * @param objectId
         * @return true if the specified object is stored in this repo or any of the
         *         known shared repositories.
         */
        public bool HasObject(AnyObjectId objectId)
        {
            //int k = this._packs.Count;
            //if (k > 0)
            //{
            //    do
            //    {
            //        if (this._packs[--k].HasObject(objectId))
            //            return true;
            //    } while (k > 0);
            //}
            return ToFile(objectId).Exists;
        }


        #region private methods
        ///**
        // * Scan the object dirs, including alternates for packs
        // * to use.
        // */
        //public void ScanForPacks()
        //{
        //    List<PackFile> p = new List<PackFile>();
        //    for (int i = 0; i < _objectsDirs.Count; ++i)
        //        ScanForPacks(new DirectoryInfo(Path.Combine(_objectsDirs[i].FullName, "pack")), p);

        //    _packs = p;

        //}

        //private void ScanForPacks(DirectoryInfo packDir, List<PackFile> packList)
        //{
        //    // Must match "pack-[0-9a-f]{40}.idx" to be an index.
        //    IEnumerable<FileInfo> idxList =
        //        packDir.GetFiles().Where(
        //            file => file.Name.Length == 49 && file.Name.EndsWith(".idx") && file.Name.StartsWith("pack-"));

        //    if (idxList == null) return;
        //    foreach (FileInfo indexName in idxList)
        //    {
        //        string n = indexName.FullName.Substring(0, indexName.FullName.Length - 4);
        //        FileInfo idxFile = new FileInfo(n + ".idx");
        //        FileInfo packFile = new FileInfo(n + ".pack");

        //        if (!packFile.Exists)
        //        {
        //            // Sometimes C Git's http fetch transport leaves a
        //            // .idx file behind and does not download the .pack.
        //            // We have to skip over such useless indexes.
        //            //
        //            continue;
        //        }

        //        try
        //        {
        //            packList.Add(new PackFile(this, idxFile, packFile));
        //        }
        //        catch (IOException)
        //        {
        //            // Whoops. That's not a pack!
        //            //
        //        }
        //    }
        //}

        private List<DirectoryInfo> ReadObjectsDirs(string objectsDir, ref List<DirectoryInfo> ret)
        {
            ret.Add(new DirectoryInfo(objectsDir));
            FileInfo altFile = new FileInfo(Path.Combine(Path.Combine(objectsDir, "info"), "alternates"));
            if (altFile.Exists)
            {
                using (StreamReader reader = altFile.OpenText())
                {
                    for (string alt = reader.ReadLine(); alt != null; alt = reader.ReadLine())
                    {
                        ReadObjectsDirs(Path.Combine(objectsDir, alt), ref ret);
                    }
                }
            }
            return ret;
        }

        #endregion

        #region properties
        private List<DirectoryInfo> _objectsDirs = new List<DirectoryInfo>();
        public DirectoryInfo ObjectsDirectory
        {
            get { return this._objectsDirs[0]; }
        }

        public DirectoryInfo Directory { get; private set; }
        public DirectoryInfo WorkingDirectory { get; private set; }
        public RepositoryConfig Config { get; private set; }
        #endregion
        /**
	     * Construct a filename where the loose object having a specified SHA-1
	     * should be stored. If the object is stored in a shared repository the path
	     * to the alternative repo will be returned. If the object is not yet store
	     * a usable path in this repo will be returned. It is assumed that callers
	     * will look for objects in a pack first.
	     *
	     * @param objectId
	     * @return suggested file name
	     */

        public FileInfo ToFile(AnyObjectId objectId)
        {
            string n = objectId.ToString();
            string d = n.Substring(0, 2);
            string f = n.Substring(2);
            for (int i = 0; i < _objectsDirs.Count; ++i)
            {
                FileInfo ret = new FileInfo(PathUtil.Combine(_objectsDirs[i].FullName, d, f));
                if (ret.Exists)
                    return ret;
            }
            return new FileInfo(PathUtil.Combine(_objectsDirs[0].FullName, d, f));
        }


        /**
         * @param id
         *            SHA-1 of an object.
         * 
         * @return a {@link ObjectLoader} for accessing the data of the named
         *         object, or null if the object does not exist.
         * @
         */
        public ObjectLoader OpenObject(AnyObjectId id)
        {
            WindowCursor wc = new WindowCursor();
            try
            {
                return openObject(wc, id);
            }
            finally
            {
                wc.release();
            }
        }


        /**
         * @param curs
         *            temporary working space associated with the calling thread.
         * @param id
         *            SHA-1 of an object.
         * 
         * @return a {@link ObjectLoader} for accessing the data of the named
         *         object, or null if the object does not exist.
         * @
         */
        public ObjectLoader openObject(WindowCursor curs, AnyObjectId id)
        {
            return objectDatabase.openObject(curs, id);
        }


        ///**
        // * Open object in all packs containing specified object.
        // *
        // * @param objectId
        // *            id of object to search for
        // * @param curs
        // *            temporary working space associated with the calling thread.
        // * @return collection of loaders for this object, from all packs containing
        // *         this object
        // * @
        // */
        //public ICollection<PackedObjectLoader> OpenObjectInAllPacks(AnyObjectId objectId, WindowCursor cursor)
        //{
        //    ICollection<PackedObjectLoader> result = new LinkedList<PackedObjectLoader>();
        //    OpenObjectInAllPacks(objectId, result, cursor);
        //    return result;
        //}

        ///**
        // * Open object in all packs containing specified object.
        // *
        // * @param objectId
        // *            id of object to search for
        // * @param resultLoaders
        // *            result collection of loaders for this object, filled with
        // *            loaders from all packs containing specified object
        // * @param curs
        // *            temporary working space associated with the calling thread.
        // * @
        // */

        //private void OpenObjectInAllPacks(AnyObjectId objectId, ICollection<PackedObjectLoader> resultLoaders, WindowCursor cursor)
        //{
        //    foreach (PackFile pack in _packs)
        //    {
        //        PackedObjectLoader loader = pack.Get(cursor, objectId);
        //        if (loader != null)
        //            resultLoaders.Add(loader);
        //    }
        //}


        /**
         * @param id
         *            SHA'1 of a blob
         * @return an {@link ObjectLoader} for accessing the data of a named blob
         * @
         */
        public ObjectLoader OpenBlob(ObjectId id)
        {
            return OpenObject(id);
        }
        /**
         * @param id
         *            SHA'1 of a tree
         * @return an {@link ObjectLoader} for accessing the data of a named tree
         * @
         */
        public ObjectLoader OpenTree(ObjectId id)
        {
            return OpenObject(id);
        }
        /**
         * Access a Commit object using a symbolic reference. This reference may
         * be a SHA-1 or ref in combination with a number of symbols translating
         * from one ref or SHA1-1 to another, such as HEAD^ etc.
         *
         * @param revstr a reference to a git commit object
         * @return a Commit named by the specified string
         * @ for I/O error or unexpected object type.
         *
         * @see #resolve(string)
         */
        public Commit MapCommit(string resolveString)
        {
            ObjectId id = Resolve(resolveString);
            return id != null ? MapCommit(id) : null;
        }
        /**
         * Access a Commit by SHA'1 id.
         * @param id
         * @return Commit or null
         * @ for I/O error or unexpected object type.
         */
        public Commit MapCommit(ObjectId id)
        {
            ObjectLoader or = OpenObject(id);
            if (or == null)
                return null;
            byte[] raw = or.getBytes();
            if (Constants.OBJ_COMMIT == or.getType())
                return new Commit(this, id, raw);
            throw new IncorrectObjectTypeException(id, ObjectType.Commit);
        }
        /**
         * Access any type of Git object by id and
         *
         * @param id
         *            SHA-1 of object to read
         * @param refName optional, only relevant for simple tags
         * @return The Git object if found or null
         * @
         */
        public object MapObject(ObjectId id, string refName)
        {
            ObjectLoader or = OpenObject(id);
            byte[] raw = or.getBytes();
            switch ((ObjectType)(or.getType()))
            {
                case ObjectType.Tree:
                    return MakeTree(id, raw);
                case ObjectType.Commit:
                    return MakeCommit(id, raw);
                case ObjectType.Tag:
                    return MakeTag(id, refName, raw);
                case ObjectType.Blob:
                    return raw;
            }
            return null;

        }


        private object MakeCommit(ObjectId id, byte[] raw)
        {
            return new Commit(this, id, raw);
        }




        /**
         * Access a Tree object using a symbolic reference. This reference may
         * be a SHA-1 or ref in combination with a number of symbols translating
         * from one ref or SHA1-1 to another, such as HEAD^{tree} etc.
         *
         * @param revstr a reference to a git commit object
         * @return a Tree named by the specified string
         * @
         *
         * @see #resolve(string)
         */
        public Tree MapTree(string revstr)
        {
            ObjectId id = Resolve(revstr);
            return (id != null) ? MapTree(id) : null;
        }
        /**
         * Access a Tree by SHA'1 id.
         * @param id
         * @return Tree or null
         * @ for I/O error or unexpected object type.
         */
        public Tree MapTree(ObjectId id)
        {
            ObjectLoader or = OpenObject(id);
            if (or == null)
                return null;
            byte[] raw = or.getBytes();
            switch (((ObjectType)or.getType()))
            {
                case ObjectType.Tree:
                    return new Tree(this, id, raw);
                case ObjectType.Commit:
                    return MapTree(ObjectId.FromString(raw, 5));
            }
            throw new IncorrectObjectTypeException(id, ObjectType.Tree);
        }

        private Tag MakeTag(ObjectId id, string refName, byte[] raw)
        {
            return new Tag(this, id, refName, raw);
        }

        private Tree MakeTree(ObjectId id, byte[] raw)
        {
            return new Tree(this, id, raw);
        }
        /**
         * Access a tag by symbolic name.
         *
         * @param revstr
         * @return a Tag or null
         * @ on I/O error or unexpected type
         */
        public Tag MapTag(string revstr)
        {
            ObjectId id = Resolve(revstr);
            return id != null ? MapTag(revstr, id) : null;
        }
        /**
         * Access a Tag by SHA'1 id
         * @param refName
         * @param id
         * @return Commit or null
         * @ for I/O error or unexpected object type.
         */
        public Tag MapTag(string refName, ObjectId id)
        {
            ObjectLoader or = OpenObject(id);
            if (or == null)
                return null;
            byte[] raw = or.getBytes();
            if (ObjectType.Tag == (ObjectType)or.getType())
                return new Tag(this, id, refName, raw);
            return new Tag(this, id, refName, null);
        }
        /**
         * Create a command to update (or create) a ref in this repository.
         * 
         * @param ref
         *            name of the ref the caller wants to modify.
         * @return an update command. The caller must finish populating this command
         *         and then invoke one of the update methods to actually make a
         *         change.
         * @
         *             a symbolic ref was passed in and could not be resolved back
         *             to the base ref, as the symbolic ref could not be read.
         */
        public RefUpdate UpdateRef(string refName)
        {
            return _refDb.NewUpdate(refName);
        }


        /**
         * Parse a git revision string and return an object id.
         *
         * Currently supported is combinations of these.
         * <ul>
         *  <li>SHA-1 - a SHA-1</li>
         *  <li>refs/... - a ref name</li>
         *  <li>ref^n - nth parent reference</li>
         *  <li>ref~n - distance via parent reference</li>
         *  <li>ref@{n} - nth version of ref</li>
         *  <li>ref^{tree} - tree references by ref</li>
         *  <li>ref^{commit} - commit references by ref</li>
         * </ul>
         *
         * Not supported is
         * <ul>
         * <li>timestamps in reflogs, ref@{full or relative timestamp}</li>
         * <li>abbreviated SHA-1's</li>
         * </ul>
         *
         * @param revstr A git object references expression
         * @return an ObjectId or null if revstr can't be resolved to any ObjectId
         * @ on serious errors
         */
        public ObjectId Resolve(string revstr)
        {
            char[] rev = revstr.ToCharArray();
            object oref = null;
            ObjectId refId = null;
            for (int i = 0; i < rev.Length; ++i)
            {
                switch (rev[i])
                {
                    case '^':
                        if (refId == null)
                        {
                            string refstr = new string(rev, 0, i);
                            refId = ResolveSimple(refstr);
                            if (refId == null)
                                return null;
                        }
                        if (i + 1 < rev.Length)
                        {
                            switch (rev[i + 1])
                            {
                                case '0':
                                case '1':
                                case '2':
                                case '3':
                                case '4':
                                case '5':
                                case '6':
                                case '7':
                                case '8':
                                case '9':
                                    int j;
                                    oref = MapObject(refId, null);
                                    if (!(oref is Commit))
                                        throw new IncorrectObjectTypeException(refId, ObjectType.Commit);
                                    for (j = i + 1; j < rev.Length; ++j)
                                    {
                                        if (!Char.IsDigit(rev[j]))
                                            break;
                                    }
                                    string parentnum = new string(rev, i + 1, j - i - 1);
                                    int pnum = int.Parse(parentnum);
                                    if (pnum != 0)
                                        refId = ((Commit)oref).ParentIds[pnum - 1];
                                    i = j - 1;
                                    break;
                                case '{':
                                    int k;
                                    string item = null;
                                    for (k = i + 2; k < rev.Length; ++k)
                                    {
                                        if (rev[k] != '}') continue;
                                        item = new string(rev, i + 2, k - i - 2);
                                        break;
                                    }
                                    i = k;
                                    if (item != null)
                                        if (item.Equals("tree"))
                                        {
                                            oref = MapObject(refId, null);
                                            while (oref is Tag)
                                            {
                                                Tag t = (Tag)oref;
                                                refId = t.Id;
                                                oref = MapObject(refId, null);
                                            }
                                            if (oref is Treeish)
                                                refId = ((Treeish)oref).TreeId;
                                            else
                                                throw new IncorrectObjectTypeException(refId, ObjectType.Tree);
                                        }
                                        else if (item.Equals("commit"))
                                        {
                                            oref = MapObject(refId, null);
                                            while (oref is Tag)
                                            {
                                                Tag t = (Tag)oref;
                                                refId = t.Id;
                                                oref = MapObject(refId, null);
                                            }
                                            if (!(oref is Commit))
                                                throw new IncorrectObjectTypeException(refId, ObjectType.Commit);
                                        }
                                        else if (item.Equals("blob"))
                                        {
                                            oref = MapObject(refId, null);
                                            while (oref is Tag)
                                            {
                                                Tag t = (Tag)oref;
                                                refId = t.Id;
                                                oref = MapObject(refId, null);
                                            }
                                            if (!(oref is byte[]))
                                                throw new IncorrectObjectTypeException(refId, ObjectType.Commit);
                                        }
                                        else if (item.Equals(""))
                                        {
                                            oref = MapObject(refId, null);
                                            if (oref is Tag)
                                                refId = ((Tag)oref).Id;
                                            else
                                            {
                                                // self
                                            }
                                        }
                                        else
                                            throw new RevisionSyntaxException(revstr);
                                    else
                                        throw new RevisionSyntaxException(revstr);
                                    break;
                                default:
                                    oref = MapObject(refId, null);
                                    if (oref is Commit)
                                        refId = ((Commit)oref).ParentIds[0];
                                    else
                                        throw new IncorrectObjectTypeException(refId, ObjectType.Commit);
                                    break;
                            }
                        }
                        else
                        {
                            oref = MapObject(refId, null);
                            if (oref is Commit)
                                refId = ((Commit)oref).ParentIds[0];
                            else
                                throw new IncorrectObjectTypeException(refId, ObjectType.Commit);
                        }
                        break;
                    case '~':
                        if (oref == null)
                        {
                            string refstr = new string(rev, 0, i);
                            refId = ResolveSimple(refstr);
                            oref = MapCommit(refId);
                        }
                        int l;
                        for (l = i + 1; l < rev.Length; ++l)
                        {
                            if (!Char.IsDigit(rev[l]))
                                break;
                        }
                        string distnum = new string(rev, i + 1, l - i - 1);
                        int dist = int.Parse(distnum);
                        while (dist > 0)
                        {
                            refId = ((Commit)oref).ParentIds[0];
                            oref = MapCommit(refId);
                            --dist;
                        }
                        i = l - 1;
                        break;
                    case '@':
                        int m;
                        string time = null;
                        for (m = i + 2; m < rev.Length; ++m)
                        {
                            if (rev[m] != '}') continue;
                            time = new string(rev, i + 2, m - i - 2);
                            break;
                        }
                        if (time != null)
                            throw new RevisionSyntaxException("reflogs not yet supported by revision parser yet", revstr);
                        i = m - 1;
                        break;
                    default:
                        if (refId != null)
                            throw new RevisionSyntaxException(revstr);
                        break;
                }
            }
            if (refId == null)
                refId = ResolveSimple(revstr);
            return refId;
        }

        private ObjectId ResolveSimple(string revstr)
        {
            if (ObjectId.IsId(revstr))
                return ObjectId.FromString(revstr);
            Ref r = _refDb.ReadRef(revstr);
            return r != null ? r.ObjectId : null;
        }
        /**
         * Close all resources used by this repository
         */
        public void Close()
        {
            //ClosePacks();
        }

        //private void ClosePacks()
        //{
        //    foreach (PackFile pack in _packs)
        //        pack.Close();

        //    _packs = new List<PackFile>();
        //}
        ///**
        // * Add a single existing pack to the list of available pack files.
        // * 
        // * @param pack
        // *            path of the pack file to open.
        // * @param idx
        // *            path of the corresponding index file.
        // * @
        // *             index file could not be opened, read, or is not recognized as
        // *             a Git pack file index.
        // */
        //public void OpenPack(FileInfo pack, FileInfo idx)
        //{
        //    string p = pack.Name;
        //    string i = idx.Name;
        //    if (p.Length != 50 || !p.StartsWith("pack-") || !p.EndsWith(".pack"))
        //        throw new ArgumentException("Not a valid pack " + pack);
        //    if (i.Length != 49 || !i.StartsWith("pack-") || !i.EndsWith(".idx"))
        //        throw new ArgumentException("Not a valid pack " + idx);
        //    if (!p.Substring(0, 45).Equals(i.Substring(0, 45)))
        //        throw new ArgumentException("Pack " + pack
        //                + "does not match index " + idx);

        //    _packs.Add(new PackFile(this, idx, pack));

        //}
        /**
         * Writes a symref (e.g. HEAD) to disk
         *
         * @param name symref name
         * @param target pointed to ref
         * @
         */
        public void WriteSymref(string name, string target)
        {
            _refDb.Link(name, target);
        }

        private GitIndex _index;
        /**
         * @return a representation of the index associated with this repo
         * @
         */
        public GitIndex Index
        {
            get
            {
                if (_index == null)
                {
                    _index = new GitIndex(this);
                    _index.Read();
                }
                else
                {
                    _index.RereadIfNecessary();
                }
                return _index;
            }
        }

        /** Clean up stale caches */
        public void RefreshFromDisk()
        {
            _refDb.ClearCache();
        }

        /// <summary>
        /// Replaces any windows director separators (backslash) with /
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        internal static byte[] GitInternalSlash(byte[] bytes)
        {
            if (Path.DirectorySeparatorChar == '/') // [henon] DirectorySeparatorChar == \
                return bytes;
            for (int i = 0; i < bytes.Length; ++i)
                if (bytes[i] == Path.PathSeparator)
                    bytes[i] = (byte)'/';
            return bytes;
        }
        /**
         * string work dir and return normalized repository path
         *
         * @param wd Work dir
         * @param f File whose path shall be stripp off it's workdir
         * @return normalized repository relative path
         */
        public static string StripWorkDir(FileSystemInfo wd, FileSystemInfo f)
        {
            string relName = f.FullName.Substring(wd.FullName.Length + 1);
            relName = relName.Replace(Path.PathSeparator, '/');
            return relName;
        }

        /**
         * @return an important state
         */
        public RepositoryState RespositoryState
        {
            get
            {
                if (WorkingDirectory.GetFiles(".dotest").Length > 0)
                    return RepositoryState.Rebasing;
                if (WorkingDirectory.GetFiles(".dotest-merge").Length > 0)
                    return RepositoryState.RebasingInteractive;
                if (WorkingDirectory.GetFiles("MERGE_HEAD").Length > 0)
                    return RepositoryState.Merging;
                if (WorkingDirectory.GetFiles("BISECT_LOG").Length > 0)
                    return RepositoryState.Bisecting;
                return RepositoryState.Safe;
            }
        }

        public void ReloadRefs()
        {
            _refs = null;
            _tags = null;
            _branches = null;
            _remoteBranches = null;
            _head = null;
        }

        private Ref _head;
        public Ref Head
        {
            get
            {
                if (_head == null)
                    _head = _refDb.ReadRef("HEAD");
                return _head;
            }
        }

        private Dictionary<string, Ref> _refs;
        public Dictionary<string, Ref> Refs
        {
            get
            {
                if (_refs == null)
                    _refs = _refDb.GetAllRefs();
                return _refs;
            }
        }

        private Dictionary<string, Ref> _tags;
        public Dictionary<string, Ref> Tags
        {
            get
            {
                if (_tags == null)
                    _tags = _refDb.GetTags();
                return _tags;
            }

        }

        private Dictionary<string, Ref> _branches;
        public Dictionary<string, Ref> Branches
        {
            get
            {
                if (_branches == null)
                    _branches = _refDb.GetBranches();
                return _branches;
            }
        }

        private Dictionary<string, Ref> _remoteBranches;
        public Dictionary<string, Ref> RemoteBranches
        {
            get
            {
                if (_remoteBranches == null)
                    _remoteBranches = _refDb.GetRemotes();
                return _remoteBranches;
            }
        }

        public Ref Peel(Ref pRef)
        {
            return _refDb.Peel(pRef);
        }

        public static Repository Open(string directory)
        {
            return Open(new DirectoryInfo(directory));
        }

        public static Repository Open(DirectoryInfo directory)
        {
            var name = directory.FullName;
            if (name.EndsWith(".git"))
                return new Repository(directory);

            var subDirectories = directory.GetDirectories(".git");
            if (subDirectories.Length > 0)
                return new Repository(subDirectories[0]);

            if (directory.Parent == null)
                return null;

            return Open(directory.Parent);
        }


        /**
         * Check validty of a ref name. It must not contain character that has
         * a special meaning in a Git object reference expression. Some other
         * dangerous characters are also excluded.
         *
         * @param refName
         *
         * @return true if refName is a valid ref name
         */
        public static bool IsValidRefName(string refName)
        {
            int len = refName.Length;
            char p = '\0';
            for (int i = 0; i < len; ++i)
            {
                char c = refName[i];
                if (c <= ' ')
                    return false;
                switch (c)
                {
                    case '.':
                        if (i == 0)
                            return false;
                        if (p == '/')
                            return false;
                        if (p == '.')
                            return false;
                        break;
                    case '/':
                        if (i == 0)
                            return false;
                        if (i == len - 1)
                            return false;
                        break;
                    case '~':
                    case '^':
                    case ':':
                    case '?':
                    case '[':
                        return false;
                    case '*':
                        return false;
                }
                p = c;
            }
            return true;
        }

        public Commit OpenCommit(ObjectId id)
        {
            return MapCommit(id);
        }

        
        internal void fireRefsMaybeChanged() // [henon] todo: remove later
        {
            OnRefsChanged();
        }
    }
}
