﻿/*
 * Copyright (C) 2007, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2008, Robin Rosenberg <robin.rosenberg@dewire.com>
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
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;
using GitSharp;
using GitSharp.Util;

namespace GitSharp.Tests
{

    /**
     * Base class for most unit tests.
     *
     * Sets up a predefined test repository and has support for creating additional
     * repositories and destroying them when the tests are finished.
     *
     * A system property <em>jgit.junit.usemmap</em> defines whether memory mapping
     * is used. Memory mapping has an effect on the file system, in that memory
     * mapped files in java cannot be deleted as long as they mapped arrays have not
     * been reclaimed by the garbage collector. The programmer cannot control this
     * with precision, though hinting using <em>{@link java.lang.System#gc}</em>
     * often helps.
     */
    public abstract class RepositoryTestCase
    {

        protected static DirectoryInfo trashParent = new DirectoryInfo("trash");

        protected DirectoryInfo trash;

        protected DirectoryInfo trash_git;

        protected static PersonIdent jauthor = new PersonIdent("J. Author", "jauthor@example.com");

        protected static PersonIdent jcommitter = new PersonIdent("J. Committer", "jcommitter@example.com");

        protected bool packedGitMMAP;

        protected Repository db;

        internal class FakeSystemReader : ISystemReader
        {
            internal Dictionary<string, string> values = new Dictionary<string, string>();
            RepositoryConfig userGitConfig;
            public string getenv(string variable)
            {
                return values[variable];
            }
            public string getProperty(string key)
            {
                return values[key];
            }
            public RepositoryConfig openUserConfig()
            {
                return userGitConfig;
            }
            public void setUserGitConfig(RepositoryConfig userGitConfig)
            {
                this.userGitConfig = userGitConfig;
            }
        }

        /**
         * Simulates the reading of system variables and properties.
         * Unit test can control the returned values by manipulating
         * {@link FakeSystemReader#values}.
         */
        internal static FakeSystemReader fakeSystemReader = new FakeSystemReader();

        static RepositoryTestCase()
        {
            RepositoryConfig.SystemReader = fakeSystemReader;
            Microsoft.Win32.SystemEvents.SessionEnded += (o, args) => // cleanup
            {
                recursiveDelete(trashParent, false, null, false);
            };
        }

        /**
         * Configure Git before setting up test repositories.
         */
        protected void configure()  // [henon] reading performance can be implemented later
        {
            // WindowCacheConfig c = new WindowCacheConfig();
            //c.setPackedGitLimit(128 * WindowCacheConfig.KB);
            //c.setPackedGitWindowSize(8 * WindowCacheConfig.KB);
            //c.setPackedGitMMAP("true".equals(System.getProperty("jgit.junit.usemmap")));
            //c.setDeltaBaseCacheLimit(8 * WindowCacheConfig.KB);
            //WindowCache.reconfigure(c);
        }

        [SetUp]
        public virtual void setUp()
        {
            //super.setUp();
            configure();
            string name = GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name;
            recursiveDelete(trashParent, true, name, false); // Cleanup old failed stuff
            trash = new DirectoryInfo(trashParent + "/trash" + DateTime.Now.Ticks + "." + (testcount++));
            trash_git = new DirectoryInfo(trash + "/.git");



            var userGitConfigFile = new FileInfo(trash_git + "/usergitconfig").FullName;
            var userGitConfig = new RepositoryConfig(null, new FileInfo(userGitConfigFile));
            fakeSystemReader.setUserGitConfig(userGitConfig);

            db = new Repository(trash_git);
            db.Create();

            string[] packs = {
				"pack-34be9032ac282b11fa9babdc2b2a93ca996c9c2f",
				"pack-df2982f284bbabb6bdb59ee3fcc6eb0983e20371",
				"pack-9fb5b411fe6dfa89cc2e6b89d2bd8e5de02b5745",
				"pack-546ff360fe3488adb20860ce3436a2d6373d2796",
				"pack-e6d07037cbcf13376308a0a995d1fa48f8f76aaa",
				"pack-3280af9c07ee18a87705ef50b0cc4cd20266cf12"
		    };
            DirectoryInfo packDir = new DirectoryInfo(db.ObjectsDirectory + "/pack");

            foreach (var packname in packs)
            {
                new FileInfo("Resources/" + packname + ".pack").CopyTo(packDir + "/" + packname + ".pack", true);
                new FileInfo("Resources/" + packname + ".idx").CopyTo(packDir + "/" + packname + ".idx", true);
            }

            new FileInfo("Resources/packed-refs").CopyTo(trash_git.FullName + "/packed-refs", true);

            fakeSystemReader.values.Clear();
            fakeSystemReader.values[Constants.OS_USER_NAME_KEY] = Constants.OS_USER_NAME_KEY;
            fakeSystemReader.values[Constants.GIT_AUTHOR_NAME_KEY] = Constants.GIT_AUTHOR_NAME_KEY;
            fakeSystemReader.values[Constants.GIT_AUTHOR_EMAIL_KEY] = Constants.GIT_AUTHOR_EMAIL_KEY;
            fakeSystemReader.values[Constants.GIT_COMMITTER_NAME_KEY] = Constants.GIT_COMMITTER_NAME_KEY;
            fakeSystemReader.values[Constants.GIT_COMMITTER_EMAIL_KEY] = Constants.GIT_COMMITTER_EMAIL_KEY;

        }


        #region --> Recursive deletion utility


        /**
         * Utility method to delete a directory recursively. It is
         * also used internally. If a file or directory cannot be removed
         * it throws an AssertionFailure.
         *
         * @param dir
         */
        protected void recursiveDelete(DirectoryInfo dir)
        {
            recursiveDelete(dir, false, this.GetType().Name + "." + ToString(), true);
        }

        protected static bool recursiveDelete(DirectoryInfo dir, bool silent, string name, bool failOnError)
        {
            try
            {
                Debug.Assert(!(silent && failOnError));
                if (!dir.Exists)
                    return silent;
                FileInfo[] ls = dir.GetFiles();
                DirectoryInfo[] subdirs = dir.GetDirectories();
                if (ls != null)
                    foreach (var e in ls)
                        PathUtil.DeleteFile(e);
                if (subdirs != null)
                    foreach (var e in subdirs)
                        silent = recursiveDelete(e, silent, name, failOnError);
                dir.Delete();
            }
            catch (IOException e)
            {
                Console.WriteLine(name + ": " + e.Message);
            }
            return silent;
        }

        private static void reportDeleteFailure(string name, bool failOnError, FileSystemInfo e)
        {
            string severity;
            if (failOnError)
                severity = "Error";
            else
                severity = "Warning";
            string msg = severity + ": Failed to delete " + e;
            if (name != null)
                msg += " in " + name;
            if (failOnError)
                Assert.Fail(msg);
            else
                System.Console.WriteLine(msg);
        }


        #endregion


        /**
	 * mock user's global configuration used instead ~/.gitconfig.
	 * This configuration can be modified by the tests without any
	 * effect for ~/.gitconfig.
	 */
        protected RepositoryConfig userGitConfig;
        private static int testcount;

        private List<Repository> repositoriesToClose = new List<Repository>();

        [TearDown]
        public void tearDown()
        {
            db.Close();
            foreach (var r in repositoriesToClose)
                r.Close();

            // Since memory mapping is controlled by the GC we need to
            // tell it this is a good time to clean up and unlock
            // memory mapped files.
            if (packedGitMMAP)
                System.GC.Collect();

            string name = GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name;
            recursiveDelete(trash, false, name, true);
            foreach (var r in repositoriesToClose)
                recursiveDelete(r.WorkingDirectory, false, name, true);
            repositoriesToClose.Clear();

            //super.tearDown();
        }


        protected FileInfo writeTrashFile(string name, string data)
        {
            var tf = new FileInfo(trash + "/" + name);
            var tfp = tf.Directory;
            if (!tfp.Exists)
            {
                tfp.Create();
                if (!tfp.Exists)
                    throw new IOException("Could not create directory " + tfp.FullName);
            }
            File.WriteAllText(tf.FullName, data, Encoding.Default);
            return tf;
        }

        protected static void checkFile(FileInfo f, string checkData)
        {
            var readData = File.ReadAllText(f.FullName, Encoding.GetEncoding("ISO-8859-1"));
            if (f.Length != readData.Length)
                throw new IOException("Internal error reading file data from " + f);
            Assert.AreEqual(checkData, readData);
        }


        /**
         * Helper for creating extra empty repos
         *
         * @return a new empty git repository for testing purposes
         *
         * @throws IOException
         */
        protected Repository createNewEmptyRepo()
        {
            var newTestRepo = new DirectoryInfo(trashParent + "/new" + DateTime.Now.Ticks + "." + (testcount++) + "/.git");
            Assert.IsFalse(newTestRepo.Exists);
            var newRepo = new Repository(newTestRepo);
            newRepo.Create();
            string name = GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name;
            repositoriesToClose.Add(newRepo);
            return newRepo;
        }
    }

}




